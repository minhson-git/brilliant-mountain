using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZAnnotation.IOCore;
using static IOApp.Configs.AppTypes;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class SecureVolumeDialog : VolumeFeatureDialog
    {
        public ObservableCollectionEx<OptionItem<SecureType>> SecureTypeItems { get; } = [];
        public int SecureTypeIndex
        {
            get;
            private set
            {
                if (SetAndNotify(ref field, value))
                    Notify(nameof(SecureType));
            }
        } = -1;
        public SecureType SecureType => SecureTypeItems.GetAtOrDefault(SecureTypeIndex, SecureType.Unsecured);

        public string Password { get; private set => SetAndNotify(ref field, value); } = string.Empty;
        public string ConfirmPassword { get; private set => SetAndNotify(ref field, value); } = string.Empty;

        public bool IsSecureOrUnsecure { get; private set => SetAndNotify(ref field, value); }

        public bool HasPassword => VolumeItems.TargetItems.Any(i => i.SecureType is SecureType.EncryptFilesAndFolders or SecureType.HideAndEncryptFilesAndFolders);

        public SecureVolumeDialog(WindowEx window, VolumeItem? item, IEnumerable<VolumeItem> items) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            VolumeItem = item;
            VolumeItems.Replace(items);

            foreach (var v in VolumeItems.Items)
            {
                if (item == null)
                    v.IsChecked = true;
                else
                {
                    if (v.Target.DeviceID == item.DeviceID && v.Target.VolumeName == item.VolumeName)
                        v.IsChecked = true;
                }

                v.Target.Status.SetAndNotify(StorageItem.S.Ready);
            }

            VolumeItems.CollectionChanged += (_, _) => Notify(nameof(HasPassword));
            Notify(nameof(HasPassword));

            if (item == null)
                items.FirstOrDefault().Let(item => IsSecureOrUnsecure = item.SecureType is SecureType.Unsecured);
            else
                IsSecureOrUnsecure = item.SecureType is SecureType.Unsecured;

            SecureTypeItems.Replace(SECURE_TYPES.Where(i => i.Key is not SecureType.Unsecured).Select(i => new OptionItem<SecureType>(i.Key, R.T(i.Value.Item1))));
            SecureTypeIndex = SecureTypeItems.IndexOf(i => i.Key is SecureType.HideFilesAndFolders);

            Password = ConfirmPassword = string.Empty;
        }

        async void Secure()
        {
            if (Status.IsBusy) return;

            var password = Password.Trim();
            var confirmPassword = ConfirmPassword.Trim();

            if (SecureType is SecureType.EncryptFilesAndFolders)
            {
                if (password is "")
                {
                    Window.Tip.Message(null, R.T(L.PasswordIsNotEmpty), null);
                    return;
                }

                if (password != confirmPassword)
                {
                    Window.Tip.Message(null, R.T(L.PasswordAndConfirmationPasswordDoNotMatch), null);
                    return;
                }
            }

            foreach (var v in VolumeItems.CheckedItems)
                if (v.Status.IsNotProcessed)
                    v.Status.SetAndNotify(StorageItem.S.ProcessInQueue);

            Status.SetAndNotify(S.Processing);

            foreach (var v in VolumeItems.CheckedItems)
            {
                if (v.Status.IsNotProcessInQueue)
                    continue;

                try
                {
                    if (Window.License.Status.IsTrial)
                        ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                    if (!FileUtils.HasEnoughFreeSpace(AppDir.DEFAULT_APP_DIR, v.Size - v.FreeSpace))
                        throw new Exception(R.T(L.Exception_NotEnoughDiskSpace));

                    if (v.SecureType is not SecureType.Unsecured)
                        throw new Exception(R.T(L.Exception_VolumeAlreadyLocked));

                    var emptyDrive = true;
                    foreach (var filePath in FileUtils.GetFiles(v.Path, "*"))
                    {
                        emptyDrive = false;
                        break;
                    }

                    if (emptyDrive)
                        throw new Exception(R.T(L.Exception_EmptyDrive));

                    if (SecureType is SecureType.HideFilesAndFolders)
                    {
                        foreach (var filePath in FileUtils.GetFiles(v.Path, "*"))
                            FileUtils.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden, true);

                        foreach (var directoryPath in FileUtils.GetDirectories(v.Path, "*"))
                            FileUtils.SetAttributes(directoryPath, File.GetAttributes(directoryPath) | FileAttributes.Hidden, true);
                    }
                    else
                    {
                        var tempVolumeOnMachinePath = Path.Combine(AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, FormatUtils.UID), v.Letter);

                        await RoboCommandHelper.FastCopyDirectory(v.Path, tempVolumeOnMachinePath);

                        await Task.Run(async () =>
                        {
                            foreach (var i in FileUtils.GetFiles(tempVolumeOnMachinePath, "*"))
                            {
                                await CypherFileItem.EncodeOne(i, i, password, true, true);

                                if (SecureType is SecureType.HideAndEncryptFilesAndFolders)
                                    FileUtils.SetAttributes(i, File.GetAttributes(i) | FileAttributes.Hidden, true);
                            }
                        });

                        await RoboCommandHelper.FastMoveDirectory(tempVolumeOnMachinePath, v.Path, true);
                    }

                    v.SecureType = SecureType;

                    v.SecureMetadataSaver.Data.SecureType = v.SecureType;
                    v.SecureMetadataSaver.Data.Password = password;
                    v.SecureMetadataSaver.Save();

                    FileUtils.SetAttributes(EnvironmentUtils.GetHiddenPath(v.Path), File.GetAttributes(EnvironmentUtils.GetHiddenPath(v.Path)) | FileAttributes.Hidden, true);

                    v.Status.SetAndNotify(StorageItem.S.Processed);
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentOutOfRangeException)
                    {
                        v.Message = string.Format(R.T(L.Premium_Title), Constants.LIMIT_TEXT);
                        v.Status.SetAndNotify(StorageItem.S.ProcessStopped);
                    }
                    else
                    {
                        v.Message = ex.Message;
                        v.Status.SetAndNotify(StorageItem.S.ProcessFailed);
                    }
                }
            }

            Status.SetAndNotify(S.Processed);

            Window.Tip.Message(null, R.T(L.Status_Processed));
        }

        async void Unsecure()
        {
            if (Status.IsBusy)
                return;

            var password = Password.Trim();

            if (VolumeItems.CheckedItems.Any(i => i.SecureType is SecureType.EncryptFilesAndFolders))
                if (password == "")
                {
                    Window.Tip.Message(null, R.T(L.PasswordIsNotEmpty), null);
                    return;
                }

            foreach (var v in VolumeItems.CheckedItems)
                if (v.Status.IsNotProcessed)
                    v.Status.SetAndNotify(StorageItem.S.ProcessInQueue);

            Status.SetAndNotify(S.Processing);

            foreach (var v in VolumeItems.CheckedItems)
            {
                if (v.Status.IsNotProcessInQueue)
                    continue;

                try
                {
                    if (Window.License.Status.IsTrial)
                        ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                    if (!File.Exists(VolumeItem.GetMetadataFilePath(v.Path)) || v.SecureType is SecureType.Unsecured)
                        throw new Exception(R.T(L.Exception_VolumeNotLocked));

                    var emptyDrive = true;
                    foreach (var filePath in FileUtils.GetFiles(v.Path, "*"))
                    {
                        emptyDrive = false;
                        break;
                    }

                    if (emptyDrive)
                        throw new Exception(R.T(L.Exception_EmptyDrive));

                    v.SecureType = v.SecureType;

                    if (v.SecureType is not SecureType.HideFilesAndFolders)
                    {
                        PasswordException.ThrowIfNotCorrect(v.SecureMetadataSaver.Data.Password, password);

                        await Task.Run(async () =>
                        {
                            var filePaths = FileUtils.GetFiles(v.Path, "*.*").Where(i => !PathUtils.Is(i, VolumeItem.GetMetadataFilePath(v.Path))).ToList();
                            foreach (var filePath in filePaths)
                                await CypherFileItem.DecodeOne(filePath, filePath, password, true);
                        });
                    }

                    if (v.SecureType is not SecureType.EncryptFilesAndFolders)
                    {
                        foreach (var filePath in FileUtils.GetFiles(v.Path, "*"))
                            FileUtils.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.Hidden, true);

                        foreach (var directoryPath in FileUtils.GetDirectories(v.Path, "*"))
                            FileUtils.SetAttributes(directoryPath, File.GetAttributes(directoryPath) & ~FileAttributes.Hidden, true);
                    }

                    FileUtils.Delete(EnvironmentUtils.GetHiddenPath(v.Path));

                    v.SecureType = SecureType.Unsecured;
                    v.Status.SetAndNotify(StorageItem.S.Processed);
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentOutOfRangeException)
                    {
                        v.Message = string.Format(R.T(L.Premium_Title), Constants.LIMIT_TEXT);
                        v.Status.SetAndNotify(StorageItem.S.ProcessStopped);
                    }
                    else if (ex is PasswordException pex && pex.Kind is PasswordException.ExceptionKind.NotCorrect)
                    {
                        v.Message = R.T(L.IncorrectPassword);
                        v.Status.SetAndNotify(StorageItem.S.ProcessFailed);
                    }
                    else
                    {
                        v.Message = ex.Message;
                        v.Status.SetAndNotify(StorageItem.S.ProcessFailed);
                    }
                }
            }

            Status.SetAndNotify(S.Processed);

            Window.Tip.Message(null, R.T(L.Status_Processed));
        }

        [RelayCommand]
        void SecureOrUnsecure()
        {
            if (IsSecureOrUnsecure)
                Secure();
            else
                Unsecure();
        }
    }
}