using FluentAssertions;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace GraspIt.Tests.Web;

public class WebPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WebPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_EndpointsReturnSuccessAndCorrectContentType()
    {
        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode(); 
        response.Content.Headers.ContentType!.ToString().Should().Contain("text/html");
    }

    [Fact]
    public async Task Get_UnknownEndpoint_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/unknown-page-that-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
