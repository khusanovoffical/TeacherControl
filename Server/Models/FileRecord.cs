namespace SchoolControl.Server.Models;

public sealed class FileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = "";
    public string StorageFileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string SenderType { get; set; } = "Teacher"; // "Teacher" or "Student"
    public string SenderName { get; set; } = "";
    public string TargetDeviceId { get; set; } = "ALL"; // "ALL" or specific DeviceId
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
