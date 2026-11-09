using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TeacherControl.Models;
using TeacherControl.Services;

namespace TeacherControl;

public partial class ScreenViewerWindow : Window
{
    private readonly DeviceItem _device;
    private readonly TeacherService _service;
    private bool _remoteControlActive = false;
    private bool _isFullscreen = false;

    public ScreenViewerWindow(DeviceItem device, TeacherService service)
    {
        InitializeComponent();
        _device = device;
        _service = service;

        DeviceTitleText.Text = $"{device.DeviceName} · {device.StudentName}";
        DeviceSubtitleText.Text = $"Device ID: {device.DeviceId} · Jonli ekran sessiyasi";

        // Agar avvaldan thumbnail mavjud bo'lsa
        if (device.ScreenThumbnail != null)
        {
            ScreenImage.Source = device.ScreenThumbnail;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // Yangi kadr kelganda
        _service.OnScreenReceived += HandleScreenReceived;

        // Ekranni uzatish buyrug'ini yuborish
        _ = _service.SendCommandAsync(_device.DeviceId, "StartScreenStream");

        Closing += ScreenViewerWindow_Closing;
    }

    private void HandleScreenReceived(string deviceId, string base64)
    {
        if (deviceId != _device.DeviceId) return;

        Dispatcher.Invoke(() =>
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();

                ScreenImage.Source = image;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch { }
        });
    }

    private void RemoteControlToggle_Checked(object sender, RoutedEventArgs e)
    {
        _remoteControlActive = true;
        RemoteControlToggle.Content = "Masofaviy boshqaruv (FAOL)";
        RemoteControlToggle.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D4ED8")!;
        RemoteControlToggle.Foreground = System.Windows.Media.Brushes.White;
    }

    private void RemoteControlToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _remoteControlActive = false;
        RemoteControlToggle.Content = "Masofaviy boshqaruv (OFF)";
        RemoteControlToggle.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1E293B")!;
        RemoteControlToggle.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#94A3B8")!;
    }

    private async void ScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_remoteControlActive) return;

        var pos = e.GetPosition(ScreenImage);
        if (ScreenImage.ActualWidth > 0 && ScreenImage.ActualHeight > 0)
        {
            double nx = Math.Clamp(pos.X / ScreenImage.ActualWidth, 0.0, 1.0);
            double ny = Math.Clamp(pos.Y / ScreenImage.ActualHeight, 0.0, 1.0);

            var json = JsonSerializer.Serialize(new { type = "MouseMove", x = nx, y = ny });
            await _service.SendRemoteInputAsync(_device.DeviceId, json);
        }
    }

    private async void ScreenImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_remoteControlActive) return;

        var btn = e.ChangedButton == MouseButton.Right ? "Right" : "Left";
        var json = JsonSerializer.Serialize(new { type = "MouseDown", button = btn });
        await _service.SendRemoteInputAsync(_device.DeviceId, json);
    }

    private async void ScreenImage_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_remoteControlActive) return;

        var btn = e.ChangedButton == MouseButton.Right ? "Right" : "Left";
        var json = JsonSerializer.Serialize(new { type = "MouseUp", button = btn });
        await _service.SendRemoteInputAsync(_device.DeviceId, json);
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_remoteControlActive) return;
        var vk = KeyInterop.VirtualKeyFromKey(e.Key);
        var json = JsonSerializer.Serialize(new { type = "KeyDown", keyCode = vk });
        await _service.SendRemoteInputAsync(_device.DeviceId, json);
    }

    private async void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (!_remoteControlActive) return;
        var vk = KeyInterop.VirtualKeyFromKey(e.Key);
        var json = JsonSerializer.Serialize(new { type = "KeyUp", keyCode = vk });
        await _service.SendRemoteInputAsync(_device.DeviceId, json);
    }

    private async void Lock_Click(object sender, RoutedEventArgs e)
    {
        await _service.SendCommandAsync(_device.DeviceId, "Lock", "O‘qituvchi tomonidan qulflangan.");
        _device.IsLocked = true;
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        await _service.SendCommandAsync(_device.DeviceId, "Unlock");
        _device.IsLocked = false;
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreen)
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            _isFullscreen = false;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ScreenViewerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _service.OnScreenReceived -= HandleScreenReceived;
        _ = _service.SendCommandAsync(_device.DeviceId, "StopScreenStream");
    }
}
