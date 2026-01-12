namespace FulfillmentService.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(FulfillmentDbContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fulfillment DB initialization error: {ex.Message}");
            throw;
        }
    }
}
