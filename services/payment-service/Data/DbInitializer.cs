namespace PaymentService.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(PaymentDbContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Payment DB initialization error: {ex.Message}");
            throw;
        }
    }
}
