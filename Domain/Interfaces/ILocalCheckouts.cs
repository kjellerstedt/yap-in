using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

public interface ILocalCheckouts
{
    /// <returns>The repo's clone under the repos directory, or <c>null</c> when there is none.</returns>
    string? FindClone(RepositoryName repository);
}
