using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Connect.Application.UnitTests.Api;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

public class CorsAndSwaggerSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsAndSwaggerSecurityTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Cors_DisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.Add("Origin", "http://malicious-site.com");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Disallowed origin should not receive Access-Control-Allow-Origin header.");
    }

    [Fact]
    public async Task Cors_AllowedOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.Add("Origin", "http://localhost:8080");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Allowed origin should receive Access-Control-Allow-Origin header.");
        Assert.Equal("http://localhost:8080", response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").FirstOrDefault());
    }

    [Fact]
    public async Task Cors_SignalRNegotiatePostRequest_AllowedOrigin_ReturnsAccessControlHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/call/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "http://localhost:8080");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "SignalR negotiate request from allowed origin should receive Access-Control-Allow-Origin header.");
        Assert.Equal("http://localhost:8080", response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").FirstOrDefault());
    }

    [Fact]
    public async Task Swagger_DevelopmentEnvironment_ReturnsSwaggerEndpoint()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
