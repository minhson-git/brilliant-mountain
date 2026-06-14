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
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using static IOApp.Features.Share;

namespace IOApp.Popups
{
    internal sealed partial class PreferencesDialog : DialogEx
    {
        public static PreferencesDialog? Inst { get; private set; }

        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();
        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();
        public SharedStorage SharedStorage { get; } = EncapsulatedSingleton<SharedStorage>.ExposeInstance();

        public IOStatus<PreferencesDialog, S> Status { get; }

        string _errorText = "*";
        public string ErrorText { get => _errorText; set => SetAndNotify(ref _errorText, value); }

        public PreferencesDialog(WindowEx window) : base(window)
        {
            InitializeComponent();
            DataContext = Inst = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                CloseButton.IsEnabled = Status.IsNotBusy;
            };

            InitAllControls();
        }

        void InitAllControls()
        {
            StorageFolderPathTextBox.Text = AppPackageStorage.AppDir;
            Status.SetAndNotify(S.Loaded);
        }

        bool IsAccessPasswordChanged()
        {
            var currentPassword = CurrentPasswordBox.Password.Trim();
            var newPassword = NewPasswordBox.Password.Trim();
            var confirmPassword = ConfirmPasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(currentPassword) && string.IsNullOrWhiteSpace(newPassword) && string.IsNullOrWhiteSpace(confirmPassword))
            {
                ErrorText = R.T(L.PasswordIsNotEmpty);
                return false;
            }

            if (currentPassword == newPassword)
                return false;

            if (newPassword != confirmPassword)
            {
                ErrorText = R.T(L.PasswordAndConfirmationPasswordDoNotMatch);
                return false;
            }

            if (currentPassword != AppLocalStorage.Password)
            {
                ErrorText = R.T(L.IncorrectPassword);
                return false;
            }

            return true;
        }

        async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Control control) return;
            if (control.Tag is not string tag) return;

            if (tag == "ResetStoragePath")
                StorageFolderPathTextBox.Text = Path.Combine(AppDir.PACKAGE_FOLDER, "AppDir");
            else if (tag == "BrowseStoragePath")
            {
                var storageFolder = await Window.Picker.OpenSingleFolder();

                if (storageFolder != null)
                {
                    if (!FileUtils.IsEmptyDirectory(storageFolder.Path))
                        Window.Tip.Message(null, R.T(L.Features_SelectEmptyFolderMessage));
                    else
                        StorageFolderPathTextBox.Text = storageFolder.Path;
                }
            }
            else if (tag == "CopyStoragePath")
            {
                SystemUtils.SetTextToClipboard(StorageFolderPathTextBox.Text, Window.Toast);
                Window.Tip.Message(null, R.T(L.Copied), StorageFolderPathTextBox.Text);
            }
            else if (tag == "ChangeStoragePath")
            {
                var newStoragePath = StorageFolderPathTextBox.Text.Trim();
                if (newStoragePath == AppPackageStorage.AppDir) return;

                var items = Home.Inst.SOURCE_FILE_ITEMS;

                IProgress<Exception> progress = new Progress<Exception>(ex =>
                {
                    if (ex == null)
                        AppInstance.Restart(string.Empty);
                    else
                    {
                        CloseCommand.Execute(null);
                        ErrorText = ex.Message;
                        Window.Cover.ShowLoading(false);
                    }
                });

                IProgress<int> itemProgress = new Progress<int>(_ =>
                {
                    Window.Cover.ShowLoading(true, $"{_}/{items.Count}");
                });

                Window.Tip.Confirm(null, R.T(L.Features_ApplyPreferenceTitle), R.T(L.Features_ApplyPreferenceSubtitle), () =>
                {
                    CloseCommand.Execute(null);
                    Window.Cover.ShowLoading(true);

                    _ = Task.Run(() =>
                    {
                        try
                        {
                            SharedStorage.IsLocked = true;

                            if (newStoragePath == Path.Combine(AppDir.PACKAGE_FOLDER, "AppDir"))
                                FileUtils.DeleteDirectory(newStoragePath, false);

                            var localNewFolderPath = Path.Combine(newStoragePath, ApplicationData.Current.LocalFolder.Name);
                            var encryptionNewFolderPath = Path.Combine(localNewFolderPath, "Encrypted");
                            FileUtils.CreateDirectoryIfNotExist(encryptionNewFolderPath);

                            foreach (var (item, index) in items.Select((v, i) => (v, i)))
                            {
                                DBManager.Inst.Execute<AppDbContext>(context =>
                                {
                                    itemProgress.Report(index + 1);

                                    var oldEncryptedPath = item.EncryptedInfo.FullName;
                                    var newEncryptedPath = Path.Combine(encryptionNewFolderPath, item.EncryptedInfo.Name);

                                    File.Copy(oldEncryptedPath, newEncryptedPath, true);

                                    var fileEntity = context.Files.FirstOrDefault(i => i.Path == oldEncryptedPath);
                                    if (fileEntity != null)
                                    {
                                        fileEntity.Path = newEncryptedPath;
                                        context.SaveChanges();

                                        item.InputInfo.Refresh(newEncryptedPath);
                                        item.EncryptedInfo = new(newEncryptedPath);
                                        item.OriginalInfo = new(item.Footer.Metadata.OriginalName);

                                        FileUtils.Delete(oldEncryptedPath);
                                    }
                                });
                            }

                            string[] dbFileNames = ["main.db", "main.db-shm", "main.db-wal"];

                            foreach (var fileName in dbFileNames)
                                File.Copy(Path.Combine(AppDir.Get(AppDir.Type.LocalFolder), fileName), Path.Combine(localNewFolderPath, fileName), true);

                            AppLocalStorage.CopyTo(Path.Combine(localNewFolderPath, "io-profiles"));

                            AppPackageStorage.OldAppDir = AppPackageStorage.AppDir;
                            AppPackageStorage.AppDir = newStoragePath;
                            SharedStorage.Sync(false);

                            progress.Report(null);
                        }
                        catch (Exception ex)
                        {
                            progress.Report(ex);
                        }
                        finally
                        {
                            SharedStorage.IsLocked = false;
                        }
                    });
                });
            }
            else if (tag == "ChangePassword")
            {
                if (!IsAccessPasswordChanged()) return;

                var items = Home.Inst.SOURCE_FILE_ITEMS.ToList();

                void ui(Exception ex)
                {
                    if (ex == null)
                        AppInstance.Restart(string.Empty);
                    else
                    {
                        Window.Dialog.Open(this);
                        ErrorText = ex.Message;
                        Window.Cover.ShowLoading(false);
                    }
                };

                void iui(int i)
                {
                    Window.Cover.ShowLoading(true, $"{i}/{items.Count}");
                };

                var currentPassword = AppLocalStorage.Password;
                var newPassword = NewPasswordBox.Password.Trim();

                Window.Tip.Confirm(null, R.T(L.Features_ApplyPreferenceTitle), R.T(L.Features_ApplyPreferenceSubtitle), () =>
                    {
                        CloseCommand.Execute(null);
                        Window.Cover.ShowLoading(true);

                        _ = Task.Run(() =>
                        {
                            try
                            {
                                SharedStorage.IsLocked = true;

                                foreach (var (item, index) in items.Select((v, i) => (v, i)))
                                {
                                    try
                                    {
                                        //Progress.Report(() => iui(index + 1));

                                        var originalFileOrFolderPath = item.Footer.Metadata.OriginalName;

                                        var outputFileOrFolderPath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, $"{Guid.NewGuid()}");
                                        outputFileOrFolderPath = Path.ChangeExtension(outputFileOrFolderPath, Path.GetExtension(originalFileOrFolderPath));

                                        CypherFileItem.DecodeOne(item.InputInfo.FullName, outputFileOrFolderPath, currentPassword, true);
                                        CypherFileItem.EncodeOne(outputFileOrFolderPath, item.InputInfo.FullName, newPassword, true);

                                        var itemFooter = FileFooter.Create(item.InputInfo.FullName);
                                        FileUtils.RemoveLastBytesFromFile(item.InputInfo.FullName, itemFooter.Size);

                                        itemFooter.Metadata.OriginalName = originalFileOrFolderPath;
                                        itemFooter.Metadata.FileType = itemFooter.Metadata.IsFile ? ZFile.GetTypeByExtension(Path.GetExtension(originalFileOrFolderPath)) : ZFile.FileType.Directory;

                                        FileFooter.AppendToFile(item.InputInfo.FullName, itemFooter.Metadata, itemFooter.FooterThumbnailBuffer);
                                        item.TryLoadFooter(true);

                                        FileUtils.Delete(outputFileOrFolderPath);
                                    }
                                    catch { }
                                }

                                AppLocalStorage.Password = newPassword;
                                SharedStorage.Sync(false);

                                //Progress.Report(() => ui(null));
                            }
                            catch (Exception ex)
                            {
                                //Progress.Report(() => ui(ex));
                            }
                            finally
                            {
                                SharedStorage.IsLocked = false;
                            }
                        });
                    }
                );
            }
        }
    }
}