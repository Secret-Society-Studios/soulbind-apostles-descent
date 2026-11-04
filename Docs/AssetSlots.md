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
| Main menu design assets | `Assets/Art/UI/MainMenu` | Main menu design (background, logo, panel, buttons) |
| Menu design assets (in-game) | `Assets/Art/UI/InGame` | In-game menu design (panel, buttons, icons, banner) |
| Apply assets to new in-game UI | n/a | Done in code once the sprites are in the slots above |
| Credits | `Assets/Art/Credits` | Text in `Assets/Resources/Credits.txt` |
| OBB | n/a | Build setting, see below |

## Sound: `Assets/Resources/SoundLibrary.asset`

| OKR | Folder (`Assets/Audio/...`) | Slots |
| --- | --- | --- |
| At least 3 soundtracks | `Music` | Main Menu Music, Match Music, Result Music (+ Extra Music) |
| In-game SFX | `SFX/Game` | Move, Spawn Land, Attack Hit, Shield Break, Heal, Purify, Soul Gained, Awaken, Death, Card Draw, Turn Start, Victory, Defeat |
| UI interaction SFX | `SFX/UI` | Button Click, Button Hover, Popup Open, Popup Close, Not Allowed |
| Card SFX (per card) | `SFX/Cards` | Cards 0-21 (0 Fool ... 21 World) |

The three music tracks already play: main menu, match, and the result screen.
The other sounds and animations are slots only; they get wired to the game as the files arrive.
Code: `GameAudio.PlaySfx(clip)` and `GameAudio.PlayMusic(clip)`; a null clip does nothing.

## OBB (Android)

An OBB lets the Android build go over the store's size limit by moving assets out of the APK.
Turn it on in **Player Settings > Android > Publishing Settings > Split Application Binary**.
Google Play now expects an AAB for new apps; there the same setting produces Play Asset Delivery packs instead of an OBB.
