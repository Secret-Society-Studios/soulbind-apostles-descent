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
        private Image _avatarPreview;
        private TMP_Text _avatarColorName;
        private readonly GameObject[] _swatchMarks = new GameObject[SeatColors.Count];

        // The preview on the left, the colours on the right.
        private void BuildAvatar()
        {
            RectTransform frame = BuildSubScreen("AvatarScreen", "Avatar");
            _avatarScreen = frame.gameObject;

            RectTransform stage = Kit.Box(frame, "Preview", Kit.Inset, Kit.Line, 14f);
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
                "Online, if an earlier seat has your colour, you get the next free one.", 14f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.TopLeft);
            Kit.At(note.rectTransform, SwatchLeft, 516f, 3f * SwatchWidth + 2f * SwatchGap, 50f);

            ShowAvatarColor();
        }

        // One colour: a tappable card with a gold edge while it is the picked one.
        private void BuildSwatch(RectTransform frame, int color)
        {
            float x = SwatchLeft + color % SwatchesPerRow * (SwatchWidth + SwatchGap);
            float y = SwatchTop + color / SwatchesPerRow * (SwatchHeight + SwatchGap);
            RectTransform card = Kit.Box(frame, "Swatch " + color, Kit.Panel, Kit.Line, 12f);
            Kit.At(card, x, y, SwatchWidth, SwatchHeight);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() => PickAvatarColor(color));

            RectTransform disc = Kit.Circle(card, "Disc", 76f, ApostleColors.Palette(color), Kit.GoldLine, 2f);
            Kit.At(disc, (SwatchWidth - 76f) / 2f, 22f, 76f, 76f);
            disc.GetComponent<Image>().raycastTarget = false;
            TMP_Text name = Kit.Label(card, "Name", ApostleColors.PaletteName(color), 16f, Kit.InterSemiBold, Kit.Soft);
            Kit.At(name.rectTransform, 0f, 108f, SwatchWidth, 26f);

            RectTransform mark = Kit.Box(card, "Picked", Color.clear, Kit.GoldBright, 12f, 3f);
            Kit.Stretch(mark);
            _swatchMarks[color] = mark.gameObject;
        }

        // Saves the colour and shows it.
        private void PickAvatarColor(int color)
        {
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
            for (int i = 0; i < _swatchMarks.Length; i++)
                _swatchMarks[i].SetActive(i == picked);
        }

        // Plays the idle clip on the preview while the screen is open.
        private void AnimateAvatarPreview()
        {
            AvatarSkin skin = AvatarSkin.Current;
            if (skin == null || !skin.HasFrames)
                return;
            int frame = Mathf.FloorToInt(Time.unscaledTime * skin.IdleFps) % skin.Idle.Length;
            _avatarPreview.sprite = skin.Idle[frame];
        }
    }
}
