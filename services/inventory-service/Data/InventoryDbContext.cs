using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryReservationItem> InventoryReservationItems => Set<InventoryReservationItem>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.ProductId)
            .IsUnique();

        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.QuantityOnHand)
            .HasDefaultValue(0);

        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.QuantityReserved)
            .HasDefaultValue(0);

        modelBuilder.Entity<InventoryReservation>()
            .HasMany(r => r.Items)
            .WithOne(i => i.Reservation!)
            .HasForeignKey(i => i.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventoryReservation>()
            .HasIndex(r => r.OrderId);

        modelBuilder.Entity<InventoryReservation>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<InventoryReservationItem>()
            .Property(i => i.Quantity)
            .IsRequired();

        modelBuilder.Entity<InventoryAdjustment>()
            .HasIndex(a => new { a.ProductId, a.CreatedAt });
    }
}
