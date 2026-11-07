using SoulBind.Core;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    // The Avatar screen: pick your Apostle's colour, shown on the animated avatar.
    public sealed partial class MainMenuView
    {
        private const float SwatchLeft = 610f, SwatchTop = 170f;
        private const float SwatchWidth = 130f, SwatchHeight = 150f, SwatchGap = 16f;
        private const int SwatchesPerRow = 3;

        private GameObject _avatarScreen;
        private bool _avatarFromLobby;
        private TMP_Text _avatarSaveStatus;
        private readonly Button[] _avatarColorButtons = new Button[SeatColors.Count];
        private readonly TMP_Text[] _avatarColorLabels = new TMP_Text[SeatColors.Count];
        private float _nextAvatarRefresh;
        private Image _avatarPreview;
        private TMP_Text _avatarColorName;
        private readonly GameObject[] _swatchMarks = new GameObject[SeatColors.Count];

        // The preview on the left, the colours on the right.
        private void BuildAvatar()
        {
            RectTransform frame = BuildSubScreen("AvatarScreen", "Avatar");
            _avatarScreen = frame.gameObject;

            RectTransform stage = Kit.Box(frame, "Preview", Kit.Inset, Kit.Line, 14f);
            Kit.UseFrame(stage);
            Kit.At(stage, 240f, 170f, 330f, 400f);
            RectTransform art = Kit.CreateRect("Art", stage);
            Kit.At(art, 25f, 20f, 280f, 300f);
            _avatarPreview = art.gameObject.AddComponent<Image>();
            _avatarPreview.preserveAspect = true;
            _avatarPreview.raycastTarget = false;
            _avatarColorName = Kit.Label(stage, "ColorName", "", 26f, Kit.Display, Kit.Title);
            Kit.At(_avatarColorName.rectTransform, 0f, 336f, 330f, 40f);

            for (int i = 0; i < SeatColors.Count; i++)
                BuildSwatch(frame, i);

            TMP_Text note = Kit.Label(frame, "Note",
                "Online, colours held by other players are unavailable.", 14f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.TopLeft);
            Kit.At(note.rectTransform, SwatchLeft, 516f, 3f * SwatchWidth + 2f * SwatchGap, 50f);

            _avatarSaveStatus = Kit.Label(frame, "SaveStatus", "", 14f, Kit.Inter, Kit.Gold);
            Kit.At(_avatarSaveStatus.rectTransform, SwatchLeft, 574f, 422f, 40f);

            ShowAvatarColor();
        }

        // One colour: a tappable card with a gold edge while it is the picked one.
        private void BuildSwatch(RectTransform frame, int color)
        {
            float x = SwatchLeft + color % SwatchesPerRow * (SwatchWidth + SwatchGap);
            float y = SwatchTop + color / SwatchesPerRow * (SwatchHeight + SwatchGap);
            RectTransform card = Kit.Box(frame, "Swatch " + color, Kit.Raised, Kit.Line, 12f);
            Kit.UseFrame(card);
            Kit.At(card, x, y, SwatchWidth, SwatchHeight);
            Button button = card.gameObject.AddComponent<Button>();
            _avatarColorButtons[color] = button;
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() => PickAvatarColor(color));

            RectTransform disc = Kit.Circle(card, "Disc", 76f, ApostleColors.Palette(color), Kit.GoldLine, 2f);
            Kit.At(disc, (SwatchWidth - 76f) / 2f, 22f, 76f, 76f);
            disc.GetComponent<Image>().raycastTarget = false;
            TMP_Text name = Kit.Label(card, "Name", ApostleColors.PaletteName(color), 16f, Kit.InterSemiBold, Kit.Soft);
            _avatarColorLabels[color] = name;
            Kit.At(name.rectTransform, 0f, 108f, SwatchWidth, 26f);

            RectTransform mark = Kit.Box(card, "Picked", Color.clear, Kit.GoldBright, 12f, 3f);
            Kit.Stretch(mark);
            _swatchMarks[color] = mark.gameObject;
        }

        // Saves the colour and shows it.
        private void PickAvatarColor(int color)
        {
            if (_avatarFromLobby)
            {
                PickLobbyColor(color);
                return;
            }
            ApostleColors.LocalChoice = color;
            ShowAvatarColor();
        }

        // Shows the saved colour on the preview and marks its card.
        private void ShowAvatarColor()
        {
            int picked = ApostleColors.LocalChoice;
            AvatarSkin skin = AvatarSkin.Current;
            bool hasFrames = skin != null && skin.HasFrames;
            _avatarPreview.enabled = hasFrames; // without frames the name and cards still show the pick
            if (hasFrames)
                _avatarPreview.color = skin.Tinted(ApostleColors.Palette(picked));
            _avatarColorName.text = ApostleColors.PaletteName(picked);
            SoulBind.Online.OnlineSession session = SoulBind.Online.OnlineSession.Instance;
            for (int i = 0; i < _swatchMarks.Length; i++)
            {
                _swatchMarks[i].SetActive(i == picked);
                bool available = !_avatarFromLobby || (session != null && session.CanPickAvatarColor(i));
                _avatarColorButtons[i].interactable = available && !_savingLobbyColor;
                _avatarColorLabels[i].text = ApostleColors.PaletteName(i) + (available ? "" : " (taken)");
                _avatarColorLabels[i].fontSize = available ? 16f : 13f;
            }
        }

        // Plays the idle clip on the preview while the screen is open.
        private void AnimateAvatarPreview()
        {
            if (_avatarFromLobby && Time.unscaledTime >= _nextAvatarRefresh)
            {
                _nextAvatarRefresh = Time.unscaledTime + 0.5f;
                SoulBind.Online.OnlineSession session = SoulBind.Online.OnlineSession.Instance;
                if (session != null)
                    session.GetRoomPlayers(_roomPlayers);
                ShowAvatarColor();
            }
            AvatarSkin skin = AvatarSkin.Current;
            if (skin == null || !skin.HasFrames)
                return;
            int frame = Mathf.FloorToInt(Time.unscaledTime * skin.IdleFps) % skin.Idle.Length;
            _avatarPreview.sprite = skin.Idle[frame];
        }
    }
}
