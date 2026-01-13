using IdentityService.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Tests.Controllers;

public class TestControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithMessage()
    {
        // Arrange
        var controller = new TestController();

        // Act
        var result = controller.Get();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be("Identity Service is working");
    }

    [Fact]
    public async Task CheckSwagger_ReturnsOkWithStatus()
    {
        // Arrange
        var controller = new TestController();

        // Act
        var result = await controller.CheckSwagger();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.ToString()!.Should().Contain("Status:");
    }
}
