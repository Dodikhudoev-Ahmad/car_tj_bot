using Microsoft.EntityFrameworkCore;
using TelegramCarsBot.Models;

namespace TelegramCarsBot.Data;

public class AppDbContext : DbContext
{
    public DbSet<Car> Cars => Set<Car>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.VinCode).IsRequired().HasMaxLength(20);
            e.Property(c => c.Description).HasMaxLength(2000);
        });
    }
}
