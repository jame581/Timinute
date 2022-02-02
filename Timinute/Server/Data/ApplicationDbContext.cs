using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Timinute.Server.Helpers;
using Timinute.Server.Models;

namespace Timinute.Server.Data
{
    public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
    {
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<ApplicationRole> ApplicationRoles { get; set; }
        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<TrackedTask> TrackedTasks { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;

        public ApplicationDbContext(
            DbContextOptions options,
            IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Setup entities

            builder.Entity<Company>()
                .Property(x => x.CompanyId)
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Entity<Company>()
                .Property(x => x.Name)
                .IsRequired();

            builder.Entity<Company>().HasKey(t => t.CompanyId);

            builder.Entity<Project>()
                .Property(x => x.ProjectId)
                .ValueGeneratedOnAdd()
                .IsRequired();

            builder.Entity<Project>().HasKey(t => t.ProjectId);

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

            // Setup relationship

            builder.Entity<Project>()
                .HasOne(c => c.Company)
                .WithMany(p => p.Projects)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<TrackedTask>()
                .HasOne(p => p.Project)
                .WithMany(t => t.TrackedTasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<TrackedTask>()
                .HasOne(t => t.User)
                .WithMany(u => u.TrackedTasks)
                .HasForeignKey(t => t.UserId);

            FillDataToDB(builder);
        }

        private static void FillDataToDB(ModelBuilder builder)
        {
            var roles = new List<ApplicationRole>
            {
                new ApplicationRole{Name = Constants.Roles.Basic, NormalizedName =  Constants.Roles.Basic.ToUpper(), Description = "Basic role with lowest rights."},
                new ApplicationRole{Name = Constants.Roles.Admin, NormalizedName =  Constants.Roles.Admin.ToUpper(), Description = "Admin role with highest rights."}
            };

            var applicationUsers = new List<ApplicationUser>()
            {
                new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "test1@email.com", FirstName = "Jan", LastName = "Testovic", EmailConfirmed = true, UserName = "test1@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg=="},
                new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "test2@email.com", FirstName = "Ivana", LastName = "Maricenkova", EmailConfirmed = true, UserName = "test2@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg=="},
                new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "test3@email.com", FirstName = "Marek", LastName = "Klukac", EmailConfirmed = true, UserName = "test3@email.com", PasswordHash = "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg=="},
            };

            builder.Entity<ApplicationRole>().HasData(roles);
            builder.Entity<ApplicationUser>().HasData(applicationUsers);

            var trackedTasks = new List<TrackedTask>
            {
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project A", Duration = TimeSpan.FromHours(2), StartDate = new DateTime(2022, 1, 1, 9, 0, 0), EndDate = new DateTime(2022, 1, 1, 11, 0, 0), UserId = applicationUsers[0].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project B", Duration = TimeSpan.FromHours(3), StartDate = new DateTime(2022, 2, 2, 10, 0, 0), EndDate = new DateTime(2022, 2, 2, 13, 0, 0), UserId = applicationUsers[0].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project C", Duration = TimeSpan.FromHours(4), StartDate = new DateTime(2022, 1, 1, 11, 0, 0), EndDate = new DateTime(2022, 1, 1, 15, 0, 0), UserId = applicationUsers[0].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project D", Duration = TimeSpan.FromHours(5), StartDate = new DateTime(2022, 2, 2, 12, 0, 0), EndDate = new DateTime(2022, 2, 2, 17, 0, 0), UserId = applicationUsers[1].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project E", Duration = TimeSpan.FromHours(6), StartDate = new DateTime(2022, 1, 1, 13, 0, 0), EndDate = new DateTime(2022, 1, 1, 19, 0, 0), UserId = applicationUsers[1].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project F", Duration = TimeSpan.FromHours(7), StartDate = new DateTime(2022, 2, 2, 14, 0, 0), EndDate = new DateTime(2022, 2, 2, 21, 0, 0), UserId = applicationUsers[2].Id },
                new TrackedTask { TaskId = Guid.NewGuid().ToString(), Name = "Project G", Duration = TimeSpan.FromHours(7), StartDate = new DateTime(2022, 2, 2, 14, 0, 0), EndDate = new DateTime(2022, 2, 2, 21, 0, 0), UserId = applicationUsers[2].Id },
            };

            builder.Entity<TrackedTask>().HasData(trackedTasks);
        }
    }
}