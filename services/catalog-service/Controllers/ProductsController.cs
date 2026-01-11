using CatalogService.Contracts;
using CatalogService.Data;
using CatalogService.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly CatalogDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IPublishEndpoint _publishEndpoint;
    private const string CacheKeyPrefix = "product_";

    public ProductsController(CatalogDbContext context, IDistributedCache cache, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _cache = cache;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // For search/filter, we might skip caching or use very specific keys.
        // For simplicity and "read-heavy" requirement, let's cache the "base" list if no filters are applied.

        bool hasFilters = !string.IsNullOrEmpty(searchTerm) || categoryId.HasValue || minPrice.HasValue || maxPrice.HasValue;

        if (!hasFilters)
        {
            string cacheKey = $"{CacheKeyPrefix}all_p{page}_s{pageSize}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<List<Product>>(cachedData)!;
            }
        }

        var query = _context.Products.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()) ||
                                     (p.Description != null && p.Description.ToLower().Contains(searchTerm.ToLower())));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= maxPrice);
        }

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (!hasFilters)
        {
            string cacheKey = $"{CacheKeyPrefix}all_p{page}_s{pageSize}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(products), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
        }

        return products;
    }

    [HttpGet("verify")]
    public async Task<ActionResult<List<dynamic>>> VerifyProducts([FromQuery] List<Guid> ids)
    {
        if (ids == null || !ids.Any())
        {
            return BadRequest("No product IDs provided");
        }

        var products = await _context.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Price })
            .ToListAsync();

        return products.Cast<dynamic>().ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        // Invalidate Cache
        await _cache.RemoveAsync($"{CacheKeyPrefix}{id}");

        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
        string cacheKey = $"{CacheKeyPrefix}{id}";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<Product>(cachedData)!;
        }

        var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        return product;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Invalidate "all" cache - simplistic approach
        // Real-world: Use tagging or more sophisticated invalidation
        // We won't iterate all pages to delete, but for a demo, we accept that new products might take 5 mins to appear in lists
        // Or we could execute a specific removal if we tracked keys.

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(Guid id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        _context.Entry(product).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();

            // Invalidate Cache
            await _cache.RemoveAsync($"{CacheKeyPrefix}{id}");

            // Publish Event
            await _publishEndpoint.Publish(new ProductUpdated(
                product.Id,
                product.Name,
                product.Description ?? "",
                product.Price,
                product.ImageUrl ?? "",
                product.CategoryId));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProductExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    private bool ProductExists(Guid id)
    {
        return _context.Products.Any(e => e.Id == id);
    }
}
