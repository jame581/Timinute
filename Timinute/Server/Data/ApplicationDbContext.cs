using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Helpers;
using Timinute.Server.Models;

namespace Timinute.Server.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        // Seeded user IDs — referenced by both ApplicationUser.HasData and the
        // OwnsOne(Preferences).HasData below so the owned entity rows have the
        // correct FK values for the seed users.
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";
        private const string SeedUserId2 = "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e";
        private const string SeedUserId3 = "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f";

        public DbSet<TrackedTask> TrackedTasks { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;

        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Setup entities

            builder.Entity<ApplicationUser>().OwnsOne(u => u.Preferences, p =>
            {
                p.Property(x => x.Theme)
                    .HasConversion<string>()
                    .HasMaxLength(8)
                    .HasDefaultValue(ThemePreference.System)
                    .IsRequired();

                p.Property(x => x.WeeklyGoalHours).HasDefaultValue(32.0m);
                p.Property(x => x.WorkdayHoursPerDay).HasDefaultValue(8.0m);

                // Seed default preferences for the three test users so the
                // owned-entity rows have a row-per-user once the migration
                // applies. Production users get their values from the column
                // DEFAULTs declared above.
                p.HasData(
                    new { ApplicationUserId = SeedUserId1, Theme = ThemePreference.System, WeeklyGoalHours = 32.0m, WorkdayHoursPerDay = 8.0m },
                    new { ApplicationUserId = SeedUserId2, Theme = ThemePreference.System, WeeklyGoalHours = 32.0m, WorkdayHoursPerDay = 8.0m },
                    new { ApplicationUserId = SeedUserId3, Theme = ThemePreference.System, WeeklyGoalHours = 32.0m, WorkdayHoursPerDay = 8.0m }
                );
            });

            builder.Entity<Project>()
                .Property(x => x.ProjectId)
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Entity<Project>().HasKey(t => t.ProjectId);

            builder.Entity<Project>()
                .HasQueryFilter(p => p.DeletedAt == null);

            builder.Entity<Project>()
                .HasIndex(p => p.DeletedAt);

            builder.Entity<TrackedTask>()
                .Property(x => x.TaskId)
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Entity<TrackedTask>()
                .Property(x => x.Name)
                .IsRequired();

            builder.Entity<TrackedTask>()
                .Property(x => x.StartDate)
                .IsRequired();

            builder.Entity<TrackedTask>()
                .Property(x => x.Duration)
                .IsRequired();

            builder.Entity<TrackedTask>().HasKey(t => t.TaskId);

            builder.Entity<TrackedTask>()
                .HasQueryFilter(t => t.DeletedAt == null);

            builder.Entity<TrackedTask>()
                .HasIndex(t => t.DeletedAt);

            // Setup relationship

            builder.Entity<TrackedTask>()
                .HasOne(p => p.Project)
                .WithMany(t => t.TrackedTasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<TrackedTask>()
                .HasOne(t => t.User)
                .WithMany(u => u.TrackedTasks)
                .HasForeignKey(t => t.UserId);

            builder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            FillDataToDB(builder);
        }

        private static void FillDataToDB(ModelBuilder builder)
        {
            const string roleBasicId = "b0a2e199-0a21-4158-8586-b1c2e2a1d64c";
            const string roleAdminId = "f3c1a2d7-4e5b-4f8a-9c6d-1a2b3c4d5e6f";

            // Use the class-level seed user IDs so the OwnsOne(Preferences)
            // seed and the ApplicationUser seed reference the same values.
            const string userId1 = SeedUserId1;
            const string userId2 = SeedUserId2;
            const string userId3 = SeedUserId3;

            var roles = new List<ApplicationRole>
            {
                new ApplicationRole{ Id = roleBasicId, ConcurrencyStamp = "e0c194a8-0001-0001-0001-000000000001", Name = Constants.Roles.Basic, NormalizedName = Constants.Roles.Basic.ToUpper(), Description = "Basic role with lowest rights."},
                new ApplicationRole{ Id = roleAdminId, ConcurrencyStamp = "e0c194a8-0001-0001-0001-000000000002", Name = Constants.Roles.Admin, NormalizedName = Constants.Roles.Admin.ToUpper(), Description = "Admin role with highest rights."}
            };

            var seedCreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Preferences navigation is null! on the seed instances — EF rejects
            // seed parents that have an owned navigation set; the owned-entity
            // rows are seeded separately via OwnsOne(...).HasData above.
            var applicationUsers = new List<ApplicationUser>()
            {
                new ApplicationUser { Id = userId1, ConcurrencyStamp = "c0c194a8-0001-0001-0001-000000000001", SecurityStamp = "s0c194a8-0001-0001-0001-000000000001", Email = "test1@email.com", FirstName = "Jan", LastName = "Testovic", EmailConfirmed = true, UserName = "test1@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", CreatedAt = seedCreatedAt, Preferences = null! },
                new ApplicationUser { Id = userId2, ConcurrencyStamp = "c0c194a8-0001-0001-0001-000000000002", SecurityStamp = "s0c194a8-0001-0001-0001-000000000002", Email = "test2@email.com", FirstName = "Ivana", LastName = "Maricenkova", EmailConfirmed = true, UserName = "test2@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", CreatedAt = seedCreatedAt, Preferences = null! },
                new ApplicationUser { Id = userId3, ConcurrencyStamp = "c0c194a8-0001-0001-0001-000000000003", SecurityStamp = "s0c194a8-0001-0001-0001-000000000003", Email = "test3@email.com", FirstName = "Marek", LastName = "Klukac", EmailConfirmed = true, UserName = "test3@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", CreatedAt = seedCreatedAt, Preferences = null! },
            };

            builder.Entity<ApplicationRole>().HasData(roles);
            builder.Entity<ApplicationUser>().HasData(applicationUsers);

            var trackedTasks = new List<TrackedTask>
            {
                new TrackedTask { TaskId = "d4e5f6a7-b8c9-4d5e-8f7a-2b3c4d5e6f7a", Name = "Project A", Duration = TimeSpan.FromHours(2), StartDate = new DateTimeOffset(2022, 1, 1, 9, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 1, 1, 11, 0, 0, TimeSpan.Zero), UserId = userId1 },
                new TrackedTask { TaskId = "e5f6a7b8-c9d0-4e5f-8a7b-3c4d5e6f7a8b", Name = "Project B", Duration = TimeSpan.FromHours(3), StartDate = new DateTimeOffset(2022, 2, 2, 10, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 2, 2, 13, 0, 0, TimeSpan.Zero), UserId = userId1 },
                new TrackedTask { TaskId = "f6a7b8c9-d0e1-4f5a-8b7c-4d5e6f7a8b9c", Name = "Project C", Duration = TimeSpan.FromHours(4), StartDate = new DateTimeOffset(2022, 1, 1, 11, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 1, 1, 15, 0, 0, TimeSpan.Zero), UserId = userId1 },
                new TrackedTask { TaskId = "a7b8c9d0-e1f2-4a5b-8c7d-5e6f7a8b9c0d", Name = "Project D", Duration = TimeSpan.FromHours(5), StartDate = new DateTimeOffset(2022, 2, 2, 12, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 2, 2, 17, 0, 0, TimeSpan.Zero), UserId = userId2 },
                new TrackedTask { TaskId = "b8c9d0e1-f2a3-4b5c-8d7e-6f7a8b9c0d1e", Name = "Project E", Duration = TimeSpan.FromHours(6), StartDate = new DateTimeOffset(2022, 1, 1, 13, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 1, 1, 19, 0, 0, TimeSpan.Zero), UserId = userId2 },
                new TrackedTask { TaskId = "c9d0e1f2-a3b4-4c5d-8e7f-7a8b9c0d1e2f", Name = "Project F", Duration = TimeSpan.FromHours(7), StartDate = new DateTimeOffset(2022, 2, 2, 14, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 2, 2, 21, 0, 0, TimeSpan.Zero), UserId = userId3 },
                new TrackedTask { TaskId = "d0e1f2a3-b4c5-4d5e-8f7a-8b9c0d1e2f3a", Name = "Project G", Duration = TimeSpan.FromHours(7), StartDate = new DateTimeOffset(2022, 2, 2, 14, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 2, 2, 21, 0, 0, TimeSpan.Zero), UserId = userId3 },
            };

            builder.Entity<TrackedTask>().HasData(trackedTasks);
        }
    }
}
