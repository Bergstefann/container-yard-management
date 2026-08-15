using FluentAssertions;
using PortYard.Domain.Validation;

namespace PortYard.Tests.Unit;

public class Iso6346Tests
{
    [Theory]
    [InlineData("MSCU1234566")]
    [InlineData("CSQU3054383")] // canonical ISO 6346 worked example
    [InlineData("TGHU7801098")]
    [InlineData("MAEU9876542")]
    public void IsValid_returns_true_for_correctly_checksummed_numbers(string containerNumber)
    {
        Iso6346.IsValid(containerNumber).Should().BeTrue();
    }

    [Fact]
    public void IsValid_returns_false_for_wrong_check_digit()
    {
        // MSCU1234566 is correct; every other trailing digit is a wrong checksum.
        Iso6346.IsValid("MSCU1234567").Should().BeFalse();
    }

    [Fact]
    public void IsValid_returns_false_for_invalid_equipment_category()
    {
        // Position 4 must be U, J, or Z. 'X' is not a valid equipment category.
        Iso6346.IsValid("MSCX1234566").Should().BeFalse();
    }

    [Fact]
    public void IsValid_returns_false_when_serial_position_contains_a_letter()
    {
        Iso6346.IsValid("MSCU12A4566").Should().BeFalse();
    }

    [Theory]
    [InlineData("MSCU123456")] // 10 chars, too short
    [InlineData("MSCU12345667")] // 12 chars, too long
    public void IsValid_returns_false_for_wrong_length(string containerNumber)
    {
        Iso6346.IsValid(containerNumber).Should().BeFalse();
    }

    [Fact]
    public void IsValid_normalises_lowercase_and_surrounding_whitespace()
    {
        Iso6346.IsValid("  mscu1234566  ").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_returns_false_for_null_or_empty(string? containerNumber)
    {
        Iso6346.IsValid(containerNumber).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MSC")]
    [InlineData("MSCU12345678")] // 12 chars, not 10
    [InlineData("MSCU12A456")] // letter in digit position
    public void ComputeCheckDigit_returns_null_for_malformed_prefix(string? prefix)
    {
        Iso6346.ComputeCheckDigit(prefix!).Should().BeNull();
    }

    [Theory]
    [InlineData("MSCU123456")]
    [InlineData("CSQU305438")]
    [InlineData("HLXU000001")]
    [InlineData("ONEU999999")]
    public void ComputeCheckDigit_output_always_makes_IsValid_true(string prefix)
    {
        var checkDigit = Iso6346.ComputeCheckDigit(prefix);

        checkDigit.Should().NotBeNull();
        Iso6346.IsValid(prefix + checkDigit).Should().BeTrue();
    }
}
