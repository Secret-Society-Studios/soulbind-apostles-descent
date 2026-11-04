# SoulBind tech plan: security, updates, memory, platforms

What the code does today, what changed in this pass, and the next steps when the game grows.

## 1. Multiplayer security

**How it works now.** The host owns the only real match. Players send small commands (9 bytes);
the host checks that the sender is the Apostle whose turn it is and that the rules allow the move,
then sends each player a snapshot. Snapshots hide other hands and the draw pile, so a modded game
can't see your cards. A player's seat always comes from their connection, never from the message.

**Added in this pass** (NetworkMatch):
- Flood control: about 8 messages a second per player (16 at once). Extra messages are dropped, and
  64 dropped messages disconnect the sender.
- Fix-up snapshots after a refused action go out at most 4 times a second per player, so a cheater
  can't make the host flood the network.
- Command bytes must be exactly 9 long.
- Version check: a player whose `BuildInfo.ProtocolVersion` differs is disconnected with a message
  ("Your game is out of date..."), shown on their menu.

**Limits to know.** The host's own game can still cheat, because it is the referee. That is normal
for player-hosted games. For ranked play, move the same `Match` code to a dedicated server
(Unity Multiplay or a small headless build). The rules are already in `SoulBind.Core` with no Unity
references, so they can run there unchanged.

## 2. Patches, DLC and auto-updates

- **Every patch:** raise `bundleVersion` (and the Android version code) in Player Settings. If the
  patch changes anything sent between players (commands, snapshots, card numbers, chat ids), also
  raise `BuildInfo.ProtocolVersion` so old and new games never share a match.
- **Auto-updates:** let the stores do it. Google Play updates the APK itself; for a prompt inside the
  game, add the Play In-App Updates plugin. On PC, Steam or itch.io (the itch app) update the game.
  A home-made updater is not worth the security risk.
- **DLC (new cards, skins, maps):** move art into Addressables groups, so a DLC is a downloadable
  bundle instead of a new APK. Cards are an `ArcanaId` enum today; a card pack needs a data-driven
  catalog (ScriptableObjects with an id and numbers) before cards can ship without a code update.
  Skins and maps are already slots (ApostleArtLibrary, MapSkin, UiSkin), so they are the easiest
  first DLC.

## 3. Memory (RAM)

- Card art imports at 1024 at most, tiles at 512, compressed (PR #36).
- New `MemoryCare`: when the phone warns that memory is low, and after every scene load, unused art
  is released and the garbage collector runs.
- Incremental GC is on in Player Settings.
- Per-frame allocations are avoided in the HUD (text only changes when its number does).
- To check: Window > Analysis > Memory Profiler on a cheap Android phone, menu to match and back
  three times; total memory should come back to the same level each time.

## 4. Platforms

- Android: ARM64, IL2CPP, min API 26, landscape only, Vulkan with OpenGL ES 3 fallback (automatic).
- PC: now keeps running in the background (`Run In Background`), so an alt-tabbed host doesn't
  freeze an online match, and the window can be resized.
- To do before release: set a real Standalone bundle id (it is still `com.DefaultCompany.2D-URP`),
  and a signing keystore for Android (Player Settings > Publishing Settings).
- The HUD keeps clear of notches and rounded corners (SafeHud), and the hand sizes by screen inches.

## 5. Optimization (already in)

GraphicsQuality Low/Medium/High per device, render scale, frame rate caps, no HDR/post on Low,
effects counts scaled, idle UI not redrawn, the hidden sky layer skipped. Next, if a phone still
struggles: profile with the Unity Profiler (connected to the phone), and look at overdraw from the
fire sea layers first.
