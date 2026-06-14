using CommunityToolkit.Mvvm.Input;
using IOApp.Gens;
using IOApp.Pages;
using IOApp.Windows;
using IOCore;
using IOCore.Annotation;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Collections;
using IOCore.Files;
using IOCore.License;
using IOCore.Premium;
using IOCore.Types;
using IOCore.Types.Items;
using IOCore.UI;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XMedia;
using XMedia.Media;
using XMedia.Media.ConverterBase;
using static IOCore.Files.MediaFamily;

namespace IOApp.Features;

[BindingProxy]
[NotifyPropertyChanged]
internal partial class MediaConverterControl : ContentPresenter
{
    public WindowLoader Window { get; }

    string? _path;

    public MediaItem Item { get; }

    ConvertArgs? _previousArgv;

    public SelectionSource<MediaType> ConvertTypeSelection { get; }

    public BitmapImage? Thumbnail
    {
        get
        {
            if (Item.Thumbnail.Bitmap is null)
                return null;

            using var skBitmap = SkiaUtils.ApplyTransform(Item.Thumbnail.Bitmap, Item.ConvertArgs.Transform);
            if (skBitmap is null)
                return null;

            return SkiaUtils.ToBitmapImage(skBitmap);
        }
    }

    public ObservableCollectionEx<IOAppItem> PromotionAppItems { get; } = [];

    public MediaConverterControl()
    {
        InitializeComponent();

        Window = new(this);

        Item = new(string.Empty);
        Item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MediaItem.Status))
                Window.Value?.SetBusy(Item.Status.IsBusy);
        };

        Item.ConvertArgs.PropertyChanged += ItemArgv_PropertyChanged;

        ConvertTypeSelection = new(MediaType.Audio);
        ConvertTypeSelection.Reset([new(MediaType.Video, T.Convert2MP4), new(MediaType.Audio, T.Convert2MP3)], MediaType.Audio);

        ConvertTypeSelection.SelectionChanged = _ =>
        {
            if (_previousArgv is null)
            {
                var videoConvertArgs = new ConvertArgs();
                videoConvertArgs.Reset(ConverterConfig.I.OutputVideoFamilies.Select(i => i.Family));
                var audioConvertArgs = new ConvertArgs();
                audioConvertArgs.Reset(ConverterConfig.I.OutputAudioFamilies.Select(i => i.Family));

                Item.ConvertArgs.Copy(_.SelectedKeyOrDefault is MediaType.Video ? videoConvertArgs : audioConvertArgs, true);
                _previousArgv = _.SelectedKeyOrDefault is MediaType.Video ? audioConvertArgs : videoConvertArgs;
            }
            else
            {
                var temp = new ConvertArgs();
                temp.Copy(_previousArgv, true);

                _previousArgv.Copy(Item.ConvertArgs, true);
                Item.ConvertArgs.Copy(temp, true);
            }

            LimitIfTrial(Item);
        };
    }

    public void SetPath(string path, MediaType convertType)
    {
        _path = path;
        ConvertTypeSelection.SelectByKey(convertType);
    }

    public void ReloadAction()
    {
        if (Item.Status.IsBusy)
            return;

        try
        {
            Item.Status.SetAndNotify(MediaItem.S.Loading);

            if (Item.InputInfo.FullName != _path)
            {
                if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path) || !ConverterConfig.I.IsInputAccepted(_path, MediaType.Media))
                    throw new UnacceptedInputException();

                Item.InputInfo.Refresh(_path);
                Item.InputInfo.Analyze();

                if (Item.InputInfo.IsCorrupted)
                    throw new UnacceptedInputException();

                Item.ConvertArgs.Reset(
                    (ConvertTypeSelection.SelectedKeyOrDefault is MediaType.Audio ? ConverterConfig.I.OutputAudioFamilies : ConverterConfig.I.OutputVideoFamilies)
                        .Select(i => i.Family));

                Item.Thumbnail.Clean();
                Item.Thumbnail.MediaInfoBase = Item.InputInfo;
                Item.Thumbnail.NeedBitmap = true;
                Item.Thumbnail.ThumbnailLzProcess?.Process();

                Notify(nameof(Thumbnail));
            }

            Item.Status.SetAndNotify(MediaItem.S.Loaded);

            LimitIfTrial(Item);

            if (IOLicense.I.Status.IsTrial)
            {
                var slug = ConverterConfig.I.GetExtra(_path, ConvertTypeSelection.SelectedKeyOrDefault is MediaType.Video);
                _ = AppSession.I.Offer(PromotionAppItems.ReplaceRange, 1, null, slug is null ? null : [slug]);
            }
        }
        catch
        {
            Item.Status.SetAndNotify(MediaItem.S.LoadFailed);
        }
    }

    public void LimitIfTrial(MediaItem item)
    {
        if (IOLicense.I.Status.IsTrial)
        {
            if (ConvertTypeSelection.SelectedKeyOrDefault is MediaType.Audio)
            {
                foreach (var outputItem in item.ConvertArgs.FamilySelection.Items)
                    if (outputItem.Key is not FamilyType.A_Mp3)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }

                foreach (var outputItem in item.ConvertArgs.AudioBitrateSelection.Items)
                    if (outputItem.Key is not MediaTypes.AudioBitrate.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
            }
            else
            {
                foreach (var outputItem in item.ConvertArgs.FamilySelection.Items)
                    if (outputItem.Key is not FamilyType.V_Mp4)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }

                foreach (var outputItem in item.ConvertArgs.ResolutionSelection.Items)
                    if (outputItem.Key is not MediaTypes.Resolution.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }

                foreach (var outputItem in item.ConvertArgs.VideoBitrateSelection.Items)
                    if (outputItem.Key is not MediaTypes.VideoBitrate.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }

                foreach (var outputItem in item.ConvertArgs.AudioBitrateSelection.Items)
                    if (outputItem.Key is not MediaTypes.AudioBitrate.Auto)
                    {
                        outputItem.Value = $"{PremiumCore.I.ConceptText} • {outputItem.OriginalValue}";
                        outputItem.IsEnabled = false;
                    }
            }
        }
    }

    void ItemArgv_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConvertArgs.Transform))
            Notify(nameof(Thumbnail));
    }

    [RelayCommand]
    void ApplyTransform(string type)
    {
        if (type is "HorizontalFlip")
            Item.ConvertArgs.Transform.Add(Transform.Kind.Flop);
        else if (type is "VerticalFlip")
            Item.ConvertArgs.Transform.Add(Transform.Kind.Flip);
        else if (type is "ClockwiseRotation")
        {
            Item.ConvertArgs.Transform.Add(Transform.Kind.Rotate90);
        }
    }

    [RelayCommand]
    async Task Convert()
    {
        if (Item?.ConvertArgs.FamilySelection.SelectedItem is null)
            return;

        if (Item.Status.IsProcessing)
        {
            Item.Cancel();
            return;
        }

        Item.Status.SetAndNotify(MediaItem.S.Processing);

        var pickFileResult = await Picker.SaveFile(Window.Value,
            picker =>
            {
                var family = MEDIA_FAMILIES[Item.ConvertArgs.FamilySelection.SelectedKeyOrDefault];
                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(PathUtils.NextAvailablePath(Item.InputInfo.FullName));
                picker.FileTypeChoices.Add(family.Name, [family.Extension]);
            });

        if (pickFileResult is null)
        {
            Item.Status.SetAndNotify(Item.Status.PrevS);
            return;
        }

        try
        {
            if (await Item.ConvertMedia(pickFileResult.Path, true, true))
            {
                SystemUtils.RevealInFileExplorer(pickFileResult.Path);
                await AskSaver.I.ToRate(Window.Value, null, 0, true, TimeSpan.FromDays(2));
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                Tip.Message(Window.Value, null, ex.Message);
        }
    }

    [RelayCommand]
    void Play()
    {
        if (Item.InputInfo.FullName is null)
            return;

        AppEx.LoadWindow<MainWindow>(window =>
        {
            window.Activate();
            MediaPlayer.Open<MediaPlayer>(window, Item.InputInfo.FullName, path => new PlayerItem(path));
        });
    }
}