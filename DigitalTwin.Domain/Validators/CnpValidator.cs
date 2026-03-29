using System.Globalization;

namespace DigitalTwin.Domain.Validators;

/// <summary>
/// Validates Romanian Personal Numeric Codes (Cod Numeric Personal — CNP).
/// Format: SAAMMDDCCNNNK (13 digits)
///   S      – gender &amp; century (1–6 for Romanian citizens)
///   AAMMDD – date of birth
///   CC     – county code (01–52 or 99)
///   NNN    – serial number
///   K      – checksum digit
/// </summary>
public static class CnpValidator
{
    private static readonly int[] ChecksumWeights = { 2, 7, 9, 1, 4, 6, 3, 5, 8, 2, 7, 9 };

    /// <summary>
    /// Returns <c>true</c> when the CNP is exactly 13 digits and the first digit is 1–6.
    /// </summary>
    public static bool IsValidFormat(string cnp)
    {
        if (cnp is null || cnp.Length != 13)
            return false;

        foreach (var c in cnp)
        {
            if (c < '0' || c > '9')
                return false;
        }

        var s = cnp[0] - '0';
        return s >= 1 && s <= 6;
    }

    /// <summary>
    /// Returns <c>true</c> when the embedded date (digits 2–7) represents a valid calendar date.
    /// </summary>
    public static bool IsValidDate(string cnp)
    {
        return ExtractDateOfBirth(cnp).HasValue;
    }

    /// <summary>
    /// Returns <c>true</c> when the county code (digits 8–9) is between 01–52 or equals 99.
    /// </summary>
    public static bool IsValidCountyCode(string cnp)
    {
        if (cnp is null || cnp.Length != 13)
            return false;

        var county = (cnp[7] - '0') * 10 + (cnp[8] - '0');
        return (county >= 1 && county <= 52) || county == 99;
    }

    /// <summary>
    /// Verifies the checksum digit (last digit) using the standard constant 279146358279.
    /// </summary>
    public static bool IsValidChecksum(string cnp)
    {
        if (cnp is null || cnp.Length != 13)
            return false;

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (cnp[i] - '0') * ChecksumWeights[i];

        var remainder = sum % 11;
        var expected = remainder == 10 ? 1 : remainder;
        return (cnp[12] - '0') == expected;
    }

    /// <summary>
    /// Extracts the full date of birth from the CNP, resolving the century from the S digit.
    /// Returns <c>null</c> when the embedded date is not a valid calendar date.
    /// </summary>
    public static DateTime? ExtractDateOfBirth(string cnp)
    {
        if (cnp is null || cnp.Length < 7)
            return null;

        var s = cnp[0] - '0';

        int century = s switch
        {
            1 or 2 => 1900,
            3 or 4 => 1800,
            5 or 6 => 2000,
            _ => -1
        };

        if (century < 0)
            return null;

        var yy = (cnp[1] - '0') * 10 + (cnp[2] - '0');
        var mm = (cnp[3] - '0') * 10 + (cnp[4] - '0');
        var dd = (cnp[5] - '0') * 10 + (cnp[6] - '0');

        var year = century + yy;

        if (DateTime.TryParseExact(
                $"{year:D4}-{mm:D2}-{dd:D2}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the date of birth embedded in the CNP matches the supplied date.
    /// </summary>
    public static bool MatchesDateOfBirth(string cnp, DateTime dateOfBirth)
    {
        var extracted = ExtractDateOfBirth(cnp);
        if (extracted is null)
            return false;

        return extracted.Value.Date == dateOfBirth.Date;
    }
}
