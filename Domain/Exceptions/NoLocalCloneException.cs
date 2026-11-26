using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Exceptions;

/// <summary>A review needs a checkout of the pull request's repo, and no session host or local clone has one.</summary>
public sealed class NoLocalCloneException(RepositoryName repository) : Exception($"No local clone of {repository}")
{
    public RepositoryName Repository { get; } = repository;
}
