namespace SchoolControl.Server.Models;

public sealed class TeacherAccount
{
    public int Id { get; set; } = 1;
    public string Username { get; set; } = "teacher";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
