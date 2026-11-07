# SoulBind: Apostle's Descent — project context

Repository context reviewed on 2026-11-07. This describes the current working tree, including work that has not been committed. It is a handoff for developers, designers, and AI assistants; it does not certify that a release has passed testing.

## Project identity

SoulBind: Apostle's Descent is a turn-based tactical hex-board card battler by Secret Society Studios for 2–6 players. Players control Apostles on an island battlefield, use tarot-inspired Arcana cards, steal Souls, protect their Vessel, and manage Corruption. The visual direction combines gothic fantasy, illustrated cards, pixel animations, and a lava-surrounded island.

Android is the main target, with Windows builds also supported. Players can play offline against bots or join player-hosted online matches. Existing project notes describe APK distribution through Google Drive and work on `develop`, with `main` reserved for releases; verify branch and distribution details before release work.

## Technology and entry points

| Area | Current configuration |
| --- | --- |
| Engine | Unity 6000.6.0f1, C# |
| Rendering | Universal Render Pipeline 17.6.0, uGUI, TextMesh Pro |
| Input | Unity Input System 1.20.0 |
| Networking | Netcode for GameObjects 2.13.3; Multiplayer Services 2.3.3; Unity Transport, Lobby/session and Relay integration |
| Tests | Unity Test Framework 1.8.0 and NUnit EditMode tests |
| Game version | 1.0.6; Android version code 7 |
| Network protocol | 4, in `Assets/Scripts/Utility/BuildInfo.cs` |
| Build scenes | `Assets/Scenes/MainMenu.unity`, then `Assets/Scenes/SampleScene.unity` |

Versions above come from local settings and package manifests. Consult those files when updating this document.

Start from MainMenu for the normal player flow. `MatchSettings` carries menu choices into the match scene. Starting SampleScene directly uses the match controller's Inspector settings when no menu settings have been set.

## Gameplay and implemented rules

The standard board is defined by `Core/StandardBoard.cs`: 14 rows and 170 hex tiles. `HexCoord` and `HexBoard` implement coordinates, distance, and board geometry; `SpawnDraft` and `SpawnPlanner` handle starting positions.

| Rule | Implemented value |
| --- | --- |
| Players | 2–6 |
| Starting / maximum Vessel (health) | 50 |
| Starting Souls for 2 / 3 / 4 / 5 / 6 players | 13 / 9 / 7 / 6 / 5 |
| Starting hand / maximum hand | 5 / 7 cards |
| Cards drawn per turn | 1 |
| Movement cost | 2 Capacity |
| Phase length | 3 rounds |
| Base Capacity | Phase 1: 8; phases 2–3: 12; phase 4 onward: 15 |
| Corrupted / severe Corruption threshold | 5 / 10 |
| Recurring Corruption damage | 2 / 4, doubled at zero Souls |
| Reaching zero Souls | Adds 5 Corruption |
| Special tiles | 3 each of Heal, Purify, and Soul |
| Tile benefits | +5 Vessel, −1 Corruption, or +3 Souls |
| Awakening | 20 Souls |
| Turn timer bounds / default | 30–45 seconds / 45 seconds |

The match combines movement, card play, engagement, a queued attack, Strike resolution, and ending the turn. `EngagementState` tracks Free, Engaged, and Disengaged states. Card costs, targeting, timed buffs, shield depletion, special tiles, turn order, and phase transitions are enforced by Core.

There are 22 Major Arcana card definitions, from The Fool to The World, in `Core/Cards/CardCatalog.cs`. Each has its own attack, defense, Capacity cost, deck count, element, and effect implementation. Cards use a shared draw/discard deck rather than an external deck-building service.

Playing two cards of an element in a turn triggers that element's combo; the implementation tracks each element separately:

- Radiance: +2 DEF.
- Celeste: −1 Corruption.
- Crimson: +2 ATK for the current attack.
- Nocturne: +1 Corruption to the target, or the nearest opponent when not attacking.

An Apostle with at least 20 Souls can Awaken. Only one Apostle is Awakened at a time. An Ascension tile appears on a farthest eligible free tile; the Awakened Apostle wins by reaching it. Dropping below 20 Souls or being eliminated removes that Awakening. Matches can also end with the last Apostle standing or with no survivors.

**Documentation discrepancy:** README's original pitch says “claim twenty-five.” Current rules use the 20-Soul Awakening and Ascension-tile flow. Use `GameConstants`, `AscensionTracker`, `Match`, and their tests as the implementation reference until the design is explicitly changed.

## Architecture and ownership

| Location | Responsibility |
| --- | --- |
| `Assets/Scripts/Core/` | Pure game state and rules: Match, battlefield, combat, turns, movement, cards, bots, commands, snapshots, tutorial scenarios, balance simulation |
| `Assets/Scripts/Core/Cards/Effects/` | Individual Arcana effect implementations and their shared play context |
| `Assets/Scripts/Managers/` | Unity runtime coordination, networking, match presentation orchestration, audio, narration, graphics, memory management |
| `Assets/Scripts/UI/` | Runtime-created menu and match UI, board/card views, animation, effects, camera, settings, tutorial guide |
| `Assets/Scripts/Entities/` | Apostle visuals and avatar animation |
| `Assets/Scripts/Utility/` | Local profile, names, tutorial progress, chat preferences, colors, sprites, build/protocol information |
| `Assets/Scripts/Editor/` | Build commands, scene/HUD setup, online setup, card-art import, balance simulation menus |
| `Assets/Tests/EditMode/` | Rules and simulation tests |
| `Assets/Resources/` | Runtime-loaded skins, art/audio/effect libraries, fonts, materials, shaders, credits |
| `Assets/Art/`, `Assets/Audio/` | Production assets, source art, recordings, and placeholders |
| `Docs/` | Technical plans, asset instructions, narrator script, and this context |

The core assembly's exact name is `Soulbind.core`, in `SoulBind.Core.asmdef`; its C# namespace is `SoulBind.Core`. It explicitly sets `noEngineReferences: true`. Keep it independent of Unity so rules can be tested and reused outside presentation code.

`Match` is the central rules API. Its partial files handle snapshot and online functionality. The runtime `MatchController` coordinates local input, bots, turn flow, and views; its partials separate chat, profile updates, and Strike presentation. `MainMenuView` similarly splits lobby, online, profile, avatar, and screen code into partial files.

Rule changes belong in Core with appropriate EditMode coverage. UI and effects consume match state and state differences; they must not independently determine damage, legal moves, or winners. Seeded random sources support reproducible setup and simulations.

## Online play and trust model

`OnlineSession` initializes Unity Services/authentication, creates or joins Multiplayer sessions, and configures Relay-backed networking. Public rooms can be listed; private rooms use join codes. It survives scene transitions and creates the NetworkManager at runtime. Online match settings allow bots to fill unused seats.

`NetworkMatch` bridges connections and Core:

1. The host owns the authoritative Match.
2. Clients submit compact, exactly 9-byte commands.
3. The host derives the player's seat from the connection and validates actions against the rules.
4. The host sends player-specific snapshots, hiding other hands and the draw pile.
5. Clients update presentation from the received state.

The network layer includes protocol compatibility checks, an approximately 8-message-per-second allowance with a burst of 16, disconnection after 64 dropped messages, and rejected-action resynchronization limited to once every 0.25 seconds per player.

Increment `BuildInfo.ProtocolVersion` when commands, snapshots, card rules/numbers, or chat IDs change. Art-only and menu-only changes can retain the protocol if compatibility is unaffected.

The player host is trusted and can manipulate its own authoritative process. Dedicated authoritative hosting for ranked play is a future option discussed in TechPlan, not an implemented service described here.

## Presentation, audio, and local data

UI is mostly constructed in C# through `UiKit`, using a 1280×720 design canvas with top-left positioning. `UiSkin` and the art libraries provide configurable artwork; missing assets generally use shape/text fallbacks or remain silent. `SafeHud` handles screen safe areas.

The main menu includes offline/online setup, lobby, profile, avatar, and settings views. `MenuBackdrop` presents the menu background with blur, dimming, and movement. `StudioIntro` supports a launch intro, with video preferred over a logo when configured, and skip input.

Combat resolves immediately in Core. `StrikeDisplay` and `MatchController.Strike.cs` present it card by card using `StrikeBreakdown`, temporarily locking input with a 20-second maximum presentation duration. Keep this presentation consistent for offline and snapshot-driven online play.

`GameAudio` manages Music, Sounds, and Narrator volume groups. `Narrator` provides priority, cooldown, and queue behavior; `MatchNarration` detects match events from state changes. `NarratorLibrary` contains Normal and Raspy voices, with Mixed, Normal, Raspy, and Off player settings. The script is `Docs/08-narrator-lines.csv`.

The guided tutorial uses a scripted two-player match against a passing bot, with progress stored locally. `PlayerProfile` stores match statistics in PlayerPrefs under `SoulBind.Profile`; tutorial completion uses `tutorialDone`. Names and chat preferences also have local utility classes. Treat these as local preferences/statistics, without assuming cloud account persistence.

`GraphicsQuality` adjusts device graphics and frame-rate behavior. `MemoryCare` responds to low-memory and scene-load events. Device profiling is still needed to establish actual performance and memory limits.

## Asset workflow

Use `Docs/AssetSlots.md` for exact folders and slots, and `Docs/HowToAddAssets.txt` for additional import guidance.

- `UiSkin`: menu/in-game backgrounds, logos, panels, buttons, and intro assets.
- `FxLibrary`: sprite animation frames, aura effects, island layers, and selected card moments.
- `SoundLibrary`: music, gameplay SFX, and per-card sounds.
- `NarratorLibrary`: voice lines and alternate takes.
- `CardArtLibrary`, `ApostleArtLibrary`, `MapSkin`, and `AvatarSkin`: their respective presentation assets.

Overwrite an existing asset in place to retain its GUID and references; do not delete and recreate it as a replacement. Commit associated `.meta` files with assets. UI skin slots expecting one sprite need Sprite Mode Single; auto-sliced Multiple imports can break those references. Pixel art normally uses Point filtering without compression; scaled logos may benefit from Bilinear filtering. Existing narration source recordings include `.m4a`; the project's runtime narration assets are converted `.ogg` files.

Some folders contain source files, alternate takes, or placeholders. A file's presence does not prove it is assigned to a runtime library or visible in a build.

## Development and validation

1. Open with the editor version in `ProjectSettings/ProjectVersion.txt` and let Unity restore the packages in `Packages/manifest.json`.
2. Use MainMenu for normal flow, or SampleScene for direct match iteration.
3. Run relevant NUnit EditMode tests in Unity's Test Runner. Coverage includes combat, card effects, capacity, movement, turns, tiles, Ascension, snapshots/commands, bots, tutorial scenarios, and balance simulation.
4. For UI/audio changes, check the relevant flow in Play Mode. For networking, use separate host/client instances. For Android layout/performance, validate on a device.
5. Use `SoulBind > Run Balance Simulation` or its Smart Bots variant for balance investigation.

The existing working notes say the Unity Editor is usually open; a second batchmode instance cannot open the same project. Use the active Editor's Test Runner in that case. Project-local logs may be available at `Logs/Editor.log`.

Runtime components and static session/settings state need careful lifecycle handling. Existing conventions reset static state using `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` for Play Mode sessions without domain reload. Use explicit Unity-object null checks instead of null-conditional access when destroyed-object semantics matter. Custom uGUI graphics need `CanvasRenderer`; existing guidance recommends `RequireComponent(typeof(CanvasRenderer))`. Fully qualify the legacy `SoulBind.Element` versus `SoulBind.Core.Element` when ambiguous.

## Builds and release considerations

- Android configuration is described in TechPlan as ARM64, IL2CPP, minimum API 26, landscape. Confirm active platform settings before release.
- `SoulBind > Build Android APK...` offers a version increment or same-version test rebuild and outputs `Builds/Android/SoulBind-v<version>.apk`. It restores version values on a reported build failure.
- `SoulBind > Build Windows` uses the current version without incrementing it, producing `Builds/Windows/SoulBind-v<version>/SoulBind.exe`.
- Keep MainMenu first and SampleScene second in the enabled build scenes.
- Windows is configured to run in the background so an alt-tabbed host continues running.
- The Standalone application identifier is still `com.DefaultCompany.2D-URP`; replace it before a production release. Verify Android signing setup separately and never commit keystores or secrets.

Store updates, Addressables/DLC, a data-driven downloadable card catalog, and dedicated servers are future directions in TechPlan. Existing notes say the self-updater experiment was dropped; do not assume an updater is shipping.

## Handoff status and boundaries

At review time, the working tree contains substantial modified and untracked work, including menu art, narration, Strike breakdown/presentation, tutorial progress, and UI refinements. Preserve it. This context describes those files as they exist locally, without implying that they are committed, released, or verified in a build.

No Unity tests or builds were run to produce this document. Feature presence was checked through source, configuration, and existing documentation. Device behavior, online service configuration, asset completeness, signing, and release readiness require their own validation.

Existing working guidance says to commit or push only when asked. For future changes, inspect the working tree first, preserve unrelated work, keep rules in Core, maintain offline/online presentation parity, and update tests and protocol versions when relevant.

## Short context for a new conversation

> We are building SoulBind: Apostle's Descent, a Unity 6000.6.0f1 tactical hex-board Arcana card battler for 2–6 players, primarily Android with Windows support. Rules live in the Unity-independent `Soulbind.core` assembly (`SoulBind.Core` namespace); Match is the central API. Unity managers coordinate bots, input, networking, audio, and presentation. Most UI is created at runtime with UiKit and configurable skin/library assets. Online play uses Unity Multiplayer sessions and Relay with a player-authoritative host, validated 9-byte commands, and per-player hidden-information snapshots. The current protocol is 4 and game version is 1.0.6 / Android code 7. Apostles start with 50 Vessel, manage Souls and Corruption, and use 22 Arcana types across four elements. Awakening requires 20 Souls; reaching the Ascension tile wins, with last-survivor and no-survivor outcomes also supported. Read Docs/ProjectContext.md, CLAUDE.md, Docs/AssetSlots.md, and relevant code/tests before changing behavior. Preserve existing local work; commit or push only when requested.
