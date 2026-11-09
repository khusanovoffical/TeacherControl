using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace TeacherControl.Models;

public sealed class DeviceItem : INotifyPropertyChanged
{
    private string _deviceId = "";
    private string _deviceName = "";
    private string _studentName = "O‘quvchi yo‘q";
    private string _ipAddress = "";
    private bool _isOnline = false;
    private bool _isLocked = false;
    private bool _isScreenSharing = false;
    private bool _isSelected = false;
    private string _lastSeen = "—";
    private BitmapSource? _screenThumbnail;

    public string DeviceId { get => _deviceId; set { _deviceId = value; OnPropertyChanged(); } }
    public string DeviceName { get => _deviceName; set { _deviceName = value; OnPropertyChanged(); } }
    public string StudentName { get => _studentName; set { _studentName = value; OnPropertyChanged(); } }
    public string IpAddress { get => _ipAddress; set { _ipAddress = value; OnPropertyChanged(); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; OnPropertyChanged(); } }
    public bool IsLocked { get => _isLocked; set { _isLocked = value; OnPropertyChanged(); } }
    public bool IsScreenSharing { get => _isScreenSharing; set { _isScreenSharing = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public string LastSeen { get => _lastSeen; set { _lastSeen = value; OnPropertyChanged(); } }

    public BitmapSource? ScreenThumbnail
    {
        get => _screenThumbnail;
        set { _screenThumbnail = value; OnPropertyChanged(); }
    }

    public void UpdateThumbnailFromBase64(string base64Jpeg)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Jpeg);
            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            ScreenThumbnail = image;
        }
        catch
        {
            // Invalid image
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
