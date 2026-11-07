# Asset slots

Every asset on the OKR lists has a folder to drop files into and a slot to drag them into.
An empty slot keeps today's look (or stays silent), so assets can land one at a time.

## Animations: `Assets/Resources/FxLibrary.asset`

Slice each sprite sheet in the Sprite Editor, then drag the frames into the slot's **Frames** list.
Set **Fps** and **Loop** per slot.

| OKR | Folder (`Assets/Art/Animations/...`) | Slot |
| --- | --- | --- |
| Background lava animation | `Lava` | Lava |
| Spawn animation (falling) | `Spawn` | Spawn Fall |
| Death animation | `Death` | Death |
| UI animations | `UI` | Button Press, Popup Open, Turn Banner |
| Main menu background animation | `MainMenuBackground` | Main Menu Background |
| Card animation (4 elements) | `Cards/Radiance`, `Celeste`, `Crimson`, `Nocturne` | Card Elements 0-3 (same order) |
| Layered island (animated) | `Island` | Island Layers (back to front) + Island Layer Clips |
| Aura effects | `Aura/Corruption`, `Heal`, `Purify`, `Shield`, `Awakened` | the matching Aura slot |

## UI design: `Assets/Resources/UiSkin.asset`

| OKR | Folder | Slots |
| --- | --- | --- |
| Main menu design assets | `Assets/Art/UI/MainMenu` | Main menu design (background, logo, panel, buttons, frame) |
| Menu design assets (in-game) | `Assets/Art/UI/InGame` | In-game menu design (panel, buttons, icons, banner) |
| Apply assets to new in-game UI | n/a | Done in code once the sprites are in the slots above |
| Credits | `Assets/Art/Credits` | Text in `Assets/Resources/Credits.txt` |
| OBB (studio opening billboard) | `Assets/Art/UI/StudioIntro` | Studio opening: Studio Video or Studio Logo, Background, Seconds |

## Sound: `Assets/Resources/SoundLibrary.asset`

| OKR | Folder (`Assets/Audio/...`) | Slots |
| --- | --- | --- |
| At least 3 soundtracks | `Music` | Main Menu Music, Match Music, Result Music (+ Extra Music) |
| In-game SFX | `SFX/Game` | Move, Spawn Land, Attack Hit, Shield Break, Heal, Purify, Soul Gained, Awaken, Death, Card Draw, Card Play, Buff, Debuff, Corrupt, Turn Start, Victory, Defeat |
| UI interaction SFX | `SFX/UI` | Button Click, Button Hover, Popup Open, Popup Close, Not Allowed |
| Card SFX (per card) | `SFX/Cards` | Cards 0-21 (0 Fool ... 21 World) |

The music and every in-game and card sound play in the game. Files named `placeholder_*` are stand-ins:
drag a real file over the slot, or overwrite the placeholder file to keep the slot as it is.
The Emperor, Moon, Sun and World moment sounds are in FxLibrary (`SFX/Cards`). UI sounds are slots only.
Code: `GameAudio.PlaySfx(clip)` and `GameAudio.PlayMusic(clip)`; a null clip does nothing.

## Narrator: `Assets/Resources/NarratorLibrary.asset`

One line can hold several takes per voice: one plays at random. With Mixed, the default, it picks from
both voices. The files are `.ogg` in
`Assets/Audio/Narrator/Normal` and `Raspy`, cut from the `.m4a` recordings in `Assets/Audio/SFX`, because
Unity can't import `.m4a`. Lines, priorities and cooldowns: `Docs/08-narrator-lines.csv` and `Narrator.cs`.
Players choose Mixed, Normal, Raspy or Off in Settings. Moments: `MatchNarration.cs`.

## Studio opening billboard

Plays once per launch, after Unity's own splash, over the main menu (UI/StudioIntro.cs).
A video (.mp4, H.264) wins over the logo; the logo fades in and out over Studio Seconds.
A tap, click or key skips it. With both slots empty nothing shows.

## Android expansion files (not the billboard)

An OBB lets the Android build go over the store's size limit by moving assets out of the APK.
Turn it on in **Player Settings > Android > Publishing Settings > Split Application Binary**.
Google Play now expects an AAB for new apps; there the same setting produces Play Asset Delivery packs instead of an OBB.
