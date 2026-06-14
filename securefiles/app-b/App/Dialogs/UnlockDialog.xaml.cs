using IOApp.Features;
using IOApp.Pages;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Utils;
using IOData;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static IOApp.Features.Share;

namespace IOApp.Popups
{
    internal partial class UnlockDialog : DialogEx
    {
        public IOStatus<UnlockDialog, S> Status { get; }

        public ObservableCollectionEx<PrivateFileItem> FileItems { get; private set; } = [];

        ulong _processTimestamp = 0UL;
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        //

        string _lastRun = "00:00:00";
        public string LastRun { get => _lastRun; set => SetAndNotify(ref _lastRun, value); }

        string _timeRun = "00:00:00";
        public string TimeRun { get => _timeRun; set => SetAndNotify(ref _timeRun, value); }

        public UnlockDialog(WindowEx window, IEnumerable<PrivateFileItem> fileItems) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.SetBusy(Status.IsBusy);

                if (Status.Any(S.Processing))
                {
                    EnableAllControls(false);
                    _timer.Start();
                }
                else if (Status.Any(S.Processed))
                {
                    EnableAllControls(true);
                    _timer.Stop();
                }
                else if (Status.Any(S.ProcessPausing))
                {
                    EnableAllControls(false);
                }
                else if (Status.Any(S.ProcessPaused))
                {
                    EnableAllControls(false);
                    _timer.Stop();
                }
                else if (Status.Any(S.ProcessStopping))
                {
                    if (Status.PrevAny(S.ProcessPaused))
                        EnableAllControls(true);
                    else
                        EnableAllControls(false);

                    _timer.Stop();
                }
                else if (Status.Any(S.ProcessStopped))
                {
                    EnableAllControls(true);
                    _timer.Stop();
                }
                else if (Status.Any(S.ProcessFailed))
                {
                    EnableAllControls(true);
                    _timer.Stop();
                }
            };

            FileItems.Replace(fileItems);

            InitAllControls();
        }

        void InitAllControls()
        {
            void fileItemsCollectionChangedAction()
            {
                if (!Status.Any(S.Loading))
                    ProcessAllButton.Visibility = Visibility.Visible;

                if (FileItems.Count > 0)
                    FileListView.Visibility = Visibility.Visible;
                else
                    FileListView.Visibility = Visibility.Collapsed;

                EnableAllControls(FileItems.Count > 0);

                Notify(nameof(FileItems));
            }

            fileItemsCollectionChangedAction();
            FileItems.CollectionChanged += (sender, e) => fileItemsCollectionChangedAction();

            _timer.Tick += (sender, e) =>
            {
                _processTimestamp++;
                TimeRun = TimeSpan.FromSeconds(_processTimestamp).ToString(@"hh\:mm\:ss");
            };
        }

        void RemoveOneButton_Click(object sender, RoutedEventArgs e)
        {
            if (Status.Any(S.Loading, S.Processing)) return;
            if ((sender as FrameworkElement)?.DataContext is not PrivateFileItem item) return;

            FileItems.Remove(item);
        }

        void EnableAllControls(bool isEnabled)
        {
            InputPasswordBox.IsEnabled = isEnabled;
            ProcessAllButton.IsEnabled = isEnabled;
        }

        public void ImportFilesOrFolders(Action startAction = null, Action<S> endAction = null, Action<List<PrivateFileItem>> packageAction = null)
        {
            startAction?.Invoke();

            FileItems.ForEach(i => i.Status.SetAndNotify(FileItem.S.ProcessInQueue));

            var locker = new object();

            var password = InputPasswordBox.Password.Trim();
            var coreCount = Environment.ProcessorCount;

            _ = Task.Run(() =>
            {
                try
                {
                    var packages = FileItems.Chunk(16);

                    foreach (var package in packages)
                    {
                        var items = new List<PrivateFileItem>();
                        var itemChunksPerPackage = package.Chunk(coreCount);

                        foreach (var itemChunk in itemChunksPerPackage)
                        {
                            _ = Parallel.ForEach(itemChunk, item =>
                            {
                                //try
                                //{
                                //    item.Status.Set(FileItem.S.Processing);
                                //    item.SetMessage(string.Empty, false);

                                //    item.TryLoadFooter(true);
                                //    if (item.Footer == null)
                                //        throw new Exception("File corrupted.");

                                //    if (!File.Exists(item.InputInfo.FullName))
                                //        throw new IOException();

                                //    var inputFilePath = item.InputInfo.FullName;
                                //    var tempInputFilePath = Path.Combine(AppDir.Get(AppDir.Type.TemporaryFolder), Guid.NewGuid().ToString());

                                //    File.Copy(inputFilePath, tempInputFilePath, true);

                                //    var itemOriginalFooter = FileFooter.Create(tempInputFilePath);
                                //    if (itemOriginalFooter.Extra.IsThumbnailEncrypted)
                                //        itemOriginalFooter.FooterThumbnailBuffer = CryptographyUtils.DecryptToBytes(itemOriginalFooter.FooterThumbnailBuffer, AppLocalStorage.Password, Constants.SALT, Constants.ITERATIONS, true);

                                //    var decryptedFilePath = Path.Combine(AppDir.Get(AppDir.Type.TemporaryFolder), Guid.NewGuid().ToString());
                                //    if (itemOriginalFooter.Metadata.IsFile)
                                //        decryptedFilePath = Path.ChangeExtension(decryptedFilePath, Path.GetExtension(itemOriginalFooter.Metadata.OriginalName));

                                //    CypherFileItem.DecodeOne(tempInputFilePath, decryptedFilePath, password, true);

                                //    var encryptedFilePath = Path.Combine(AppDir.Get(AppDir.Type.LocalFolder), "Encrypted", Guid.NewGuid().ToString());
                                //    CypherFileItem.EncodeOne(decryptedFilePath, encryptedFilePath, AppLocalStorage.Password, true);

                                //    var itemNewFooter = FileFooter.Create(encryptedFilePath);

                                //    FileUtils.RemoveLastBytesFromFile(encryptedFilePath, itemNewFooter.Size);
                                //    FileFooter.AppendToFile(encryptedFilePath, itemOriginalFooter.Metadata, itemNewFooter.FooterThumbnailBuffer);

                                //    item.InputInfo = new(encryptedFilePath);
                                //    item.EncryptedInfo = new(encryptedFilePath);

                                //    if (item.TryLoadFooter(true))
                                //        item.LoadThumbnailFromFooter(Constants.THUMBNAIL_ENCRYPT_PASSWORD);

                                //    item.CorrectFileType(item.Footer.Metadata.FileType);
                                //    item.OriginalInfo = new(item.Footer.Metadata.OriginalName);

                                //    DBManager.Execute<AppDbContext>(context =>
                                //    {
                                //        context.Files.Add(new(item.EncryptedInfo.FullName)
                                //        {
                                //            FileType = item.FileType,
                                //            CensorType = item.Censor,
                                //        });

                                //        context.SaveChanges();

                                //        item.Status.Set(FileItem.S.Processed);
                                //        item.SetMessage(string.Empty, false);

                                //        FileUtils.Delete(inputFilePath);
                                //        FileUtils.Delete(decryptedFilePath);
                                //    });
                                //}
                                //catch (Exception ex)
                                //{
                                //    item.Status.Set(FileItem.S.ProcessFailed);

                                //    if (ex is IOException)
                                //        item.SetMessage(R.T(L.FileOrFolderDoesNotExist), false);
                                //    else
                                //        item.SetMessage(R.T(L.IncorrectPassword), false);
                                //}
                                //finally
                                //{
                                //    lock (locker)
                                //    {
                                //        items.Add(item);
                                //    }
                                //}
                            });
                        }

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            lock (locker)
                            {
                                packageAction?.Invoke(items);
                            }
                        });
                    }

                    DispatcherQueue.TryEnqueue(() => endAction?.Invoke(S.Processed));
                }
                catch (Exception)
                {
                    DispatcherQueue.TryEnqueue(() => endAction?.Invoke(S.ProcessFailed));
                }
            });
        }

        void ProcessAllButton_Click(object sender, RoutedEventArgs e)
        {
            ImportFilesOrFolders(
                () => Status.SetAndNotify(S.Processing),
                s =>
                {
                    if (FileItems.Count == 0)
                    {
                        //Home.ApplyFilter();
                        //Close();
                    }

                    Status.SetAndNotify(s);
                },
                items =>
                {
                    foreach (var i in items)
                        i.NotifyAll();

                    FileItems.Remove(i => i.Status.Any(FileItem.S.Processed));

                    var processedItems = items.Where(i => i.Status.Any(FileItem.S.Processed));

                    //Home.SOURCE_FILE_ITEMS.AddRange(processedItems);
                    //Home.FileItems.Add(processedItems);
                }
            );
        }
    }
}