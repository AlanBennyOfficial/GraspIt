using FluentAssertions;
using GraspIt.Web.Controllers;
using GraspIt.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Xunit;

namespace GraspIt.Tests.Web.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsAViewResult()
    {
        // Arrange
        var controller = new HomeController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        viewResult.ViewName.Should().BeNull(); // default view returns null view name
    }

    [Fact]
    public void Error_ReturnsAViewResult_WithAnErrorViewModel()
    {
        // Arrange
        var controller = new HomeController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ControllerContext.HttpContext.TraceIdentifier = "test-trace-id";

        // Act
        var result = controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<ErrorViewModel>(viewResult.ViewData.Model);
        model.RequestId.Should().Be("test-trace-id");
    }
}
