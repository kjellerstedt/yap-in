using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace YapIn.Domain.Models.Primitives;

/// <summary>How far back the merged and closed views search.</summary>
public sealed record LookbackDays
{
    public const int Min = 1;
    public const int Max = 365;
    public static readonly LookbackDays Default = new(14);

    public int Value { get; }

    public LookbackDays(int value)
    {
        if (value is < Min or > Max) throw new ArgumentOutOfRangeException(nameof(value), $"Lookback is {Min} to {Max} days.");
        Value = value;
    }

    public static bool TryParse(string? raw, [NotNullWhen(true)] out LookbackDays? lookbackDays)
    {
        lookbackDays = null;
        if (raw is null || raw.Length > 3) return false;
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var days) == false || days is < Min or > Max) return false;
        lookbackDays = new LookbackDays(days);
        return true;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
