using Microsoft.EntityFrameworkCore;
using SchoolControl.Server.Models;

namespace SchoolControl.Server.Data;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<StudentProfile> Students => Set<StudentProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var student = modelBuilder.Entity<StudentProfile>();
        student.HasKey(x => x.Id);
        student.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
        student.Property(x => x.LastName).HasMaxLength(60).IsRequired();
        student.HasIndex(x => x.CreatedAtUtc);
    }
}