namespace TeacherControl.Models;

public sealed class FileItem
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string FormattedSize { get; set; } = "";
    public string SenderType { get; set; } = "Teacher";
    public string SenderName { get; set; } = "";
    public string Target { get; set; } = "ALL";
    public string UploadedAt { get; set; } = "";
}
