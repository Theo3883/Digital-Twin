using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class SensitiveDataSanitizerTests
{
    private readonly SensitiveDataSanitizer _sut = new();

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsUnchanged()
    {
        Assert.Equal(string.Empty, _sut.Sanitize(string.Empty));
    }

    [Theory]
    [InlineData("Pacient: 1234567890123 data nasterii", "[CNP]")]   // Romanian CNP
    [InlineData("ID 8901234567890 confirmat", "[CNP]")]
    public void Sanitize_CnpPattern_IsRedacted(string input, string expectedToken)
        => Assert.Contains(expectedToken, _sut.Sanitize(input));

    [Theory]
    [InlineData("Contactati pacientul la test.user+label@example.ro", "[EMAIL]")]
    [InlineData("Email: doctor@clinic.med.ro pentru confirmare", "[EMAIL]")]
    public void Sanitize_EmailAddress_IsRedacted(string input, string expectedToken)
        => Assert.Contains(expectedToken, _sut.Sanitize(input));

    [Theory]
    [InlineData("Tel: 0741 234 567 confirmat", "[PHONE]")]
    [InlineData("+40 723 456 789 contact", "[PHONE]")]
    public void Sanitize_PhoneNumber_IsRedacted(string input, string expectedToken)
        => Assert.Contains(expectedToken, _sut.Sanitize(input));

    [Theory]
    [InlineData("Token: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature", "[TOKEN]")]
    public void Sanitize_BearerToken_IsRedacted(string input, string expectedToken)
        => Assert.Contains(expectedToken, _sut.Sanitize(input));

    [Fact]
    public void Sanitize_PlainTextWithNoSensitiveData_IsUnchanged()
    {
        const string safe = "Temperatura corporala normala. Tensiunea arteriala normala.";
        Assert.Equal(safe, _sut.Sanitize(safe));
    }

    [Fact]
    public void Sanitize_MultiplePatterns_AllRedacted()
    {
        const string input = "Pacient 1234567890123 email test@x.com tel 0741234567";
        var result = _sut.Sanitize(input);

        Assert.Contains("[CNP]", result);
        Assert.Contains("[EMAIL]", result);
        Assert.Contains("[PHONE]", result);
        Assert.DoesNotContain("1234567890123", result);
        Assert.DoesNotContain("test@x.com", result);
    }

    [Fact]
    public void BuildSanitizedPreview_ExceedsMaxLength_IsTruncated()
    {
        var longText = new string('A', 3000);
        var result = _sut.BuildSanitizedPreview([longText], maxLength: 200);

        Assert.True(result.Length <= 240); // maxLength(200) + truncation suffix (~25 chars)
        Assert.Contains("truncated", result);
    }

    [Fact]
    public void BuildSanitizedPreview_MultiplePages_JoinedWithSeparator()
    {
        var result = _sut.BuildSanitizedPreview(["Page one text.", "Page two text."], maxLength: 2000);
        Assert.Contains("---", result);
        Assert.Contains("Page one text.", result);
        Assert.Contains("Page two text.", result);
    }
}
