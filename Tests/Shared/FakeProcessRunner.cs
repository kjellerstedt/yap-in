using System.Collections.Concurrent;
using YapIn.Infrastructure.Processes;

namespace YapIn.Tests.Shared;

/// <summary>
/// Stands in for every program the server runs (gh, npx, orca, osascript): answers with <see cref="Respond"/> and
/// records each call, since the arguments a program was given are what only this boundary can show.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public ConcurrentQueue<ProcessRequest> Requests { get; } = new();
    public Func<ProcessRequest, ProcessResult> Respond { get; set; } = _ => Succeeds("");

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Respond(request));
    }

    public IReadOnlyList<ProcessRequest> RequestsTo(string program) => Requests.Where(request => request.FileName == program).ToList();

    public static ProcessResult Succeeds(string output) => new(0, output, "");

    public static ProcessResult Fails(string error) => new(1, "", error);
}
