using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class NameMatchingTests
{
    private readonly NameMatchingService _sut = new();

    [Fact]
    public void Match_ExactMatch_ReturnsTrue()
    {
        var result = _sut.Match("Sandu Theodor", "Sandu Theodor");

        Assert.True(result.IsMatch);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Match_ReversedOrder_ReturnsTrue()
    {
        var result = _sut.Match("Sandu Theodor", "Theodor Sandu");

        Assert.True(result.IsMatch);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Match_OcrMisread_Distance1_ReturnsTrue()
    {
        // OCR reads "Teodor" instead of "Theodor" (1 char diff)
        var result = _sut.Match("Sandu Theodor", "Sandu Teodor");

        Assert.True(result.IsMatch);
        Assert.True(result.Distance <= 2);
    }

    [Fact]
    public void Match_OcrMisread_Distance2_ReturnsTrue()
    {
        // OCR reads "Teoder" instead of "Theodor" (2 char diff)
        var result = _sut.Match("Sandu Theodor", "Sandu Teoder");

        Assert.True(result.IsMatch);
        Assert.True(result.Distance <= 2);
    }

    [Fact]
    public void Match_DiacriticsNormalization_ReturnsTrue()
    {
        var result = _sut.Match("Ștefănescu Ion", "Stefanescu Ion");

        Assert.True(result.IsMatch);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Match_CompletelyDifferentName_ReturnsFalse()
    {
        var result = _sut.Match("Sandu Theodor", "Popescu Maria");

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Match_ExtraMiddleName_InDocument_ReturnsTrue()
    {
        // Document has middle name, profile doesn't
        var result = _sut.Match("Sandu Theodor", "Sandu Ion Theodor");

        Assert.True(result.IsMatch);
    }

    [Fact]
    public void Match_CaseInsensitive_ReturnsTrue()
    {
        var result = _sut.Match("SANDU THEODOR", "sandu theodor");

        Assert.True(result.IsMatch);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Match_NullExpected_ReturnsFalse()
    {
        var result = _sut.Match(null!, "Sandu Theodor");

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Match_NullActual_ReturnsFalse()
    {
        var result = _sut.Match("Sandu Theodor", null!);

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Match_EmptyStrings_ReturnsFalse()
    {
        var result = _sut.Match("", "");

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Match_SingleTokenName_ReturnsFalse_WhenTooFarApart()
    {
        // Very different single-token "names"
        var result = _sut.Match("Alexandra", "Bogdan");

        Assert.False(result.IsMatch);
    }
}
