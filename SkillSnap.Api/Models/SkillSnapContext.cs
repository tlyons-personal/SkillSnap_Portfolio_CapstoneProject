using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Shared.Models;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Models
{
    public class SkillSnapContext : IdentityDbContext<ApplicationUser>
    {
        public SkillSnapContext(DbContextOptions<SkillSnapContext> options) : base(options) {}
        public DbSet<PortfolioUser> PortfolioUsers { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Skill> Skills { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Store SkillLevel as its string name ("Advanced") instead of an integer
            modelBuilder.Entity<Skill>()
                .Property(s => s.Level)
                .HasConversion<string>();

            // Creating an index on the Name property of the PortfolioUser entity
            modelBuilder.Entity<PortfolioUser>()
                .HasIndex(pu => pu.UserName)
                .IsUnique();

            // Deleting a PortfolioUser cascades to its Projects
            modelBuilder.Entity<PortfolioUser>()
                .HasMany(pu => pu.Projects)
                .WithOne(p => p.PortfolioUser)
                .HasForeignKey(p => p.PortfolioUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a PortfolioUser cascades to its Skills
            modelBuilder.Entity<PortfolioUser>()
                .HasMany(pu => pu.Skills)
                .WithOne(s => s.PortfolioUser)
                .HasForeignKey(s => s.PortfolioUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
