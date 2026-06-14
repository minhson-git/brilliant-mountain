using CommunityToolkit.Mvvm.Input;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Tube;
using IOMedia.Tube.TubeBase;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;
using static IOMedia.Tube.Profile;

namespace IOApp.Dialogs
{
    internal sealed partial class DownloaderDialog : DialogEx
    {
        public TubeLocalStorage TubeLocalStorage { get; } = EncapsulatedSingleton<TubeLocalStorage>.ExposeInstance();

        public IOStatus<DownloaderDialog, S> Status { get; }

        string? _urlText;
        public string? UrlText { get => _urlText; set => SetAndNotify(ref _urlText, value); }

        public UnifiedMediaItem? PreviewedMediaItem { get; private set; }

        public ObservableCollectionEx<OptionItem<UnifiedStreamInfo>> PreviewedStreamItems { get; } = [];
        int _previewedStreamIndex = -1;
        public int PreviewedStreamIndex
        {
            get => _previewedStreamIndex;
            set
            {
                if (_previewedStreamIndex != value)
                {
                    SetAndNotify(ref _previewedStreamIndex, value);

                    if (PreviewedMediaItem != null && PreviewedStreamItems.ElementAtOrDefault(_previewedStreamIndex)?.Key is UnifiedStreamInfo usi)
                    {
                        var containerName = usi.Container.ToUpperInvariant();

                        if (usi.IsVideo)
                        {
                            var qualityMapName = usi.VideoQuality > 0 ? V_QUALITY_MAP[(VQuality)MathUtils.FindNearest(V_QUALITY_MAP.Select(i => (int)i.Key), usi.VideoQuality)].Name : string.Empty;
                            var resolutionText = usi.VideoResolution.Width > 0 && usi.VideoResolution.Height > 0 ? $"{usi.VideoResolution.Width}x{usi.VideoResolution.Height}" : string.Empty;

                            var languageGroup = PreviewedMediaItem.AudioStreamGroups.ElementAtOrDefault(_audioLanguageIndex);

                            PreviewedMediaItem.Size = usi.Size.Bytes + (languageGroup?.AudioStreamInfos.FirstOrDefault()?.Size.Bytes ?? 0);
                            PreviewedMediaItem.Info = string.Join(' ', new[] { containerName, qualityMapName, resolutionText }.Where(s => !string.IsNullOrWhiteSpace(s)));

                            PreviewedMediaItem.Quality = usi.VideoQuality;
                        }
                        else
                        {
                            var bitrateText = usi.AudioBitrate.BitsPerSecond > 0 ? usi.AudioBitrate.ToString() : string.Empty;
                            var codecName = !string.IsNullOrWhiteSpace(usi.AudioCodec) ? usi.AudioCodec : string.Empty;

                            PreviewedMediaItem.Size = usi.Size.Bytes;
                            PreviewedMediaItem.Info = string.Join(' ', new[] { containerName, bitrateText, codecName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                            PreviewedMediaItem.AudioBitrate = usi.AudioBitrate.BitsPerSecond;
                            PreviewedMediaItem.AudioCodec = usi.AudioCodec;
                        }

                        PreviewedMediaItem.Container = usi.Container;
                    }
                }
            }
        }

        //

        public ObservableCollectionEx<OptionItem<int>> QualityItems { get; } = [];
        int _qualityIndex = 0;
        public int QualityIndex
        {
            get => _qualityIndex;
            set
            {
                if (SetAndNotify(ref _qualityIndex, value))
                {
                    if (PreviewedMediaItem != null)
                        PreviewedMediaItem.Quality = Quality;

                    Notify(nameof(Quality));
                }
            }
        }
        public int Quality => QualityItems.GetAtOrDefault(_qualityIndex, (int)VQualityRecord.PremiumQuality);

        //

        public ObservableCollectionEx<OptionItem<string>> AudioLanguageItems { get; } = [];
        int _audioLanguageIndex = -1;
        public int AudioLanguageIndex
        {
            get => _audioLanguageIndex;
            set
            {
                SetAndNotify(ref _audioLanguageIndex, value);

                if (PreviewedMediaItem != null)
                {
                    if (MediaType == MediaType.Video)
                        return;

                    UnifiedAudioStreamInfoGroup? audioStreamGroups;

                    if (AudioLanguageItems.ElementAtOrDefault(_audioLanguageIndex)?.Key is string language)
                        audioStreamGroups = PreviewedMediaItem.AudioStreamGroups.FirstOrDefault(i => i.LanguageCode?.Equals(language, StringComparison.InvariantCultureIgnoreCase) ?? false);
                    else
                        audioStreamGroups = PreviewedMediaItem.AudioStreamGroups.FirstOrDefault();

                    if (audioStreamGroups != null)
                    {
                        PreviewedStreamItems.Replace(audioStreamGroups.AudioStreamInfos.Select(i =>
                        {
                            var streamText = $"{i.AudioCodec} • {i.Container.ToUpperInvariant()}";

                            if (i.AudioBitrate.BitsPerSecond > 0)
                                streamText = $"{i.AudioBitrate} • " + streamText;

                            var optionItem = new OptionItem<UnifiedStreamInfo>(i, streamText);

                            if (Window.LicenseStatus.IsTrial && i.AudioBitrate.KiloBitsPerSecond > (double)AQualityRecord.PremiumQuality)
                            {
                                optionItem.Value = $"{PremiumCore.ConceptText} • {optionItem.OriginalValue}";
                                optionItem.IsEnabled = false;
                            }

                            return optionItem;
                        }));

                        PreviewedStreamIndex = PreviewedStreamItems.IndexOf(i => MathUtils.IsSameOrLessThan(i.Key.AudioBitrate.KiloBitsPerSecond, (double)AQualityRecord.PremiumQuality));
                    }

                    Notify(nameof(PreviewedStreamItems), nameof(PreviewedStreamIndex));
                }
            }
        }

        public MediaType MediaType { get; set; }

        public Action<string>? OpenDownloadedFile;

        public DownloaderDialog(WindowEx window, MediaType type) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.Cover.ShowLoading(Status.IsLoading, Cover.CanvasType.None);
                Window.SetBusy(Status.IsBusy);
            };

            MediaType = type;

            TubeLocalStorage.ConvertToMp3 = true;
            TubeLocalStorage.ConvertToMp4 = true;
        }

        protected override void OnLicenseStatusChanged()
        {
            if (Window.LicenseStatus.IsPremium)
            {
                PreviewedStreamItems.ForEach(i =>
                {
                    i.Value = i.OriginalValue;
                    i.IsEnabled = true;
                });
            }
        }

        void BuildVideoStreams()
        {
            if (PreviewedMediaItem is null)
                return;

            var useSubstituteQualities = PreviewedMediaItem.VideoStreams.Count == 0 || PreviewedMediaItem.VideoStreams.All(i => i.VideoQuality == 0);
            if (useSubstituteQualities)
            {
                QualityItems.Replace(SUPPORT_VIDEO_QUALITIES.Where(i => i.Quality.Anys(VQuality._720, VQuality._1080)).Select(i =>
                {
                    var optionItem = new OptionItem<int>((int)i.Quality, $"{i.Name} • {i.CodeName}");

                    if (Window.LicenseStatus.IsTrial && i.Quality > VQualityRecord.PremiumQuality)
                    {
                        optionItem.Value = $"{PremiumCore.ConceptText} • {optionItem.OriginalValue}";
                        optionItem.IsEnabled = false;
                    }

                    return optionItem;
                }));

                QualityIndex = QualityItems.IndexOf(i => i.Key <= (int)VQualityRecord.PremiumQuality);
            }
            else
            {
                PreviewedStreamItems.Replace(PreviewedMediaItem.VideoStreams.Select(i =>
                {
                    var streamText = i.Container.ToUpperInvariant();
                    if (i.VideoQuality > 0)
                        streamText = $"{i.VideoQuality}p • " + streamText;

                    var optionItem = new OptionItem<UnifiedStreamInfo>(i, streamText);

                    if (Window.LicenseStatus.IsTrial && i.VideoQuality > (int)VQualityRecord.PremiumQuality)
                    {
                        optionItem.Value = $"{PremiumCore.ConceptText} • {optionItem.OriginalValue}";
                        optionItem.IsEnabled = false;
                    }

                    return optionItem;
                }));


                PreviewedStreamIndex = PreviewedStreamItems.IndexOf(i => i.Key.VideoQuality <= (int)VQualityRecord.PremiumQuality);
            }

            Notify(nameof(PreviewedStreamItems), nameof(QualityItems));
        }

        void BuildAudioStreams()
        {
            if (PreviewedMediaItem is null)
                return;

            int index = -1;
            ListEx<OptionItem<string>> audioLanguages = [];

            PreviewedMediaItem.AudioStreamGroups.ForEach(i =>
            {
                if (i.LanguageCode is null)
                    return;

                if (i.IsDefault)
                {
                    PreviewedMediaItem.AudioLanguageCode = i.LanguageCode;
                    index = PreviewedMediaItem.AudioStreamGroups.IndexOf(i);
                }

                audioLanguages.Add(new OptionItem<string>(i.LanguageCode, CultureHelper.GetRegionName(i.LanguageCode)));
            });

            AudioLanguageItems.Replace(audioLanguages);
            AudioLanguageIndex = index;

            Notify(nameof(AudioLanguageItems), nameof(AudioLanguageIndex));
        }

        async void Scan(string? url)
        {
            if (Status.IsLoading)
                return;

            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                Status.SetAndNotify(S.Loading);

                PreviewedMediaItem = await TubeManager.Inst.GetMediaItem(url, ex => Window.Tip.Close());
                Notify(nameof(PreviewedMediaItem));

                if (PreviewedMediaItem != null)
                {
                    if (MediaType == MediaType.Video)
                    {
                        BuildVideoStreams();
                        BuildAudioStreams();
                    }
                    else if (MediaType == MediaType.Media)
                    {
                        BuildAudioStreams();
                    }

                    Status.SetAndNotify(S.Loaded);
                }

                if (FindSite(url).Source == Site.Unknown)
                    throw new NotSupportedException("The provided URL is not supported by the downloader.");
                else
                    throw new Exception();
            }
            catch (Exception ex)
            {
                if (ex is NotSupportedException)
                    Window.Tip.Message(null, "Download Not Support Site Message");

                Status.SetAndNotify(S.LoadFailed);
            }
        }

        void UrlTextBoxComp_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                Scan(_urlText);
        }

        [RelayCommand]
        void Scan() => Scan(_urlText);

        [RelayCommand]
        void Paste() => Dispatch.Async(async () => UrlText = await SystemUtils.GetTextFromClipboard(true));

        [RelayCommand]
        void Download()
        {
            if (PreviewedMediaItem is null)
                return;

            if (PreviewedMediaItem.Status.IsProcessing)
                PreviewedMediaItem.Cancel();
            else
            {
                PreviewedMediaItem.CancellationTokenSource = new();

                PreviewedMediaItem.DoConvertToMpX = TubeLocalStorage.ConvertToMp4 || TubeLocalStorage.ConvertToMp3;
                PreviewedMediaItem.AudioLanguageCode = AudioLanguageItems.GetAtOrDefault(AudioLanguageIndex, "auto");
                PreviewedMediaItem.DoRemoveAudioFromVideo = TubeLocalStorage.RemoveAudioFromVideo;

                if (PreviewedMediaItem.Quality == 0 && PreviewedMediaItem.AudioBitrate == 0)
                {
                    PreviewedMediaItem.Quality = 1;
                    PreviewedMediaItem.AudioBitrate = 1;
                }

                DownloadItem(PreviewedMediaItem, new(TubeLocalStorage.OutputPath, MathUtils.InterpolateValue(TubeLocalStorage.DownloadSpeed, 1, 10, -2, 3), true));
            }
        }

        public Task DownloadItem(UnifiedMediaItem item, DownloadArgv argv, Action? startAction = null, Action? endAction = null)
        {
            if (item.CancellationTokenSource!.IsCancellationRequested)
                throw new OperationCanceledException();

            Status.SetAndNotify(S.Processing);

            startAction?.Invoke();

            return TubeManager.Inst.Download(item, argv,
                (status, ex) =>
                {
                    item.Status.SetAndNotify(status);

                    if (status == Item.S.Processed)
                    {
                        _ = Ask.Inst.ToRate(Window, null, 0, true, TimeSpan.FromHours(1));

                        item.OutputInfo.Notify();
                        _lzQueue.Enqueue(item.Thumbnail.GetThumbnailLzTaskLoader());
                    }

                    if (status == Item.S.Processed)
                        Status.SetAndNotify(S.Processed);
                    else
                        Status.SetAndNotify(S.ProcessFailed);

                    endAction?.Invoke();
                }
            );
        }

        [RelayCommand]
        void Open()
        {
            if (PreviewedMediaItem is null)
                return;

            OpenDownloadedFile?.Invoke(PreviewedMediaItem.OutputPath);
            Window.Dialog.Close();
        }
    }
}