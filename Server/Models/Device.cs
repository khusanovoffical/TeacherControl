namespace SchoolControl.Server.Models;

public sealed class Device
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string CurrentStudentName { get; set; } = "";
    public bool IsOnline { get; set; }
    public bool IsLocked { get; set; }
    public bool IsScreenSharing { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}
