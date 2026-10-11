using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core context for RaceDayDB. The fluent configuration below mirrors the
/// Part 1 SQL script (Database Schema Creation (SQL Script).sql) so the
/// Code-First migration produces the same tables, keys, constraints and defaults.
/// </summary>
public class RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "Role IN ('Organiser', 'Participant')"));
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).UseIdentityColumn(1, 1);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Organiser>(entity =>
        {
            entity.ToTable("Organisers");
            entity.HasKey(e => e.OrganiserId);
            entity.Property(e => e.OrganiserId).UseIdentityColumn(1, 1);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.OrganisationName).HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
            entity.HasOne(e => e.User)
                .WithOne(u => u.Organiser)
                .HasForeignKey<Organiser>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.ToTable("Participants");
            entity.HasKey(e => e.ParticipantId);
            entity.Property(e => e.ParticipantId).UseIdentityColumn(1, 1);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.DateOfBirth).HasColumnType("date");
            entity.Property(e => e.ProfilePictureUrl).HasColumnName("ProfilePictureURL").HasMaxLength(500);
            entity.HasOne(e => e.User)
                .WithOne(u => u.Participant)
                .HasForeignKey<Participant>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "EventType IN ('Run', 'Walk', 'Cycle')"));
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).UseIdentityColumn(1, 1);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EventDate).HasColumnType("datetime");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Distance).HasPrecision(5, 2);
            entity.Property(e => e.EventType).HasMaxLength(20);
            entity.Property(e => e.BannerImageUrl).HasColumnName("BannerImageURL").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.HasOne(e => e.Organiser)
                .WithMany(o => o.Events)
                .HasForeignKey(e => e.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryId).UseIdentityColumn(1, 1);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.HasOne(e => e.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "Status IN ('Pending', 'Confirmed', 'Cancelled')"));
            entity.HasKey(e => e.EnrolmentId);
            entity.Property(e => e.EnrolmentId).UseIdentityColumn(1, 1);
            entity.HasIndex(e => new { e.ParticipantId, e.EventId }).IsUnique().HasDatabaseName("UQ_Enrolment");
            entity.Property(e => e.RegistrationDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(EnrolmentStatuses.Pending);
            entity.HasOne(e => e.Participant)
                .WithMany(p => p.Enrolments)
                .HasForeignKey(e => e.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event)
                .WithMany(ev => ev.Enrolments)
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "Status IN ('Published', 'Pending', 'Disqualified')"));
            entity.HasKey(e => e.ResultId);
            entity.Property(e => e.ResultId).UseIdentityColumn(1, 1);
            entity.HasIndex(e => e.EnrolmentId).IsUnique();
            entity.Property(e => e.FinishTime).HasColumnType("time");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(ResultStatuses.Published);
            entity.HasOne(e => e.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(e => e.EnrolmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
