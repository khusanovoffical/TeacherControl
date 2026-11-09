namespace SchoolControl.Server.Models;

public sealed class DevicePolicy
{
    public int Id { get; set; } = 1;
    public string BlockedProcessesJson { get; set; } = "[]";
    public bool InternetRestricted { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
