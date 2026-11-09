using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using TeacherControl.Models;

namespace TeacherControl.Services;

public sealed class TeacherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private HubConnection? _hubConnection;

    public string ServerUrl { get; set; } = "http://localhost:5123";
    public string TeacherKey { get; set; } = "school_teacher_secret_2026";
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public ObservableCollection<DeviceItem> Devices { get; } = new();
    public ObservableCollection<FileItem> Files { get; } = new();

    public event Action<string>? OnStatusChanged;
    public event Action<string, string>? OnScreenReceived;

    public async Task<bool> InitializeAsync(string serverUrl, string teacherKey)
    {
        ServerUrl = serverUrl.TrimEnd('/');
        TeacherKey = teacherKey;

        try
        {
            // 1. Health check
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ServerUrl}/health");
            var res = await Http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                OnStatusChanged?.Invoke($"Server xatosi: {(int)res.StatusCode}");
                return false;
            }

            // 2. Initial Devices load
            await RefreshDevicesAsync();
            await RefreshFilesAsync();

            // 3. Connect SignalR
            await ConnectSignalRAsync();

            OnStatusChanged?.Invoke("Serverga ulangan");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Ulanib bo‘lmadi: {ex.Message}");
            return false;
        }
    }

    private async Task ConnectSignalRAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{ServerUrl}/hubs/school")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Reconnecting += _ =>
        {
            Application.Current.Dispatcher.Invoke(() => OnStatusChanged?.Invoke("Qayta ulanmoqda..."));
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async _ =>
        {
            Application.Current.Dispatcher.Invoke(() => OnStatusChanged?.Invoke("Serverga ulangan"));
            await _hubConnection.SendAsync("RegisterTeacher", TeacherKey);
        };

        _hubConnection.Closed += _ =>
        {
            Application.Current.Dispatcher.Invoke(() => OnStatusChanged?.Invoke("Aloqa uzildi"));
            return Task.CompletedTask;
        };

        // Real-time device updates
        _hubConnection.On<JsonElement>("DeviceStatusChanged", elem =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var deviceId = elem.GetProperty("deviceId").GetString() ?? "";
                var isOnline = elem.GetProperty("isOnline").GetBoolean();

                var item = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
                if (item != null)
                {
                    item.IsOnline = isOnline;
                    if (elem.TryGetProperty("studentName", out var sn))
                        item.StudentName = sn.GetString() ?? item.StudentName;
                    if (elem.TryGetProperty("isLocked", out var lk))
                        item.IsLocked = lk.GetBoolean();
                    if (elem.TryGetProperty("isScreenSharing", out var sc))
                        item.IsScreenSharing = sc.GetBoolean();
                    item.LastSeen = "Hozirgina";
                }
                else
                {
                    // Yangi qurilma qo'shish
                    var name = elem.TryGetProperty("deviceName", out var dn) ? dn.GetString() ?? "PC" : "PC";
                    var student = elem.TryGetProperty("studentName", out var s) ? s.GetString() ?? "" : "";
                    Devices.Add(new DeviceItem
                    {
                        DeviceId = deviceId,
                        DeviceName = name,
                        StudentName = student,
                        IsOnline = isOnline,
                        LastSeen = "Hozirgina"
                    });
                }
            });
        });

        // Ekran thumbnail
        _hubConnection.On<string, string>("ReceiveScreenThumbnail", (deviceId, base64) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var item = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
                if (item != null)
                {
                    item.UpdateThumbnailFromBase64(base64);
                }
                OnScreenReceived?.Invoke(deviceId, base64);
            });
        });

        // O'quvchi fayl yuklaganda
        _hubConnection.On<JsonElement>("StudentFileUploaded", elem =>
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await RefreshFilesAsync();
            });
        });

        await _hubConnection.StartAsync();
        await _hubConnection.SendAsync("RegisterTeacher", TeacherKey);
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ServerUrl}/api/teacher/devices");
            req.Headers.Add("X-Teacher-Key", TeacherKey);
            using var res = await Http.SendAsync(req);

            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<DeviceApiDto>>() ?? [];
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Devices.Clear();
                    foreach (var d in list)
                    {
                        Devices.Add(new DeviceItem
                        {
                            DeviceId = d.DeviceId,
                            DeviceName = d.DeviceName,
                            StudentName = string.IsNullOrWhiteSpace(d.CurrentStudentName) ? "O‘quvchi yo‘q" : d.CurrentStudentName,
                            IpAddress = d.IpAddress,
                            IsOnline = d.IsOnline,
                            IsLocked = d.IsLocked,
                            IsScreenSharing = d.IsScreenSharing,
                            LastSeen = d.LastSeenUtc.ToLocalTime().ToString("HH:mm:ss")
                        });
                    }
                });
            }
        }
        catch { }
    }

    public async Task RefreshFilesAsync()
    {
        try
        {
            var list = await Http.GetFromJsonAsync<List<FileApiDto>>($"{ServerUrl}/api/files") ?? [];
            Application.Current.Dispatcher.Invoke(() =>
            {
                Files.Clear();
                foreach (var f in list)
                {
                    Files.Add(new FileItem
                    {
                        Id = f.Id,
                        FileName = f.OriginalFileName,
                        FormattedSize = FormatBytes(f.FileSizeBytes),
                        SenderType = f.SenderType,
                        SenderName = f.SenderName,
                        Target = f.TargetDeviceId,
                        UploadedAt = f.UploadedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                    });
                }
            });
        }
        catch { }
    }

    public async Task<bool> SendCommandAsync(string targetDeviceId, string command, string payload = "")
    {
        try
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("SendCommandToDevice", targetDeviceId, command, payload);
                return true;
            }
            else
            {
                // Fallback REST API
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{ServerUrl}/api/teacher/command");
                req.Headers.Add("X-Teacher-Key", TeacherKey);
                req.Content = JsonContent.Create(new { TargetDeviceId = targetDeviceId, Command = command, Payload = payload });
                var res = await Http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task SendRemoteInputAsync(string targetDeviceId, string inputJson)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.SendAsync("SendRemoteInput", targetDeviceId, inputJson);
            }
            catch { }
        }
    }

    public async Task<bool> UploadFileAsync(string filePath, string targetDeviceId)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            var url = $"{ServerUrl}/api/files/upload?targetDeviceId={targetDeviceId}&senderType=Teacher&senderName=O‘qituvchi";
            var res = await Http.PostAsync(url, content);
            if (res.IsSuccessStatusCode)
            {
                await RefreshFilesAsync();
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<string> GetPoliciesAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"{ServerUrl}/api/policies");
            if (doc.TryGetProperty("blockedProcessesJson", out var prop))
            {
                return prop.GetString() ?? "[]";
            }
        }
        catch { }
        return "[]";
    }

    public async Task<bool> SavePoliciesAsync(string blockedJson)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{ServerUrl}/api/policies");
            req.Headers.Add("X-Teacher-Key", TeacherKey);
            req.Content = JsonContent.Create(new { Id = 1, BlockedProcessesJson = blockedJson });
            var res = await Http.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public sealed class DeviceApiDto
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string CurrentStudentName { get; set; } = "";
    public bool IsOnline { get; set; }
    public bool IsLocked { get; set; }
    public bool IsScreenSharing { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class FileApiDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string SenderType { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string TargetDeviceId { get; set; } = "";
    public DateTimeOffset UploadedAtUtc { get; set; }
}
