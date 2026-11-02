using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    // The screens the main menu's column opens: Singleplayer, Collections and Settings.
    // (Multiplayer is MainMenuView.Online.cs; the Lobby is MainMenuView.Lobby.cs.)
    public sealed partial class MainMenuView
    {
        // ---------- Main column ----------

        private const float MenuButtonHeight = 50f;
        private const float MenuButtonGap = 10f;

        private GameObject _singleScreen;
        private GameObject _collectionsScreen;
        private GameObject _settingsScreen;
        private TMP_Text _chatMuteLabel;

        // One full-width button in the main column, row 0 at the top.
        private static void AddMenuButton(RectTransform column, int row, string text, Kit.ButtonStyle style,
            UnityEngine.Events.UnityAction onClick)
        {
            float size = style == Kit.ButtonStyle.Primary || style == Kit.ButtonStyle.Outline ? 21f : 18f;
            TMP_Text label = Kit.Button(column, text, text, style, onClick, size);
            float y = row * (MenuButtonHeight + MenuButtonGap);
            Kit.At((RectTransform)label.transform.parent, 0f, y, 420f, MenuButtonHeight);
        }

        // A sub-screen: frame lines, the back button and a title. Starts hidden.
        private RectTransform BuildSubScreen(string name, string title)
        {
            RectTransform frame = Kit.CreateFrame(_root, name);
            AddFrameLines(frame, false);
            BuildBackButton(frame, () => ShowScreen(Screen.Main));
            TMP_Text heading = Kit.Label(frame, "Title", title, 44f, Kit.Display, Kit.Title);
            Kit.At(heading.rectTransform, 0f, 50f, 1280f, 56f);
            frame.gameObject.SetActive(false);
            return frame;
        }

        // ---------- Singleplayer ----------

        // How many Apostles, then Play vs Bots or Pass & Play on this device.
        private void BuildSingleplayer()
        {
            RectTransform frame = BuildSubScreen("SingleplayerScreen", "Singleplayer");
            _singleScreen = frame.gameObject;

            RectTransform column = Kit.At(Kit.CreateRect("Column", frame), 430f, 200f, 420f, 360f);
            RectTransform stepper = Kit.Box(column, "Apostles", Kit.Panel, Kit.Line, 10f);
            Kit.At(stepper, 0f, 0f, 420f, 60f);
            TMP_Text label = Kit.Label(stepper, "Label", "APOSTLES", 16f, Kit.Display, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.At(label.rectTransform, 20f, 0f, 200f, 60f);
            label.characterSpacing = 12f;
            _mainPlayers = BuildStepper(stepper, 270f, 8f, ChangePlayers);

            _botsLine = Kit.Label(column, "BotsLine", "", 14f, Kit.Inter, Kit.Muted);
            Kit.At(_botsLine.rectTransform, 0f, 70f, 420f, 22f);

            TMP_Text play = Kit.Button(column, "PlayBots", "Play vs Bots", Kit.ButtonStyle.Primary, PlayBots, 24f);
            Kit.At((RectTransform)play.transform.parent, 0f, 106f, 420f, 64f);

            TMP_Text local = Kit.Button(column, "PassAndPlay", "Pass & Play", Kit.ButtonStyle.Secondary,
                PassAndPlay, 18f);
            Kit.At((RectTransform)local.transform.parent, 0f, 184f, 420f, 56f);
            TMP_Text localNote = Kit.Label(column, "PassAndPlayNote",
                "Everyone plays on this device, taking turns.", 13f, Kit.Inter, Kit.Muted);
            Kit.At(localNote.rectTransform, 0f, 246f, 420f, 20f);
        }

        // ---------- Collections ----------

        // The card gallery: every card with its element and numbers. Card art can be added to the tiles later.
        private void BuildCollections()
        {
            RectTransform frame = BuildSubScreen("CollectionsScreen", "Collections");
            _collectionsScreen = frame.gameObject;

            const int columns = 6;
            const float left = 101f, top = 136f, width = 168f, height = 96f, gap = 14f;
            IReadOnlyList<CardDefinition> cards = CardCatalog.All;
            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                float x = left + (i % columns) * (width + gap);
                float y = top + (i / columns) * (height + gap);
                RectTransform tile = Kit.Box(frame, card.Name, Kit.Panel, Kit.GoldLine, 10f);
                Kit.At(tile, x, y, width, height);

                TMP_Text name = Kit.Label(tile, "Name", card.Name, 16f, Kit.InterBold, Kit.Text);
                Kit.At(name.rectTransform, 6f, 10f, width - 12f, 24f);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                TMP_Text element = Kit.Label(tile, "Element", card.Element.ToString().ToUpperInvariant(), 11f,
                    Kit.InterSemiBold, Kit.Gold);
                Kit.At(element.rectTransform, 6f, 36f, width - 12f, 18f);
                element.characterSpacing = 8f;
                TMP_Text numbers = Kit.Label(tile, "Numbers",
                    $"ATK {card.Attack}   DEF {card.Defense}   CAP {card.Cap}", 13f, Kit.Inter, Kit.Soft);
                Kit.At(numbers.rectTransform, 6f, 62f, width - 12f, 20f);
            }

            TMP_Text note = Kit.Label(frame, "Note", "Card art, Apostles and emotes will appear here as they are added.",
                14f, Kit.Inter, Kit.Muted);
            Kit.At(note.rectTransform, 0f, 600f, 1280f, 22f);
        }

        // ---------- Settings ----------

        private void BuildSettings()
        {
            RectTransform frame = BuildSubScreen("SettingsScreen", "Settings");
            _settingsScreen = frame.gameObject;

            RectTransform column = Kit.At(Kit.CreateRect("Column", frame), 390f, 170f, 500f, 400f);

            // How to Play lives here now that the main screen has no ? button.
            TMP_Text help = Kit.Button(column, "HowToPlay", "How to Play", Kit.ButtonStyle.Secondary,
                () => HelpPopup.Open(transform), 18f);
            Kit.At((RectTransform)help.transform.parent, 0f, 0f, 500f, 56f);

            _chatMuteLabel = Kit.Button(column, "ChatMute", "", Kit.ButtonStyle.Secondary, ToggleChatMute, 18f);
            Kit.At((RectTransform)_chatMuteLabel.transform.parent, 0f, 70f, 500f, 56f);
            ShowChatMute();

            TMP_Text later = Kit.Label(column, "ComingSoon",
                "Sound, music and graphics settings will go here.", 14f, Kit.Inter, Kit.Muted);
            Kit.At(later.rectTransform, 0f, 146f, 500f, 22f);
        }

        private void ToggleChatMute()
        {
            ChatSettings.MuteOthers = !ChatSettings.MuteOthers;
            ShowChatMute();
        }

        private void ShowChatMute()
        {
            _chatMuteLabel.text = ChatSettings.MuteOthers ? "Match chat: other players muted" : "Match chat: on";
        }
    }
}
