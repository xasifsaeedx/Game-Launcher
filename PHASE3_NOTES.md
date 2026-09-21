# Phase 3 - Smart Launching

## Implemented

- Persistent multiple launch profiles per game.
- Default one-click profile selection.
- Optional profile-specific installation selection for multi-store games.
- Optional game launch-argument override.
- Ordered pre-launch actions.
- Ordered companion programs that run alongside the game.
- Optional automatic companion termination after game exit.
- Ordered post-game actions.
- Pre/post actions may optionally block until the program exits.
- Profile editor integrated into every library card.
- Normal Play falls back to the existing direct launch when no default profile exists.
- Launch profiles are stored in the same local SQLite database as the game library.
- Phase 3 tests cover persistence, one-default behavior across merged installations, execution order, companion cleanup, argument override, and existing Phase 0-2 behavior.

## Execution order

1. Run enabled PreLaunch actions in order.
2. Start enabled Companion actions in order.
3. Launch and track the selected game installation.
4. When the tracked game process exits, stop companions marked CloseWithGame.
5. Run enabled PostGame actions in order.
6. Persist the completed play session.

## Intentional limits

- Phase 3 runs trusted local executables chosen by the user; it does not download or install companion applications.
- Companion cleanup uses normal Windows process-tree termination for companions explicitly configured to close with the game.
- There is no scripting language or shell-command engine. Actions are executable + arguments + working directory only.
- Profiles do not yet control the gameplay overlay; RTSS/overlay behavior belongs to Phase 4.
