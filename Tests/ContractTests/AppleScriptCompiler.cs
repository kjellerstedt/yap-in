namespace YapIn.ContractTests;

internal static class AppleScriptCompiler
{
    /// <summary>Compiles the script lines with osacompile (it resolves the app's terms but runs nothing).</summary>
    public static async Task AssertCompilesAsync(IReadOnlyList<string> scriptLines)
    {
        RealTools.SkipUnlessOnPath("osacompile");
        var outputDirectory = Directory.CreateTempSubdirectory("yap-in-osacompile-");
        try
        {
            var arguments = new[] { "-o", Path.Combine(outputDirectory.FullName, "script.scpt") }.Concat(scriptLines.SelectMany(line => new[] { "-e", line })).ToList();
            var result = await RealTools.RunAsync("osacompile", arguments);
            Assert.True(result.Succeeded, $"osacompile rejected the script:\n{result.StandardError}");
        }
        finally
        {
            outputDirectory.Delete(recursive: true);
        }
    }
}
