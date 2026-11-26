using YapIn.Domain.Interfaces;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Infrastructure.Checkouts;

/// <param name="ReposDirectory">Where clones are looked for (~/repos by default).</param>
public sealed record LocalCheckoutOptions(string ReposDirectory);

internal sealed class LocalCheckouts(LocalCheckoutOptions options) : ILocalCheckouts
{
    public string? FindClone(RepositoryName repository)
    {
        var clonePath = Path.Combine(options.ReposDirectory, repository.Value);
        return Directory.Exists(Path.Combine(clonePath, ".git")) ? clonePath : null;
    }
}
