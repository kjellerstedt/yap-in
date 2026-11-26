using YapIn.Domain.Models.Primitives;

namespace YapIn.IntegrationTests.Primitives;

public class PullRequestUrlTests
{
    [Theory]
    [InlineData("https://github.com/acme/panel-service/pull/722", "acme", "panel-service", 722)]
    [InlineData("https://github.com/pauljoda/Crest/pull/1", "pauljoda", "Crest", 1)]
    [InlineData("https://github.com/a/b.c_d-e/pull/2147483647", "a", "b.c_d-e", int.MaxValue)]
    public void Accepts_pull_request_addresses(string raw, string owner, string repository, int number)
    {
        Assert.True(PullRequestUrl.TryParse(raw, out var pullRequestUrl));
        Assert.Equal(owner, pullRequestUrl!.Owner.Value);
        Assert.Equal(repository, pullRequestUrl.Repository.Value);
        Assert.Equal(number, pullRequestUrl.Number.Value);
        Assert.Equal(raw, pullRequestUrl.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com/acme/search/pull/1")]
    [InlineData("https://github.com.evil.example/acme/search/pull/1")]
    [InlineData("https://github.com/acme/search/pull/1?x=1")]
    [InlineData("https://github.com/acme/search/pull/1#top")]
    [InlineData("https://github.com/acme/search/pull/1/files")]
    [InlineData("https://github.com/acme/search/pull/0")]
    [InlineData("https://github.com/acme/search/pull/-1")]
    [InlineData("https://github.com/acme/search/issues/1")]
    [InlineData("https://github.com/acme/../pull/1")]
    [InlineData("https://github.com/acme/se'arch/pull/1")]
    [InlineData("https://github.com/acme/search/pull/1\n")]
    [InlineData("https://github.com/acme/search/pull/1 && rm -rf ~")]
    [InlineData("https://github.com/-acme/search/pull/1")]
    [InlineData("https://github.com/acme/sëarch/pull/1")]
    [InlineData("https://github.com/acme/search/pull/١")]
    public void Refuses_anything_else(string? raw)
    {
        Assert.False(PullRequestUrl.TryParse(raw, out _));
    }

    [Fact]
    public void Refuses_numbers_too_large_to_be_a_pull_request_rather_than_wrapping_them()
    {
        Assert.False(PullRequestUrl.TryParse("https://github.com/acme/search/pull/2147483648", out _));
        Assert.False(PullRequestUrl.TryParse("https://github.com/acme/search/pull/9999999999", out _));
    }

    [Fact]
    public void Refuses_the_longest_names_GitHub_allows_only_when_over_the_length_limit()
    {
        var longest = $"https://github.com/{new string('a', 39)}/{new string('b', 100)}/pull/1";
        Assert.True(PullRequestUrl.TryParse(longest, out _));

        var tooLongRepository = $"https://github.com/{new string('a', 39)}/{new string('b', 101)}/pull/1";
        Assert.False(PullRequestUrl.TryParse(tooLongRepository, out _));
    }

    [Fact]
    public void Refuses_huge_input_without_scanning_it()
    {
        var huge = "https://github.com/acme/search/pull/" + new string('1', 40_000_000);
        Assert.False(PullRequestUrl.TryParse(huge, out _));
    }
}
