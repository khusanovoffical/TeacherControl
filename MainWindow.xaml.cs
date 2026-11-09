using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TeacherControl.Models;
using TeacherControl.Services;

namespace TeacherControl;

public partial class MainWindow : Window
{
    private readonly TeacherService _service = new();
    private readonly ObservableCollection<string> _blockedProcesses = new();

    public MainWindow()
    {
        InitializeComponent();

        DashboardDataGrid.ItemsSource = _service.Devices;
        ScreensItemsControl.ItemsSource = _service.Devices;
        DevicesDataGrid.ItemsSource = _service.Devices;
        FilesDataGrid.ItemsSource = _service.Files;
        BlockedProcessesListBox.ItemsSource = _blockedProcesses;

        _service.OnStatusChanged += HandleStatusChanged;
        _service.Devices.CollectionChanged += (_, _) => UpdateStats();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var serverUrl = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL") ?? "http://localhost:5123";
        var apiKey = Environment.GetEnvironmentVariable("SCHOOLCONTROL_TEACHER_API_KEY") ?? "school_teacher_secret_2026";

        SettingsServerUrlBox.Text = serverUrl;
        SettingsApiKeyBox.Text = apiKey;
        ServerUrlLabel.Text = serverUrl;

        await ConnectToServerAsync(serverUrl, apiKey);
        await LoadPoliciesAsync();
    }

    private async Task ConnectToServerAsync(string url, string key)
    {
        ServerStatusText.Text = "Ulanmoqda...";
        ServerStatusDot.Fill = (SolidColorBrush)FindResource("WarningBrush");

        var ok = await _service.InitializeAsync(url, key);
        if (ok)
        {
            ServerStatusText.Text = "Serverga ulangan";
            ServerStatusDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            UpdateStats();
        }
        else
        {
            ServerStatusText.Text = "Ulanib bo‘lmadi";
            ServerStatusDot.Fill = (SolidColorBrush)FindResource("DangerBrush");
        }
    }

    private void HandleStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            ServerStatusText.Text = status;
            if (status.Contains("ulangan"))
                ServerStatusDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            else
                ServerStatusDot.Fill = (SolidColorBrush)FindResource("DangerBrush");
            UpdateStats();
        });
    }

    private void UpdateStats()
    {
        var total = _service.Devices.Count;
        var online = _service.Devices.Count(d => d.IsOnline);
        var offline = total - online;
        var locked = _service.Devices.Count(d => d.IsLocked);

        StatTotalDevices.Text = total.ToString();
        StatOnlineDevices.Text = online.ToString();
        StatOfflineDevices.Text = offline.ToString();
        StatLockedDevices.Text = locked.ToString();
    }

    // --- NAVIGATSIYA ---
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;

        if (ViewDashboard != null) ViewDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewScreens != null) ViewScreens.Visibility = tag == "Screens" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewDevices != null) ViewDevices.Visibility = tag == "Devices" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewFiles != null) ViewFiles.Visibility = tag == "Files" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewPolicies != null) ViewPolicies.Visibility = tag == "Policies" ? Visibility.Visible : Visibility.Collapsed;
        if (ViewSettings != null) ViewSettings.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- DASHBOARD VA QIDIRUV ---
    private void QuickSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = QuickSearchBox.Text.Trim();
        DashboardDataGrid.Items.Filter = string.IsNullOrWhiteSpace(q) ? null :
            item => item is DeviceItem d &&
                ($"{d.DeviceName} {d.StudentName} {d.IpAddress}".Contains(q, StringComparison.CurrentCultureIgnoreCase));
    }

    private async void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        await _service.RefreshDevicesAsync();
        await _service.RefreshFilesAsync();
        UpdateStats();
    }

    private async void LockAll_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Barcha o‘quvchilar kompyuterini qulflamoqchimisiz?", "Tasdiqlash", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _service.SendCommandAsync("ALL", "Lock", "Dars davom etmoqda. E’tiboringizni doskaga qarating!");
            foreach (var d in _service.Devices) d.IsLocked = true;
            UpdateStats();
        }
    }

    private async void UnlockAll_Click(object sender, RoutedEventArgs e)
    {
        await _service.SendCommandAsync("ALL", "Unlock");
        foreach (var d in _service.Devices) d.IsLocked = false;
        UpdateStats();
    }

    // --- EKRANLAR (SCREENS) ---
    private void ScreenCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is DeviceItem device)
        {
            OpenScreenViewer(device);
        }
    }

    private void OpenScreen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is DeviceItem device)
        {
            OpenScreenViewer(device);
        }
    }

    private void OpenScreenViewer(DeviceItem device)
    {
        var viewer = new ScreenViewerWindow(device, _service);
        viewer.Show();
    }

    private async void RefreshScreens_Click(object sender, RoutedEventArgs e)
    {
        await _service.SendCommandAsync("ALL", "StartScreenStream");
    }

    // --- QURILMALARNI BOSHQARISH (DEVICES) ---
    private void SelectAllDevices_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in _service.Devices) d.IsSelected = true;
    }

    private void DeselectAllDevices_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in _service.Devices) d.IsSelected = false;
    }

    private async void LockSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _service.Devices.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Hech qanday kompyuter tanlanmagan.", "Xabar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var d in selected)
        {
            await _service.SendCommandAsync(d.DeviceId, "Lock", "O‘qituvchi tomonidan qulflangan.");
            d.IsLocked = true;
        }
        UpdateStats();
    }

    private async void UnlockSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _service.Devices.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) return;

        foreach (var d in selected)
        {
            await _service.SendCommandAsync(d.DeviceId, "Unlock");
            d.IsLocked = false;
        }
        UpdateStats();
    }

    private async void RestartSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _service.Devices.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (MessageBox.Show($"Tanlangan {selected.Count} ta kompyuterni qayta ishga tushirishni xohlaysizmi?", "Tasdiqlash", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            foreach (var d in selected)
            {
                await _service.SendCommandAsync(d.DeviceId, "Restart");
            }
        }
    }

    private async void ShutdownSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _service.Devices.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (MessageBox.Show($"DIQQAT: Tanlangan {selected.Count} ta kompyuter o‘chiriladi. Ishonchingiz komilmi?", "O‘chirishni tasdiqlash", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            foreach (var d in selected)
            {
                await _service.SendCommandAsync(d.DeviceId, "Shutdown");
            }
        }
    }

    // --- FAYLLAR ALMASHINUVI (FILES) ---
    private async void SendFileToStudents_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "O‘quvchilarga tarqatish uchun fayl tanlang",
            Filter = "Barcha fayllar (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            var target = "ALL";
            var selected = _service.Devices.Where(d => d.IsSelected).ToList();
            if (selected.Count == 1)
            {
                target = selected[0].DeviceId;
            }

            var ok = await _service.UploadFileAsync(dlg.FileName, target);
            if (ok)
            {
                MessageBox.Show("Fayl o‘quvchilarga muvaffaqiyatli tarqatildi!", "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Faylni yuklashda xatolik yuz berdi.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void DownloadFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid fileId)
        {
            var record = _service.Files.FirstOrDefault(f => f.Id == fileId);
            var dlg = new SaveFileDialog
            {
                FileName = record?.FileName ?? "file",
                Title = "Faylni saqlash joyini tanlang"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    using var http = new System.Net.Http.HttpClient();
                    var bytes = await http.GetByteArrayAsync($"{_service.ServerUrl}/api/files/download/{fileId}");
                    await File.WriteAllBytesAsync(dlg.FileName, bytes);
                    MessageBox.Show("Fayl muvaffaqiyatli saqlandi!", "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Yuklab bo‘lmadi: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // --- ILOVALAR SIYOSATI (POLICIES) ---
    private async Task LoadPoliciesAsync()
    {
        var json = await _service.GetPoliciesAsync();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            _blockedProcesses.Clear();
            foreach (var item in list) _blockedProcesses.Add(item);
        }
        catch { }
    }

    private void AddBlockedProcess_Click(object sender, RoutedEventArgs e)
    {
        var val = NewProcessNameBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(val) && !_blockedProcesses.Contains(val))
        {
            _blockedProcesses.Add(val);
            NewProcessNameBox.Text = "";
        }
    }

    private void RemoveBlockedProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string proc)
        {
            _blockedProcesses.Remove(proc);
        }
    }

    private async void SavePolicies_Click(object sender, RoutedEventArgs e)
    {
        var json = JsonSerializer.Serialize(_blockedProcesses);
        var ok = await _service.SavePoliciesAsync(json);
        if (ok)
        {
            MessageBox.Show("Ilovalar siyosati muvaffaqiyatli saqlandi va barcha kompyuterlarga yuborildi!", "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Siyosatni saqlashda xatolik yuz berdi.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- SERVER SOZLAMALARI ---
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var url = SettingsServerUrlBox.Text.Trim().TrimEnd('/');
        var key = SettingsApiKeyBox.Text.Trim();

        ServerUrlLabel.Text = url;
        await ConnectToServerAsync(url, key);
    }
}
