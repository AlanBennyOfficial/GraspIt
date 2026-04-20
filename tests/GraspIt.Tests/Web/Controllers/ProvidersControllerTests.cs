using AutoMapper;
using FluentAssertions;
using GraspIt.Application.DTOs;
using GraspIt.Application.Mappings;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using GraspIt.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace GraspIt.Tests.Web.Controllers;

public class ProvidersControllerTests
{
    private readonly Mock<IMapper> _mapperMock;

    public ProvidersControllerTests()
    {
        _mapperMock = new Mock<IMapper>();
    }

    private GraspItDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<GraspItDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new GraspItDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WithListOfProviders()
    {
        // Arrange
        var context = GetDbContext();
        context.AiProviders.Add(new AiProvider { Name = "Prov1" });
        await context.SaveChangesAsync();

        var controller = new ProvidersController(context, _mapperMock.Object);

        var dtos = new List<AiProviderDto> { new AiProviderDto { Name = "Prov1" } };
        _mapperMock.Setup(m => m.Map<List<AiProviderDto>>(It.IsAny<List<AiProvider>>())).Returns(dtos);

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<AiProviderDto>>(viewResult.ViewData.Model);
        model.Should().HaveCount(1);
        model.First().Name.Should().Be("Prov1");
    }

    [Fact]
    public void Create_ReturnsViewResult()
    {
        // Arrange
        var controller = new ProvidersController(GetDbContext(), _mapperMock.Object);

        // Act
        var result = controller.Create();

        // Assert
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_Post_ValidModel_RedirectsToIndexAndSaves()
    {
        // Arrange
        var context = GetDbContext();
        var controller = new ProvidersController(context, _mapperMock.Object);
        var dto = new AiProviderDto { Name = "New Prov", IsDefault = true };

        // Act
        _mapperMock.Setup(m => m.Map<AiProvider>(It.IsAny<AiProviderDto>())).Returns(new AiProvider { Name = "New Prov" });
        var result = await controller.Create(dto);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        redirectResult.ActionName.Should().Be("Index");

        var saved = await context.AiProviders.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved.Name.Should().Be("New Prov");
    }

    [Fact]
    public async Task Create_Post_InvalidModel_ReturnsViewResult()
    {
        // Arrange
        var context = GetDbContext();
        var controller = new ProvidersController(context, _mapperMock.Object);
        controller.ModelState.AddModelError("Name", "Required");
        var dto = new AiProviderDto();

        // Act
        var result = await controller.Create(dto);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        viewResult.Model.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task Edit_Get_NullId_ReturnsNotFound()
    {
        var controller = new ProvidersController(GetDbContext(), _mapperMock.Object);
        var result = await controller.Edit(id: null);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_ValidId_ReturnsViewResult()
    {
        var context = GetDbContext();
        context.AiProviders.Add(new AiProvider { Id = 1, Name = "Existing" });
        await context.SaveChangesAsync();
        var controller = new ProvidersController(context, _mapperMock.Object);

        _mapperMock.Setup(m => m.Map<AiProviderDto>(It.IsAny<AiProvider>())).Returns(new AiProviderDto { Name = "Existing" });
        var result = await controller.Edit(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<AiProviderDto>(viewResult.Model);
        model.Name.Should().Be("Existing");
    }

    [Fact]
    public async Task Edit_Post_IdMismatch_ReturnsNotFound()
    {
        var controller = new ProvidersController(GetDbContext(), _mapperMock.Object);
        var result = await controller.Edit(1, new AiProviderDto { Id = 2 });
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ValidModel_RedirectsToIndexAndUpdates()
    {
        var context = GetDbContext();
        context.AiProviders.Add(new AiProvider { Id = 1, Name = "Existing", IsDefault = false });
        context.AiProviders.Add(new AiProvider { Id = 2, Name = "Default", IsDefault = true });
        await context.SaveChangesAsync();
        var controller = new ProvidersController(context, _mapperMock.Object);
        var dto = new AiProviderDto { Id = 1, Name = "Updated", IsDefault = true };
        _mapperMock.Setup(m => m.Map(It.IsAny<AiProviderDto>(), It.IsAny<AiProvider>())).Callback<AiProviderDto, AiProvider>((s, d) => { d.Name = s.Name; d.IsDefault = s.IsDefault; });

        var result = await controller.Edit(1, dto);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        redirectResult.ActionName.Should().Be("Index");

        var updated = await context.AiProviders.FindAsync(1);
        updated.Name.Should().Be("Updated");
        updated.IsDefault.Should().BeTrue();

        var other = await context.AiProviders.FindAsync(2);
        other.IsDefault.Should().BeFalse(); // Clears previous defaults
    }

    [Fact]
    public async Task Delete_ValidId_RemovesAndRedirects()
    {
        var context = GetDbContext();
        context.AiProviders.Add(new AiProvider { Id = 1 });
        await context.SaveChangesAsync();
        var controller = new ProvidersController(context, _mapperMock.Object);

        var result = await controller.Delete(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        redirectResult.ActionName.Should().Be("Index");

        var remaining = await context.AiProviders.ToListAsync();
        remaining.Should().BeEmpty();
    }
}
