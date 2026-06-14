using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace IOApp.Features
{
    public class FileEncryption
    {
        private const int _NONCE_SIZE = 12;
        private const int _TAG_SIZE = 16;
        private const int CHUNK_SIZE = 1024 * 1024; // 1MB each chunk
        private const int MAX_PARALLEL_CHUNKS = 4;
        private const int MAX_CHUNKS_LIMIT = 10_000_000; // ~10TB with chunk 1MB
        private const int MAX_CHUNK_SIZE = 100 * 1024 * 1024; // 100MB max

        private const string MAGIC = "FENC";
        private const byte VERSION = 1;
        private const int HEADER_SIZE = 24;
        private const int SALT_SIZE = 32; // 256-bit salt for PBKDF2
        private const int PBKDF2_ITERATIONS = 600_000; // OWASP 2023 recommendation

        /// <summary>
        /// Derive encryption key from password using PBKDF2
        /// </summary>
        public static byte[] DeriveKeyFromPassword(string password, byte[] salt, int keySize = 32)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            if (salt.Length != SALT_SIZE)
                throw new ArgumentException($"Salt must be {SALT_SIZE} bytes", nameof(salt));

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, PBKDF2_ITERATIONS, HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(keySize);
        }

        /// <summary>
        /// Encrypt with deterministic nonce to ensure 0% collision
        /// Nonce format: [4 bytes counter][8 bytes random prefix]
        /// </summary>
        public static byte[] Encrypt(byte[] plainBytes, byte[] key, int chunkIndex, ReadOnlySpan<byte> noncePrefix)
        {
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("Key must be 128, 192, or 256 bits", nameof(key));

            if (noncePrefix.Length != 8)
                throw new ArgumentException("Nonce prefix must be 8 bytes", nameof(noncePrefix));

            var cipherSize = plainBytes.Length;
            var result = new byte[_NONCE_SIZE + _TAG_SIZE + cipherSize];
            var nonce = result.AsSpan(0, _NONCE_SIZE);
            var tag = result.AsSpan(_NONCE_SIZE, _TAG_SIZE);
            var cipherBytes = result.AsSpan(_NONCE_SIZE + _TAG_SIZE, cipherSize);

            // Deterministic nonce: [counter(4)][prefix(8)]
            BinaryPrimitives.WriteInt32LittleEndian(nonce, chunkIndex);
            noncePrefix.CopyTo(nonce.Slice(4));

            using var aes = new AesGcm(key, _TAG_SIZE);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            return result;
        }

        public static byte[] Decrypt(byte[] encrypted, byte[] key)
        {
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("Key must be 128, 192, or 256 bits", nameof(key));

            var cipherSize = encrypted.Length - _NONCE_SIZE - _TAG_SIZE;
            var nonce = encrypted.AsSpan(0, _NONCE_SIZE);
            var tag = encrypted.AsSpan(_NONCE_SIZE, _TAG_SIZE);
            var cipherBytes = encrypted.AsSpan(_NONCE_SIZE + _TAG_SIZE, cipherSize);
            var plainBytes = new byte[cipherSize];
            using var aes = new AesGcm(key, _TAG_SIZE);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return plainBytes;
        }

        /// <summary>
        /// Encrypt file with parallel processing
        /// Header format (24 bytes):
        /// [Magic(4)] [Version(1)] [ChunkSize(4)] [FileSize(8)] [TotalChunks(4)] [Pad(3)]
        /// All integers use little-endian format
        /// </summary>
        public static async Task EncryptFileAsync(string inputPath, string outputPath, byte[] key,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("Key must be 128, 192, or 256 bits", nameof(key));

            var fileInfo = new FileInfo(inputPath);
            var fileSize = fileInfo.Length;
            var totalChunks = (int)Math.Ceiling((double)fileSize / CHUNK_SIZE);

            // Validate limits
            if (totalChunks > MAX_CHUNKS_LIMIT)
                throw new ArgumentException($"File too large: {totalChunks} chunks exceeds limit of {MAX_CHUNKS_LIMIT}");

            using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                FileShare.None, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);

            try
            {
                // Write header với explicit little-endian
                var header = new byte[HEADER_SIZE];
                Encoding.ASCII.GetBytes(MAGIC).CopyTo(header, 0);
                header[4] = VERSION;
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(5), CHUNK_SIZE);
                BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(9), fileSize);
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(17), totalChunks);
                await outputStream.WriteAsync(header, cancellationToken);

                // Generate nonce prefix cho deterministic nonces (8 bytes random)
                var noncePrefix = new byte[8];
                RandomNumberGenerator.Fill(noncePrefix);
                // Ghi nonce prefix vào file để decrypt biết
                await outputStream.WriteAsync(noncePrefix, cancellationToken);

                // Channels
                var readChannel = Channel.CreateBounded<(int index, byte[] data)>(
                    new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                    {
                        SingleWriter = true,
                        SingleReader = false,
                        FullMode = BoundedChannelFullMode.Wait
                    });
                var writeChannel = Channel.CreateBounded<(int index, byte[] packet)>(
                    new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                    {
                        SingleWriter = false,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });

                int processedCount = 0;
                long totalBytesWritten = 0; // Track bytes cho validation

                // Read task
                var readTask = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i < totalChunks; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var buffer = ArrayPool<byte>.Shared.Rent(CHUNK_SIZE);
                            try
                            {
                                var bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, CHUNK_SIZE), cancellationToken);
                                var chunk = new byte[bytesRead];
                                Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                                await readChannel.Writer.WriteAsync((i, chunk), cancellationToken);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                            }
                        }
                    }
                    finally
                    {
                        readChannel.Writer.Complete();
                    }
                }, cancellationToken);

                // Process tasks - Dùng deterministic nonce
                var processTasks = Enumerable.Range(0, MAX_PARALLEL_CHUNKS).Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var (index, data) in readChannel.Reader.ReadAllAsync(cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Encrypt với deterministic nonce
                            var encryptedChunk = Encrypt(data, key, index, noncePrefix);

                            // Clear plaintext từ memory
                            Array.Clear(data, 0, data.Length);

                            // Create packet với explicit little-endian
                            var packet = new byte[4 + encryptedChunk.Length];
                            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0), encryptedChunk.Length);
                            Buffer.BlockCopy(encryptedChunk, 0, packet, 4, encryptedChunk.Length);

                            await writeChannel.Writer.WriteAsync((index, packet), cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        writeChannel.Writer.TryComplete(ex);
                        throw;
                    }
                }, cancellationToken)).ToArray();

                // Write task
                var writeTask = Task.Run(async () =>
                {
                    var nextIndex = 0;
                    var buffer = new System.Collections.Generic.Dictionary<int, byte[]>();

                    await foreach (var item in writeChannel.Reader.ReadAllAsync(cancellationToken))
                    {
                        buffer[item.index] = item.packet;

                        // Limit buffer size
                        if (buffer.Count > MAX_PARALLEL_CHUNKS * 4)
                            throw new InvalidOperationException("Write buffer overflow - possible pipeline issue");

                        while (buffer.TryGetValue(nextIndex, out var packet))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            await outputStream.WriteAsync(packet, cancellationToken);
                            Interlocked.Add(ref totalBytesWritten, packet.Length);
                            buffer.Remove(nextIndex);

                            nextIndex++;
                            Interlocked.Exchange(ref processedCount, nextIndex);
                            progress?.Report((double)nextIndex / totalChunks * 100);
                        }
                    }

                    // Flush to disk
                    await outputStream.FlushAsync(cancellationToken);
                }, cancellationToken);

                // Orchestration với proper error propagation
                Exception? primaryException = null;
                try
                {
                    await readTask;
                    await Task.WhenAll(processTasks);
                    writeChannel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    primaryException = ex;
                    writeChannel.Writer.TryComplete(ex);
                }

                // Đợi writeTask và gom exceptions
                try
                {
                    await writeTask;
                }
                catch (Exception writeEx)
                {
                    if (primaryException is not null)
                    {
                        // Có exception gốc, wrap cả hai
                        throw new AggregateException("Multiple errors during encryption", primaryException, writeEx);
                    }
                    throw; // Chỉ có writeTask error
                }

                // Throw primary exception nếu có
                if (primaryException is not null)
                    throw primaryException;
            }
            catch (Exception)
            {
                // Cleanup: xóa file output khi lỗi/cancel
                try
                {
                    outputStream.Dispose();
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { /* Ignore cleanup errors */ }
                throw;
            }
            finally
            {
                // Clear key từ stack (best effort)
                if (key is not null)
                    Array.Clear(key, 0, key.Length);
            }
        }

        /// <summary>
        /// Encrypt file with password (PBKDF2 key derivation)
        /// Format: [Salt(32)][EncryptedFile]
        /// </summary>
        public static async Task EncryptFileAsync(string inputPath, string outputPath, string password,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            // Generate random salt
            var salt = new byte[SALT_SIZE];
            RandomNumberGenerator.Fill(salt);

            // Derive key từ password
            var key = DeriveKeyFromPassword(password, salt);

            // Tạo temp file để encrypt
            var tempOutput = outputPath + ".tmp";

            try
            {
                // Encrypt với key
                await EncryptFileAsync(inputPath, tempOutput, key, progress, cancellationToken);

                // Prepend salt vào đầu file
                using (var finalOutput = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Ghi salt trước
                    await finalOutput.WriteAsync(salt, cancellationToken);

                    // Copy encrypted content
                    using var tempInput = new FileStream(tempOutput, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await tempInput.CopyToAsync(finalOutput, 81920, cancellationToken);
                }

                // Xóa temp file
                File.Delete(tempOutput);
            }
            catch
            {
                // Cleanup
                try
                {
                    if (File.Exists(tempOutput))
                        File.Delete(tempOutput);
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { }
                throw;
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
                Array.Clear(salt, 0, salt.Length);
            }
        }

        /// <summary>
        /// Decrypt file with parallel processing
        /// </summary>
        public static async Task DecryptFileAsync(string inputPath, string outputPath, byte[] key,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("Key must be 128, 192, or 256 bits", nameof(key));

            using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                FileShare.None, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);

            try
            {
                // Read header
                var header = new byte[HEADER_SIZE];
                var read = await inputStream.ReadAsync(header.AsMemory(0, HEADER_SIZE), cancellationToken);
                if (read != HEADER_SIZE)
                    throw new InvalidDataException("Header incomplete");

                var magic = Encoding.ASCII.GetString(header, 0, 4);
                if (magic != MAGIC)
                    throw new InvalidDataException("Invalid file format");

                var version = header[4];
                if (version != VERSION)
                    throw new NotSupportedException($"Unsupported version: {version}");

                // Parse header với explicit little-endian
                var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(5));
                var fileSize = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(9));
                var totalChunks = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(17));

                // Read nonce prefix (8 bytes)
                var noncePrefix = new byte[8];
                if (await inputStream.ReadAsync(noncePrefix.AsMemory(0, 8), cancellationToken) != 8)
                    throw new InvalidDataException("Cannot read nonce prefix");

                // Validate
                if (chunkSize <= 0 || chunkSize > MAX_CHUNK_SIZE)
                    throw new InvalidDataException($"Invalid chunk size: {chunkSize}");

                if (fileSize < 0)
                    throw new InvalidDataException($"Invalid file size: {fileSize}");

                if (totalChunks <= 0 || totalChunks > MAX_CHUNKS_LIMIT)
                    throw new InvalidDataException($"Invalid chunk count: {totalChunks}");

                var expectedChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
                if (totalChunks != expectedChunks)
                    throw new InvalidDataException($"Chunk count mismatch: expected {expectedChunks}, got {totalChunks}");

                // Channels
                var readChannel = Channel.CreateBounded<(int index, byte[] data)>(
                    new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                    {
                        SingleWriter = true,
                        SingleReader = false,
                        FullMode = BoundedChannelFullMode.Wait
                    });
                var writeChannel = Channel.CreateBounded<(int index, byte[] data)>(
                    new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                    {
                        SingleWriter = false,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });

                int processedCount = 0;
                long totalBytesWritten = 0;

                // Read task
                var readTask = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i < totalChunks; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var lengthBytes = new byte[4];
                            if (await inputStream.ReadAsync(lengthBytes.AsMemory(0, 4), cancellationToken) != 4)
                                throw new InvalidDataException($"Chunk {i}: cannot read length");

                            var chunkLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                            var maxEncryptedSize = chunkSize + _NONCE_SIZE + _TAG_SIZE;
                            if (chunkLength <= 0 || chunkLength > maxEncryptedSize)
                                throw new InvalidDataException($"Chunk {i}: invalid length {chunkLength}");

                            var encryptedChunk = new byte[chunkLength];
                            var dataRead = await inputStream.ReadAsync(encryptedChunk, 0, chunkLength, cancellationToken);
                            if (dataRead != chunkLength)
                                throw new InvalidDataException($"Chunk {i}: expected {chunkLength}, got {dataRead}");

                            await readChannel.Writer.WriteAsync((i, encryptedChunk), cancellationToken);
                        }
                    }
                    finally
                    {
                        readChannel.Writer.Complete();
                    }
                }, cancellationToken);

                // Process tasks
                var processTasks = Enumerable.Range(0, MAX_PARALLEL_CHUNKS).Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var chunk in readChannel.Reader.ReadAllAsync(cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var decryptedChunk = Decrypt(chunk.data, key);

                                // Clear encrypted data
                                Array.Clear(chunk.data, 0, chunk.data.Length);

                                await writeChannel.Writer.WriteAsync((chunk.index, decryptedChunk), cancellationToken);
                            }
                            catch (CryptographicException ex)
                            {
                                var error = new InvalidDataException($"Chunk {chunk.index}: decryption failed - data corrupted", ex);
                                writeChannel.Writer.TryComplete(error);
                                throw error;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        writeChannel.Writer.TryComplete(ex);
                        throw;
                    }
                }, cancellationToken)).ToArray();

                // Write task
                var writeTask = Task.Run(async () =>
                {
                    var nextIndex = 0;
                    var buffer = new System.Collections.Generic.Dictionary<int, byte[]>();

                    await foreach (var item in writeChannel.Reader.ReadAllAsync(cancellationToken))
                    {
                        buffer[item.index] = item.data;

                        if (buffer.Count > MAX_PARALLEL_CHUNKS * 4)
                            throw new InvalidOperationException("Write buffer overflow - possible pipeline issue");

                        while (buffer.TryGetValue(nextIndex, out var data))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            await outputStream.WriteAsync(data, cancellationToken);
                            Interlocked.Add(ref totalBytesWritten, data.Length);

                            // Clear decrypted data sau khi ghi
                            Array.Clear(data, 0, data.Length);
                            buffer.Remove(nextIndex);

                            nextIndex++;
                            Interlocked.Exchange(ref processedCount, nextIndex);
                            progress?.Report((double)nextIndex / totalChunks * 100);
                        }
                    }

                    // Validate accumulated bytes
                    if (totalBytesWritten != fileSize)
                        throw new InvalidDataException($"Output size mismatch: expected {fileSize}, got {totalBytesWritten}");

                    await outputStream.FlushAsync(cancellationToken);
                }, cancellationToken);

                // Orchestration với proper error propagation
                Exception? primaryException = null;
                try
                {
                    await readTask;
                    await Task.WhenAll(processTasks);
                    writeChannel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    primaryException = ex;
                    writeChannel.Writer.TryComplete(ex);
                }

                // Đợi writeTask và gom exceptions
                try
                {
                    await writeTask;
                }
                catch (Exception writeEx)
                {
                    if (primaryException is not null)
                    {
                        // Có exception gốc, wrap cả hai
                        throw new AggregateException("Multiple errors during decryption", primaryException, writeEx);
                    }
                    throw; // Chỉ có writeTask error
                }

                // Throw primary exception nếu có
                if (primaryException is not null)
                    throw primaryException;
            }
            catch (Exception)
            {
                // Cleanup: xóa file output khi lỗi/cancel
                try
                {
                    outputStream.Dispose();
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { /* Ignore cleanup errors */ }
                throw;
            }
            finally
            {
                // Clear key
                if (key is not null)
                    Array.Clear(key, 0, key.Length);
            }
        }

        /// <summary>
        /// Decrypt file with password (PBKDF2 key derivation)
        /// </summary>
        public static async Task DecryptFileAsync(string inputPath, string outputPath, string password,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            byte[] key = [];
            byte[] salt = [];
            var tempInput = inputPath + ".tmp";

            try
            {
                // Đọc salt từ đầu file
                using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    salt = new byte[SALT_SIZE];
                    if (await input.ReadAsync(salt.AsMemory(0, SALT_SIZE), cancellationToken) != SALT_SIZE)
                        throw new InvalidDataException("Cannot read salt - file may be corrupted or not encrypted with password");

                    // Tạo temp file không có salt
                    using var temp = new FileStream(tempInput, FileMode.Create, FileAccess.Write, FileShare.None);
                    await input.CopyToAsync(temp, 81920, cancellationToken);
                }

                // Derive key từ password + salt
                key = DeriveKeyFromPassword(password, salt);

                // Decrypt file temp
                await DecryptFileAsync(tempInput, outputPath, key, progress, cancellationToken);

                // Xóa temp file
                File.Delete(tempInput);
            }
            catch
            {
                // Cleanup
                try
                {
                    if (File.Exists(tempInput))
                        File.Delete(tempInput);
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { }
                throw;
            }
            finally
            {
                if (key is not null)
                    Array.Clear(key, 0, key.Length);
                if (salt is not null)
                    Array.Clear(salt, 0, salt.Length);
            }
        }
    }
}
