using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// Quick chat in a match. A Chat button left of Menu opens a picker with the preset lines
    /// (QuickChat.Lines), the emote slots and a mute switch. Messages show as a bubble above the
    /// speaker's cards for a few seconds. Emote art comes from UiSkin; an empty slot shows a numbered
    /// placeholder. MatchHud creates it, so it needs no setup.
    /// </summary>
    public sealed class ChatView : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080).
        private const float ButtonTop = 124f;      // the same row as the Menu button
        private const float ButtonRight = 152f;    // Menu is 120 wide at 20 from the edge, plus a gap
        private const float ButtonWidth = 110f;
        private const float ButtonHeight = 56f;
        private const float PickerWidth = 460f;
        private const float PickerHeight = 398f;
        private const float Pad = 16f;
        private const float LineHeight = 40f;
        private const float LineGap = 6f;
        private const float EmoteSize = 46f;
        private const float BubbleWidth = 180f;
        private const float BubbleHeight = 48f;
        private const float BubbleEmoteSize = 72f;
        private const float BubbleRaise = 96f;     // above the cards, clear of the name plate
        private const float ShowSeconds = 3f;
        private const float FadeSeconds = 0.4f;    // the last part of ShowSeconds
        private const float PhoneScale = 1.2f;

        private MatchController _controller;
        private RectTransform _root;
        private Canvas _canvas;
        private TableHandsView _hands;
        private bool _handsSearched;
        private GameObject _picker;
        private TMP_Text _muteLabel;

        // One bubble per seat. Index = Apostle id (0 unused).
        private readonly Bubble[] _bubbles = new Bubble[GameConstants.MaxPlayers + 1];

        private sealed class Bubble
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TMP_Text Text;
            public Image Emote;
            public TMP_Text EmotePlaceholder;
            public float ShownAt = -1f; // -1 = hidden
        }

        public static ChatView Create(RectTransform hudRoot, MatchController controller)
        {
            var go = new GameObject("QuickChat", typeof(RectTransform));
            go.layer = hudRoot.gameObject.layer;
            RectTransform rect = Kit.Stretch((RectTransform)go.transform);
            rect.SetParent(hudRoot, false);
            Kit.Stretch(rect);

            ChatView view = go.AddComponent<ChatView>();
            view._controller = controller;
            view._root = rect;
            view._canvas = hudRoot.GetComponentInParent<Canvas>();
            view.Build();
            controller.ChatShown += view.Show;
            return view;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.ChatShown -= Show;
        }

        // ---------- Building ----------

        private void Build()
        {
            float scale = Application.isMobilePlatform ? PhoneScale : 1f;

            TMP_Text chat = Kit.Button(_root, "ChatButton", "Chat", Kit.ButtonStyle.Secondary, TogglePicker, 22f);
            var button = (RectTransform)chat.transform.parent;
            PinTopRight(button, ButtonRight, ButtonTop, ButtonWidth, ButtonHeight);
            button.localScale = Vector3.one * scale;

            RectTransform picker = Kit.Box(_root, "ChatPicker", new Color(0.06f, 0.04f, 0.09f, 0.96f), Kit.GoldLine, 12f, 2f);
            PinTopRight(picker, 20f, ButtonTop + ButtonHeight * scale + 10f, PickerWidth, PickerHeight);
            picker.localScale = Vector3.one * scale;
            // A click on the panel itself must not fall through to the board.
            picker.GetComponent<Image>().raycastTarget = true;
            _picker = picker.gameObject;
            BuildPicker(picker);
            _picker.SetActive(false);

            for (int seat = 1; seat < _bubbles.Length; seat++)
                _bubbles[seat] = BuildBubble(seat, scale);
        }

        private void BuildPicker(RectTransform picker)
        {
            TMP_Text title = Kit.Label(picker, "Title", "QUICK CHAT", 13f, Kit.InterBold, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(title.rectTransform, Pad, 14f, 200f, 20f);

            // The lines, two columns.
            float lineWidth = (PickerWidth - Pad * 2f - LineGap) / 2f;
            for (int i = 0; i < QuickChat.Lines.Length; i++)
            {
                int chatId = i;
                float x = Pad + (i % 2) * (lineWidth + LineGap);
                float y = 44f + (i / 2) * (LineHeight + LineGap);
                TMP_Text line = Kit.Button(picker, "Line " + i, QuickChat.Lines[i], Kit.ButtonStyle.Secondary,
                    () => Say(chatId), 17f);
                Kit.At((RectTransform)line.transform.parent, x, y, lineWidth, LineHeight);
            }

            // The emote slots, in one row under the lines.
            int rows = (QuickChat.Lines.Length + 1) / 2;
            float emoteTop = 44f + rows * (LineHeight + LineGap) + 6f;
            float emoteGap = (PickerWidth - Pad * 2f - EmoteSize * QuickChat.EmoteCount) / (QuickChat.EmoteCount - 1);
            UiSkin skin = UiSkin.Current;
            for (int i = 0; i < QuickChat.EmoteCount; i++)
            {
                int chatId = QuickChat.GetEmoteId(i);
                float x = Pad + i * (EmoteSize + emoteGap);
                TMP_Text slot = Kit.Button(picker, "Emote " + i, "", Kit.ButtonStyle.Ghost, () => Say(chatId), 16f, 23f);
                var slotRect = (RectTransform)slot.transform.parent;
                Kit.At(slotRect, x, emoteTop, EmoteSize, EmoteSize);
                Sprite art = skin != null ? skin.GetEmote(i) : null;
                if (art != null)
                    AddEmoteImage(slotRect, art, 4f);
                else
                    slot.text = "E" + (i + 1);
            }

            // Mute other players.
            float muteTop = emoteTop + EmoteSize + 12f;
            _muteLabel = Kit.Button(picker, "Mute", "", Kit.ButtonStyle.Ghost, ToggleMute, 15f);
            Kit.At((RectTransform)_muteLabel.transform.parent, Pad, muteTop, PickerWidth - Pad * 2f, 40f);
            ShowMute();
        }

        private Bubble BuildBubble(int seat, float scale)
        {
            RectTransform rect = Kit.Box(_root, "Bubble " + seat, new Color(0.96f, 0.92f, 0.85f, 0.97f), Kit.Gold, 18f, 2f);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
            rect.localScale = Vector3.one * scale;
            rect.GetComponent<Image>().raycastTarget = false;

            var bubble = new Bubble { Rect = rect };
            bubble.Group = rect.gameObject.AddComponent<CanvasGroup>();
            bubble.Group.blocksRaycasts = false;
            bubble.Group.interactable = false;

            bubble.Text = Kit.Label(rect, "Text", "", 20f, Kit.InterBold, Kit.Background);
            Kit.Stretch(bubble.Text.rectTransform, 6f);
            bubble.Text.textWrappingMode = TextWrappingModes.NoWrap;
            bubble.Text.overflowMode = TextOverflowModes.Ellipsis;

            bubble.Emote = AddEmoteImage(rect, null, 6f);
            bubble.EmotePlaceholder = Kit.Label(rect, "EmotePlaceholder", "", 26f, Kit.Display, Kit.Background);
            Kit.Stretch(bubble.EmotePlaceholder.rectTransform);

            rect.gameObject.SetActive(false);
            return bubble;
        }

        private static Image AddEmoteImage(RectTransform parent, Sprite art, float inset)
        {
            RectTransform rect = Kit.Stretch(Kit.CreateRect("Art", parent), inset);
            var image = (Image)rect.gameObject.AddComponent(typeof(Image));
            image.sprite = art;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = art != null;
            return image;
        }

        // Top-right anchoring, measured from the right and top edges like the Menu button.
        private static void PinTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        // ---------- Picking ----------

        private void TogglePicker()
        {
            bool open = !_picker.activeSelf;
            _picker.SetActive(open);
            if (open)
                _picker.transform.SetAsLastSibling();
        }

        private void Say(int chatId)
        {
            _picker.SetActive(false);
            _controller.SayChat(chatId);
        }

        private void ToggleMute()
        {
            ChatSettings.MuteOthers = !ChatSettings.MuteOthers;
            ShowMute();
        }

        private void ShowMute()
        {
            _muteLabel.text = ChatSettings.MuteOthers ? "Other players: muted (tap to unmute)" : "Mute other players";
        }

        // ---------- Bubbles ----------

        // The controller says someone spoke.
        private void Show(int seat, int chatId)
        {
            if (seat <= 0 || seat >= _bubbles.Length || !QuickChat.IsValid(chatId))
                return;
            if (ChatSettings.MuteOthers && seat != _controller.ViewerId)
                return;

            Bubble bubble = _bubbles[seat];
            bool emote = QuickChat.IsEmote(chatId);
            float size = emote ? BubbleEmoteSize : BubbleWidth;
            bubble.Rect.sizeDelta = new Vector2(size, emote ? BubbleEmoteSize : BubbleHeight);
            bubble.Text.text = emote ? string.Empty : QuickChat.GetLine(chatId);

            int index = emote ? QuickChat.GetEmoteIndex(chatId) : -1; // -1 = not an emote
            UiSkin skin = UiSkin.Current;
            Sprite art = emote && skin != null ? skin.GetEmote(index) : null;
            bubble.Emote.sprite = art;
            bubble.Emote.enabled = art != null;
            bubble.EmotePlaceholder.text = emote && art == null ? "E" + (index + 1) : string.Empty;

            bubble.ShownAt = Time.unscaledTime;
            bubble.Group.alpha = 1f;
            bubble.Rect.gameObject.SetActive(true);
            bubble.Rect.SetAsLastSibling();
        }

        // After the pieces and hands moved this frame, each bubble sits above its speaker and fades out.
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || _canvas == null)
                return;
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (_hands == null && !_handsSearched)
            {
                _hands = FindAnyObjectByType<TableHandsView>();
                _handsSearched = true; // searched once: the hands view is made with the scene, never later
            }

            float now = Time.unscaledTime;
            for (int seat = 1; seat < _bubbles.Length; seat++)
            {
                Bubble bubble = _bubbles[seat];
                if (bubble.ShownAt < 0f)
                    continue;
                float age = now - bubble.ShownAt;
                if (age >= ShowSeconds || !TryGetSpot(seat, cam, out Vector2 screen))
                {
                    Hide(bubble);
                    continue;
                }
                bubble.Group.alpha = Mathf.Clamp01((ShowSeconds - age) / FadeSeconds);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, uiCamera, out Vector2 local);
                Rect area = _root.rect;
                Vector2 size = bubble.Rect.sizeDelta * bubble.Rect.localScale.x;
                Vector2 spot = local + new Vector2(0f, BubbleRaise);
                spot.x = Mathf.Clamp(spot.x, area.xMin + size.x / 2f, area.xMax - size.x / 2f);
                spot.y = Mathf.Min(spot.y, area.yMax - size.y);
                bubble.Rect.anchoredPosition = spot;
            }
        }

        // Above the speaker's cards on the table, or above their piece if they have no hand there.
        private bool TryGetSpot(int seat, Camera cam, out Vector2 screen)
        {
            if (_hands != null && _hands.TryGetHandTop(seat, cam, out screen))
                return true;
            screen = Vector2.zero;
            var piece = _controller.GetPieceView(seat);
            if (piece == null || !piece.gameObject.activeInHierarchy)
                return false;
            Vector3 point = cam.WorldToScreenPoint(piece.TopPoint);
            screen = point;
            return point.z > 0f;
        }

        private static void Hide(Bubble bubble)
        {
            bubble.ShownAt = -1f;
            bubble.Rect.gameObject.SetActive(false);
        }
    }
}
