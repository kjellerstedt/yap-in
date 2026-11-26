using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Models;

/// <summary>
/// A review is running in <see cref="SessionHost"/>. <see cref="ToolOutput"/> is set only when the review tool gave no
/// review page (it may be asking a question, e.g. for your own pull request), so you can see what it said.
/// </summary>
public sealed record ReviewStarted(ReviewServerUrl? ReviewPageUrl, string SessionHost, string? ToolOutput);

/// <summary>
/// What a code review tool reported. <see cref="CheckoutPath"/> is a checkout of the pull request the tool made and
/// vouches for as its own, where an agent can look at the code; <c>null</c> when it made none.
/// </summary>
public sealed record CodeReviewStarted(ReviewServerUrl? ReviewPageUrl, string? CheckoutPath, string Output);

/// <summary>
/// Where an agent session runs. <see cref="HostWorkspaceId"/> is the host's own handle for a workspace it manages
/// (e.g. an Orca worktree); <c>null</c> for a plain directory, which a terminal can run in.
/// </summary>
public sealed record AgentWorkspace(string Directory, string? HostWorkspaceId);
