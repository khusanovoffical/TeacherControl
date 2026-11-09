using Microsoft.EntityFrameworkCore;
using SchoolControl.Server.Models;

namespace SchoolControl.Server.Data;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<StudentSession> StudentSessions => Set<StudentSession>();
    public DbSet<DevicePolicy> DevicePolicies => Set<DevicePolicy>();
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();
    public DbSet<TeacherAccount> TeacherAccounts => Set<TeacherAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var student = modelBuilder.Entity<StudentProfile>();
        student.HasKey(x => x.Id);
        student.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
        student.Property(x => x.LastName).HasMaxLength(60).IsRequired();
        student.Property(x => x.DeviceId).HasMaxLength(80).HasDefaultValue("").IsRequired();
        student.Property(x => x.DeviceName).HasMaxLength(120).HasDefaultValue("").IsRequired();
        student.HasIndex(x => x.CreatedAtUtc);
        student.HasIndex(x => x.DeviceId);

        var device = modelBuilder.Entity<Device>();
        device.HasKey(x => x.DeviceId);
        device.Property(x => x.DeviceId).HasMaxLength(80).IsRequired();
        device.Property(x => x.DeviceName).HasMaxLength(120).IsRequired();
        device.Property(x => x.IpAddress).HasMaxLength(60);
        device.Property(x => x.CurrentStudentName).HasMaxLength(120);

        var session = modelBuilder.Entity<StudentSession>();
        session.HasKey(x => x.Id);
        session.Property(x => x.DeviceId).HasMaxLength(80).IsRequired();
        session.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
        session.Property(x => x.LastName).HasMaxLength(60).IsRequired();

        var policy = modelBuilder.Entity<DevicePolicy>();
        policy.HasKey(x => x.Id);

        var fileRecord = modelBuilder.Entity<FileRecord>();
        fileRecord.HasKey(x => x.Id);
        fileRecord.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        fileRecord.Property(x => x.StorageFileName).HasMaxLength(255).IsRequired();
        fileRecord.Property(x => x.TargetDeviceId).HasMaxLength(80).IsRequired();

        var teacher = modelBuilder.Entity<TeacherAccount>();
        teacher.HasKey(x => x.Id);
        teacher.Property(x => x.Username).HasMaxLength(60).IsRequired();
    }
}
