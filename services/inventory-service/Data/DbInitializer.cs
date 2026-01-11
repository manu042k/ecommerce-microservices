using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(InventoryDbContext context)
    {
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Inventory DB initialization error: {ex.Message}");
            throw;
        }
    }
}
