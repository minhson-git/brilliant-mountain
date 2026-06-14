using IOCore;
using IOCore.Cryptography;
using IOCore.Exs;
using IOCore.Files;
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
        const int _NONCE_SIZE = 12;
        const int _TAG_SIZE = 16;
        const int CHUNK_SIZE = 1024 * 1024;
        const int MAX_PARALLEL_CHUNKS = 4;
        const int MAX_CHUNKS_LIMIT = 10_000_000;
        const int MAX_CHUNK_SIZE = 100 * 1024 * 1024;
        const int BUFFER_SIZE = 8 * 1024;

        const string MAGIC = "FENC";
        const byte VERSION = 1;
        const int HEADER_SIZE = 24;
        const int PBKDF2_ITERATIONS = 600_000;

        static byte[] Encrypt(byte[] plainBytes, byte[] key, int chunkIndex, ReadOnlySpan<byte> noncePrefix)
        {
            var cipherSize = plainBytes.Length;
            var result = new byte[_NONCE_SIZE + _TAG_SIZE + cipherSize];
            var nonce = result.AsSpan(0, _NONCE_SIZE);
            var tag = result.AsSpan(_NONCE_SIZE, _TAG_SIZE);
            var cipherBytes = result.AsSpan(_NONCE_SIZE + _TAG_SIZE, cipherSize);

            BinaryPrimitives.WriteInt32LittleEndian(nonce, chunkIndex);
            noncePrefix.CopyTo(nonce[4..]);

            using var aes = new AesGcm(key, _TAG_SIZE);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            return result;
        }

        static byte[] Decrypt(byte[] encrypted, byte[] key)
        {
            var cipherSize = encrypted.Length - _NONCE_SIZE - _TAG_SIZE;
            var nonce = encrypted.AsSpan(0, _NONCE_SIZE);
            var tag = encrypted.AsSpan(_NONCE_SIZE, _TAG_SIZE);
            var cipherBytes = encrypted.AsSpan(_NONCE_SIZE + _TAG_SIZE, cipherSize);
            var plainBytes = new byte[cipherSize];

            using var aes = new AesGcm(key, _TAG_SIZE);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return plainBytes;
        }

        public static async Task EncryptFileAsync(string inputPath, string outputPath, string password, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            PasswordException.ThrowIfNullOrWhiteSpace(password);

            var tempOutputPath = outputPath + ".tmp";

            try
            {
                var key = AesUtils.DeriveKeyFromPassword(password, AesUtils.DEFAULT_SALT, PBKDF2_ITERATIONS).Take(32).ToArray();

                var fileInfo = new FileInfo(inputPath);
                var fileSize = fileInfo.Length;
                var totalChunks = (int)Math.Ceiling((double)fileSize / CHUNK_SIZE);

                if (totalChunks > MAX_CHUNKS_LIMIT)
                    throw new ArgumentException($"File too large: {totalChunks} chunks exceeds limit of {MAX_CHUNKS_LIMIT}");

                using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);
                using var outputStream = new FileStream(tempOutputPath, FileMode.Create, FileAccess.Write, FileShare.None, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);

                try
                {
                    var headerOffset = 0;

                    var header = new byte[HEADER_SIZE];

                    Encoding.ASCII.GetBytes(MAGIC).CopyTo(header, 0);
                    headerOffset += MAGIC.Length;

                    header[headerOffset] = VERSION;
                    headerOffset += 1;

                    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(headerOffset), CHUNK_SIZE);
                    headerOffset += sizeof(int);

                    BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(9), fileSize);
                    headerOffset += sizeof(long);

                    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(17), totalChunks);

                    await outputStream.WriteAsync(header, cancellationToken);

                    var noncePrefix = new byte[8];
                    RandomNumberGenerator.Fill(noncePrefix);

                    await outputStream.WriteAsync(noncePrefix, cancellationToken);

                    var writeChannel = Channel.CreateBounded<(int index, byte[] packet)>(
                        new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                        {
                            SingleWriter = false,
                            SingleReader = true,
                            FullMode = BoundedChannelFullMode.Wait
                        });
                    var readChannel = Channel.CreateBounded<(int index, byte[] data)>(
                        new BoundedChannelOptions(MAX_PARALLEL_CHUNKS * 2)
                        {
                            SingleWriter = true,
                            SingleReader = false,
                            FullMode = BoundedChannelFullMode.Wait
                        });

                    int processedCount = 0;
                    long totalBytesWritten = 0;

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

                    var processTasks = Enumerable.Range(0, MAX_PARALLEL_CHUNKS).Select(_ => Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var (index, data) in readChannel.Reader.ReadAllAsync(cancellationToken))
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var encryptedChunk = Encrypt(data, key, index, noncePrefix);

                                Array.Clear(data, 0, data.Length);

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

                    var writeTask = Task.Run(async () =>
                    {
                        var nextIndex = 0;
                        var buffer = new System.Collections.Generic.Dictionary<int, byte[]>();

                        await foreach (var item in writeChannel.Reader.ReadAllAsync(cancellationToken))
                        {
                            buffer[item.index] = item.packet;

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

                        await outputStream.FlushAsync(cancellationToken);
                        await outputStream.DisposeAsync();
                    }, cancellationToken);

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

                    try
                    {
                        await writeTask;
                    }
                    catch (Exception writeEx)
                    {
                        if (primaryException is not null)
                            throw new AggregateException("Multiple errors during encryption", primaryException, writeEx);

                        throw;
                    }

                    if (primaryException is not null)
                        throw primaryException;
                }
                catch (Exception)
                {
                    outputStream.Dispose();
                    FileUtils.Delete(outputPath, false);

                    throw;
                }

                using var finalOutput = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var tempInput = new FileStream(tempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await tempInput.CopyToAsync(finalOutput, BUFFER_SIZE, cancellationToken);

                FileUtils.Delete(tempOutputPath);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                FileUtils.Delete(tempOutputPath);
                FileUtils.Delete(outputPath);
            }
        }

        public static async Task DecryptFileAsync(string inputPath, string outputPath, string password, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            var tempInputPath = inputPath + ".tmp";

            try
            {
                var key = AesUtils.DeriveKeyFromPassword(password, AesUtils.DEFAULT_SALT, PBKDF2_ITERATIONS).Take(32).ToArray();

                using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using var temp = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await input.CopyToAsync(temp, BUFFER_SIZE, cancellationToken);
                }

                using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);
                using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, CHUNK_SIZE, FileOptions.SequentialScan | FileOptions.Asynchronous);

                try
                {
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

                    var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(5));
                    var fileSize = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(9));
                    var totalChunks = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(17));

                    var noncePrefix = new byte[8];
                    if (await inputStream.ReadAsync(noncePrefix.AsMemory(0, 8), cancellationToken) != 8)
                        throw new InvalidDataException("Cannot read nonce prefix");

                    if (chunkSize <= 0 || chunkSize > MAX_CHUNK_SIZE)
                        throw new InvalidDataException($"Invalid chunk size: {chunkSize}");

                    if (fileSize < 0)
                        throw new InvalidDataException($"Invalid file size: {fileSize}");

                    if (totalChunks <= 0 || totalChunks > MAX_CHUNKS_LIMIT)
                        throw new InvalidDataException($"Invalid chunk count: {totalChunks}");

                    var expectedChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
                    if (totalChunks != expectedChunks)
                        throw new InvalidDataException($"Chunk count mismatch: expected {expectedChunks}, got {totalChunks}");

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
                                var dataRead = await inputStream.ReadAsync(encryptedChunk.AsMemory(0, chunkLength), cancellationToken);
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

                    var processTasks = Enumerable.Range(0, MAX_PARALLEL_CHUNKS).Select(_ => Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var (index, data) in readChannel.Reader.ReadAllAsync(cancellationToken))
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                try
                                {
                                    var decryptedChunk = Decrypt(data, key);

                                    Array.Clear(data, 0, data.Length);

                                    await writeChannel.Writer.WriteAsync((index, decryptedChunk), cancellationToken);
                                }
                                catch (CryptographicException ex)
                                {
                                    var error = new InvalidDataException($"Chunk {index}: decryption failed - data corrupted", ex);
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

                                Array.Clear(data, 0, data.Length);
                                buffer.Remove(nextIndex);

                                nextIndex++;
                                Interlocked.Exchange(ref processedCount, nextIndex);
                                progress?.Report((double)nextIndex / totalChunks * 100);
                            }
                        }

                        if (totalBytesWritten != fileSize)
                            throw new InvalidDataException($"Output size mismatch: expected {fileSize}, got {totalBytesWritten}");

                        await outputStream.FlushAsync(cancellationToken);
                    }, cancellationToken);

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

                    try
                    {
                        await writeTask;
                    }
                    catch (Exception writeEx)
                    {
                        if (primaryException is not null)
                            throw new AggregateException("Multiple errors during decryption", primaryException, writeEx);
                        throw;
                    }

                    if (primaryException is not null)
                        throw primaryException;
                }
                catch
                {
                    FileUtils.Delete(outputPath);
                    throw;
                }

                FileUtils.Delete(tempInputPath);
            }
            catch
            {
                FileUtils.Delete(tempInputPath);
                FileUtils.Delete(outputPath);
            }
        }
    }
}
