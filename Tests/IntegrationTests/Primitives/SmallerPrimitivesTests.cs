using YapIn.Domain.Models.Primitives;

namespace YapIn.IntegrationTests.Primitives;

public class LookbackDaysTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("14", 14)]
    [InlineData("365", 365)]
    public void Accepts_one_day_to_a_year(string raw, int days)
    {
        Assert.True(LookbackDays.TryParse(raw, out var lookback));
        Assert.Equal(days, lookback!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("366")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData(" 5")]
    [InlineData("1e2")]
    [InlineData("٥")]
    [InlineData("99999999999999999999")]
    public void Refuses_anything_else(string? raw)
    {
        Assert.False(LookbackDays.TryParse(raw, out _));
    }

    [Fact]
    public void Cannot_be_constructed_out_of_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LookbackDays(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LookbackDays(int.MaxValue));
    }
}

public class RepositoryNameTests
{
    [Theory]
    [InlineData("search")]
    [InlineData("billing")]
    [InlineData("acme.infra_v2")]
    public void Accepts_repository_names(string value) => Assert.Equal(value, new RepositoryName(value).Value);

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("a'b")]
    [InlineData("a\nb")]
    public void Refuses_names_that_are_not_a_single_safe_directory(string value) =>
        Assert.Throws<ArgumentException>(() => new RepositoryName(value));

    [Fact]
    public void Refuses_names_over_a_hundred_characters()
    {
        Assert.True(RepositoryName.IsValid(new string('a', 100)));
        Assert.False(RepositoryName.IsValid(new string('a', 101)));
        Assert.False(RepositoryName.IsValid(new string('a', 10_000_000)));
    }
}

public class ReviewServerUrlTests
{
    [Theory]
    [InlineData("→ http://localhost:4949\nnext: run poll", "http://localhost:4949")]
    [InlineData("review at http://localhost:4949/#t-3 now", "http://localhost:4949/#t-3")]
    [InlineData("http://localhost:1", "http://localhost:1")]
    [InlineData("http://localhost:65535", "http://localhost:65535")]
    public void Finds_the_local_review_address(string diffoOutput, string expected)
    {
        Assert.True(ReviewServerUrl.TryFindIn(diffoOutput, out var reviewServerUrl));
        Assert.Equal(expected, reviewServerUrl!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("diffo: not inside a git repository")]
    [InlineData("http://localhost:0")]
    [InlineData("http://localhost:99999")]
    [InlineData("https://localhost:4949")]
    [InlineData("http://evil.example:4949")]
    [InlineData("http://127.0.0.1:4949")]
    public void Finds_nothing_else(string? diffoOutput)
    {
        Assert.False(ReviewServerUrl.TryFindIn(diffoOutput, out _));
    }

    [Fact]
    public void Stops_the_address_at_characters_that_could_carry_markup()
    {
        Assert.True(ReviewServerUrl.TryFindIn("http://localhost:4949/x\"><script>", out var reviewServerUrl));
        Assert.Equal("http://localhost:4949/x", reviewServerUrl!.Value);
    }

    [Fact]
    public void Looks_only_at_the_start_of_huge_output()
    {
        Assert.True(ReviewServerUrl.TryFindIn("http://localhost:4949 " + new string('x', 20_000_000), out _));
        Assert.False(ReviewServerUrl.TryFindIn(new string('x', 20_000_000) + " http://localhost:4949", out _));
    }
}
