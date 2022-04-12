using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class NameTests
{
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    public void NullOrEmptyNamesThrowsException(string name)
    {
        Assert.Throws<InvalidNameException>(() => Name.From(name));
    }

    [Theory]
    [InlineData(" Name ")]
    public void ValidNameHasWhitespaceTrimmed(string name)
    {
        var result = Name.From(name);
        Assert.Equal(name.Trim(), result);
    }

    [Theory]
    [InlineData("NaMe")]
    public void NameEqualityIgnoresCase(string name)
    {
        var upperCase = Name.From(name.ToUpper());
        var lowerCase = Name.From(name.ToLower());
        Assert.Equal(upperCase, lowerCase);
    }
}