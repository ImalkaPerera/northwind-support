using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void TheHashIsNotThePassword()
    {
        var hash = _hasher.Hash("LocalDev-Password-1");

        Assert.NotEqual("LocalDev-Password-1", hash);
        Assert.DoesNotContain("LocalDev-Password-1", hash);
    }

    [Fact]
    public void TheRightPasswordVerifies()
    {
        var hash = _hasher.Hash("LocalDev-Password-1");

        Assert.True(_hasher.Verify(hash, "LocalDev-Password-1"));
    }

    [Theory]
    [InlineData("localdev-password-1")]
    [InlineData("LocalDev-Password-2")]
    [InlineData(" LocalDev-Password-1")]
    [InlineData("")]
    public void AWrongPasswordFails(string attempt)
    {
        var hash = _hasher.Hash("LocalDev-Password-1");

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Fact]
    public void ThePasswordIsSalted_SoTheSamePasswordGivesADifferentHashEachTime()
    {
        var first = _hasher.Hash("LocalDev-Password-1");
        var second = _hasher.Hash("LocalDev-Password-1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, "LocalDev-Password-1"));
        Assert.True(_hasher.Verify(second, "LocalDev-Password-1"));
    }
}
