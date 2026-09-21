# Phase 4 - Gameplay Overlay

## Implemented

- Global persistent overlay settings.
- Overlay enabled by default.
- Independent FPS, GPU usage, and GPU temperature toggles.
- Configurable telemetry refresh interval with a 500 ms default.
- Actual detected game process ID is exposed by the game runtime.
- Gameplay overlay starts only after the tracked game process has been detected.
- Overlay session is disposed and its OSD slot cleared when the tracked game exits.
- RTSS is located from Windows installed-program records and standard install paths.
- RTSS is automatically started when installed but not already running.
- FPS is read from the RTSS application entry matching the tracked game process ID.
- OSD text is written into a dedicated `GameLauncher.Phase4` RTSS OSD slot.
- GPU usage and temperature are read using LibreHardwareMonitor.
- Overlay failures are isolated from launching and session tracking.
- Overlay settings window reports RTSS availability/path.
- Phase 4 tests cover formatting, settings persistence, disabled behavior, and game-session overlay lifecycle.

## Runtime flow

1. Phase 1/3 game launch detects the actual game process.
2. Its process ID is passed to the gameplay overlay service.
3. If overlay is enabled, RTSS is verified or started.
4. The launcher polls:
   - RTSS per-process framerate.
   - LibreHardwareMonitor GPU load.
   - LibreHardwareMonitor GPU temperature.
5. The formatted OSD is refreshed at the configured interval.
6. When the game process exits, the overlay loop stops and its RTSS OSD slot is cleared.
7. Existing play-session completion continues normally.

## Dependencies

- `LibreHardwareMonitorLib 0.9.6` is a NuGet dependency.
- RTSS is an external runtime prerequisite for rendering the OSD. It is not redistributed by this project.
- MSI Afterburner is not required.

## Intentional limits

- Phase 4 does not bundle or install RTSS.
- The overlay currently contains only the requested FPS, GPU usage, and GPU temperature metrics.
- OSD styling/position continues to use RTSS configuration rather than duplicating RTSS's presentation controls in the launcher.
- Hardware telemetry is best-effort. Unsupported/blocked sensors display `--` rather than preventing gameplay.
- The launcher leaves RTSS itself running after gameplay; only the launcher's OSD entry is removed.
