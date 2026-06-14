using IOCore;
using IOCore.Base;
using IOCore.Premium;
using System.Linq;

namespace IOApp.Dialogs
{
    internal sealed partial class MiniPremiumDialog : PremiumDialog
    {
        public class FeatureEx(char? icon, string? title, string? headline, bool isAvailableInFree, bool isAvailableInPremium) :
            Feature(null, null, title, headline, null, icon)
        {
            public bool IsAvailableInFree { get; private set; } = isAvailableInFree;
            public bool IsAvailableInPremium { get; private set; } = isAvailableInPremium;
        }

        public ObservableCollectionEx<FeatureEx> FeatureExs { get; private set; } = [];

        public MiniPremiumDialog(WindowEx window) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            FeatureExs.Replace(PremiumCore.Features.Select(i => new FeatureEx(i.Icon, i.Title, i.Headline, false, true)));
            Notify(nameof(FeatureExs));
        }
    }

    internal class MiniPremiumDialogProxy : BindingProxy<MiniPremiumDialog> { }
}