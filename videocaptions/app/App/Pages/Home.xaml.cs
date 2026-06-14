using CommunityToolkit.Mvvm.Input;
using IOCore.Base;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Utils;
using IOMedia.Tube;
using IOMedia.Tube.TubeBase;
using System;
using System.IO;
using System.Linq;
using ZAnnotation.IOCore;

namespace IOApp.Pages
{
    [BindingProxy]
    internal partial class Home : DownloaderPage
    {
        public override string UrlText { get; set => SetAndNotify(ref field, value); } = string.Empty;
        public FileInfoBase OutputInfo { get; set => SetAndNotify(ref field, value); }

        public ObservableCollectionEx<OptionItem<string>> CaptionLanguageItems { get; } = [];
        public int CaptionLanguageIndex
        {
            get;
            set
            {
                SetAndNotify(ref field, value);

                if (PreviewedMediaItem is not null)
                {
                    if (CaptionLanguageIndex < 0 || CaptionLanguageIndex >= CaptionLanguageItems.Count)
                    {
                        CaptionYoutubeTextBox.Text = R.T(L.NoCaption);
                        return;
                    }

                    var languageCode = CaptionLanguageItems[CaptionLanguageIndex].Key;

                    var track = PreviewedMediaItem.CaptionTracks.FirstOrDefault(t => t.LanguageCode == languageCode);

                    CaptionYoutubeTextBox.Text = track?.Caption ?? R.T(L.NoCaption);
                }
            }
        } = -1;

        public Home()
        {
            InitializeComponent();
            DataContext = this;

            TubeManager.Inst.Switched += (_, e) =>
            {
                if (e.Downloader is YoutubeDLDownloader && e.ActionType is DownloaderActionType.GetMediaItem)
                    Window.Tip.Message(ScanIconButton, R.T(L.BePatient));
            };

            OutputInfo = new();
        }

        public void BuildCaptions()
        {
            if (PreviewedMediaItem is null)
                return;

            CaptionLanguageItems.Replace(PreviewedMediaItem.CaptionTracks
                .Select(i => i.LanguageCode)
                .OfType<string>()
                .Select(code => new OptionItem<string>(code, CultureHelper.GetRegionName(code))));

            CaptionLanguageIndex = PreviewedMediaItem.CaptionTracks.FindIndex(i => i.LanguageCode == "en");

            if (CaptionLanguageIndex == -1 && PreviewedMediaItem.CaptionTracks.Count > 0)
                CaptionLanguageIndex = 0;
        }

        public override async void Scan(string? url)
        {
            if (Status.IsLoading)
                return;

            if (string.IsNullOrWhiteSpace(url))
            {
                Window.Tip.Message(null, string.Empty, R.T(L.EmptyLink));
                return;
            }

            try
            {
                Status.SetAndNotify(S.Loading);

                PreviewedMediaItem = await TubeManager.Inst.GetMediaItemWithCaptionsOnly(url, ex => Window.Tip.Close());

                BuildCaptions();

                Status.SetAndNotify(S.Loaded);
            }
            catch (Exception)
            {
                Status.SetAndNotify(S.LoadFailed);
            }
        }

        [RelayCommand]
        void Save()
        {
            if (PreviewedMediaItem is null)
                return;

            try
            {
                Status.SetAndNotify(S.Loading);

                var outputPath = Path.Combine(TubeData.OutputPath, $"{PathUtils.GetValidFileName(PreviewedMediaItem.Title)}.txt");
                File.WriteAllText(outputPath, CaptionYoutubeTextBox.Text);

                OutputInfo.Refresh(outputPath);
                OutputInfo.Notify(null);

                Status.SetAndNotify(S.Loaded);
            }
            catch (Exception)
            {
                Status.SetAndNotify(S.LoadFailed);
            }
        }
    }
}
