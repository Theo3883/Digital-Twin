using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class DocumentIdentityExtractorTests
{
    private readonly DocumentIdentityExtractorService _sut = new();

    [Fact]
    public void Extract_UnlabeledName_NearCnpAndAnchors_ExtractsNameAndCnp()
    {
        const string text = """
            Sandu Theodor
            Data nașterii
            08.03.2004
            Sex Masculin
            CNP 6030405315420
            Telefon 0756677624
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Sandu Theodor", result.ExtractedName);
        Assert.Equal("6030405315420", result.ExtractedCnp);
        Assert.True(result.NameConfidence >= 0.5f);
        Assert.Equal(1.0f, result.CnpConfidence);
    }

    [Fact]
    public void Extract_LabeledName_ExtractsNameFromLabel()
    {
        const string text = """
            Nume: Popescu Ion
            CNP 1980512345678
            Data 01.05.1998
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Popescu Ion", result.ExtractedName);
        Assert.Equal("1980512345678", result.ExtractedCnp);
        Assert.True(result.NameConfidence >= 0.9f);
    }

    [Theory]
    [InlineData("Pacient: Ionescu Maria-Elena")]
    [InlineData("Nume și prenume: Ionescu Maria-Elena")]
    [InlineData("Nume pacient: Ionescu Maria-Elena")]
    public void Extract_VariousLabelFormats_ExtractsName(string labelLine)
    {
        var text = $"{labelLine}\nCNP 2900101123456\nTelefon 0741234567";

        var result = _sut.Extract(text);

        Assert.Equal("Ionescu Maria-Elena", result.ExtractedName);
    }

    [Fact]
    public void Extract_NoCnpInDocument_ReturnsNullCnp()
    {
        const string text = """
            Popescu Ion
            Data nașterii
            01.05.1990
            Sex Masculin
            """;

        var result = _sut.Extract(text);

        Assert.Null(result.ExtractedCnp);
        Assert.Equal(0f, result.CnpConfidence);
    }

    [Fact]
    public void Extract_NoNameInDocument_ReturnsNullName()
    {
        const string text = """
            Laborator Clinic
            CNP 6030405315420
            Rezultat: 5.5 mg/dl
            Diagnostic: normal
            """;

        var result = _sut.Extract(text);

        Assert.Null(result.ExtractedName);
        Assert.Equal("6030405315420", result.ExtractedCnp);
    }

    [Fact]
    public void Extract_RomanianDiacriticsInName_ExtractsCorrectly()
    {
        const string text = """
            Ștefănescu Ioana-Maria
            Data nașterii
            15.06.1995
            Sex Feminin
            CNP 2950615123456
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Ștefănescu Ioana-Maria", result.ExtractedName);
    }

    [Fact]
    public void Extract_MultipleCnps_ReturnsFirst()
    {
        const string text = """
            Popescu Ion
            CNP 1980512345678
            Medic: Dr. Ionescu
            CNP medic 1750301123456
            """;

        var result = _sut.Extract(text);

        Assert.Equal("1980512345678", result.ExtractedCnp);
    }

    [Fact]
    public void Extract_EmptyText_ReturnsEmptyIdentity()
    {
        var result = _sut.Extract("");

        Assert.Null(result.ExtractedName);
        Assert.Null(result.ExtractedCnp);
        Assert.Equal(0f, result.NameConfidence);
        Assert.Equal(0f, result.CnpConfidence);
    }

    [Fact]
    public void Extract_NullText_ReturnsEmptyIdentity()
    {
        var result = _sut.Extract(null!);

        Assert.Null(result.ExtractedName);
        Assert.Null(result.ExtractedCnp);
    }

    [Fact]
    public void Extract_NameInTopLines_HigherConfidence()
    {
        const string text = """
            Marinescu Alexandru
            Data nașterii
            22.11.1988
            CNP 1881122123456
            Sex Masculin
            Telefon 0741234567
            Laborator Central
            Rezultate analize
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Marinescu Alexandru", result.ExtractedName);
        Assert.True(result.NameConfidence >= 0.7f, $"Expected ≥0.7 but got {result.NameConfidence}");
    }

    /// <summary>
    /// Regression: discharge letters have two "Nume:" lines — first is blank (underscores),
    /// second contains the real name combined with "Vârstă: 68 ani" on the same OCR line.
    /// The extractor must skip the blank first match and strip the inline field from the second.
    /// </summary>
    [Fact]
    public void Extract_DischargeLetter_TwoNumeLines_BlankFirstRealSecond_ExtractsName()
    {
        const string text = """
            SCRISOARE MEDICALĂ / BILET DE IEȘIRE DIN SPITAL
            Date: 20.11.2023 n. 315/CS
            Nume: _____
            Nume: Sandu Teodor    Vârstă: 68 ani
            Adresă: Str. Libertății 12    CNP: 6030405315420
            Diagnostic: Fibrilație atrială paroxistică (I48.0)
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Sandu Teodor", result.ExtractedName);
        Assert.Equal("6030405315420", result.ExtractedCnp);
        Assert.True(result.NameConfidence >= 0.9f);
    }

    [Fact]
    public void Extract_DischargeLetterInlineVarsta_StripsInlineField()
    {
        const string text = """
            Nume: Ionescu Maria    Vârsta: 55 ani
            CNP: 2690215123456
            """;

        var result = _sut.Extract(text);

        Assert.Equal("Ionescu Maria", result.ExtractedName);
        Assert.Equal("2690215123456", result.ExtractedCnp);
    }
}
