# Phase 7 - History + Console Accounts

## Goal

Build a durable gaming-history layer without treating historical or console ownership as locally installed games and without scraping account passwords/session cookies.

## Implemented import paths

### Steam live sync

Phase 7 uses Valve's consumer Steam Web API:

- interface: `IPlayerService`
- method: `GetOwnedGames`
- endpoint host used by the launcher: `api.steampowered.com`
- requested data: AppID, game name, cumulative playtime, last-played timestamp when returned

Requirements:

- numeric SteamID64
- Steam Web API key
- account game details visible to the requesting key/account

Security:

- Steam password is never requested
- API key is protected with Windows DPAPI / CurrentUser scope
- key is not written to logs or GitHub
- disconnect removes credentials but keeps imported history

### PlayStation Data Access Excel

Sony's current Data Access flow downloads an Excel workbook.

The parser:

- accepts `.xlsx` only when the selected source is PlayStation
- scans worksheets for a game-related sheet/header combination
- recognizes common title, external ID, platform, playtime, and last-played columns
- avoids importing generic unrelated account sheets based only on a `Name` column
- reports failure when no recognizable game-history table exists

The parser does not claim every Sony workbook contains game-history or playtime fields.

### Generic CSV/JSON

CSV/JSON import is available for:

- PlayStation
- Xbox
- Microsoft Store
- GOG
- EA
- Ubisoft
- Battle.net
- Epic
- Steam fallback

Header/property matching is flexible and case/punctuation-insensitive.

Common aliases include:

- `title`, `game title`, `game name`, `product name`
- `game id`, `appid`, `product id`, `title id`
- `platform`, `system`, `device`, `console`
- playtime fields containing playtime/time-played/hours/minutes
- last-played fields

Playtime parsing supports:

- explicit seconds
- explicit minutes
- explicit hours
- colon durations
- text such as `12h 30m`

A unitless numeric `playtime` field is intentionally not guessed.

## Storage model

Historical account records live in a dedicated `game_history` table.

Identity:

- source + external ID is unique
- if an imported record has no external ID, a deterministic fallback ID is derived from source + normalized title + platform

Snapshot merge rules:

- playtime keeps the maximum known cumulative value
- last played keeps the latest known timestamp
- missing new values do not erase known values
- source/platform/title metadata can refresh

## History view

The Gaming History view combines:

- imported account records
- locally installed games
- launcher-tracked playtime

It displays:

- game title
- imported account sources
- known account playtime
- launcher-tracked playtime
- last known played date
- current installed status

Important: account playtime and launcher-tracked playtime are displayed separately because they can overlap.

## Automatic behavior

When Steam Auto Sync is enabled:

1. normal installed-library discovery runs;
2. Hatchable auto-sync runs independently;
3. Steam history sync runs independently;
4. Steam failure becomes a status warning only;
5. launcher/library operation continues normally.

## Intentional limits

- No PlayStation password/token scraping.
- No unofficial PSN session-cookie integration.
- No Xbox account-token interception.
- No EA/Ubisoft/Battle.net/GOG credential scraping.
- No claim that privacy exports necessarily contain complete playtime.
- No fuzzy cross-source title matching.
- No imported account playtime is turned into synthetic launcher sessions.
- Phase 7 does not yet import trophies/achievements; the primary scope is owned/played-game history and available playtime metadata.

## Adding new source-specific importers

A source-specific importer should implement `IHistoryFileParser` or `ISteamHistoryClient`-style API integration and must:

- operate read-only against the account source
- avoid raw passwords/session-cookie scraping
- preserve source/external IDs when available
- keep cumulative history idempotent
- include parser/API regression tests
- fail clearly when source format changes
