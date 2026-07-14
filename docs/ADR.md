# Architecture Decision Records — Savepoint

Each ADR captures a decision made during the design of Savepoint, the context that led to it, and its consequences.

---

## ADR-0001 — Rename "Checkpoint" to "Savepoint"

**Status:** Accepted

**Context:**
The original name "Checkpoint" is generic and collides with well-established terms in other domains (ML model checkpoints, Check Point the firewall vendor), hurting discoverability on GitHub and app stores.

**Decision:**
Rename the application and all related entities (the engine, individual scheduled alerts, feature names) from "Checkpoint" to "Savepoint." The naming is applied consistently across the app, the CRUD entity, and the internal engine ("Savepoint Engine").

**Consequences:**
- Better search/discoverability.
- Keeps the video-game "save your progress" metaphor already central to the product's philosophy.
- All documentation and future code must use "savepoint" consistently for the entity, not "checkpoint."

---

## ADR-0002 — Native AOT over self-contained deployment

**Status:** Accepted, pending validation spike

**Context:**
A .NET desktop app targeting Linux and Windows without requiring the user to install a runtime separately was an initial concern. Self-contained deployment (`dotnet publish --self-contained`) solves this but produces larger binaries and has slower cold-start (JIT warm-up). Native AOT produces smaller binaries and near-instant startup, which matters for an app whose core UX depends on the overlay feeling immediate.

**Decision:**
Target Native AOT for the release builds instead of self-contained deployment.

**Consequences:**
- Faster startup, smaller binaries, no runtime dependency — all desirable for this app.
- AOT does not cross-compile: CI needs a dedicated build job per target platform (Linux, Windows, later macOS).
- AOT/trimming has known friction points with reflection-heavy code. A one-day validation spike (fullscreen overlay across all detected monitors, on Linux and Windows) is required before this decision is fully locked in for v0.1.
- If the spike reveals blocking issues in Avalonia's multi-monitor/window management services (the one identified risk area not under the project's direct control), self-contained deployment is the documented fallback, with a possible migration to AOT in v0.2.

---

## ADR-0003 — No MVVM; use code-behind

**Status:** Accepted

**Context:**
MVVM (with a toolkit like CommunityToolkit.Mvvm or ReactiveUI) is justified when an app has multiple complex views, heavy shared state, and a need to test presentation logic in isolation. Savepoint's UI surface is limited to a CRUD window and an overlay with a simple state machine (pending → visible → recovery → unlocked). This does not justify the indirection of a binding engine, and reflection-based binding introduces unnecessary AOT/trimming risk (see ADR-0002).

**Decision:**
Use plain Avalonia code-behind for all views instead of adopting an MVVM framework.

**Consequences:**
- Simpler codebase, fewer layers, faster to reason about for this app's actual scope.
- Removes one of the main sources of AOT/trimming risk (reflection-based bindings).
- If the app's UI complexity grows significantly beyond the current scope, this decision should be revisited.

---

## ADR-0004 — Manual JSON serialization for Import/Export

**Status:** Accepted

**Context:**
`System.Text.Json`'s default `JsonSerializer.Serialize<T>` path relies on reflection, which is a known friction point under aggressive trimming/AOT. The data model for savepoints is simple (name, icon, schedule, level, snooze count), making a source-generator context unnecessary overhead for the problem.

**Decision:**
Implement Import/Export serialization manually using `Utf8JsonWriter` and `JsonDocument`, rather than the reflection-based `JsonSerializer` API.

**Consequences:**
- Removes another AOT/trimming risk without needing source generators.
- Slightly more code to write and maintain by hand, but the model is simple enough that this is a small, contained cost.

---

## ADR-0005 — No ORM; plain ADO.NET over SQLite

**Status:** Accepted

**Context:**
The application's data layer is a basic CRUD over a small set of entities (savepoints and their configuration). An ORM or query wrapper would add a layer of abstraction, dependencies, and potential reflection usage that this scope does not justify.

**Decision:**
Access SQLite directly via plain ADO.NET (`Microsoft.Data.Sqlite` or equivalent), without an ORM.

**Consequences:**
- Removes a class of AOT/trimming risk entirely from the data layer.
- Keeps the persistence layer simple and dependency-light, matching the actual complexity of the CRUD.

---

## ADR-0006 — Risk-context awareness instead of a Do Not Disturb mode

**Status:** Accepted

**Context:**
Interrupting a user mid-video-call, mid-screen-share, or mid-presentation carries a cost beyond hyperfocus (professional/social damage) that has nothing to do with the app's core purpose. A generic "Do Not Disturb" mode was initially proposed but rejected: it contradicts the app's philosophy by giving the user a way to suppress interruptions, which is precisely what the app exists to prevent.

**Decision:**
Extend the existing "wait for a clearing signal" mechanism (already used for keyboard activity) to also cover risk contexts: active screen sharing, exclusive fullscreen presentation mode, and active video calls. The overlay trigger is deferred until the risk context clears — the checkpoint/savepoint is never dismissed, paused, or suppressed.

**Consequences:**
- Prevents reputational harm from mistimed interruptions without introducing any user-facing "silence everything" control.
- Requires OS-level probes to detect screen sharing / call / presentation state per platform (Linux, Windows, later macOS) — additional platform-specific implementation work.
- **Technical caveat:** this is the highest-risk item in the entire project from an implementation standpoint. Reliable, automatic detection of screen sharing or active calls is not guaranteed on every platform or desktop environment — for instance, Wayland compositors restrict the window/process information available to background applications in ways X11 does not, which can make automatic detection partially or fully infeasible depending on the user's setup. The feature must degrade gracefully: where automatic detection isn't reliable, a manual exclusion list or a manual "I'm in a call" toggle should be offered as a fallback, and the feature should not be marketed as universally automatic.

---

## ADR-0007 — Button repositioning is non-negotiable; no accessibility opt-out

**Status:** Accepted

**Context:**
The confirmation button's position changes within a safe central area each time the overlay appears, specifically to force conscious visual search and motor action, breaking autopilot dismissal via muscle memory. An accessibility opt-out for users with motor difficulties was proposed and explicitly rejected.

**Decision:**
The button repositioning mechanic has no configuration option and no exceptions. If this mechanic is a barrier for a given user, that user is not the target audience for Savepoint.

**Consequences:**
- Keeps the anti-autopilot mechanism intact and consistent for the app's actual target audience (people who lose themselves in hyperfocus).
- Explicitly narrows the target audience — this is a conscious tradeoff, not an oversight.

---

## ADR-0008 — Snooze limits are defined by interruption level, moved to v0.1

**Status:** Accepted

**Context:**
An unrestricted snooze mechanism would let a hyperfocused user (the app's exact target user) snooze indefinitely, reintroducing the original problem the app exists to solve. Differentiating urgency between savepoint types (e.g. drinking water vs. taking medication) is more fundamental to the app's core value than personalization features like themes or sounds.

**Decision:**
Every savepoint has an interruption level — Gentle, Standard, or Critical — with snooze behavior defined per level:
- Gentle: no snooze, only dismiss/ignore.
- Standard: max 3 snoozes.
- Critical: no snooze.

This is included in **v0.1**, not deferred to a later release.

**Consequences:**
- Prevents indefinite snoozing from undermining the app's core promise.
- Requires the scheduler and overlay state machine to be level-aware from the first release, slightly increasing v0.1 scope compared to a flat, level-less design.

---

## ADR-0009 — No "pause all" / "snooze all" mechanism

**Status:** Accepted

**Context:**
A "pause checkpoints" / "snooze all" tray option was proposed as a relief valve for situations like important meetings, but was rejected on the same grounds as ADR-0006: it is a Do Not Disturb mode by another name, and gives the exact user the app targets an easy way to disable the app's entire purpose from the tray menu.

**Decision:**
No global pause or snooze-all feature exists anywhere in the app. The only legitimate ways to stop interruptions are: quitting the app entirely, or disabling a specific savepoint from the CRUD. Both require a conscious, deliberate action.

**Consequences:**
- Preserves the app's core guarantee that interruptions cannot be casually waved away.
- Users who want temporary relief have no built-in "soft" option — they must make an explicit, visible decision (quit or disable), which is intentional.

---

## ADR-0010 — Tray behavior and application lifecycle

**Status:** Accepted

**Context:**
The app needs to behave as a persistent background process without being accidentally closed, while still being discoverable and simple to interact with.

**Decision:**
- First run shows the CRUD window.
- Closing the CRUD window sends the app to the tray; it never quits.
- The app auto-starts with the system after first run (configurable, opt-out available).
- The only way to quit the app is via "Exit" in the tray context menu (Show, About, Exit).
- The tray icon is static — its mere presence signals the app is active; no dynamic state indicators.
- Double-clicking the tray icon opens the CRUD window.
- The tray is completely inaccessible while an overlay is active — no escape route during a savepoint.
- If the user closes the window in first run without creating any savepoint, a one-time notice explains the app is still running in the tray.

**Consequences:**
- Removes any accidental or convenient way to disable the app's monitoring.
- Keeps the tray interface intentionally minimal, consistent with the app's "no unnecessary friction, no unnecessary escape hatches" philosophy.

---

## ADR-0011 — Statistics feature removed from scope

**Status:** Accepted (supersedes earlier draft that included it in v0.2)

**Context:**
A lightweight statistics/history feature (meals acknowledged, water consistency, snooze patterns, etc.) was part of an earlier draft. On review, it was judged to be outside the app's core scope — Savepoint's purpose is interruption and attention recovery, not habit tracking or productivity analytics — and would add complexity without serving that purpose.

**Decision:**
Remove the Statistics feature entirely from the product scope and the roadmap.

**Consequences:**
- Reduces v0.2 scope and long-term maintenance surface.
- Keeps the app's feature set tightly aligned with its stated mission, avoiding scope creep into adjacent but unrelated product categories (habit trackers, analytics dashboards).

---

## ADR-0012 — Multi-monitor support and Risk-context awareness each moved to their own dedicated version

**Status:** Accepted (supersedes earlier roadmap placement in v0.1, and a subsequent draft that bundled both into a single v0.2)

**Context:**
Both multi-monitor overlay rendering and risk-context awareness (detecting active calls, screen sharing, and presentations across platforms) carry meaningful implementation complexity: the former depends on Avalonia's platform-specific window management services and is the one identified risk area for Native AOT (see ADR-0002); the latter requires per-OS probes into call/screen-share/presentation state, with no shared cross-platform API, and is the single highest technical-risk item in the project (see ADR-0006's technical caveat — e.g. Wayland's restrictions on background-app window/process access). Bundling both into v0.1, or even into a single shared follow-up release, risked letting the harder, less predictable feature (risk-context awareness) block or delay the more contained one (multi-monitor support), and made it harder to ship and validate each independently.

**Decision:**
Move Multi-monitor support and Risk-context awareness out of v0.1, into two separate, dedicated releases:
- **v0.2 — Multi-monitor Support**, which also includes the AOT + multi-monitor validation spike (see ADR-0002).
- **v0.3 — Risk-context Awareness**, including the manual exclusion/toggle fallback (see NFR-3.3).

v0.1 ships with a single-monitor overlay and no risk-context deferral. Personalization moves from v0.2 to v0.4, and Smarter Savepoints moves from v0.3 to v0.5.

**Consequences:**
- v0.1 becomes lighter and faster to ship, focused on validating the core scheduling → overlay → recovery → tray loop on a single monitor.
- Each of the two complex features can be scoped, built, and validated independently, without one's uncertainty (particularly risk-context awareness's platform limitations) blocking the other's release.
- The AOT validation spike for multi-monitor rendering (see ADR-0002) no longer blocks v0.1; it becomes a prerequisite for v0.2 instead.
- Until v0.2 ships, v0.1 users on multi-monitor setups will only see the overlay on their primary monitor. Until v0.3 ships, interruptions can still occur during calls/screen shares/presentations. Both are accepted, temporary gaps, not silent omissions.
- The overall roadmap grows by one additional version (v0.1 through v0.5 instead of v0.1 through v0.4), which is an accepted cost of shipping incrementally and de-risking each feature on its own.

---

## ADR-0013 — Single project instead of five class libraries

**Status:** Accepted

**Context:**
The initial scaffold split the codebase into five separate class library projects (`Savepoint.App`, `.UI`, `.Engine`, `.Platform`, `.Data`), each with its own `.csproj`. This contradicts the project's own precedent (ADR-0003, ADR-0004, ADR-0005: no MVVM, no ORM, no reflection-based serialization) of rejecting layers the app's actual scope doesn't justify. Savepoint ships as a single Native AOT executable; there is no second executable or plugin host that needs to share `Engine`, no team large enough to need hard compile-time boundaries, and multi-assembly AOT trimming is strictly more fragile than single-assembly trimming (compounding the risk ADR-0002 already flags). The scaffold also pre-created folders for v0.2/v0.3/v0.4 features (`RiskContext/`, `RiskContextProbes/`, `MonitorDetection/`, `ImportExport/`) before any of that code exists.

**Decision:**
Use a single project, `Savepoint`, with folders (`Ui/`, `Engine/`, `Platform/`, `Data/`) providing the same logical separation without assembly boundaries. Folders for features not yet in scope are created when that version's work starts, not ahead of time.

**Consequences:**
- One `.csproj`, one set of AOT/trimming settings, no cross-project reference management.
- Logical boundaries are enforced by convention and code review instead of the compiler; acceptable given the app's single-executable, small-scope nature.
- If a second executable (e.g. a CLI) or a plugin surface is ever introduced, `Engine`/`Data` can be extracted into their own project(s) at that point — this decision should be revisited if that happens.

---

## ADR-0014 — MIT License

**Status:** Accepted

**Context:**
Savepoint is an open-source project (see Overview in `PROJECT_SCAFFOLD.md`). A license needs to be chosen and published before external contributors or users can rely on the code.

**Decision:**
License the project under the MIT License.

**Consequences:**
- Permissive terms (use, copy, modify, merge, publish, distribute, sublicense, sell) with no copyleft obligation, maximizing ease of adoption and contribution.
- No warranty or liability is assumed by the authors.
- The full license text lives in `LICENSE` at the repository root.

---

