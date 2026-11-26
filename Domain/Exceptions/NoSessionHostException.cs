using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Exceptions;

/// <summary>The chosen session host could not provide a workspace for the repo.</summary>
public sealed class NoSessionHostException(RepositoryName repository, string sessionHost)
    : Exception($"{sessionHost} cannot open {repository}")
{
    public RepositoryName Repository { get; } = repository;
}
