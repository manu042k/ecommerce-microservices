using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.OrderId);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.ProviderPaymentId)
            .IsUnique(false);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Currency)
            .HasConversion(s => s.ToLowerInvariant(), s => s.ToLowerInvariant());

        modelBuilder.Entity<Payment>()
            .HasMany(p => p.Refunds)
            .WithOne(r => r.Payment)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Refund>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<WebhookEvent>()
            .HasIndex(w => new { w.Provider, w.EventId })
            .IsUnique();
    }
}
