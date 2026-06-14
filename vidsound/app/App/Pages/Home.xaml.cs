using CommunityToolkit.Mvvm.Input;
using FFMpegCore;
using IOApp.Features;
using IOCore;
using IOCore.Annotation;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.License;
using IOCore.Premium;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using XMedia;
using XMedia.Media;
using XMedia.Media.PlayerBase;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;
using static IOCore.Files.MediaTypes;

namespace IOApp.Pages;

[BindingProxy]
internal partial class Home : MainWindowPage, IDisposable
{
    public PlayerEx PlayerVideo { get; }
    public PlayerEx PlayerAudio { get; }

    public IOStatus<Home, S> Status { get; }

    public string InputVideoTypes => FormatUtils.LayoutGridString(PlayerConfig.I.InputVideoExtensions);
    public string InputAudioTypes => FormatUtils.LayoutGridString(PlayerConfig.I.InputAudioExtensions);


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

    bool _seekWhilePlaying;

    public Home()
    {
        InitializeComponent();
    }

    protected void PresenterVideo_Loaded(object sender, RoutedEventArgs e) => PlayerVideo.PresentAt(sender as ContentPresenter);
    protected void PresenterAudio_Loaded(object sender, RoutedEventArgs e) => PlayerAudio.PresentAt(sender as ContentPresenter);

    protected void DragDropVideoGrid_Loaded(object sender, RoutedEventArgs e) =>
        sender.Var<Panel>(panel =>
        {
            //panel.Loaded -= DragDropVideoGrid_Loaded;
            //DragDrop.Register(panel, storageItems =>
            //    _ = PlayerContext.AddVideoFile(Window, storageItems.Where(i => FileUtils.IsFile(i.Path)).Select(i => i.Path).FirstOrDefault()));
        });

    protected void DragDropAudioGrid_Loaded(object sender, RoutedEventArgs e) =>
        sender.Var<Panel>(panel =>
        {
            //panel.Loaded -= DragDropAudioGrid_Loaded;
            //DragDrop.Register(panel, storageItems =>
            //    _ = PlayerContext.AddAudioFile(Window, storageItems.Where(i => FileUtils.IsFile(i.Path)).Select(i => i.Path).FirstOrDefault()));
        });

    protected void TimeSlider_Loaded(object sender, RoutedEventArgs e) =>
    sender.Var<Slider>(slider =>
    {
        //slider.Loaded -= TimeSlider_Loaded;

        slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
        {
            if (PlayerVideo.Player.IsPlaying)
            {
                _seekWhilePlaying = true;
                PlayerVideo.Player.Pause();
                PlayerAudio.Player.Pause();
            }
            else
                _seekWhilePlaying = false;

            PlayerVideo.Player.Config.Player.SeekAccurate = true;

            PlayerAudio.Player.Config.Player.SeekAccurate = true;
            PlayerAudio.Player.CurTime = PlayerVideo.Player.CurTime;
        }), true);

        slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
        {
            if (_seekWhilePlaying)
            {
                _seekWhilePlaying = false;

                PlayerVideo.Player.Config.Player.SeekAccurate = false;
                PlayerVideo.Resume();

                PlayerAudio.Player.Config.Player.SeekAccurate = false;
                PlayerAudio.Resume();

                PlayerAudio.Player.CurTime = PlayerVideo.Player.CurTime;
            }
        }), true);

        slider.ValueChanged += (_, _) =>
        {
            if (PlayerVideo.Player.CurTime >= PlayerVideo.Player.Duration)
            {
                PlayerAudio.Player.Pause();
                PlayerAudio.Player.Config.Player.SeekAccurate = true;
                PlayerAudio.Player.CurTime = 0;
            }
        };
    });
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
                if (!PlayerConfig.I.IsInputAccepted(path, mediaType))
                    throw new UnacceptedInputException();

                var item = new PlayerItem(path);
                item.InputInfo.Analyze();

                player.Playlist.ReplaceRange([item]);
                //AddItem(item, mediaType, true);
            }

            if (!PlayerConfig.I.IsInputAccepted(path, mediaType))
                throw new UnacceptedInputException();

            var info = MediaInfoBase.Create(path);
            info.Analyze();

            if (info.IsCorrupted)
                throw new Exception();

            var mediaItem = new MediaItem(info);
            setItem(mediaItem);

            var outputFamilies = mediaType == MediaType.Video
                ? ConverterConfig.I.OutputVideoFamilies.Select(i => i.Family)
                : ConverterConfig.I.OutputAudioFamilies.Select(i => i.Family);

            mediaItem.ConvertArgs.Reset(outputFamilies);

            setSpeedValue();

            LimitIfTrial(mediaItem);

            mediaItem.Thumbnail.MediaInfoBase = mediaItem.InputInfo;
            mediaItem.Thumbnail.NeedBitmap = true;

            if (IOLicense.I.Status.IsTrial)
            {
                var slug = ConverterConfig.I.GetExtra(path, true);
                _ = Promotion.I.Offer(items =>
                {
                    Menu.PromotionAppItems.ReplaceRange(items);
                    Notify(nameof(Menu.PromotionAppItems));
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
        if (IOLicense.I.Status.IsTrial)
        {
            foreach (var outputItem in item.ConvertArgs.FamilySelection.Items)
            {
                if (outputItem.Key != FamilyType.V_Mp4)
                {
                    outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                    outputItem.IsEnabled = false;
                }
            }

            foreach (var outputItem in item.ConvertArgs.ResolutionSelection.Items)
            {
                if (outputItem.Key != Resolution.Auto)
                {
                    outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                    outputItem.IsEnabled = false;
                }
            }

            foreach (var outputItem in item.ConvertArgs.VideoBitrateSelection.Items)
            {
                if (outputItem.Key != VideoBitrate.Auto)
                {
                    outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                    outputItem.IsEnabled = false;
                }
            }

            foreach (var outputItem in item.ConvertArgs.AudioBitrateSelection.Items)
            {
                if (outputItem.Key != AudioBitrate.Auto)
                {
                    outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                    outputItem.IsEnabled = false;
                }
            }
        }
    }

    [RelayCommand]
    void OpenDownloadDialog(string type)
    {
        //if (type == "Video")
        //{
        //    var dialog = new DownloaderDialog(Window, MediaType.Video)
        //    {
        //        OpenDownloadedFile = LoadVideo
        //    };
        //    Window.Dialog.Open(dialog);
        //}    
        //else if (type == "Audio")
        //{
        //    var dialog = new DownloaderDialog(Window, MediaType.Media)
        //    {
        //        OpenDownloadedFile = LoadAudio
        //    };
        //    Window.Dialog.Open(dialog);
        //}    
    }

    [RelayCommand]
    void ApplyTransform(string type)
    {
        //if (VideoItem == null)
        //    return;

        //if (type == "HorizontalFlip")
        //{
        //    VideoItem.ConvertArgs.Transform.Add(Transform.Kind.Flop);
        //    PlayerVideo.Player.HFlip = !PlayerVideo.Player.HFlip;
        //}
        //else if (type == "VerticalFlip")
        //{
        //    VideoItem.ConvertArgs.Transform.Add(Transform.Kind.Flip);
        //    PlayerVideo.Player.VFlip = !PlayerVideo.Player.VFlip;
        //}
        //else if (type == "ClockwiseRotation")
        //{
        //    VideoItem.ConvertArgs.Transform.Add(Transform.Kind.Rotate90);
        //    PlayerVideo.Player.RotateRight();
        //}

        //PlayerVideo.Update();
    }

    [RelayCommand]
    void Export()
    {
        Dispatch.Async(async () =>
        {
            if (VideoItem?.ConvertArgs.FamilySelection is null)
                return;

            if (AudioItem?.ConvertArgs.FamilySelection is null)
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

                VideoItem.ConvertArgs.AudioFilterArgument = string.Empty;

                if (PlayerVideo.Player.Audio.Volume != 1 || PlayerVideo.Player.Audio.Mute)
                {
                    if (PlayerVideo.Player.Audio.Mute)
                        VideoItem.ConvertArgs.AudioFilterArgument += "volume=0";
                    else
                    {
                        float volume = PlayerVideo.Player.Audio.Volume / 100f;
                        VideoItem.ConvertArgs.AudioFilterArgument += $"volume={volume}";
                    }
                }

                if (VideoSpeedValue != 1)
                {
                    VideoItem.ConvertArgs.VideoFilterArgument = $"setpts=PTS/{VideoSpeedValue.ToString(CultureInfo.InvariantCulture)}";

                    if (VideoItem.ConvertArgs.AudioFilterArgument is not null || VideoItem.ConvertArgs.AudioFilterArgument != string.Empty)
                        VideoItem.ConvertArgs.AudioFilterArgument += ",";

                    var remaining = VideoSpeedValue;

                    if (VideoSpeedValue < 0.5f)
                        while (remaining < 0.5f)
                        {
                            VideoItem.ConvertArgs.AudioFilterArgument += "atempo=0.5,";
                            remaining /= 0.5f;
                        }
                    else if (VideoSpeedValue > 2.0f)
                        while (remaining > 2.0f)
                        {
                            VideoItem.ConvertArgs.AudioFilterArgument += "atempo=2.0,";
                            remaining /= 2.0f;
                        }

                    VideoItem.ConvertArgs.AudioFilterArgument += $"atempo={remaining.ToString(CultureInfo.InvariantCulture)}";
                }

                var extensionVideo = MEDIA_FAMILIES[VideoItem.ConvertArgs.FamilySelection.SelectedKeyOrDefault].Extension;
                string tempInputVideoFilePath = AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, "temp_output_video");
                tempInputVideoFilePath = Path.ChangeExtension(tempInputVideoFilePath, extensionVideo);

                VideoItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "Percent")
                        progress.Report(VideoItem.Percent);
                };

                await VideoItem.ConvertMedia(tempInputVideoFilePath, true, true);
                currentStep++;

                // Audio Setting

                AudioItem.ConvertArgs.AudioFilterArgument = string.Empty;

                if (PlayerAudio.Player.Audio.Volume != 1 || PlayerAudio.Player.Audio.Mute)
                {
                    if (PlayerAudio.Player.Audio.Mute)
                        AudioItem.ConvertArgs.AudioFilterArgument += "volume=0";
                    else
                    {
                        float volume = PlayerAudio.Player.Audio.Volume / 100f;
                        AudioItem.ConvertArgs.AudioFilterArgument += $"volume={volume}";
                    }
                }

                if (AudioSpeedValue != 1)
                {
                    if (VideoItem.ConvertArgs.AudioFilterArgument is not null || VideoItem.ConvertArgs.AudioFilterArgument != string.Empty)
                        AudioItem.ConvertArgs.AudioFilterArgument += ",";

                    var remaining = AudioSpeedValue;

                    if (AudioSpeedValue < 0.5f)
                        while (remaining < 0.5f)
                        {
                            AudioItem.ConvertArgs.AudioFilterArgument += "atempo=0.5,";
                            remaining /= 0.5f;
                        }
                    else if (AudioSpeedValue > 2.0f)
                        while (remaining > 2.0f)
                        {
                            AudioItem.ConvertArgs.AudioFilterArgument += "atempo=2.0,";
                            remaining /= 2.0f;
                        }

                    AudioItem.ConvertArgs.AudioFilterArgument += $"atempo={remaining.ToString(CultureInfo.InvariantCulture)}";
                }

                var extensionAudio = MEDIA_FAMILIES[AudioItem.ConvertArgs.FamilySelection.SelectedKeyOrDefault].Extension;
                string tempInputAudioFilePath = AppDir.LGetFilePath(AppDir.Type.TemporaryFolder, "temp_output_audio");
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

                var extensionOutput = MEDIA_FAMILIES[VideoItem.ConvertArgs.FamilySelection.SelectedKeyOrDefault].Extension;
                string outputPath = Path.Combine(ConverterConfig.I.DefaultOutputPath, "output");
                outputPath = Path.ChangeExtension(outputPath, extensionOutput);

                //Replace Audio
                //var arguments = $"-i \"{inputVideo}\" -i \"{inputMusic}\" -c:v copy -map 0:v:0 -map 1:a:0 -shortest \"{outputVideo}\"";

                //Mix Audio
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

                _ = AskSaver.I.ToRate(Window, "ConvertingSucceeded", 1, true, TimeSpan.FromDays(2), true, 5);

                Status.SetAndNotify(S.Processed);
            }
            catch (Exception ex)
            {
                if (ex is MissingMediaStreamException)
                {
                    Tip.Message(Window, null, ex.Message);
                    Status.SetAndNotify(S.ProcessFailed);
                }
                else if (ex is OperationCanceledException)
                    Status.SetAndNotify(S.ProcessStopped);
                else
                {
                    Tip.Message(Window, null, ex.Message);
                    Status.SetAndNotify(S.ProcessFailed);
                }
            }
        });
    }

    #region Commands

    [RelayCommand]
    void AddFile(string type)
    {
        //if (type == "Video")
        //    _ = PlayerContext.AddVideoFile(Window, null);
        //else if (type == "Audio")
        //    _ = PlayerContext.AddAudioFile(Window, null);
    }

    [RelayCommand]
    void Mute(string type)
    {
        if (type is "Video")
            PlayerVideo.Player.Audio.Mute = !PlayerVideo.Player.Audio.Mute;
        else if (type is "Audio")
            PlayerAudio.Player.Audio.Mute = !PlayerAudio.Player.Audio.Mute;
    }

    [RelayCommand]
    void Play()
    {
        PlayerVideo.Player.TogglePlayPause();
        PlayerAudio.Player.TogglePlayPause();
    }

    #endregion

    public void AddItem(object? item, MediaType? type, bool scrollToView) =>
        item.Var<PlayerItem>(_ =>
        {
            if (type == null)
                return;

            var player = type == MediaType.Video ? PlayerVideo : PlayerAudio;

            player.Current = _;
            player.Current.ThumbnailEnqueued(_lzQueue);

            player.Play();
            player.Player.TogglePlayPause();

            Notify(nameof(PlayerVideo), nameof(PlayerAudio));

            //if (Window.License.Status.IsTrial)
            //{
            //    var slug = PlayerConfig.I.GetExtra(PlayerVideo.Current.InputInfo.Extension, PlayerVideo.Current.InputInfo.HasVideo);
            //    _ = Promotion.I.Offer(items =>
            //    {
            //        PromotionAppItems.ReplaceRange(items);
            //        Notify(nameof(PromotionAppItems));
            //    }, 1, null, slug == null ? null : [slug]);
            //}
        });

    public void Dispose()
    {
        PlayerVideo.Clear();
        PlayerAudio.Clear();
    }
}
