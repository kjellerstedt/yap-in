using YapIn.Domain.Exceptions;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.GitHub;

/// <summary>Whose gh sign-in the server is using. Asked once, since the sign-in does not change while the server runs.</summary>
internal sealed class GitHubViewer(IProcessRunner processRunner)
{
    private GitHubLogin? login;

    public async Task<GitHubLogin> GetAsync(CancellationToken cancellationToken)
    {
        if (login is not null) return login;
        var output = (await GhCli.OutputAsync(processRunner, ["api", "user", "--jq", ".login"], cancellationToken)).Trim();
        login = GitHubLogin.IsValid(output) ? new GitHubLogin(output) : throw new ExternalToolFailedException("gh", "returned no valid login");
        return login;
    }
}
