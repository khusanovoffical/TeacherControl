using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace TeacherControl;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<StudentRow> Students { get; } = new();
    public int StudentCount => Students.Count;
    public int OnlineCount => Students.Count(s => s.Status == "Onlayn");
    public int TodayCount => 0;
    public Visibility EmptyStateVisibility => Students.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        RefreshLocalView();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLocalView();

    private void RefreshLocalView()
    {
        // Until the authenticated school server is configured, do not invent device or attendance data.
        OnPropertyChanged(nameof(StudentCount));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(TodayCount));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        StudentsGrid.Items.Refresh();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            StudentsGrid.Items.Filter = null;
            return;
        }
        StudentsGrid.Items.Filter = item => item is StudentRow row &&
            ($"{row.FirstName} {row.LastName}".Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class StudentRow
{
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string Status { get; init; } = "Noma’lum";
    public string LastSeen { get; init; } = "";
}