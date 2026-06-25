using System.Windows;
using Netswitch.UI.ViewModels;

namespace Netswitch.UI.Windows;

public partial class SecurityDashboardWindow : Window
{
    public SecurityDashboardWindow(SecurityDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        Loaded += async (s, e) => 
        {
            await viewModel.RefreshDevicesAsync();
        };
    }
}
