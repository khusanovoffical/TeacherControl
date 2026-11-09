using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolControl.Server.Data;
using SchoolControl.Server.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SchoolDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SchoolControl")
        ?? "Data Source=schoolcontrol.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("SchoolApps", policy =>
        policy.WithOrigins(allowedOrigins)
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
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
    await db.Database.EnsureCreatedAsync();

    // EnsureCreated does not alter tables created by an earlier app version.
    await EnsureColumnAsync(db, "DeviceId", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync(db, "DeviceName", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync(db, "LastSeenUtc", "TEXT NULL");
}

app.UseCors("SchoolApps");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "SchoolControl API",
    version = "0.2.0",
    status = "running",
    docs = app.Environment.IsDevelopment() ? "/swagger" : null
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timeUtc = DateTimeOffset.UtcNow }));

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

    // A device registration must always carry its persistent installation ID.
    // Rejecting an empty/invalid ID prevents accidental duplicate student rows.
    if (!Guid.TryParseExact(deviceId, "N", out _))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["deviceId"] = ["Qurilma identifikatori noto‘g‘ri. Student dasturini qayta sozlang."]
        });

    if (deviceName.Length is < 1)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["deviceName"] = ["Kompyuter nomi yuborilishi kerak."]
        });

    var now = DateTimeOffset.UtcNow;
    StudentProfile? student = await db.Students
        .FirstOrDefaultAsync(s => s.DeviceId == deviceId, cancellationToken);

    if (student is null)
    {
        student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now
        };
        db.Students.Add(student);
    }

    student.FirstName = firstName;
    student.LastName = lastName;
    student.DeviceId = deviceId;
    student.DeviceName = deviceName;
    student.LastSeenUtc = now;

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
})
.RequireRateLimiting("student-registration");

app.MapGet("/api/teacher/students", async Task<IResult> (
    HttpContext context,
    SchoolDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!HasTeacherKey(context, app.Configuration))
        return Results.Unauthorized();

    var students = await db.Students
        .AsNoTracking()
        .OrderBy(s => s.LastName)
        .ThenBy(s => s.FirstName)
        .Select(s => new
        {
            s.Id,
            s.FirstName,
            s.LastName,
            s.DeviceName,
            s.CreatedAtUtc,
            s.LastSeenUtc
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(students);
});

app.MapGet("/api/teacher/dashboard", async Task<IResult> (
    HttpContext context,
    SchoolDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!HasTeacherKey(context, app.Configuration))
        return Results.Unauthorized();

    var total = await db.Students.CountAsync(cancellationToken);
    return Results.Ok(new
    {
        totalStudents = total,
        registeredDevices = await db.Students.CountAsync(s => s.DeviceId != "", cancellationToken),
        note = "Faqat ro‘yxat va qurilma identifikatori ko‘rsatiladi; davomat kuzatilmaydi."
    });
});

app.Run();

static bool HasTeacherKey(HttpContext context, IConfiguration configuration)
{
    var expected = configuration["SCHOOLCONTROL_TEACHER_API_KEY"];
    if (string.IsNullOrWhiteSpace(expected))
        return false;

    if (!context.Request.Headers.TryGetValue("X-Teacher-Key", out var supplied))
        return false;

    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(expected),
        System.Text.Encoding.UTF8.GetBytes(supplied.ToString()));
}

static async Task EnsureColumnAsync(SchoolDbContext db, string columnName, string columnDefinition)
{
    var connection = db.Database.GetDbConnection();
    var wasClosed = connection.State != System.Data.ConnectionState.Open;
    if (wasClosed)
        await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Students')";
        await using var reader = await command.ExecuteReaderAsync();
        var found = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        await reader.DisposeAsync();
        if (!found)
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE Students ADD COLUMN {columnName} {columnDefinition}");
    }
    finally
    {
        if (wasClosed)
            await connection.CloseAsync();
    }
}

public sealed record StudentRegistrationRequest(
    string FirstName,
    string LastName,
    string? DeviceId = null,
    string? DeviceName = null);
