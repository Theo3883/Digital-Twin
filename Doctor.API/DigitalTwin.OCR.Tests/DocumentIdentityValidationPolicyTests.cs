using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Policies;
using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class DocumentIdentityValidationPolicyTests
{
    private readonly DocumentIdentityValidationPolicy _sut = new(new NameMatchingService());

    [Fact]
    public void Validate_BothMatch_ReturnsValid()
    {
        var identity = new DocumentIdentity("Sandu Theodor", "6030405315420", 0.9f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Validate_FuzzyNameMatch_ReturnsValid()
    {
        var identity = new DocumentIdentity("Sandu Teodor", "6030405315420", 0.8f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingName_ReturnsMissingName()
    {
        var identity = new DocumentIdentity(null, "6030405315420", 0f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.False(result.IsValid);
        Assert.Equal(IdentityValidationFailureReason.MissingName, result.FailureReason);
    }

    [Fact]
    public void Validate_MissingCnp_ReturnsMissingCnp()
    {
        var identity = new DocumentIdentity("Sandu Theodor", null, 0.9f, 0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.False(result.IsValid);
        Assert.Equal(IdentityValidationFailureReason.MissingCnp, result.FailureReason);
    }

    [Fact]
    public void Validate_CnpMismatch_ReturnsCnpMismatch()
    {
        var identity = new DocumentIdentity("Sandu Theodor", "1234567890123", 0.9f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.False(result.IsValid);
        Assert.Equal(IdentityValidationFailureReason.CnpMismatch, result.FailureReason);
    }

    [Fact]
    public void Validate_NameMismatch_ReturnsNameMismatch()
    {
        var identity = new DocumentIdentity("Popescu Maria", "6030405315420", 0.9f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.False(result.IsValid);
        Assert.Equal(IdentityValidationFailureReason.NameMismatch, result.FailureReason);
    }

    [Fact]
    public void Validate_BothMissing_ReturnsMissingNameFirst()
    {
        var identity = new DocumentIdentity(null, null, 0f, 0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.False(result.IsValid);
        Assert.Equal(IdentityValidationFailureReason.MissingName, result.FailureReason);
    }

    [Fact]
    public void Validate_ReversedNameOrder_ReturnsValid()
    {
        var identity = new DocumentIdentity("Theodor Sandu", "6030405315420", 0.9f, 1.0f);

        var result = _sut.Validate(identity, "Sandu Theodor", "6030405315420");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DiacriticsInName_ReturnsValid()
    {
        var identity = new DocumentIdentity("Stefanescu Ion", "2900101123456", 0.9f, 1.0f);

        var result = _sut.Validate(identity, "Ștefănescu Ion", "2900101123456");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ToUserMessage_ReturnsDescriptiveMessage()
    {
        var result = IdentityValidationResult.Failure(
            IdentityValidationFailureReason.CnpMismatch,
            "Sandu Theodor", "1234567890123", "Sandu Theodor", "6030405315420");

        Assert.Contains("CNP", result.ToUserMessage());
        Assert.Contains("does not match", result.ToUserMessage());
    }
}
