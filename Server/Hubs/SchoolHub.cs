using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolControl.Server.Data;
using SchoolControl.Server.Models;

namespace SchoolControl.Server.Hubs;

public sealed class SchoolHub(SchoolDbContext db, ILogger<SchoolHub> logger, IConfiguration config) : Hub
{
    // Mapping: ConnectionId <-> DeviceId
    private static readonly ConcurrentDictionary<string, string> ConnectionToDevice = new();
    private static readonly ConcurrentDictionary<string, string> DeviceToConnection = new();

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToDevice.TryRemove(Context.ConnectionId, out var deviceId))
        {
            DeviceToConnection.TryRemove(deviceId, out _);
            try
            {
                var device = await db.Devices.FindAsync(deviceId);
                if (device != null)
                {
                    device.IsOnline = false;
                    device.LastSeenUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();
                }

                await Clients.Group("Teachers").SendAsync("DeviceStatusChanged", new
                {
                    DeviceId = deviceId,
                    IsOnline = false,
                    LastSeenUtc = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Xatolik device statusini yangilashda");
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<bool> RegisterTeacher(string teacherKey)
    {
        var expected = config["SCHOOLCONTROL_TEACHER_API_KEY"] ?? "school_teacher_secret_2026";
        if (teacherKey != expected)
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Teachers");
        logger.LogInformation("Teacher ulandi: {ConnectionId}", Context.ConnectionId);
        return true;
    }

    public async Task RegisterStudent(string deviceId, string deviceName, string firstName, string lastName)
    {
        ConnectionToDevice[Context.ConnectionId] = deviceId;
        DeviceToConnection[deviceId] = Context.ConnectionId;

        await Groups.AddToGroupAsync(Context.ConnectionId, "Students");

        var now = DateTimeOffset.UtcNow;
        var fullName = $"{firstName} {lastName}".Trim();

        var device = await db.Devices.FindAsync(deviceId);
        if (device == null)
        {
            device = new Device
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                IpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "",
                CurrentStudentName = fullName,
                IsOnline = true,
                FirstSeenUtc = now,
                LastSeenUtc = now
            };
            db.Devices.Add(device);
        }
        else
        {
            device.DeviceName = deviceName;
            device.CurrentStudentName = fullName;
            device.IpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? device.IpAddress;
            device.IsOnline = true;
            device.LastSeenUtc = now;
        }

        // Sessiya yozuvi
        var session = new StudentSession
        {
            DeviceId = deviceId,
            FirstName = firstName,
            LastName = lastName,
            StartedAtUtc = now
        };
        db.StudentSessions.Add(session);

        await db.SaveChangesAsync();

        logger.LogInformation("Student ulandi: {DeviceId} ({FullName})", deviceId, fullName);

        // Teacherlarga xabar
        await Clients.Group("Teachers").SendAsync("DeviceStatusChanged", new
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            StudentName = fullName,
            IsOnline = true,
            IsLocked = device.IsLocked,
            IsScreenSharing = device.IsScreenSharing,
            IpAddress = device.IpAddress,
            LastSeenUtc = now
        });
    }

    // Teacher komandalari
    public async Task SendCommandToDevice(string targetDeviceId, string command, string payload)
    {
        logger.LogInformation("Command: {Command} -> {Target}", command, targetDeviceId);

        // DB holatini yangilash (Lock / Unlock bo'lsa)
        if (command == "Lock" || command == "Unlock")
        {
            bool lockState = command == "Lock";
            if (targetDeviceId == "ALL")
            {
                var allDevices = await db.Devices.ToListAsync();
                foreach (var d in allDevices) d.IsLocked = lockState;
                await db.SaveChangesAsync();
            }
            else
            {
                var d = await db.Devices.FindAsync(targetDeviceId);
                if (d != null)
                {
                    d.IsLocked = lockState;
                    await db.SaveChangesAsync();
                }
            }
        }

        if (targetDeviceId == "ALL")
        {
            await Clients.Group("Students").SendAsync("ReceiveCommand", command, payload);
        }
        else if (DeviceToConnection.TryGetValue(targetDeviceId, out var connId))
        {
            await Clients.Client(connId).SendAsync("ReceiveCommand", command, payload);
        }

        // Barcha o'qituvchilarga ham holat o'zgargani haqida xabar berish
        await Clients.Group("Teachers").SendAsync("CommandBroadcasted", targetDeviceId, command, payload);
    }

    // Student -> Teacher ekran thumbnail uzatish
    public async Task SendScreenThumbnail(string deviceId, string base64Jpeg)
    {
        await Clients.Group("Teachers").SendAsync("ReceiveScreenThumbnail", deviceId, base64Jpeg);
    }

    // Teacher -> Student masofaviy boshqaruv (mouse/keyboard input)
    public async Task SendRemoteInput(string targetDeviceId, string inputJson)
    {
        if (DeviceToConnection.TryGetValue(targetDeviceId, out var connId))
        {
            await Clients.Client(connId).SendAsync("ReceiveRemoteInput", inputJson);
        }
    }

    // Student Heartbeat
    public async Task Heartbeat(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return;
        var device = await db.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.IsOnline = true;
            device.LastSeenUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
