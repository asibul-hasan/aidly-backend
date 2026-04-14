using Microsoft.EntityFrameworkCore;
using Aidly.src.Modules.Core.Domain.Common.User;
using Aidly.src.Modules.Core.Domain.Common.Company;

namespace Aidly.src.Modules.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User configuration
        modelBuilder.Entity<User>().HasKey(u => u.UserNo);
        modelBuilder.Entity<User>().Property(u => u.UserNo).UseIdentityByDefaultColumn();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.UserName).IsUnique();

        // Company configuration
        modelBuilder.Entity<Company>().HasKey(c => c.CompanyNo);
        modelBuilder.Entity<Company>().Property(c => c.CompanyNo).UseIdentityByDefaultColumn();
        modelBuilder.Entity<Company>().HasIndex(c => c.CompanyId).IsUnique();
    }
}
