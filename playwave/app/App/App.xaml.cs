using IOApp.Configs;
using IOApp.Gens;
using IOApp.Pages;
using IOCore;
using IOCore.Files;
using IOCore.License;
using IOCore.Premium;
using IOCore.Utils;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using XMedia;
using XMedia.Grabber;
using Windows.Foundation;
using IOApp.Windows;
using IOCore.Welcome;
using IOCore.Base;

#if DEBUG
using IOCore.DataUtils;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;
using XMedia.Media.PlayerBase;
using Windows.ApplicationModel;
#endif

namespace IOApp;

public partial class App : AppEx
{
    [STAThread]
    static int Main()
    {
#if DEBUG
        //IOInit.RegisterExtensions();
        //return 0;
#endif

        return EP(() =>
        {
            StoreContextProxy.Setup("9NTKGQL0HKSR",
            [
                new("9P9L7VS58SF7", T._1Month, T.Premium_MonthlyBilling),
                new("9N306Q90H0NX", T._6Months, T.Premium_SemiAnnuallyBilling),
                new("9P61XFK3LPHT", T.Premium_BuyOnceUseForever, null, true)
            ]);

            PremiumCore.I.Init(ThemeKind.Lay, ConceptKind.Premium,
            [
                new(null, null, T.Premium_Player0Title,     T.Premium_Player0Headline,     null, '\uE708'),
                new(null, null, T.Premium_Player1Title,     T.Premium_Player1Headline,     null, '\uEC49'),
                new(null, null, T.Premium_Converter0Title,  T.Premium_Converter0Headline,  null, '\uE835'),
                new(null, null, T.Premium_Converter1Title,  T.Premium_Converter1Headline,  null, '\uE728'),
                new(null, null, T.Premium_Getter0Title,     T.Premium_Getter0Headline,     null, '\uF6B8')
            ]);

            WelcomeCore.I.Init(LayoutKind.Lay, ResourceHelper.GetString(nameof(L.ToSMediaFull), true),
            [
                new("\uE768", T.WelcomeTitle1, T.WelcomeDesc1),
                new("\uE8B1", T.WelcomeTitle2, T.WelcomeDesc2),
                new("\uEBD2", T.WelcomeTitle3, T.WelcomeDesc3),
                new("\uE72E", T.WelcomeTitle4, T.WelcomeDesc4)
            ]);

#if DEBUG
            IOLicense.DevLicense = IOLicense.License.Default;
#endif

            _ = new App();
        });
    }

    public App()
    {
        InitializeComponent();
    }

    protected override async Task OnReady(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AQualityRecord.PremiumQuality = AQuality._64;
        PlayerProfile.Setup();

        var isLocalExternalOutdated = MediaContext.Init(null,
            new(SimpleMediaProfiles.INPUT_MEDIA_FAMILIES, SimpleMediaProfiles.OUTPUT_MEDIA_FAMILIES),
            new(SimpleMediaProfiles.INPUT_MEDIA_FAMILIES),
            new(Mode.AudioGrabber, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic))
        );

        if (isLocalExternalOutdated)
        {
            var splash = RegisterWindow(new(Role.Secondary, new(150, 150)), config =>
            {
                var window = new Splash(config);
                window.Activate();
                return window;
            }, true);

            await Task.Run(MediaContext.RestoreBinaries);

            splash?.AppWindow.Hide();
        }

        RegisterWindow(new(Role.Main, new(300, 300 / 3 * 5), new(300, 300 / 3 * 5)), config =>
        {
            var window = new MainWindow(config);
            window.Activate();
            return window;
        }, false);

        RegisterWindow(new(Role.Secondary, new(500, 360 / 3 * 5), new(360, 360 / 3 * 5)), config =>
        {
            var window = new GrabberWindow(config);
            window.Activate();
            return window;
        }, false);

        RegisterWindow(new(Role.Secondary, new(480, 640)), config =>
        {
            var window = new ConverterWindow(config);
            window.Activate();
            return window;
        }, false);

        RegisterWindow(new(Role.Secondary, new(1060, 760)), config =>
        {
            var window = new PremiumWindow(config);
            window.Activate();
            return window;
        }, false);

        LoadWindow(NavArgs.WindowType ?? typeof(MainWindow));

        _ = Tray.I;
    }

    public enum VerbType
    {
        Undefined,
        Open,
        Play,
        ConvertToMp3,
        ConvertToMp4,
    }

    protected override void OnReactivating(ExtendedActivationKind kind, object data)
    {
        base.OnReactivating(kind, data);

        if (kind is ExtendedActivationKind.File && data is FileActivatedEventArgs fileArgs)
        {
            NavArgs.Verb = fileArgs.Verb;
            NavArgs.Params.ReplaceRange(fileArgs.Files.Select(i => i.Path));

            if (NavArgs.Verb is nameof(VerbType.ConvertToMp3) or nameof(VerbType.ConvertToMp4))
                NavArgs.WindowType = typeof(ConverterWindow);
            else
            {
                NavArgs.WindowType = typeof(MainWindow);
                NavArgs.PageType = typeof(MediaPlayer);
            }
        }
        else if (kind is ExtendedActivationKind.Protocol && data is ProtocolActivatedEventArgs protocolArgs)
        {
            var decoder = new WwwFormUrlDecoder(protocolArgs.Uri.Query);

            decoder.FirstOrDefault(i => i.Name is "verb")?.Value.Let(_ => NavArgs.Verb = _);
            decoder.FirstOrDefault(i => i.Name is "params")?.Value.Let(NavArgs.Params.Replace);

            var pageName = FormatUtils.KebabCaseToCamelCase(protocolArgs.Uri.Host);

            if (NavArgs.Verb is nameof(VerbType.ConvertToMp3) or nameof(VerbType.ConvertToMp4))
                NavArgs.WindowType = typeof(ConverterWindow);
            else
            {
                NavArgs.WindowType = typeof(MainWindow);
                NavArgs.PageType = pageName is nameof(MediaPlayer) ? typeof(MediaPlayer) : typeof(Home);
            }
        }
        else
        {
            NavArgs.WindowType = typeof(MainWindow);
            NavArgs.PageType = typeof(Home);
        }
    }
}

#if DEBUG
class ContextMenuVerb(string vId, string vSwitch, string vContent)
{
    public string Id = vId;
    public string Switch = vSwitch;
    public string Content = vContent;
}

class ContextMenuItem(string[] extensions, ContextMenuVerb[] verbs)
{
    public string[] Extensions = extensions;
    public ContextMenuVerb[] Verbs = verbs;
}

class IOInit
{
    public static void RegisterExtensions()
    {
        var extensionTemplate = "<uap3:Extension Category=\"windows.fileTypeAssociation\"><uap3:FileTypeAssociation Name=\"%name%\" Parameters=\"&quot;%1&quot;\">%extensionContent%</uap3:FileTypeAssociation></uap3:Extension>";

        var supportedFileTypesTemplate = "<uap:SupportedFileTypes>%supportedFileTypeContent%</uap:SupportedFileTypes>";
        var fileTypeTemplate = "<uap:FileType>%fileType%</uap:FileType>";

        var supportedVerbsTemplate = "<uap2:SupportedVerbs>%supportedVerbContent%</uap2:SupportedVerbs>";
        var verbTemplate = "<uap3:Verb Id=\"%verbId%\" Parameters=\"&quot;%1&quot; %verbSwitch%\">%verbContent%</uap3:Verb>";

        var baseContextMenuVerbs = new List<ContextMenuVerb>()
        {
            new(App.VerbType.Open.ToString(), $"/{App.VerbType.Open}", $"Open with {Package.Current?.DisplayName}"),
            new(App.VerbType.ConvertToMp3.ToString(), $"/{App.VerbType.ConvertToMp3}", $"Convert to MP3 with {Package.Current?.DisplayName}"),
        };

        var mediaTypes = PlayerConfig.I.InputMediaExtensions.Except(PlayerConfig.I.InputVideoExtensions).Select(i => fileTypeTemplate.Replace("%fileType%", i)).ToArray();
        var baseVerbs = baseContextMenuVerbs.Select(i => verbTemplate.Replace("%verbId%", i.Id).Replace("%verbSwitch%", i.Switch).Replace("%verbContent%", i.Content));

        var supportedMediaTypes = supportedFileTypesTemplate.Replace("%supportedFileTypeContent%", string.Join("\n", mediaTypes));
        var supportedbaseVerbs = supportedVerbsTemplate.Replace("%supportedVerbContent%", string.Join("\n", baseVerbs));

        var mediaExtension = extensionTemplate.Replace("%name%", "audio").Replace("%extensionContent%", supportedMediaTypes + "\n" + supportedbaseVerbs);

        //

        var videoContextMenuVerbs = new List<ContextMenuVerb>()
            {
                new(App.VerbType.Open.ToString(), $"/{App.VerbType.Open}", $"Open with {Package.Current?.DisplayName}"),
                new(App.VerbType.ConvertToMp3.ToString(), $"/{App.VerbType.ConvertToMp3}", $"Convert to MP3 with {Package.Current?.DisplayName}"),
                new(App.VerbType.ConvertToMp4.ToString(), $"/{App.VerbType.ConvertToMp4}", $"Convert to MP4 with {Package.Current?.DisplayName}"),
            };

        var videoTypes = PlayerConfig.I.InputVideoExtensions.Select(i => fileTypeTemplate.Replace("%fileType%", i)).ToArray();
        var videoVerbs = videoContextMenuVerbs.Select(i => verbTemplate.Replace("%verbId%", i.Id).Replace("%verbSwitch%", i.Switch).Replace("%verbContent%", i.Content));

        var supportedVideoTypes = supportedFileTypesTemplate.Replace("%supportedFileTypeContent%", string.Join("\n", videoTypes));
        var supportedVideoVerbs = supportedVerbsTemplate.Replace("%supportedVerbContent%", string.Join("\n", videoVerbs));

        var videoExtension = extensionTemplate.Replace("%name%", "video").Replace("%extensionContent%", supportedVideoTypes + "\n" + supportedVideoVerbs);

        //

        var directoryPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (directoryPath is null)
            return;

        var path = Path.Combine(directoryPath, "Package.appxmanifest");
        var document = XmlUtils.LoadXmlDocument(path);

        if (document?.DocumentElement is null)
            return;

        document.DocumentElement.SetAttribute("xmlns:uap2", "http://schemas.microsoft.com/appx/manifest/uap/windows10/2");
        document.DocumentElement.SetAttribute("xmlns:uap3", "http://schemas.microsoft.com/appx/manifest/uap/windows10/3");
        document.DocumentElement.SetAttribute("xmlns:desktop", "http://schemas.microsoft.com/appx/manifest/desktop/windows10");

        var identity = document.GetElementsByTagName("Identity")[0];
        if (identity?.Attributes is null)
            return;

        var name = identity.Attributes.GetNamedItem("Name")?.InnerText + ".exe";

        var displayName = document.GetElementsByTagName("Properties")?[0]?.FirstChild?.InnerText ?? "";
        var protocol = FormatUtils.ToKebabCase(displayName);

        var appExecutionAlias = $"<uap3:Extension\r\n\t\t\t\t\t  Category=\"windows.appExecutionAlias\"\r\n\t\t\t\t\t  EntryPoint=\"Windows.FullTrustApplication\">\r\n\t\t\t\t\t<uap3:AppExecutionAlias>\r\n\t\t\t\t\t\t<desktop:ExecutionAlias Alias=\"{name}\" />\r\n\t\t\t\t\t</uap3:AppExecutionAlias>\r\n\t\t\t\t</uap3:Extension>";
        var windowsProtocol = $"<uap:Extension Category=\"windows.protocol\">\r\n\t\t\t\t\t<uap:Protocol Name=\"{protocol}\">\r\n\t\t\t\t\t\t<uap:DisplayName>{displayName}</uap:DisplayName>\r\n\t\t\t\t\t</uap:Protocol>\r\n\t\t\t\t</uap:Extension>";

        var extensionsElement = document.GetElementsByTagName("Extensions");
        if (extensionsElement.Count == 0)
        {
            var appElement = document.GetElementsByTagName("Application")[0];
            if (appElement is null)
                return;

            var newExtensionsElement = document.CreateNode(System.Xml.XmlNodeType.Element, "Extensions", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            appElement.AppendChild(newExtensionsElement);
        }

        var node = document.GetElementsByTagName("Extensions")[0];
        if (node is null)
            return;

        node.RemoveAll();

        var extensionsXml = new List<string>();
        extensionsXml.AddRange([windowsProtocol, appExecutionAlias]);

        if (PlayerProfile.HasPlayer)
            extensionsXml.AddRange([mediaExtension, videoExtension]);

        node.InnerXml = string.Join("\n", extensionsXml);

        document.Save(path);

        System.Windows.MessageBox.Show("Comment IOInit.RegisterExtensions() in App.xaml.cs to start.", "Called RegisterExtensions()");
        Application.Current.Exit();
    }
}
#endif