using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

        private const int GalleryColumns = 8;
        private const float GalleryLeft = 60f, GalleryTop = 130f;
        private const float TileWidth = 84f, TileHeight = 118f, TileGap = 8f; // about the art's 5:7
        private const float PreviewLeft = 820f, PreviewWidth = 300f, PreviewHeight = 420f;

        private Image _previewArt;
        private TMP_Text _previewName;
        private TMP_Text _previewStats;
        private TMP_Text _previewEffect;
        private TMP_Text _previewMissing;

        // The card gallery: every card's art in a grid. Tapping one shows it large, with its numbers.
        private void BuildCollections()
        {
            RectTransform frame = BuildSubScreen("CollectionsScreen", "Collections");
            _collectionsScreen = frame.gameObject;

            CardArtLibrary art = CardArtLibrary.Current;
            IReadOnlyList<CardDefinition> cards = CardCatalog.All;
            for (int i = 0; i < cards.Count; i++)
            {
                float x = GalleryLeft + (i % GalleryColumns) * (TileWidth + TileGap);
                float y = GalleryTop + (i / GalleryColumns) * (TileHeight + TileGap);
                BuildGalleryTile(frame, cards[i], art, x, y);
            }

            BuildPreview(frame);
            if (cards.Count > 0)
                ShowCard(cards[0].Id);
        }

        // One card: its art, or its name on a plain tile while it has none.
        private void BuildGalleryTile(RectTransform frame, CardDefinition card, CardArtLibrary art,
            float x, float y)
        {
            ArcanaId id = card.Id;
            TMP_Text label = Kit.Button(frame, card.Name, "", Kit.ButtonStyle.Ghost, () => ShowCard(id), 12f, 8f);
            var tile = (RectTransform)label.transform.parent;
            Kit.At(tile, x, y, TileWidth, TileHeight);

            Sprite front = art != null ? art.GetFront(id) : null;
            if (front != null)
            {
                AddArt(tile, front, 3f);
                return;
            }
            tile.GetComponent<Image>().color = Kit.Panel;
            label.text = card.Name;
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildPreview(RectTransform frame)
        {
            RectTransform box = Kit.Box(frame, "Preview", Kit.Inset, Kit.GoldLine, 12f);
            Kit.At(box, PreviewLeft, GalleryTop, PreviewWidth, PreviewHeight);
            _previewArt = AddArt(box, null, 6f);
            _previewMissing = Kit.Label(box, "NoArt", "No art yet", 18f, Kit.Inter, Kit.Muted);
            Kit.Stretch(_previewMissing.rectTransform);

            _previewName = Kit.Label(frame, "PreviewName", "", 22f, Kit.Display, Kit.Title);
            Kit.At(_previewName.rectTransform, PreviewLeft - 40f, GalleryTop + PreviewHeight + 10f,
                PreviewWidth + 80f, 30f);
            _previewStats = Kit.Label(frame, "PreviewStats", "", 14f, Kit.InterSemiBold, Kit.Soft);
            Kit.At(_previewStats.rectTransform, PreviewLeft - 40f, GalleryTop + PreviewHeight + 42f,
                PreviewWidth + 80f, 20f);
            _previewEffect = Kit.Label(frame, "PreviewEffect", "", 13f, Kit.Inter, Kit.Muted);
            Kit.At(_previewEffect.rectTransform, PreviewLeft - 40f, GalleryTop + PreviewHeight + 66f,
                PreviewWidth + 80f, 40f);

            // The left side's bottom line, under the grid.
            TMP_Text note = Kit.Label(frame, "Note", "Tap a card to see it large.", 14f, Kit.Inter, Kit.Muted);
            float gridBottom = GalleryTop + 3f * (TileHeight + TileGap);
            Kit.At(note.rectTransform, GalleryLeft, gridBottom + 6f,
                GalleryColumns * (TileWidth + TileGap) - TileGap, 22f);
        }

        private void ShowCard(ArcanaId id)
        {
            CardDefinition card = CardCatalog.Get(id);
            CardArtLibrary art = CardArtLibrary.Current;
            Sprite front = art != null ? art.GetFront(id) : null;
            _previewArt.sprite = front;
            _previewArt.enabled = front != null;
            _previewMissing.gameObject.SetActive(front == null);
            _previewName.text = card.Name;
            _previewStats.text = $"{card.Element.ToString().ToUpperInvariant()}   ATK {card.Attack}   " +
                                 $"DEF {card.Defense}   CAP {card.Cap}";
            _previewEffect.text = card.Effect;
        }

        // A sprite filling parent (kept in proportion), not clickable itself.
        private static Image AddArt(RectTransform parent, Sprite sprite, float inset)
        {
            RectTransform rect = Kit.Stretch(Kit.CreateRect("Art", parent), inset);
            var image = (Image)rect.gameObject.AddComponent(typeof(Image));
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
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

            // Graphics and Volume (the same rows as the in-game Menu's Settings).
            SettingsPanel.Build(column, 0f, 150f, 500f);
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
