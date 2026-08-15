using System.Text;

namespace PortYard.Domain.Validation;

/// <summary>
/// Validates container numbers against ISO 6346: 4 letters (3-letter owner code + equipment
/// category), 6 digits, and 1 check digit — e.g. "MSCU1234565".
/// </summary>
/// <remarks>
/// The check digit exists because container numbers are read off a steel door by eye,
/// hand-keyed at a gate kiosk, and OCR'd by a camera gantry — all error-prone. Validating
/// it at the API boundary stops a single mistyped or misread character from becoming a
/// phantom container that never matches anything in the yard, instead of a checkout an
/// operator can catch immediately.
/// </remarks>
public static class Iso6346
{
    private const string ValidEquipmentCategories = "UJZ";

    /// <summary>
    /// Returns true if <paramref name="containerNumber"/> is a structurally and
    /// numerically valid ISO 6346 container number. Accepts lowercase and surrounding
    /// whitespace, normalising both before validating.
    /// </summary>
    public static bool IsValid(string? containerNumber)
    {
        if (string.IsNullOrWhiteSpace(containerNumber))
            return false;

        var normalised = Normalise(containerNumber);

        if (normalised.Length != 11)
            return false;

        for (var i = 0; i < 4; i++)
        {
            if (!char.IsAsciiLetterUpper(normalised[i]))
                return false;
        }

        if (!ValidEquipmentCategories.Contains(normalised[3]))
            return false;

        for (var i = 4; i < 11; i++)
        {
            if (!char.IsAsciiDigit(normalised[i]))
                return false;
        }

        var expectedCheckDigit = ComputeCheckDigit(normalised[..10]);
        var actualCheckDigit = normalised[10] - '0';

        return expectedCheckDigit == actualCheckDigit;
    }

    /// <summary>
    /// Computes the ISO 6346 check digit for the first 10 characters (4 letters + 6 digits)
    /// of a container number. Returns null if <paramref name="prefix"/> is not exactly 10
    /// characters of that shape.
    /// </summary>
    public static int? ComputeCheckDigit(string prefix)
    {
        if (prefix is null || prefix.Length != 10)
            return null;

        var normalised = Normalise(prefix);

        long sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var value = CharValue(normalised[i]);
            if (value is null)
                return null;

            sum += value.Value * (1L << i);
        }

        var checkDigit = (int)(sum % 11);
        return checkDigit == 10 ? 0 : checkDigit;
    }

    private static string Normalise(string containerNumber)
    {
        var builder = new StringBuilder(containerNumber.Length);
        foreach (var c in containerNumber)
        {
            if (!char.IsWhiteSpace(c))
                builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Maps a letter to its ISO 6346 numeric value (A=10, B=12, ... skipping every
    /// multiple of 11) or a digit to itself. Built programmatically rather than as a
    /// hardcoded table so the "skip multiples of 11" rule stays visible and verifiable.
    /// </summary>
    private static int? CharValue(char c)
    {
        if (char.IsAsciiDigit(c))
            return c - '0';

        if (!char.IsAsciiLetterUpper(c))
            return null;

        var value = 10;
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            while (value % 11 == 0)
                value++;

            if (letter == c)
                return value;

            value++;
        }

        return null;
    }
}
