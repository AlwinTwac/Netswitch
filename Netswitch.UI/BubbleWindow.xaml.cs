using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Netswitch.UI;

public partial class BubbleWindow : Window
{
    private double _scale = 1.0;

    public event EventHandler? RestoreRequested;

    public BubbleWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionBottomRight();
    }

    public void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        Left = Math.Max(workArea.Left + 12, workArea.Right - width - 24);
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - height - 24);
    }

    public void ApplyScale(double scale)
    {
        if (scale <= 0)
        {
            return;
        }

        _scale = scale;
        RootScale.ScaleX = scale;
        RootScale.ScaleY = scale;
        Width = 220 * scale;
        Height = 220 * scale;
        PositionBottomRight();
    }

    public void ApplyOpacity(double opacity)
    {
        opacity = Math.Clamp(opacity, 0.2, 1.0);
        Root.Opacity = opacity;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        RestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpacityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && double.TryParse(tag, out var value))
        {
            ApplyOpacity(value);
        }
    }

    private void ScaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && double.TryParse(tag, out var value))
        {
            ApplyScale(value);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (BubbleGrid.ContextMenu is { } menu)
        {
            menu.PlacementTarget = BubbleGrid;
            menu.IsOpen = true;
        }
    }

    public void SyncWithMainWindow(WindowState state)
    {
        if (state == WindowState.Minimized)
        {
            if (!IsVisible)
            {
                Show();
                ApplyScale(_scale);
                PositionBottomRight();
            }
        }
        else
        {
            Hide();
        }
    }
}
