using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Netswitch.UI.Controls;

public partial class LoadingSpinner : UserControl
{
    private Storyboard? _animation;

    public LoadingSpinner()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Navigate through the visual tree: UserControl -> Viewbox -> Canvas
        if (Content is Viewbox viewbox && viewbox.Child is Canvas canvas)
        {
            _animation = (Storyboard)canvas.Resources["SpinnerAnimation"];
            _animation?.Begin();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _animation?.Stop();
    }
}
