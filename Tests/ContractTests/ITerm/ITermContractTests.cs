using YapIn.Infrastructure.Adapters.ITerm;

namespace YapIn.ContractTests.ITerm;

/// <summary>Against the real AppleScript compiler and iTerm's own scripting dictionary. Compiles only; opens nothing.</summary>
public sealed class ITermContractTests
{
    [Fact]
    public async Task Our_script_compiles_against_iterms_scripting_dictionary()
    {
        // Arrange
        RealTools.SkipUnlessInstalled("/Applications/iTerm.app");

        // Act and assert
        await AppleScriptCompiler.AssertCompilesAsync(ITermSessionHost.Script);
    }
}
