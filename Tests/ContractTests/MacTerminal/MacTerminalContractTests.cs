using YapIn.Infrastructure.Adapters.MacTerminal;

namespace YapIn.ContractTests.MacTerminal;

/// <summary>Against the real AppleScript compiler and Terminal's own scripting dictionary. Compiles only; opens nothing.</summary>
public sealed class MacTerminalContractTests
{
    [Fact]
    public async Task Our_script_compiles_against_terminals_scripting_dictionary()
    {
        // Arrange
        RealTools.SkipUnlessInstalled("/System/Applications/Utilities/Terminal.app");

        // Act and assert
        await AppleScriptCompiler.AssertCompilesAsync(MacTerminalSessionHost.Script);
    }
}
