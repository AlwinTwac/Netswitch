using System;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using Netswitch.Core.Models;

namespace Netswitch.UI.Windows;

public partial class NetworkAlertWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private int _secondsRemaining = 8;

    public NetworkAlertWindow(NetworkConditionAlert alert, bool playSoundEnabled)
    {
        InitializeComponent();
        
        // Set notification content based on condition
        TitleText.Text = alert.Condition switch
        {
            NetworkCondition.Slow => "Network Slow",
            NetworkCondition.Unstable => "Network Unstable",
            NetworkCondition.HighLatency => "High Latency",
            NetworkCondition.PacketLoss => "Packet Loss Detected",
            NetworkCondition.Disconnected => "Network Disconnected",
            _ => "Network Warning"
        };

        MessageText.Text = alert.Message;

        if (alert.LatencyMs.HasValue)
        {
            DetailsPanel.Visibility = Visibility.Visible;
            LatencyText.Text = $"{alert.LatencyMs:F0} ms";
        }

        // Setup auto-close timer
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _closeTimer.Tick += CloseTimer_Tick;
        
        // Play notification sound if enabled
        if (playSoundEnabled)
        {
            PlayNetworkAlertSound();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position in bottom-right corner (offset from device notifications)
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 240; // Higher than device notifications
        
        // Start auto-close timer
        _closeTimer.Start();
    }

    private void CloseTimer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        AutoCloseText.Text = $"Closing in {_secondsRemaining}s...";
        
        if (_secondsRemaining <= 0)
        {
            _closeTimer.Stop();
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _closeTimer.Stop();
        Close();
    }

    private static void PlayNetworkAlertSound()
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Sounds/network_alert.wav");
                
                var player = new SoundPlayer();
                using var stream = Application.GetResourceStream(uri)?.Stream;
                
                if (stream != null)
                {
                    player.Stream = stream;
                    player.PlaySync();
                }
                else
                {
                    // Fallback to system sound
                    SystemSounds.Hand.Play();
                }
            }
            catch
            {
                // Final fallback to system sound
                try
                {
                    SystemSounds.Hand.Play();
                }
                catch
                {
                    // Ignore if all sound methods fail
                }
            }
        });
    }
}
