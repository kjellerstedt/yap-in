# yap-in

Yet Another PR Inbox.

A local page listing the GitHub pull requests that involve you, grouped and filtered, with a Review button that either
opens the pull request's changes on GitHub or starts a [diffo](https://github.com/DiffoHQ/diffo) review with a Claude
session attached.

The .NET 10 server follows the narrow integration testing reference layout:

| Folder | Project | Responsibility |
| --- | --- | --- |
| `API` | `YapIn.Api` | Response contracts: the JSON the page reads |
| `Web` | `YapIn.Web` | HTTP endpoints, the localhost guard, background refresh, and the composition root: `Program.cs` and `Startup/` (what each interface is, and setup); serves the built page from `wwwroot` |
| `Domain` | `YapIn.Domain` | Checked types (`PullRequestUrl`, `LookbackDays`…), models, the inbox cache and review orchestration, and the roles it needs (one file each in `Interfaces/`): `IPullRequestSearch`, `ICodeReviewTool`, `IAgentSessionHost`, `ILocalCheckouts` |
| `Infrastructure` | `YapIn.Infrastructure` | GitHub search, local checkouts and `Adapters/` (one folder per external tool), all through the `IProcessRunner` seam |
| `Tests/IntegrationTests` | `YapIn.IntegrationTests` | Narrow integration tests: the real registrations and adapters, with a fake process runner and clock |
| `Tests/ContractTests` | `YapIn.ContractTests` | Contract tests: the real orca, gh, diffo and AppleScript compiler, read-only; skipped with a reason when a tool is missing |
| `Tests/Shared` | | Sources linked into both test projects (the fake process runner, sample orca rows) |
| `frontend` | | Vue 3 page, built into `Web/wwwroot` |

Dependencies point inward: `Web → API, Domain, Infrastructure`; `Infrastructure → Domain`. Only `Web/Startup` registers
services, so Domain and Infrastructure hold no DI code.

GitHub is searched through `gh api graphql`, so the server holds no token. Each view is kept warm with a one-minute
refresh. With diffo chosen, a review starts diffo, then an agent session in the session host you chose in setup.

### External tools

Each lives in its own folder under `Infrastructure/Adapters`. To add one, add the adapter and one entry to
`Web/Startup/ToolOptions.cs`; setup and the wiring both read that list. To remove one, delete both.

| Adapter | Role | Notes |
| --- | --- | --- |
| `Diffo` | `ICodeReviewTool` "diffo" | Runs a pinned diffo version through npx; the agent attaches with diffo's `/diffo` skill |
| `Orca` | `IAgentSessionHost` "orca" | One Orca worktree per pull request (setup skipped), status PR Review |
| `ITerm` | `IAgentSessionHost` "iterm" | A new iTerm window in the local clone, or the review tool's own checkout |
| `MacTerminal` | `IAgentSessionHost` "terminal" | A new macOS Terminal window, the same way |

`Adapters/Helpers` holds what several adapters share (running an AppleScript through osascript).

GitHub stays outside `Adapters`: it is where the inbox comes from, not a tool you can swap.

## Run

Needs the .NET 10 SDK, Node with npm, and `gh` logged in (`gh auth status`). The Makefile finds `dotnet` on `PATH` or
in `~/.dotnet`, where the SDK install script puts it; to use another, run `make DOTNET=/path/to/dotnet run`.

```sh
make run        # installs page dependencies if needed, builds the page, serves http://localhost:4477
make dev        # in a second terminal: the page with live reload on http://localhost:5173, /api forwarded to make run
make test
```

The first start asks, in the terminal you ran it from, what the Review button does, and saves the answer in
`~/.config/yap-in/settings.json`:

- **Open on GitHub**: the button links to the pull request's changes. Nothing else to install.
- **diffo**: the button starts a diffo review and an agent session attached to it. Setup then asks where sessions run
  (Orca, iTerm or Terminal) and prints how to install diffo's `/diffo` skill for your agents:
  `npx -y @diffohq/diffo@0.35.0 setup`.

Run `dotnet run --project Web -- --setup` to choose again. With no terminal (a service) and nothing saved, the server
stops and says so rather than guess.

Settings, in `Web/appsettings.json` or as environment variables (`Reviews__SessionHost=iterm`):

- `Urls`: where the server listens; default `http://127.0.0.1:4477`.
- `Reviews:ReviewTool` (`github` or `diffo`), `Reviews:SessionHost` (`orca`, `iterm` or `terminal`, only for diffo):
  the choices setup saved; a setting overrides the saved choice. A session host that cannot open the repo (Orca
  without it, or Orca not running) fails the review; there is no fall-through.
- `Reviews:Agent`: the agent each host starts; default `claude`.
- `Reviews:ReposDirectory`: where a terminal host finds the clone; default `~/repos`.

## Test

```sh
dotnet test
(cd frontend && npm test && npm run typecheck)
```

The integration tests run the real services and adapters with only the programs faked (a fake process runner), so no
external tool is needed; `Tests/IntegrationTests/Host` covers what the driver bypasses: the localhost guard and the
JSON written to the page.

The contract tests are the ones that talk to the real tools, so they show when orca, diffo, gh or Terminal change under
us. They change nothing: they list Orca's worktrees, ask orca and diffo for their own help and check every flag we send
is in it, compile our AppleScript against iTerm's and Terminal's real scripting dictionaries, and run our GitHub search
(read-only) through your own `gh`. A tool that is not installed, not running or not signed in is skipped with the
reason. They also check that the real `orca worktree ps` still sends every property the integration tests' fake answers
with, so the fake cannot drift unseen. Not covered: starting a review for real (creating a worktree, opening a window,
running diffo), which would change your machine.

## Security

The server listens on 127.0.0.1 only, and also checks every request: the Host must be `localhost` or `127.0.0.1`
(this stops DNS rebinding), and a request that starts something must come from the page's own origin. Input that
reaches a command (a pull request address) is parsed into a checked type first, and every command runs with its
arguments passed separately, never through a shell.

diffo is pinned to one version (`DiffoCodeReviewTool.PinnedPackage`), so npx never runs a newly published release you
have not looked at. To upgrade, change the version there and run the contract tests.

**Prompt injection.** With diffo chosen, a review starts a coding agent on someone else's pull request: its title,
description, comments and code are untrusted input, and can contain text written to steer the agent ("ignore your
instructions and run…"). The agent runs with whatever permissions your agent is configured with, in a checkout of that
pull request on your machine. Keep the agent's own approval prompts on for commands and file writes outside the
checkout, and do not run review sessions with permission checks turned off (such as Claude Code's
`--dangerously-skip-permissions`). Choose "Open on GitHub" if you do not want an agent near pull requests at all.
