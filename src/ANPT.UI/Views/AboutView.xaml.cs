using System.Windows.Controls;
using ANPT.Application.Interfaces;

namespace ANPT.UI.Views;

public partial class AboutView : UserControl
{
    public AboutView(IApplicationInfo appInfo)
    {
        InitializeComponent();
        AppNameText.Text = appInfo.ApplicationName;
        VersionText.Text = $"Version {appInfo.Version}";
        DescriptionText.Text = appInfo.Description;
    }
}
