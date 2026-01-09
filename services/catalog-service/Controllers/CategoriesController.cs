using CatalogService.Data;
using CatalogService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly CatalogDbContext _context;
    private readonly IDistributedCache _cache;
    private const string CacheKeyPrefix = "categories_";

    public CategoriesController(CatalogDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
    {
        string cacheKey = $"{CacheKeyPrefix}all";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<Category>>(cachedData)!;
        }

        var categories = await _context.Categories.ToListAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(categories), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Aggressive caching
        });

        return categories;
    }

    [HttpPost]
    public async Task<ActionResult<Category>> CreateCategory(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync($"{CacheKeyPrefix}all"); // Invalidate cache

        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, category);
    }
}
