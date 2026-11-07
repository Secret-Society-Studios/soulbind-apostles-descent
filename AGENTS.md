# SoulBind: Apostle's Descent: project context

A turn-based hex-board card battler for 2 to 6 players. Apostles move on a hex island, play Arcana
cards to strike each other, steal Souls, and manage Corruption. A match ends by Ascension, last
Apostle standing, or no survivors. Played against bots or online (player-hosted).

- **Engine:** Unity 6000.6.0f1, URP 17, C#. The Input System package. Netcode for GameObjects 2.13.
- **Platforms:** Android (ARM64, IL2CPP, min API 26, landscape) is the main target. Windows builds too.
- **Repo:** `Secret-Society-Studios/soulbind-apostles-descent` (public). Work happens on `develop`,
  and `main` is the release branch. Builds are shared by sideloading the APK from Google Drive. There is no
  store or self-updater (that was tried on `feature/apk-self-updater` and dropped).

## Layout

| Path | What it holds |
|---|---|
| `Assets/Scripts/Core` | Pure game rules (`Soulbind.core` asmdef, **no Unity references**): `Match`, `Battlefield`, `HexBoard`, combat, cards (`Cards/`), bots, snapshots, commands |
| `Assets/Scripts/Managers` | Runtime glue: `MatchController` (partials: Chat, Profile, Strike), `NetworkMatch`, `OnlineSession`, `GameAudio`, `Narrator`, `MatchNarration`, `StrikeDisplay` |
| `Assets/Scripts/UI` | All UI and views, mostly built in code: `MainMenuView` (partials), `UiKit`, `UiSkin`, `BoardView`, `CardBody`, `TableHandsView`, `PlayQueue`, `SettingsPanel`, `TutorialGuide` |
| `Assets/Scripts/Utility` | `BuildInfo`, `PlayerProfile`, `TutorialProgress`, color/sprite helpers |
| `Assets/Scripts/Editor` | `SoulBind/...` menu tools: build APK / Windows, create main menu scene, HUD builders, balance simulation, card art import |
| `Assets/Tests/EditMode` | NUnit tests. `Tests.asmdef` references only `Soulbind.core` |
| `Assets/Resources` | Skin and library assets loaded at runtime: `UiSkin`, `SoundLibrary`, `NarratorLibrary`, `FxLibrary`, art libraries |
| `Assets/Art`, `Assets/Audio` | Art and sound. Slots are listed in `Docs/AssetSlots.md` |
| `Docs/` | `TechPlan.md` (security, updates, memory, platforms), `AssetSlots.md` (where each asset goes), `08-narrator-lines.csv` (narrator script) |

Scenes: `MainMenu.unity`, `SampleScene.unity` (the match).

## How the code is written

- **Rules stay in Core.** Anything about who wins, damage, cards, or tiles goes in `SoulBind.Core`
  with a test. Views only show the state. They never change the result.
- **Views diff the match state** (previous vs current snapshot) to decide what to animate or play,
  so the same code works offline and online.
- **UI is built at runtime** through `UiKit` (`Kit.Box`, `Kit.Label`, `Kit.Button`, `Kit.At(x, y, w, h)`
  on a 1280x720 design canvas, top-left origin). Art comes from slots on `UiSkin`/libraries. Code
  always falls back to drawn shapes or text when a slot is empty.
- **Domain reload is off.** Static state must be reset with
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- Components are added at runtime. Use ternary null checks for Unity objects (`x != null ? x.Y : null`),
  not `?.`.
- Comments explain *why*, in plain English.
- `SoulBind.Element` (legacy) and `SoulBind.Core.Element` clash: fully qualify.
- Custom uGUI graphics need `[RequireComponent(typeof(CanvasRenderer))]` (this uGUI's `Graphic`
  doesn't add it).

## Key systems

- **Online:** the host runs the only real `Match`. Clients send 9-byte commands, and the host validates them and
  sends per-player snapshots (hidden hands and draw pile). Flood limits and a version check are in
  `NetworkMatch`. **Raise `BuildInfo.ProtocolVersion`** (currently 4) whenever commands,
  snapshots, card numbers or chat ids change.
- **Strike display:** rules resolve a Strike at once. `StrikeDisplay` + `MatchController.Strike.cs`
  then play it card by card (damage split by `StrikeBreakdown`), locking input until done (max 20 s).
- **Narrator:** `Narrator` (priority, cooldown, queue) + `NarratorLibrary` (lines, two voices:
  Normal / Raspy, picked at random in Mixed mode) + `MatchNarration` (diffs the match to trigger lines).
- **Audio:** `GameAudio` with separate Music, Sounds and Narrator volumes, set in `SettingsPanel`.
- **Tutorial:** the first Play vs bot offers the tutorial (`TutorialProgress`, PlayerPrefs `tutorialDone`).
- **Main menu:** `MainMenuView` builds all screens. `MenuBackdrop` blurs, dims and drifts the
  `UiSkin.MenuBackground` picture. The logo is `UiSkin.MenuLogo`. The panel and button frame art is
  `UiSkin.MenuFrame` (menu only, via `Kit.MenuFrames`).

## Rules at a glance (`Core/GameConstants.cs`)

2 to 6 players. Vessel (HP) is 50. Hand is 5, max 7. Moving costs 2 Capacity. Capacity is 8, then
12, then 15 by phase (3 rounds per phase). Corruption of 5 or more means Corrupted, and 10 or more
means severe. Special tiles are Heal (+5, not while at full Vessel), Purify (-1 Corruption, not at 0),
and Soul (+3), 3 of each. Awaken at 20 Souls.

## Art import gotchas

- New PNGs often import as **Sprite Mode: Multiple** (auto-sliced). Skin slots expect a single
  sprite (`fileID: 21300000`). Set Single, or edit the `.meta`.
- Replacing an image by deleting it gives it a **new GUID** and breaks its slot. Overwrite the file in
  place instead.
- Pixel art: Point filter, no compression. A logo shown smaller than its source looks better on Bilinear.
- Unity can't import `.m4a`. Convert to `.ogg` first.

## Working on it

- The Unity Editor is usually open. Batchmode test runs don't work while it is open, so run EditMode
  tests from the Test Runner. Runtime errors are in `Logs/Editor.log` (project folder).
- **Every release:** raise `bundleVersion` and the Android version code in Player Settings (now
  1.0.6 / 7). Build with `SoulBind > Build Android APK...`.
- Never commit keystores or secrets (the repo is public).
- Commit or push only when asked.
