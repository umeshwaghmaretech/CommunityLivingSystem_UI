using System.Text.RegularExpressions;

namespace Mesa.WhatsApp.Domain.ValueObjects;

public sealed partial class PhoneNumber : IEquatable<PhoneNumber>
{
    public string E164 { get; }

    private PhoneNumber(string e164) => E164 = e164;

    public static bool TryCreate(string? raw, out PhoneNumber? phone, out string? error)
    {
        phone = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Phone number is required.";
            return false;
        }

        var digits = DigitsOnly().Replace(raw.Trim(), string.Empty);
        if (raw.Trim().StartsWith('+'))
        {
            digits = DigitsOnly().Replace(raw.Trim()[1..], string.Empty);
        }

        // Indian numbers: 10 digits starting with 6-9 → +91
        if (digits.Length == 10 && digits[0] is >= '6' and <= '9')
        {
            digits = "91" + digits;
        }

        // Strip leading 0 after country code mistakes like 091...
        if (digits.StartsWith("091") && digits.Length == 13)
        {
            digits = digits[1..];
        }

        if (digits.Length is < 10 or > 15)
        {
            error = "Phone number length is invalid after normalisation.";
            return false;
        }

        var e164 = "+" + digits;
        if (!E164Pattern().IsMatch(e164))
        {
            error = "Phone number is not valid E.164.";
            return false;
        }

        phone = new PhoneNumber(e164);
        return true;
    }

    public static string Mask(string? e164)
    {
        if (string.IsNullOrWhiteSpace(e164) || e164.Length < 6)
            return "***";

        return e164[..4] + new string('*', Math.Max(0, e164.Length - 7)) + e164[^3..];
    }

    public bool Equals(PhoneNumber? other) => other is not null && E164 == other.E164;
    public override bool Equals(object? obj) => obj is PhoneNumber other && Equals(other);
    public override int GetHashCode() => E164.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => E164;

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex DigitsOnly();

    [GeneratedRegex(@"^\+[1-9]\d{9,14}$")]
    private static partial Regex E164Pattern();
}