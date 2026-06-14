using CommunityToolkit.Mvvm.Input;
using FFMpegCore;
using IOApp.Dialogs;
using IOApp.Features;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Libs;
using IOCore.Premium;
using IOCore.Utils;
using IOMedia.Media;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;
using static IOCore.Files.MediaTypes;

namespace IOApp.Pages
{
    internal partial class Home : PlayerPage
    {
        public ConverterConfig ConverterConfig { get; } = EncapsulatedSingleton<ConverterConfig>.ExposeInstance();

        MediaItem? _videoItem;
        public MediaItem? VideoItem { get => _videoItem; private set => SetAndNotify(ref _videoItem, value); }

        MediaItem? _audioItem;
        public MediaItem? AudioItem { get => _audioItem; private set => SetAndNotify(ref _audioItem, value); }

        float _videoSpeedValue = 1.0f;
        public float VideoSpeedValue
        {
            get => _videoSpeedValue;
            set
            {
                SetAndNotify(ref _videoSpeedValue, value);
                PlayerVideo.Player.Speed = _videoSpeedValue;
            }
        }

        float _audioSpeedValue = 1.0f;
        public float AudioSpeedValue
        {
            get => _audioSpeedValue;
            set
            {
                SetAndNotify(ref _audioSpeedValue, value);
                PlayerAudio.Player.Speed = _audioSpeedValue;
            }
        }

        int _percent = 0;
        public int Percent { get => _percent; set => SetAndNotify(ref _percent, value); }

        public string OutputPath { get; set; } = string.Empty;

        public Home()
        {
            InitializeComponent();
            DataContext = this;
        }

        void LoadMedia(string path, MediaType mediaType, Action<MediaItem> setItem, Action notifyAction, Action setSpeedValue)
        {
            if (Status.IsBusy)
                return;

            try
            {
                Status.SetAndNotify(S.Loading);

                var player = mediaType == MediaType.Video ? PlayerVideo : PlayerAudio;

                player.Playlist.Clear();

                if (!player.Playlist.Any(i => i.InputInfo.FullName == path))
                {
                    if (!PlayerConfig.IsInputAccepted(path, mediaType))
                        throw new UnacceptedInputException();

                    var item = MediaItem.Create<PlayerItem>(path);
                    item.InputInfo.Analyze();

                    player.Playlist.Replace([item]);
                    AddItem(item, mediaType, true);
                }

                if (!PlayerConfig.IsInputAccepted(path, mediaType))
                    throw new UnacceptedInputException();

                var info = MediaInfoBase.Create(path);
                info.Analyze();

                if (info.IsCorrupted)
                    throw new Exception();

                var mediaItem = MediaItem.Create(info);
                setItem(mediaItem);

                var outputFamilies = mediaType == MediaType.Video
                    ? ConverterConfig.OutputVideoFamilies.Select(i => i.Family)
                    : ConverterConfig.OutputAudioFamilies.Select(i => i.Family);

                mediaItem.ConvertArgv.Build(outputFamilies);

                setSpeedValue();

                LimitIfTrial(mediaItem);

                mediaItem.Thumbnail.MediaInfoBase = mediaItem.InputInfo;
                mediaItem.Thumbnail.NeedBitmap = true;

                if (Window.LicenseStatus.IsTrial)
                {
                    var slug = ConverterConfig.GetExtra(path, true);
                    Promotion.Inst.Offer(items =>
                    {
                        PromotionAppItems.Replace(items);
                        Notify(nameof(PromotionAppItems));
                    }, 1, null, slug == null ? null : [slug]);
                }

                Status.SetAndNotify(S.Loaded);
            }
            catch
            {
                Status.SetAndNotify(S.LoadFailed);
            }
            finally
            {
                notifyAction();
            }
        }

        public void LoadVideo(string path) => LoadMedia(path, MediaType.Video, item => VideoItem = item,
            () => Notify(nameof(VideoItem), nameof(VideoSpeedValue)),
            () => VideoSpeedValue = 1);

        public void LoadAudio(string path) => LoadMedia(path, MediaType.Audio, item => AudioItem = item,
            () => Notify(nameof(AudioItem), nameof(AudioSpeedValue)),
            () => AudioSpeedValue = 1);

        public void LimitIfTrial(MediaItem item)
        {
            if (Window.LicenseStatus.IsTrial)
            {
                item.ConvertArgv.Families.ForEach(outputItem =>
                {
                    if (outputItem.Key != FamilyType.V_Mp4)
                    {
                        outputItem.Value = $"{PremiumCore.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
                });

                item.ConvertArgv.Resolutions.ForEach(outputItem =>
                {
                    if (outputItem.Key != Resolution.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
                });

                item.ConvertArgv.VideoBitrates.ForEach(outputItem =>
                {
                    if (outputItem.Key != VideoBitrate.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
                });

                item.ConvertArgv.AudioBitrates.ForEach(outputItem =>
                {
                    if (outputItem.Key != AudioBitrate.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
                });
            }
        }

        [RelayCommand]
        void OpenDownloadDialog(string type)
        {
            if (type == "Video")
            {
                var dialog = new DownloaderDialog(Window, MediaType.Video)
                {
                    OpenDownloadedFile = LoadVideo
                };
                Window.Dialog.Open(dialog);
            }    
            else if (type == "Audio")
            {
                var dialog = new DownloaderDialog(Window, MediaType.Media)
                {
                    OpenDownloadedFile = LoadAudio
                };
                Window.Dialog.Open(dialog);
            }    
        }

        [RelayCommand]
        void ApplyTransform(string type)
        {
            if (VideoItem == null)
                return;

            if (type == "HorizontalFlip")
            {
                VideoItem.ConvertArgv.Transform.Add(Transform.Kind.Flop);
                PlayerVideo.Player.HFlip = !PlayerVideo.Player.HFlip;
            }
            else if (type == "VerticalFlip")
            {
                VideoItem.ConvertArgv.Transform.Add(Transform.Kind.Flip);
                PlayerVideo.Player.VFlip = !PlayerVideo.Player.VFlip;
            }
            else if (type == "ClockwiseRotation")
            {
                VideoItem.ConvertArgv.Transform.Add(Transform.Kind.Rotate90);
                PlayerVideo.Player.RotateRight();
            }

            PlayerVideo.Update();
        }

        [RelayCommand]
        void Export()
        {
            Dispatch.Async(async () =>
            {
                if (VideoItem?.ConvertArgv.Family is null)
                    return;

                if (AudioItem?.ConvertArgv.Family is null)
                    return;

                if (Status.IsBusy)
                {
                    VideoItem.Cancel();
                    AudioItem.Cancel();
                    return;
                }

                Status.SetAndNotify(S.Processing);

                try
                {
                    var currentStep = 0;
                    var progressSteps = 3;

                    IProgress<double> progress = new Progress<double>(i =>
                    {
                        int currentProgress = (int)i;
                        Percent = (currentStep * 100 + currentProgress) / progressSteps;
                    });

                    // Video Setting

                    VideoItem.ConvertArgv.AudioFilterArgument = string.Empty;

                    if (PlayerVideo.Player.Audio.Volume != 1 || PlayerVideo.Player.Audio.Mute)
                    {
                        if (PlayerVideo.Player.Audio.Mute)
                            VideoItem.ConvertArgv.AudioFilterArgument += "volume=0";
                        else
                        {
                            float volume = PlayerVideo.Player.Audio.Volume / 100f;
                            VideoItem.ConvertArgv.AudioFilterArgument += $"volume={volume}";
                        }
                    }

                    if (VideoSpeedValue != 1)
                    {
                        VideoItem.ConvertArgv.VideoFilterArgument = $"setpts=PTS/{VideoSpeedValue.ToString(CultureInfo.InvariantCulture)}";

                        if (VideoItem.ConvertArgv.AudioFilterArgument.IsNotNullAndNotEmpty())
                            VideoItem.ConvertArgv.AudioFilterArgument += ",";

                        var remaining = VideoSpeedValue;

                        if (VideoSpeedValue < 0.5f)
                            while (remaining < 0.5f)
                            {
                                VideoItem.ConvertArgv.AudioFilterArgument += "atempo=0.5,";
                                remaining /= 0.5f;
                            }
                        else if (VideoSpeedValue > 2.0f)
                            while (remaining > 2.0f)
                            {
                                VideoItem.ConvertArgv.AudioFilterArgument += "atempo=2.0,";
                                remaining /= 2.0f;
                            }

                        VideoItem.ConvertArgv.AudioFilterArgument += $"atempo={remaining.ToString(CultureInfo.InvariantCulture)}";
                    }

                    var extensionVideo = MEDIA_FAMILIES[VideoItem.ConvertArgv.Family.Value].Extension;
                    string tempInputVideoFilePath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, "temp_output_video");
                    tempInputVideoFilePath = Path.ChangeExtension(tempInputVideoFilePath, extensionVideo);

                    VideoItem.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == "Percent")
                            progress.Report(VideoItem.Percent);
                    };

                    await VideoItem.ConvertMedia(tempInputVideoFilePath, true, true);
                    currentStep++;

                    // Audio Setting

                    AudioItem.ConvertArgv.AudioFilterArgument = string.Empty;

                    if (PlayerAudio.Player.Audio.Volume != 1 || PlayerAudio.Player.Audio.Mute)
                    {
                        if (PlayerAudio.Player.Audio.Mute)
                            AudioItem.ConvertArgv.AudioFilterArgument += "volume=0";
                        else
                        {
                            float volume = PlayerAudio.Player.Audio.Volume / 100f;
                            AudioItem.ConvertArgv.AudioFilterArgument += $"volume={volume}";
                        }
                    }

                    if (AudioSpeedValue != 1)
                    {
                        if (AudioItem.ConvertArgv.AudioFilterArgument.IsNotNullAndNotEmpty())
                            AudioItem.ConvertArgv.AudioFilterArgument += ",";

                        var remaining = AudioSpeedValue;

                        if (AudioSpeedValue < 0.5f)
                            while (remaining < 0.5f)
                            {
                                AudioItem.ConvertArgv.AudioFilterArgument += "atempo=0.5,";
                                remaining /= 0.5f;
                            }
                        else if (AudioSpeedValue > 2.0f)
                            while (remaining > 2.0f)
                            {
                                AudioItem.ConvertArgv.AudioFilterArgument += "atempo=2.0,";
                                remaining /= 2.0f;
                            }

                        AudioItem.ConvertArgv.AudioFilterArgument += $"atempo={remaining.ToString(CultureInfo.InvariantCulture)}";
                    }

                    var extensionAudio = MEDIA_FAMILIES[AudioItem.ConvertArgv.Family.Value].Extension;
                    string tempInputAudioFilePath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, "temp_output_audio");
                    tempInputAudioFilePath = Path.ChangeExtension(tempInputAudioFilePath, extensionAudio);

                    //await FFMpegArguments.FromFileInput(AudioItem.InputInfo.FullName).OutputToFile(tempInputAudioFilePath).ProcessAsynchronously();

                    AudioItem.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == "Percent")
                            progress.Report(AudioItem.Percent);
                    };

                    await AudioItem.ConvertMedia(tempInputAudioFilePath, true, true);
                    currentStep++;

                    //

                    var extensionOutput = MEDIA_FAMILIES[VideoItem.ConvertArgv.Family.Value].Extension;
                    string outputPath = Path.Combine(ConverterConfig.DefaultOutputPath, "output");
                    outputPath = Path.ChangeExtension(outputPath, extensionOutput);

                    // Replace Audio
                    //var arguments = $"-i \"{inputVideo}\" -i \"{inputMusic}\" -c:v copy -map 0:v:0 -map 1:a:0 -shortest \"{outputVideo}\"";

                    // Mix Audio
                    //var arguments = $"-i \"{tempInputVideoFilePath}\" -i \"{tempInputAudioFilePath}\" -filter_complex \"[0:a][1:a]amix=inputs=2:duration=shortest[aout]\" -map 0:v -map \"[aout]\" -c:v copy -shortest \"{outputPath}\"";

                    TimeSpan temp = new();

                    await FFMpegArguments
                    .FromFileInput(tempInputVideoFilePath)
                    .AddFileInput(tempInputAudioFilePath)
                    .OutputToFile(outputPath, true, options => options.WithCustomArgument(@"-filter_complex ""[0:a][1:a]amix=inputs=2:duration=shortest[aout]"" -map 0:v -map ""[aout]"" -c:v copy -shortest"))
                    .NotifyOnProgress(progress.Report, temp)
                    .ProcessAsynchronously();

                    OutputPath = outputPath;
                    Notify(nameof(OutputPath));

                    SystemUtils.RevealInFileExplorer(OutputPath);

                    _ = Ask.Inst.ToRate(Window, "ConvertingSucceeded", 1, true, TimeSpan.FromDays(2), true, 5);

                    Status.SetAndNotify(S.Processed);
                }
                catch (Exception ex)
                {
                    if (ex is MissingMediaStreamException)
                    {
                        Window.Tip.Message(null, null, ex.Message);
                        Status.SetAndNotify(S.ProcessFailed);
                    }
                    else if (ex is OperationCanceledException)
                        Status.SetAndNotify(S.ProcessStopped);
                    else
                    {
                        Window.Tip.Message(null, null, ex.Message);
                        Status.SetAndNotify(S.ProcessFailed);
                    }
                }
            });
        }
    }

    internal class HomeProxy : BindingProxy<Home> { }
}
