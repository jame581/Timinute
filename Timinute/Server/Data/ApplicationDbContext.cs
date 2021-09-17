using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Timinute.Server.Models;

namespace Timinute.Server.Data
{
    public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
    {
        public DbSet<Company> Companies { get; set; }

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
                .HasForeignKey(p => p.CompanyId);

            builder.Entity<TrackedTask>()
                .HasOne(p => p.Project)
                .WithMany(t => t.TrackedTasks)
                .HasForeignKey(t => t.ProjectId);

            builder.Entity<TrackedTask>()
                .HasOne(t => t.User)
                .WithMany(u => u.TrackedTasks)
                .HasForeignKey(t => t.UserId);
        }
    }
}