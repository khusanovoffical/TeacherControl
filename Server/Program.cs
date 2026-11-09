using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolControl.Server.Data;
using SchoolControl.Server.Hubs;
using SchoolControl.Server.Models;
using SchoolControl.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Fayllar uchun maksimal hajm (masalan, 200MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

builder.Services.AddDbContext<SchoolDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SchoolControl")
        ?? "Data Source=schoolcontrol.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB (base64 screen thumbnail uchun)
    hubOptions.EnableDetailedErrors = true;
});

// UDP Discovery Service (Local network avtomatik topish uchun)
builder.Services.AddHostedService<DiscoveryService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000", "http://localhost:5123"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("SchoolApps", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("student-registration", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

// Ma'lumotlar bazasini initsializatsiya qilish
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Default policy mavjudligini tekshirish
    if (!await db.DevicePolicies.AnyAsync())
    {
        db.DevicePolicies.Add(new DevicePolicy
        {
            Id = 1,
            BlockedProcessesJson = "[\"telegram.exe\", \"discord.exe\", \"steam.exe\"]",
            InternetRestricted = false,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

app.UseCors("SchoolApps");
app.UseRateLimiter();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// SignalR Hub
app.MapHub<SchoolHub>("/hubs/school");

// Root & Health
app.MapGet("/", () => Results.Ok(new
{
    name = "SchoolControl Central API & SignalR",
    version = "1.0.0",
    status = "running",
    serverTimeUtc = DateTimeOffset.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timeUtc = DateTimeOffset.UtcNow }));

// --- TALABA RO'YXATDAN O'TISHI (Mavjud API to'liq saqlanadi) ---
app.MapPost("/api/students/register", async Task<IResult> (
    StudentRegistrationRequest request,
    SchoolDbContext db,
    CancellationToken cancellationToken) =>
{
    var firstName = request.FirstName?.Trim() ?? "";
    var lastName = request.LastName?.Trim() ?? "";
    var deviceId = request.DeviceId?.Trim() ?? "";
    var deviceName = request.DeviceName?.Trim() ?? "";

    if (firstName.Length is < 1 or > 60 || lastName.Length is < 1 or > 60)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["Ism va familiya 1–60 belgi oralig‘ida bo‘lishi kerak."]
        });

    if (deviceId.Length > 80 || deviceName.Length > 120)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["device"] = ["Qurilma identifikatori yoki nomi juda uzun."]
        });

    if (!Guid.TryParseExact(deviceId, "N", out _) && !Guid.TryParse(deviceId, out _))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["deviceId"] = ["Qurilma identifikatori noto‘g‘ri."]
        });

    if (deviceName.Length < 1)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["deviceName"] = ["Kompyuter nomi yuborilishi kerak."]
        });

    var now = DateTimeOffset.UtcNow;
    var student = await db.Students.FirstOrDefaultAsync(s => s.DeviceId == deviceId, cancellationToken);
    if (student is null)
    {
        student = new StudentProfile { Id = Guid.NewGuid(), CreatedAtUtc = now };
        db.Students.Add(student);
    }

    student.FirstName = firstName;
    student.LastName = lastName;
    student.DeviceId = deviceId;
    student.DeviceName = deviceName;
    student.LastSeenUtc = now;

    // Devices jadvalida ham saqlash
    var device = await db.Devices.FindAsync([deviceId], cancellationToken);
    if (device == null)
    {
        device = new Device
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            CurrentStudentName = $"{firstName} {lastName}".Trim(),
            FirstSeenUtc = now,
            LastSeenUtc = now,
            IsOnline = true
        };
        db.Devices.Add(device);
    }
    else
    {
        device.DeviceName = deviceName;
        device.CurrentStudentName = $"{firstName} {lastName}".Trim();
        device.LastSeenUtc = now;
        device.IsOnline = true;
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        student.Id,
        student.FirstName,
        student.LastName,
        student.DeviceName,
        student.CreatedAtUtc,
        student.LastSeenUtc
    });
}).RequireRateLimiting("student-registration");

// --- O'QITUVCHI DASHBOARD VA QURILMALAR ---
app.MapGet("/api/teacher/dashboard", async (SchoolDbContext db, HttpContext context, CancellationToken ct) =>
{
    if (!HasTeacherKey(context, app.Configuration)) return Results.Unauthorized();

    var devices = await db.Devices.AsNoTracking().ToListAsync(ct);
    var now = DateTimeOffset.UtcNow;
    var onlineDevices = devices.Count(d => d.IsOnline && (now - d.LastSeenUtc).TotalSeconds < 60);

    return Results.Ok(new
    {
        totalDevices = devices.Count,
        onlineDevices,
        offlineDevices = devices.Count - onlineDevices,
        lockedDevices = devices.Count(d => d.IsLocked),
        activeScreenSessions = devices.Count(d => d.IsScreenSharing),
        serverStatus = "Online",
        serverTimeUtc = now
    });
});

app.MapGet("/api/teacher/devices", async (SchoolDbContext db, HttpContext context, CancellationToken ct) =>
{
    if (!HasTeacherKey(context, app.Configuration)) return Results.Unauthorized();

    var devices = await db.Devices.AsNoTracking().OrderBy(d => d.DeviceName).ToListAsync(ct);
    return Results.Ok(devices);
});

app.MapGet("/api/teacher/students", async (SchoolDbContext db, HttpContext context, CancellationToken ct) =>
{
    if (!HasTeacherKey(context, app.Configuration)) return Results.Unauthorized();

    var students = await db.Students.AsNoTracking()
        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
        .ToListAsync(ct);
    return Results.Ok(students);
});

// --- BUYRUQLAR (LOCK, UNLOCK, RESTART, SHUTDOWN) ---
app.MapPost("/api/teacher/command", async (
    DeviceCommandRequest req,
    IHubContext<SchoolHub> hub,
    SchoolDbContext db,
    HttpContext context) =>
{
    if (!HasTeacherKey(context, app.Configuration)) return Results.Unauthorized();

    var target = req.TargetDeviceId?.Trim() ?? "ALL";
    var cmd = req.Command?.Trim() ?? "";
    var payload = req.Payload?.Trim() ?? "";

    if (cmd == "Lock" || cmd == "Unlock")
    {
        bool isLocked = cmd == "Lock";
        if (target == "ALL")
        {
            var all = await db.Devices.ToListAsync();
            foreach (var d in all) d.IsLocked = isLocked;
        }
        else
        {
            var d = await db.Devices.FindAsync(target);
            if (d != null) d.IsLocked = isLocked;
        }
        await db.SaveChangesAsync();
    }

    if (target == "ALL")
    {
        await hub.Clients.Group("Students").SendAsync("ReceiveCommand", cmd, payload);
    }
    else
    {
        await hub.Clients.Group("Students").SendAsync("ReceiveCommandTargeted", target, cmd, payload);
    }

    return Results.Ok(new { success = true, command = cmd, target });
});

// --- FAYLLAR BILAN ISHLASH ---
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsDir);

app.MapPost("/api/files/upload", async (
    IFormFile file,
    string? targetDeviceId,
    string? senderType,
    string? senderName,
    SchoolDbContext db,
    IHubContext<SchoolHub> hub) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "Fayl bo‘sh" });

    // Maksimal 100MB
    if (file.Length > 100 * 1024 * 1024)
        return Results.BadRequest(new { error = "Fayl hajmi 100MB dan oshmasligi kerak" });

    var fileId = Guid.NewGuid();
    var ext = Path.GetExtension(file.FileName);
    var storageName = $"{fileId:N}{ext}";
    var filePath = Path.Combine(uploadsDir, storageName);

    await using (var stream = File.Create(filePath))
    {
        await file.CopyToAsync(stream);
    }

    var record = new FileRecord
    {
        Id = fileId,
        OriginalFileName = Path.GetFileName(file.FileName),
        StorageFileName = storageName,
        FileSizeBytes = file.Length,
        SenderType = senderType ?? "Teacher",
        SenderName = senderName ?? "O‘qituvchi",
        TargetDeviceId = targetDeviceId ?? "ALL",
        UploadedAtUtc = DateTimeOffset.UtcNow
    };

    db.FileRecords.Add(record);
    await db.SaveChangesAsync();

    // SignalR orqali xabar
    if (record.SenderType == "Teacher")
    {
        await hub.Clients.Group("Students").SendAsync("FileAvailable", new
        {
            record.Id,
            record.OriginalFileName,
            record.FileSizeBytes,
            record.SenderName,
            record.TargetDeviceId
        });
    }
    else
    {
        await hub.Clients.Group("Teachers").SendAsync("StudentFileUploaded", new
        {
            record.Id,
            record.OriginalFileName,
            record.FileSizeBytes,
            record.SenderName,
            record.TargetDeviceId
        });
    }

    return Results.Ok(record);
}).DisableAntiforgery();

app.MapGet("/api/files", async (SchoolDbContext db) =>
{
    var files = await db.FileRecords.AsNoTracking()
        .OrderByDescending(f => f.UploadedAtUtc)
        .Take(50)
        .ToListAsync();
    return Results.Ok(files);
});

app.MapGet("/api/files/download/{id:guid}", async (Guid id, SchoolDbContext db) =>
{
    var record = await db.FileRecords.FindAsync(id);
    if (record == null) return Results.NotFound();

    var filePath = Path.Combine(uploadsDir, record.StorageFileName);
    if (!File.Exists(filePath)) return Results.NotFound("Fayl diskda topilmadi");

    return Results.File(filePath, "application/octet-stream", record.OriginalFileName);
});

// --- ILOVALAR SIYOSATI (POLICY) ---
app.MapGet("/api/policies", async (SchoolDbContext db) =>
{
    var policy = await db.DevicePolicies.FirstOrDefaultAsync(p => p.Id == 1);
    return Results.Ok(policy ?? new DevicePolicy());
});

app.MapPost("/api/policies", async (
    DevicePolicy policy,
    SchoolDbContext db,
    IHubContext<SchoolHub> hub,
    HttpContext context) =>
{
    if (!HasTeacherKey(context, app.Configuration)) return Results.Unauthorized();

    var current = await db.DevicePolicies.FirstOrDefaultAsync(p => p.Id == 1);
    if (current == null)
    {
        policy.Id = 1;
        policy.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.DevicePolicies.Add(policy);
    }
    else
    {
        current.BlockedProcessesJson = policy.BlockedProcessesJson;
        current.InternetRestricted = policy.InternetRestricted;
        current.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    await db.SaveChangesAsync();

    // Talabalarga SignalR orqali yangi siyosatni yuborish
    await hub.Clients.Group("Students").SendAsync("PolicyUpdated", policy.BlockedProcessesJson);

    return Results.Ok(new { success = true, policy });
});

app.Run();

static bool HasTeacherKey(HttpContext context, IConfiguration configuration)
{
    var expected = configuration["SCHOOLCONTROL_TEACHER_API_KEY"] ?? "school_teacher_secret_2026";
    if (!context.Request.Headers.TryGetValue("X-Teacher-Key", out var supplied))
        return false;

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(supplied.ToString()));
}

public sealed record StudentRegistrationRequest(
    string FirstName,
    string LastName,
    string? DeviceId = null,
    string? DeviceName = null);

public sealed record DeviceCommandRequest(
    string TargetDeviceId,
    string Command,
    string? Payload = null);
