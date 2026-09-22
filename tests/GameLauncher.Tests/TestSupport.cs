namespace GameLauncher.Tests;

internal sealed class FixedAdapter : IGameSourceAdapter
{
    private readonly DiscoveredGame _game;

    private FixedAdapter(string id, GameSource source, string externalId, string title)
    {
        Id = id;
        Source = source;
        DisplayName = id;

        var gameId = StableId.FromText($"{id}-game:{externalId}");
        var installId = StableId.FromText($"{id}-install:{externalId}");
        var now = DateTimeOffset.UtcNow;

        _game = new DiscoveredGame(
            new Game(gameId, title, now, now),
            new GameInstallation(
                installId, gameId, source, externalId,
                $@"C:\Games\{id}\{externalId}",
                null,
                source == GameSource.Steam ? $"steam://rungameid/{externalId}" : null,
                true));
    }

    public string Id { get; }
    public string DisplayName { get; }
    public GameSource Source { get; }

    public static FixedAdapter Create(string id, GameSource source, string externalId, string title) =>
        new(id, source, externalId, title);

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DiscoveredGame>>(new[] { _game });
}

internal sealed class MutableAdapter : IGameSourceAdapter
{
    private readonly string _externalId;
    private readonly string _title;

    public MutableAdapter(string id, GameSource source, string externalId, string title)
    {
        Id = id;
        DisplayName = id;
        Source = source;
        _externalId = externalId;
        _title = title;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public GameSource Source { get; }
    public bool IsInstalled { get; set; } = true;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredGame>>(
                Array.Empty<DiscoveredGame>());
        }

        var gameId = StableId.FromText($"{Id}-game:{_externalId}");
        var installId = StableId.FromText($"{Id}-install:{_externalId}");
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<DiscoveredGame> result =
        [
            new DiscoveredGame(
                new Game(gameId, _title, now, now),
                new GameInstallation(
                    installId,
                    gameId,
                    Source,
                    _externalId,
                    $@"C:\Games\{Id}\{_externalId}",
                    null,
                    null,
                    true))
        ];

        return Task.FromResult(result);
    }
}

internal sealed class StaticHttpHandler : HttpMessageHandler
{
    private readonly byte[] _content;

    public StaticHttpHandler(byte[] content)
    {
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_content)
        };

        return Task.FromResult(response);
    }
}

internal sealed class RecordingGameRuntime : IGameRuntime
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public RecordingGameRuntime(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        _start = start;
        _end = end;
    }

    public GameInstallation? LastInstallation { get; private set; }

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        LastInstallation = installation;
        _events.Add("game:start");
        return Task.FromResult<IGameRunHandle>(
            new RecordingGameRunHandle(_events, _start, _end, installation.ExecutablePath));
    }
}

internal sealed class RecordingGameRunHandle : IGameRunHandle
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _end;

    public RecordingGameRunHandle(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end,
        string? executablePath)
    {
        _events = events;
        StartedUtc = start;
        _end = end;
        DetectedExecutablePath = executablePath;
    }

    public DateTimeOffset StartedUtc { get; }
    public int? ProcessId => 1001;
    public string? DetectedExecutablePath { get; }

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:wait");
        return Task.FromResult(_end);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class RecordingExternalRuntime : IExternalProgramRuntime
{
    private readonly List<string> _events;

    public RecordingExternalRuntime(List<string> events)
    {
        _events = events;
    }

    public Task<IExternalProgramHandle> StartAsync(
        LaunchAction action,
        CancellationToken cancellationToken = default)
    {
        _events.Add($"start:{action.Name}");
        return Task.FromResult<IExternalProgramHandle>(
            new RecordingExternalHandle(_events, action.Name));
    }
}

internal sealed class RecordingExternalHandle : IExternalProgramHandle
{
    private readonly List<string> _events;
    private readonly string _name;

    public RecordingExternalHandle(List<string> events, string name)
    {
        _events = events;
        _name = name;
    }

    public bool IsRunning { get; private set; } = true;

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        _events.Add($"wait:{_name}");
        IsRunning = false;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _events.Add($"stop:{_name}");
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class MemoryOverlaySettingsRepository : IOverlaySettingsRepository
{
    private OverlaySettings _settings;

    public MemoryOverlaySettingsRepository(OverlaySettings settings)
    {
        _settings = settings;
    }

    public Task<OverlaySettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        _settings = settings.Normalize();
        return Task.CompletedTask;
    }
}

internal sealed class RecordingOverlayRuntime : IGameplayOverlayRuntime
{
    private readonly List<string> _events;

    public RecordingOverlayRuntime(List<string> events)
    {
        _events = events;
    }

    public int StartCount { get; private set; }

    public Task<IOverlaySession> StartAsync(
        int processId,
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        StartCount++;
        _events.Add($"overlay:start:{processId}");
        return Task.FromResult<IOverlaySession>(
            new RecordingOverlaySession(_events));
    }

    public Task<OverlayRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OverlayRuntimeStatus(true, "ready"));
}

internal sealed class RecordingOverlaySession : IOverlaySession
{
    private readonly List<string> _events;

    public RecordingOverlaySession(List<string> events)
    {
        _events = events;
    }

    public ValueTask DisposeAsync()
    {
        _events.Add("overlay:dispose");
        return ValueTask.CompletedTask;
    }
}

internal sealed class OverlayAwareGameRuntime : IGameRuntime
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public OverlayAwareGameRuntime(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        _start = start;
        _end = end;
    }

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:start");
        return Task.FromResult<IGameRunHandle>(
            new OverlayAwareGameRunHandle(_events, _start, _end));
    }
}

internal sealed class OverlayAwareGameRunHandle : IGameRunHandle
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _end;

    public OverlayAwareGameRunHandle(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        StartedUtc = start;
        _end = end;
    }

    public DateTimeOffset StartedUtc { get; }
    public int? ProcessId => 4242;
    public string? DetectedExecutablePath => @"C:\Games\Overlay\Game.exe";

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:wait");
        return Task.FromResult(_end);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class PrefixSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => "protected::" + plaintext;

    public string Unprotect(string protectedValue) =>
        protectedValue.StartsWith("protected::", StringComparison.Ordinal)
            ? protectedValue["protected::".Length..]
            : throw new InvalidOperationException("Invalid protected value.");
}

internal sealed class RoutingHttpHandler : HttpMessageHandler
{
    private readonly byte[] _releaseJson;
    private readonly byte[] _installer;
    private readonly byte[] _checksum;

    public RoutingHttpHandler(
        byte[] releaseJson,
        byte[] installer,
        byte[]? checksum = null)
    {
        _releaseJson = releaseJson;
        _installer = installer;
        _checksum = checksum ?? Array.Empty<byte>();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var content = path.EndsWith(
                "GameLauncher-Setup.exe.sha256",
                StringComparison.OrdinalIgnoreCase)
            ? _checksum
            : path.EndsWith(
                "GameLauncher-Setup.exe",
                StringComparison.OrdinalIgnoreCase)
                ? _installer
                : _releaseJson;

        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
    }
}

internal sealed class FakeRuntime : IGameRuntime
{
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public FakeRuntime(DateTimeOffset start, DateTimeOffset end)
    {
        _start = start;
        _end = end;
    }

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IGameRunHandle>(
            new FakeRunHandle(_start, _end, installation.ExecutablePath));
}

internal sealed class FakeRunHandle : IGameRunHandle
{
    private readonly DateTimeOffset _end;

    public FakeRunHandle(DateTimeOffset start, DateTimeOffset end, string? executablePath)
    {
        StartedUtc = start;
        _end = end;
        DetectedExecutablePath = executablePath;
    }

    public DateTimeOffset StartedUtc { get; }
    public int? ProcessId => 1002;
    public string? DetectedExecutablePath { get; }

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_end);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal static class Assert
{
    public static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected condition to be true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void NotEqual<T>(T notExpected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new InvalidOperationException($"Did not expect '{actual}'.");
        }
    }

    public static void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected sequence '{string.Join(", ", expected)}', got '{string.Join(", ", actual)}'.");
        }
    }

    public static void NotNull(object? value)
    {
        if (value is null) throw new InvalidOperationException("Expected a non-null value.");
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GameLauncherTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
