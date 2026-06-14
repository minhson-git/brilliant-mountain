using IOApp.Configs;
using IOCore.Core;
using IOCore.Gens;
using Microsoft.UI.Xaml.Controls;

namespace IOApp.Features
{
    internal partial class TrialInfoButton : UserControl
    {
        public string Title => string.Format(R.T(L.Premium_Title), Constants.LIMIT_TEXT);
        public string Desc => string.Format(R.T(L.Premium_Desc), Constants.LIMIT_TEXT);

        public TrialInfoButton()
        {
            InitializeComponent();
        }
    }
}