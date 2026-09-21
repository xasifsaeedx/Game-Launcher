# Phase 5 - Hatchable Sync

## Implemented on the Windows launcher

- Configurable Hatchable base URL with the user's My Game Library deployment as the default.
- Device-token authentication; no Hatchable browser cookies are copied into the launcher.
- Windows DPAPI encryption for the locally stored launcher token.
- Local SQLite cache of the remote Next 100 list and state.
- Remote matching:
  1. Steam App ID.
  2. Exact normalized title fallback.
- Next 100 rank and synced state displayed on matching installed game cards.
- Full Next Play window showing all ranked games, platform, preference, progress, rating, playtime, and whether each game is installed locally.
- Progress and rating editing from the desktop launcher.
- Cumulative local playtime and last-played upload.
- Optional automatic sync at startup/library refresh and after a completed game session.
- Sync failures are isolated from local launching/session tracking.

## Implemented on My Game Library (Hatchable)

- Secure launcher-device token creation/revocation page at `/launcher`.
- Only token hashes are stored server-side.
- Public token-authenticated `/api/launcher-sync` endpoint for desktop use.
- Member-only progress/rating endpoint for the existing website.
- Per-member launcher state:
  - progress status
  - 1-10 rating
  - cumulative playtime
  - last played
- Existing Next 100 detail cards now show/edit launcher progress and rating and display synced playtime.
- Existing green/yellow/red Played / Play next / Didn't like workflow remains intact.

## Data ownership

Hatchable owns:
- Next 100 ranking.
- Existing preference status (played / next / dislike).
- Remote progress/rating state.

Launcher owns:
- Locally measured play sessions.
- Locally measured cumulative playtime.
- Locally measured last-played timestamp.

Merge rules:
- Remote playtime uses the maximum cumulative value so a stale client does not reduce history.
- Progress/rating can be explicitly cleared.
- A first local session may initialize progress to Playing.
- Completed maps preference to Played unless the user explicitly marked Didn't like.

## Security

- Launcher tokens are random 256-bit values with a `gl_` prefix.
- Hatchable stores SHA-256 token hashes only.
- Desktop stores the token through Windows DPAPI / CurrentUser scope.
- Tokens are never committed to GitHub.
- Device tokens can be revoked from the Hatchable Launcher Sync page.

## Intentional limits

- Phase 5 does not import historical PSN/EA/Ubisoft/etc. account play history; that remains Phase 7.
- Matching is deliberately conservative. There is no fuzzy automatic game matching.
- There is no real-time WebSocket dependency; sync occurs explicitly/automatically at safe lifecycle points.
- The launcher does not overwrite the website's Next 100 rank.
