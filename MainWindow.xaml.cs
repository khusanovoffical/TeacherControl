using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace TeacherControl;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private string _serverStatus = "Server sozlanmagan";
    private string _todayCount = "—";

    public ObservableCollection<StudentRow> Students { get; } = new();
    public int StudentCount => Students.Count;
    public int RegisteredDeviceCount => Students.Count(s => !string.IsNullOrWhiteSpace(s.DeviceName) && s.DeviceName != "Noma’lum qurilma");
    public int OnlineCount => 0;
    public string TodayCount { get => _todayCount; private set { _todayCount = value; OnPropertyChanged(); } }
    public string ServerStatus { get => _serverStatus; private set { _serverStatus = value; OnPropertyChanged(); } }
    public Visibility EmptyStateVisibility => Students.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await RefreshFromServerAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshFromServerAsync();

    private async Task RefreshFromServerAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable("SCHOOLCONTROL_API_URL")?.TrimEnd('/');
        var teacherKey = Environment.GetEnvironmentVariable("SCHOOLCONTROL_TEACHER_API_KEY");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(teacherKey))
        {
            ServerStatus = "Server sozlamasi kerak";
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/teacher/students");
            request.Headers.Add("X-Teacher-Key", teacherKey);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ServerStatus = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Teacher kaliti noto‘g‘ri" : $"Server: {(int)response.StatusCode}";
                return;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<StudentApiRow>>() ?? [];
            Students.Clear();
            foreach (var row in rows)
                Students.Add(new StudentRow
                {
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    DeviceName = string.IsNullOrWhiteSpace(row.DeviceName) ? "Noma’lum qurilma" : row.DeviceName,
                    Status = row.DeviceId.Length > 0 ? "Ro‘yxatdan o‘tgan" : "Qurilma ulanmagan",
                    LastSeen = row.LastSeenUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—"
                });

            ServerStatus = "Server ulangan";
            OnPropertyChanged(nameof(StudentCount));
            OnPropertyChanged(nameof(RegisteredDeviceCount));
            OnPropertyChanged(nameof(OnlineCount));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            ApplySearch();
        }
        catch (HttpRequestException)
        {
            ServerStatus = "Serverga ulanib bo‘lmadi";
        }
        catch (TaskCanceledException)
        {
            ServerStatus = "Server javobi kechikdi";
        }
        catch (Exception)
        {
            ServerStatus = "Ma’lumotni yuklashda xatolik";
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        if (StudentsGrid is null || SearchBox is null) return;
        var query = SearchBox.Text.Trim();
        StudentsGrid.Items.Filter = string.IsNullOrWhiteSpace(query) ? null :
            item => item is StudentRow row &&
                ($"{row.FirstName} {row.LastName} {row.DeviceName}".Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class StudentApiRow
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
}

public sealed class StudentRow
{
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string Status { get; init; } = "Noma’lum";
    public string LastSeen { get; init; } = "";
}