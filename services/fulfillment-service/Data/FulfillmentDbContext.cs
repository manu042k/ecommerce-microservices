using FulfillmentService.Models;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentService.Data;

public class FulfillmentDbContext : DbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options)
    {
    }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<ShipmentTimelineEntry> ShipmentTimelineEntries => Set<ShipmentTimelineEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Shipment>()
            .HasIndex(s => s.OrderId);

        modelBuilder.Entity<Shipment>()
            .HasIndex(s => s.Status);

        modelBuilder.Entity<Shipment>()
            .OwnsOne(s => s.Destination, navigationBuilder =>
            {
                navigationBuilder.Property(a => a.Country).HasMaxLength(64);
                navigationBuilder.Property(a => a.PostalCode).HasMaxLength(32);
            });

        modelBuilder.Entity<Shipment>()
            .HasMany(s => s.Items)
            .WithOne(i => i.Shipment)
            .HasForeignKey(i => i.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Shipment>()
            .HasMany(s => s.Timeline)
            .WithOne(t => t.Shipment)
            .HasForeignKey(t => t.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Shipment>()
            .Property(s => s.Carrier)
            .HasConversion(v => v == null ? null : v.ToLowerInvariant(), v => v ?? string.Empty);

        modelBuilder.Entity<ShipmentItem>()
            .HasIndex(i => i.ShipmentId);

        modelBuilder.Entity<ShipmentTimelineEntry>()
            .HasIndex(t => t.ShipmentId);
    }
}
