using Microsoft.EntityFrameworkCore;
using Aidly.src.Modules.Core.Domain.Common;

namespace Aidly.src.Modules.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Explicitly ensuring UserNo is the Primary Key and correctly mapped as Identity for Postgres
        modelBuilder.Entity<User>().HasKey(u => u.UserNo);
        modelBuilder.Entity<User>().Property(u => u.UserNo).UseIdentityByDefaultColumn();

        // Additional configuration
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.UserName).IsUnique();
    }
}
