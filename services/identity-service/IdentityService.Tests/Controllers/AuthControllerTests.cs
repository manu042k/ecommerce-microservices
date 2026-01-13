using IdentityService.Controllers;
using IdentityService.Dtos;
using IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;

namespace IdentityService.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var identityService = CreateIdentityService();
        var controller = new AuthController(identityService.Object);
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "password123"
        };
        var authResponse = new AuthResponse
        {
            AccessToken = "test-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        identityService
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(authResponse);

        // Act
        var result = await controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<AuthResponse>();
        var response = okResult.Value as AuthResponse;
        response!.AccessToken.Should().Be("test-token");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var identityService = CreateIdentityService();
        var controller = new AuthController(identityService.Object);
        var request = new LoginRequest
        {
            Username = "invaliduser",
            Password = "wrongpassword"
        };

        identityService
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync((AuthResponse?)null);

        // Act
        var result = await controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult!.Value.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var identityService = CreateIdentityService();
        var controller = new AuthController(identityService.Object);
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "password123",
            FirstName = "John",
            LastName = "Doe"
        };

        identityService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be("User registered successfully");
    }

    [Fact]
    public async Task Register_WithFailedRegistration_ReturnsBadRequest()
    {
        // Arrange
        var identityService = CreateIdentityService();
        var controller = new AuthController(identityService.Object);
        var request = new RegisterRequest
        {
            Username = "existinguser",
            Email = "existing@example.com",
            Password = "password123",
            FirstName = "Jane",
            LastName = "Doe"
        };

        identityService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().Be("Registration failed");
    }

    private static Mock<IIdentityService> CreateIdentityService()
    {
        return new Mock<IIdentityService>();
    }
}
