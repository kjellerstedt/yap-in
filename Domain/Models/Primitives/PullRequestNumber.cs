using System.Globalization;

namespace YapIn.Domain.Models.Primitives;

public sealed record PullRequestNumber
{
    public int Value { get; }

    public PullRequestNumber(int value)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "A pull request number is 1 or more.");
        Value = value;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
