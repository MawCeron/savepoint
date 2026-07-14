# Savepoint — Project Scaffold

## Overview

Savepoint is an open-source desktop **Attention Interrupt System** for people who experience deep hyperfocus. It creates deliberate, hard-to-ignore interruptions at scheduled moments, gives the user a recovery window to regain conscious attention, then lets them continue.

This document defines the project structure, functional and non-functional requirements, and the roadmap, derived from the product decisions documented in `ADR.md`.

---

## Tech Stack

| Layer | Choice |
|---|---|
| UI Framework | Avalonia UI |
| UI Pattern | Code-behind (no MVVM) |
| Runtime | .NET, Native AOT |
| Data Access | Plain ADO.NET (no ORM) |
| Storage | SQLite |
| Serialization | Manual JSON (`Utf8JsonWriter` / `JsonDocument`) |
| Platforms (initial) | Linux, Windows |
| Platforms (planned) | macOS |

See `ADR.md` for the reasoning behind each of these choices.

---

## Proposed Folder Structure

```
savepoint/
├── src/
│   └── Savepoint/                     # single project, Native AOT executable (see ADR-0013)
│       ├── Program.cs
│       ├── TrayIcon/
│       ├── App.axaml(.cs)
│       │
│       ├── Ui/                        # Avalonia views (code-behind, no MVVM)
│       │   ├── Crud/                  # Savepoint list, create/edit forms
│       │   ├── Overlay/               # Fullscreen interruption overlay
│       │   └── Settings/
│       │
│       ├── Engine/                    # Core domain logic
│       │   ├── Scheduler/             # Daily / weekly / interval / one-time triggers
│       │   └── StateMachine/          # pending -> visible -> recovery -> unlocked; level-aware snooze rules (ADR-0008)
│       │
│       ├── Platform/                  # OS-specific services
│       │   ├── InputMonitoring/       # keyboard idle detection
│       │   └── Autostart/
│       │
│       └── Data/                      # SQLite access
│           └── SavepointRepository.cs
│
├── tests/
│   └── Savepoint.Tests/
│
├── spikes/
│   └── aot-multimonitor-overlay/      # Validation spike (see ADR-0002)
│
├── docs/
│   ├── ADR.md
│   └── PROJECT_SCAFFOLD.md
│
└── build/
    ├── linux/
    └── windows/
```

Folders for v0.2+ features (multi-monitor detection, risk-context probes, import/export) are added when those versions start, not scaffolded ahead of time — see ADR-0013.

---

## Functional Requirements

### FR-1 — Savepoint CRUD
- FR-1.1: The user can create, edit, delete, and list savepoints.
- FR-1.2: Each savepoint has: name, icon, schedule type, interruption level.
- FR-1.3: Supported schedule types: daily, weekly, interval, one-time.
- FR-1.4: The CRUD window is shown automatically on first run.

### FR-2 — Scheduling & Triggering
- FR-2.1: The scheduler evaluates due savepoints and marks them as *pending*.
- FR-2.2: A pending savepoint does not trigger the overlay while keyboard activity is detected.
- FR-2.3: After 2 seconds without typing, the overlay is shown.
- FR-2.4 *(v0.3)*: If a risk context is active (screen share, call, exclusive fullscreen presentation), the overlay trigger is deferred until the risk context clears. Detection reliability is platform-dependent (see NFR-3.3); where automatic detection isn't feasible, a manual exclusion/toggle fallback may be offered instead.

### FR-3 — Overlay & Recovery Window
- FR-3.1: The overlay is fullscreen. In v0.1 it renders on the primary monitor only; rendering on all detected monitors is deferred to v0.2 (see NFR-4.2).
- FR-3.2: On appearing, the overlay starts an 8-second recovery window.
- FR-3.3: The confirmation button is disabled during the recovery window.
- FR-3.4: After the recovery window, the confirmation button becomes enabled.
- FR-3.5: The confirmation button's position changes within a safe central area every time it is shown (non-configurable, non-negotiable).
- FR-3.6: The tray is inaccessible while an overlay is active.

### FR-4 — Interruption Levels & Snooze
- FR-4.1: Every savepoint has an interruption level: Gentle, Standard, or Critical.
- FR-4.2: Gentle savepoints cannot be snoozed — they can only be dismissed/ignored.
- FR-4.3: Standard savepoints can be snoozed up to 3 times.
- FR-4.4: Critical savepoints cannot be snoozed under any circumstance.

### FR-5 — Tray & Application Lifecycle
- FR-5.1: Closing the CRUD window sends the app to the tray; it never quits the app.
- FR-5.2: The app auto-starts with the system after first run (configurable, opt-out in settings).
- FR-5.3: The tray icon is static — no dynamic state indicators.
- FR-5.4: Double-clicking the tray icon opens the CRUD window.
- FR-5.5: The tray context menu offers exactly: Show, About, Exit.
- FR-5.6: There is no "pause all" or "snooze all" mechanism anywhere in the app.
- FR-5.7: If the user closes the CRUD window in first run without creating any savepoint, a one-time notice informs them the app is still running in the tray.
- FR-5.8: Quitting the app is only possible via Exit in the tray menu.

### FR-6 — Import / Export (v0.4)
- FR-6.1: The user can export all savepoints to a JSON file.
- FR-6.2: The user can import savepoints from a previously exported JSON file.
- FR-6.3: Serialization/deserialization is implemented manually (no reflection-based serializer).

---

## Non-Functional Requirements

### NFR-1 — Privacy & Data Ownership
- NFR-1.1: No user accounts.
- NFR-1.2: No cloud dependency — fully offline.
- NFR-1.3: No telemetry by default.
- NFR-1.4: All data is stored locally (SQLite).

### NFR-2 — Performance
- NFR-2.1: The app must start near-instantly (Native AOT, no JIT warm-up).
- NFR-2.2: The overlay must render without perceptible delay once triggered.
- NFR-2.3: Idle resource usage (tray-only state) must be minimal (background scheduler only).

### NFR-3 — Portability
- NFR-3.1: The app must run on Linux and Windows without requiring a separately installed .NET runtime.
- NFR-3.2: macOS support is planned but not required for v0.1–v0.5.
- NFR-3.3: Risk-context detection (screen share, call, presentation) capabilities are inherently platform- and desktop-environment-dependent (e.g. Wayland restricts background-app access to window/process information in ways X11 does not). The app must not assume uniform detection capability across platforms, and must support a manual exclusion/toggle fallback where automatic detection isn't reliable.

### NFR-4 — Build & CI
- NFR-4.1: Each target platform is built natively (AOT does not cross-compile) — CI requires a Linux runner and a Windows runner.
- NFR-4.2: Multi-monitor overlay rendering under Native AOT must be validated via a spike before being locked into v0.2's scope (see ADR-0002 and ADR-0012). This no longer blocks v0.1, since v0.1 ships single-monitor only.

### NFR-5 — Maintainability
- NFR-5.1: No ORM or reflection-heavy frameworks (avoids AOT/trimming fragility, keeps the CRUD's actual complexity honest).
- NFR-5.2: No MVVM/binding engine — UI logic lives in code-behind given the app's limited view surface.

### NFR-6 — Design Integrity
- NFR-6.1: No feature may provide a way to bypass, pause, or globally snooze interruptions — any such feature request must be rejected at the design stage, not just the implementation stage.
- NFR-6.2: Out-of-scope features (e.g. statistics/analytics) must not be added even if easy to implement, if they don't serve the app's core interruption mechanism.

---

## Roadmap

### v0.1 — First Savepoint
- Daily, weekly, interval, and one-time savepoints (see ADR-0015)
- Intelligent trigger timing
- Interruption levels (Gentle / Standard / Critical) with level-based snooze rules
- Fullscreen overlay (single monitor)
- Recovery window
- Tray lifecycle (autostart, tray-only exit, first-run notice)
- Settings

### v0.2 — Multi-monitor Support
- Multi-monitor support
- AOT + multi-monitor validation spike (see ADR-0002 and ADR-0012)

### v0.3 — Risk-context Awareness
- Risk-context awareness (calls, screen sharing, presentations)
- Manual exclusion/toggle fallback where automatic detection isn't reliable (see NFR-3.3)

### v0.4 — Personalization
- Themes
- Categories
- Better animations
- Sounds
- Import / Export

### v0.5 — Smarter Savepoints
- Context-aware savepoints (beyond risk-context detection)
- Session summaries
- Backup & Sync

---

## Open Validation Items

- [ ] Spike: Native AOT build with fullscreen overlay across all detected monitors, on Linux and Windows (see ADR-0002 and ADR-0012). Blocks locking in multi-monitor support for v0.2; self-contained deployment is the fallback if it fails. Does not block v0.1, which ships single-monitor only.
