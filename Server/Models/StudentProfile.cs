namespace SchoolControl.Server.Models;

public sealed class StudentProfile
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}