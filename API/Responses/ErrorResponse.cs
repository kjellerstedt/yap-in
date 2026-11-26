namespace YapIn.Api.Responses;

/// <summary>What the page is told when a request fails: a fixed message, never program output or a stack trace.</summary>
public sealed record ErrorResponse(string Error);
