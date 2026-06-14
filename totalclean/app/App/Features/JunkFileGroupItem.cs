using IOCore;
using IOCore.Base;
using IOCore.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace IOApp.Features
{
    public partial class JunkFileGroupItem : BaseItem
    {
        public enum S
        {
            Loading,
            Ready,

            ProcessInQueue,
            Processing,

            Processed,
            ProcessFailed,

            ProcessPaused,
            ProcessStopped
        };

        [Flags]
        public enum SHERB_FLAGS : uint
        {
            NOCONFIRMATION = 0x00000001,
            NOPROGRESSUI = 0x00000002,
            NOSOUND = 0x00000004
        }

        public IOStatus<JunkFileGroupItem, S> Status { get; }

        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsRecycleBin { get; set; }
        public List<string> Paths { get; set; } = [];

        long _totalSize = 0;
        public long TotalSize { get => _totalSize; set => SetAndNotify(ref _totalSize, value); }

        public CancellationTokenSource CancellationTokenSource = new();

        public JunkFileGroupItem()
        {
            Status = new(this);
        }

        public JunkFileGroupItem(string name, string icon, List<string> pathItems, bool isRecycleBin = false) : this()
        {
            Name = name;
            Icon = icon;
            IsRecycleBin = isRecycleBin;

            Paths = pathItems;

            _isSelected = true;
            _isActivated = false;
        }

        public async Task Scan(Action<Action> action)
        {
            await Task.Run(() =>
            {
                long totalSize = 0L;
                CancellationTokenSource = new();

                try
                {
                    if (IsRecycleBin)
                    {
                        if (CancellationTokenSource.IsCancellationRequested)
                            throw new OperationCanceledException();

                        var recycleBinInfo = new SHQUERYRBINFO
                        {
                            cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>()
                        };

                        var result = PInvoke.SHQueryRecycleBin(null, ref recycleBinInfo);

                        if (result == 0)
                            totalSize = recycleBinInfo.i64Size;
                    }
                    else
                    {
                        foreach (var path in Paths)
                        {
                            if (CancellationTokenSource.IsCancellationRequested)
                                throw new OperationCanceledException();

                            if (Directory.Exists(path))
                                totalSize += FileUtils.GetDirectorySize(path);
                        }
                    }

                    action.Invoke(() =>
                    {
                        TotalSize = totalSize;
                        IsActivated = TotalSize > 0;

                        Notify(null);
                        Status.SetAndNotify(S.Processed);
                    });
                }
                catch (Exception)
                {
                    action.Invoke(() =>
                    {
                        Notify(null);   
                        Status.SetAndNotify(S.ProcessFailed);
                    });
                }
            });
        }

        public async Task Clean(Action<Action> action)
        {
            await Task.Run(() =>
            {
                CancellationTokenSource = new();

                try
                {
                    if (IsRecycleBin)
                    {
                        if (CancellationTokenSource.IsCancellationRequested)
                            throw new OperationCanceledException();

                        try
                        {
                            var flags = (uint)(SHERB_FLAGS.NOCONFIRMATION | SHERB_FLAGS.NOPROGRESSUI | SHERB_FLAGS.NOSOUND);
                            PInvoke.SHEmptyRecycleBin(HWND.Null, string.Empty, flags);
                            action.Invoke(() => TotalSize = 0);
                        }
                        catch { }
                    }
                    else
                    {
                        foreach (var path in Paths)
                        {
                            if (CancellationTokenSource.IsCancellationRequested)
                                throw new OperationCanceledException();

                            var pathSize = FileUtils.GetDirectorySize(path);

                            try
                            {
                                if (!Directory.Exists(path)) continue;

                                foreach (var filePath in Directory.GetFiles(path))
                                    FileUtils.Delete(filePath);

                                foreach (var dirPath in Directory.GetDirectories(path))
                                {
                                    if (dirPath.Contains("")) continue;

                                    FileUtils.Delete(dirPath);
                                }

                                action.Invoke(() => TotalSize = Math.Clamp(TotalSize - pathSize, 0, TotalSize));
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(e);
                                continue;
                            }
                        }
                    }

                    var finalSize = Paths.Select(i => FileUtils.GetDirectorySize(i)).Sum();

                    action.Invoke(() =>
                    {
                        TotalSize = finalSize;
                        IsActivated = TotalSize > 0;

                        Notify(null);
                        Status.SetAndNotify(S.Processed);
                    });
                }
                catch
                {
                    action.Invoke(() =>
                    {
                        Notify(null);
                        Status.SetAndNotify(S.ProcessFailed);
                    });
                }
            });
        }
    }
}