using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Core.Services;

public sealed class SmartLaunchService
{
    private readonly LaunchProfileService _profiles;
    private readonly GameSessionService _sessions;
    private readonly IExternalProgramRuntime _programRuntime;

    public SmartLaunchService(
        LaunchProfileService profiles,
        GameSessionService sessions,
        IExternalProgramRuntime programRuntime)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _programRuntime = programRuntime ?? throw new ArgumentNullException(nameof(programRuntime));
    }

    public async Task<PlaySession> LaunchAsync(
        GameLibraryItem item,
        Guid? profileId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        LaunchProfileDetails? details = profileId.HasValue
            ? await _profiles.GetProfileAsync(profileId.Value, cancellationToken)
            : await _profiles.GetDefaultProfileAsync(item, cancellationToken);

        if (details is null)
        {
            var installation = item.PreferredInstallation
                ?? throw new InvalidOperationException("No installed copy is available for this game.");

            return await _sessions.LaunchAndTrackAsync(installation, cancellationToken);
        }

        var relatedIds = LaunchProfileService.GetRelatedGameIds(item);
        if (!relatedIds.Contains(details.Profile.GameId))
        {
            throw new InvalidOperationException("The selected launch profile belongs to a different game.");
        }

        var installationToLaunch = SelectInstallation(item, details.Profile);
        if (!string.IsNullOrWhiteSpace(details.Profile.GameArgumentsOverride))
        {
            installationToLaunch = installationToLaunch with
            {
                LaunchArguments = details.Profile.GameArgumentsOverride,
                LaunchUri = !string.IsNullOrWhiteSpace(installationToLaunch.ExecutablePath)
                    ? null
                    : installationToLaunch.LaunchUri
            };
        }

        var enabledActions = details.Actions
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.SortOrder)
            .ToArray();

        foreach (var action in enabledActions.Where(x => x.Stage == LaunchActionStage.PreLaunch))
        {
            await RunOneShotActionAsync(action, cancellationToken);
        }

        var companions = new List<(LaunchAction Action, IExternalProgramHandle Handle)>();
        try
        {
            foreach (var action in enabledActions.Where(x => x.Stage == LaunchActionStage.Companion))
            {
                var handle = await _programRuntime.StartAsync(action, cancellationToken);
                companions.Add((action, handle));
            }

            var session = await _sessions.LaunchAndTrackAsync(
                installationToLaunch,
                cancellationToken);

            await StopCompanionsAsync(companions);

            foreach (var action in enabledActions.Where(x => x.Stage == LaunchActionStage.PostGame))
            {
                await RunOneShotActionAsync(action, cancellationToken);
            }

            return session;
        }
        catch
        {
            await StopCompanionsAsync(companions);
            throw;
        }
        finally
        {
            foreach (var (_, handle) in companions)
            {
                await handle.DisposeAsync();
            }
        }
    }

    private async Task RunOneShotActionAsync(
        LaunchAction action,
        CancellationToken cancellationToken)
    {
        await using var handle = await _programRuntime.StartAsync(action, cancellationToken);
        if (action.WaitForExit)
        {
            await handle.WaitForExitAsync(cancellationToken);
        }
    }

    private static async Task StopCompanionsAsync(
        IReadOnlyList<(LaunchAction Action, IExternalProgramHandle Handle)> companions)
    {
        foreach (var companion in companions.Reverse())
        {
            if (!companion.Action.CloseWithGame || !companion.Handle.IsRunning) continue;

            try
            {
                await companion.Handle.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Companion cleanup is best effort and must not destroy a completed game session.
            }
        }
    }

    private static GameInstallation SelectInstallation(
        GameLibraryItem item,
        LaunchProfile profile)
    {
        if (profile.InstallationId.HasValue)
        {
            var selected = item.Installations.FirstOrDefault(
                x => x.Id == profile.InstallationId.Value && x.IsInstalled);

            if (selected is not null) return selected;
        }

        return item.PreferredInstallation
            ?? throw new InvalidOperationException("No installed copy is available for this game.");
    }
}
