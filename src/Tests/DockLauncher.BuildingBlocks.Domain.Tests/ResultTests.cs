using DockLauncher.BuildingBlocks.Domain.Results;
using FluentAssertions;

namespace DockLauncher.BuildingBlocks.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldExposeSuccessfulState()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }
}