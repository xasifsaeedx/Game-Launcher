using GameLauncher.Tests;

var failures = new List<string>();

foreach (var test in LauncherTestSuite.All)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        var failure = $"FAIL  {test.Name}: {ex.Message}";
        failures.Add(failure);
        Console.WriteLine(failure);
    }
}

if (failures.Count > 0)
{
    Environment.ExitCode = 1;
    Console.WriteLine($"{failures.Count} test(s) failed.");
    return;
}

Console.WriteLine($"All {LauncherTestSuite.All.Count} launcher tests passed.");
