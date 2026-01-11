using OrderService.Data;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Data;

public class DbInitializer
{
    public static async Task InitializeAsync(OrderDbContext context)
    {
        try
        {
            // In production, use Migrate()
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Database initialization error: {ex.Message}");
            throw;
        }
    }
}
