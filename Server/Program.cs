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
    version = "0.1.0",
    status = "running",
    docs = app.Environment.IsDevelopment() ? "/swagger" : null
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timeUtc = DateTimeOffset.UtcNow }));

app.MapPost("/api/students/register", async Task<IResult> (
    StudentRegistrationRequest request,
    SchoolDbContext db,
    CancellationToken cancellationToken) =>
{
    var firstName = request.FirstName.Trim();
    var lastName = request.LastName.Trim();

    if (firstName.Length is < 1 or > 60 || lastName.Length is < 1 or > 60)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["Ism va familiya 1–60 belgi oralig‘ida bo‘lishi kerak."]
        });

    var student = new StudentProfile
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    db.Students.Add(student);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/students/{student.Id}", new
    {
        student.Id,
        student.FirstName,
        student.LastName,
        student.CreatedAtUtc
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
        .OrderByDescending(s => s.CreatedAtUtc)
        .Select(s => new { s.Id, s.FirstName, s.LastName, s.CreatedAtUtc })
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
        onlineStudents = (int?)null,
        todayCheckIns = (int?)null,
        note = "Onlayn holati va davomat modullari keyingi bosqichda ulanadi."
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

public sealed record StudentRegistrationRequest(string FirstName, string LastName);