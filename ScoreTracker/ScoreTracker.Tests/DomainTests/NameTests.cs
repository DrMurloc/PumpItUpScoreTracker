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

    // ---- Implicit conversions ----

    [Fact]
    public void ImplicitConversionFromStringDelegatesToFrom()
    {
        Name name = "Hello";
        Assert.Equal("Hello", (string)name);
    }

    [Fact]
    public void ImplicitConversionFromEmptyStringThrows()
    {
        Assert.Throws<InvalidNameException>(() =>
        {
            Name name = "";
        });
    }

    [Fact]
    public void ImplicitConversionFromNullableStringReturnsNullForNullInput()
    {
        Name? name = (string?)null;
        Assert.Null(name);
    }

    [Fact]
    public void ImplicitConversionFromNullableStringReturnsValueForNonNullInput()
    {
        Name? name = (string?)"Hello";
        Assert.NotNull(name);
        Assert.Equal("Hello", (string)name!.Value);
    }

    [Fact]
    public void ImplicitConversionToStringExposesUnderlyingName()
    {
        var name = Name.From("Hello");
        string back = name;
        Assert.Equal("Hello", back);
    }

    // ---- TryParse ----

    [Fact]
    public void TryParseReturnsTrueAndOutValueForValidInput()
    {
        Assert.True(Name.TryParse("Hello", out var result));
        Assert.Equal("Hello", (string)result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseReturnsFalseForInvalidInput(string input)
    {
        Assert.False(Name.TryParse(input, out _));
    }

    // ---- Contains ----

    [Fact]
    public void ContainsReturnsTrueForCaseInsensitiveSubstring()
    {
        var name = Name.From("Hello World");

        Assert.True(name.Contains("WORLD"));
        Assert.True(name.Contains("hello"));
        Assert.True(name.Contains("o W"));
    }

    [Fact]
    public void ContainsReturnsFalseForNonMatchingSubstring()
    {
        var name = Name.From("Hello World");

        Assert.False(name.Contains("Goodbye"));
    }

    // ---- CompareTo ----

    [Fact]
    public void CompareToOrdersAlphabeticallyIgnoringCase()
    {
        Assert.True(Name.From("apple").CompareTo(Name.From("Banana")) < 0);
        Assert.True(Name.From("BANANA").CompareTo(Name.From("apple")) > 0);
        Assert.Equal(0, Name.From("Same").CompareTo(Name.From("SAME")));
    }
}