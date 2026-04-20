using FluentAssertions;
using GraspIt.Web.Models;
using Xunit;

namespace GraspIt.Tests.Web.Models;

public class ErrorViewModelTests
{
    [Fact]
    public void ErrorViewModel_Properties_WorkAsExpected()
    {
        var model = new ErrorViewModel { RequestId = "123" };
        model.RequestId.Should().Be("123");
        model.ShowRequestId.Should().BeTrue();

        var emptyModel = new ErrorViewModel();
        emptyModel.ShowRequestId.Should().BeFalse();
    }
}
