using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;

namespace GameLauncher.Core.Services;

public sealed class LaunchProfileService
{
    private readonly ILaunchProfileRepository _repository;

    public LaunchProfileService(ILaunchProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<LaunchProfileDetails>> GetProfilesAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var profiles = new List<LaunchProfile>();
        foreach (var gameId in GetRelatedGameIds(item))
        {
            profiles.AddRange(await _repository.GetLaunchProfilesAsync(gameId, cancellationToken));
        }

        var details = new List<LaunchProfileDetails>();
        foreach (var profile in profiles
                     .GroupBy(x => x.Id)
                     .Select(x => x.First())
                     .OrderByDescending(x => x.IsDefault)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var actions = await _repository.GetLaunchActionsAsync(profile.Id, cancellationToken);
            details.Add(new LaunchProfileDetails(profile, actions));
        }

        return details;
    }

    public async Task<LaunchProfileDetails?> GetDefaultProfileAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var profiles = await GetProfilesAsync(item, cancellationToken);
        return profiles.FirstOrDefault(x => x.Profile.IsDefault);
    }

    public async Task<LaunchProfileDetails?> GetProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetLaunchProfileAsync(profileId, cancellationToken);
        if (profile is null) return null;

        var actions = await _repository.GetLaunchActionsAsync(profileId, cancellationToken);
        return new LaunchProfileDetails(profile, actions);
    }

    public async Task SaveAsync(
        GameLibraryItem item,
        LaunchProfile profile,
        IReadOnlyList<LaunchAction> actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(actions);

        if (profile.Id == Guid.Empty) throw new ArgumentException("Profile ID cannot be empty.", nameof(profile));
        if (profile.GameId == Guid.Empty) throw new ArgumentException("Game ID cannot be empty.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("Profile name is required.", nameof(profile));

        var relatedIds = GetRelatedGameIds(item);
        if (!relatedIds.Contains(profile.GameId))
        {
            throw new InvalidOperationException("The launch profile is not linked to this game.");
        }

        if (profile.InstallationId.HasValue &&
            item.Installations.All(x => x.Id != profile.InstallationId.Value))
        {
            throw new InvalidOperationException("The selected installation is not part of this game.");
        }

        foreach (var action in actions)
        {
            ValidateAction(profile.Id, action);
        }

        if (profile.IsDefault)
        {
            foreach (var gameId in relatedIds)
            {
                await _repository.ClearDefaultLaunchProfilesAsync(gameId, cancellationToken);
            }
        }

        await _repository.UpsertLaunchProfileAsync(
            profile with { Name = profile.Name.Trim() },
            cancellationToken);

        await _repository.ReplaceLaunchActionsAsync(
            profile.Id,
            actions.OrderBy(x => x.SortOrder).ToArray(),
            cancellationToken);
    }

    public async Task SetDefaultAsync(
        GameLibraryItem item,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var profile = await _repository.GetLaunchProfileAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException("Launch profile not found.");

        var relatedIds = GetRelatedGameIds(item);
        if (!relatedIds.Contains(profile.GameId))
        {
            throw new InvalidOperationException("The launch profile is not linked to this game.");
        }

        foreach (var gameId in relatedIds)
        {
            await _repository.ClearDefaultLaunchProfilesAsync(gameId, cancellationToken);
        }

        await _repository.UpsertLaunchProfileAsync(
            profile with
            {
                IsDefault = true,
                UpdatedUtc = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    public Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteLaunchProfileAsync(profileId, cancellationToken);

    public static IReadOnlySet<Guid> GetRelatedGameIds(GameLibraryItem item)
    {
        var ids = new HashSet<Guid> { item.Game.Id };
        foreach (var installation in item.Installations)
        {
            ids.Add(installation.GameId);
        }

        return ids;
    }

    private static void ValidateAction(Guid profileId, LaunchAction action)
    {
        if (action.Id == Guid.Empty) throw new ArgumentException("Action ID cannot be empty.", nameof(action));
        if (action.ProfileId != profileId) throw new ArgumentException("Action belongs to a different profile.", nameof(action));
        if (string.IsNullOrWhiteSpace(action.Name)) throw new ArgumentException("Action name is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(action.ExecutablePath)) throw new ArgumentException("Action executable is required.", nameof(action));
        if (action.SortOrder < 0) throw new ArgumentOutOfRangeException(nameof(action), "Sort order cannot be negative.");
    }
}
