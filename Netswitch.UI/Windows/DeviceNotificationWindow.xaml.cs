using System;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Netswitch.Core.Models;

namespace Netswitch.UI.Windows;

public partial class DeviceNotificationWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private int _secondsRemaining = 10;

    public DeviceNotificationWindow(NetworkDevice device, bool isConnected, bool playSoundEnabled)
    {
        InitializeComponent();
        
        // Set notification content
        if (isConnected)
        {
            IconText.Text = "🔌";
            TitleText.Text = "Device Connected";
            TitleText.Foreground = Brushes.LightGreen;
        }
        else
        {
            IconText.Text = "⚠️";
            TitleText.Text = "Device Disconnected";
            TitleText.Foreground = Brushes.Orange;
        }

        DeviceNameText.Text = string.IsNullOrWhiteSpace(device.HostName) 
            ? device.IpAddress 
            : device.HostName;
        
        IpAddressText.Text = device.IpAddress;
        MacAddressText.Text = device.MacAddress ?? "Unknown";
        TimeText.Text = device.LastSeen.ToLocalTime().ToString("g");

        // Setup auto-close timer
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _closeTimer.Tick += CloseTimer_Tick;
        
        // Play notification sound if enabled
        if (playSoundEnabled)
        {
            PlayNotificationSound(isConnected);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position in bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
        
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

    private static void PlayNotificationSound(bool isConnected)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var soundFile = isConnected ? "connected.wav" : "disconnected.wav";
                var uri = new Uri($"pack://application:,,,/Sounds/{soundFile}");
                
                var player = new SoundPlayer();
                using var stream = Application.GetResourceStream(uri)?.Stream;
                
                if (stream != null)
                {
                    player.Stream = stream;
                    player.PlaySync();
                    
                    if (isConnected)
                    {
                        // Play twice for connection
                        stream.Position = 0;
                        player.PlaySync();
                    }
                }
                else
                {
                    // Fallback to system sounds
                    if (isConnected)
                    {
                        SystemSounds.Asterisk.Play();
                        System.Threading.Thread.Sleep(200);
                        SystemSounds.Asterisk.Play();
                    }
                    else
                    {
                        SystemSounds.Exclamation.Play();
                    }
                }
            }
            catch
            {
                // Final fallback to system sounds
                try
                {
                    if (isConnected)
                        SystemSounds.Asterisk.Play();
                    else
                        SystemSounds.Exclamation.Play();
                }
                catch
                {
                    // Ignore if all sound methods fail
                }
            }
        });
    }
}
