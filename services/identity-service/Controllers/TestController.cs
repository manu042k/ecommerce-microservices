using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/auth/test")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Identity Service is working");
    }

    [HttpGet("swagger-check")]
    public async Task<IActionResult> CheckSwagger()
    {
        try
        {
            var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:8080/swagger/v1/swagger.json");
            return Ok($"Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return Ok($"Status: Connection failed - {ex.Message}");
        }
    }
}
