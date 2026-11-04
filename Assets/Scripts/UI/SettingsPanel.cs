using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The settings rows: Graphics (Auto, Low, Medium, High) and Volume. The main menu's Settings screen
    /// shows them in place; during a match, SettingsPopup shows them over the game.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const float RowHeight = 48f;
        private const float RowGap = 64f;
        private const float LabelWidth = 160f;

        private TMP_Text _graphics;
        private TMP_Text _percent;

        /// <summary>Builds the rows at x, y (design pixels from the parent's top-left), width wide.</summary>
        public static SettingsPanel Build(RectTransform parent, float x, float y, float width)
        {
            RectTransform root = Kit.At(Kit.CreateRect("Settings", parent), x, y, width, RowGap + RowHeight);
            SettingsPanel panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.BuildRows(root, width);
            return panel;
        }

        private void BuildRows(RectTransform root, float width)
        {
            float control = width - LabelWidth - 20f;

            // ---------- Graphics ----------
            RowLabel(root, "GraphicsLabel", "Graphics", 0f);
            _graphics = Kit.Button(root, "Graphics", GraphicsQuality.ShortLabel, Kit.ButtonStyle.Secondary,
                CycleGraphics, 18f);
            Kit.At((RectTransform)_graphics.transform.parent, LabelWidth + 20f, 0f, control, RowHeight);

            // ---------- Volume ----------
            RowLabel(root, "VolumeLabel", "Volume", RowGap);
            Slider slider = BuildSlider(root, LabelWidth + 20f, RowGap + RowHeight / 2f - 8f, control - 70f);
            _percent = Kit.Label(root, "Percent", "", 18f, Kit.InterSemiBold, Kit.Text, TextAlignmentOptions.Right);
            Kit.At(_percent.rectTransform, width - 64f, RowGap, 64f, RowHeight);
            slider.SetValueWithoutNotify(GameAudio.Volume);
            slider.onValueChanged.AddListener(SetVolume);
            ShowPercent(GameAudio.Volume);
        }

        private void OnEnable()
        {
            // The tier may have changed elsewhere (the other menu) since this was built.
            if (_graphics != null)
                _graphics.text = GraphicsQuality.ShortLabel;
        }

        // Volume is saved to disk when the rows close, not on every slider step.
        private void OnDisable() => GameAudio.Save();

        private void CycleGraphics()
        {
            GraphicsQuality.Cycle();
            _graphics.text = GraphicsQuality.ShortLabel;
        }

        private void SetVolume(float volume)
        {
            GameAudio.Volume = volume;
            ShowPercent(volume);
        }

        private void ShowPercent(float volume) => _percent.SetText("{0}%", Mathf.RoundToInt(volume * 100f));

        private static void RowLabel(RectTransform root, string name, string text, float y)
        {
            TMP_Text label = Kit.Label(root, name, text, 20f, Kit.InterSemiBold, Kit.Soft, TextAlignmentOptions.Left);
            Kit.At(label.rectTransform, 0f, y, LabelWidth, RowHeight);
        }

        // A gold bar with a round handle, 0 to 1.
        private static Slider BuildSlider(RectTransform root, float x, float y, float width)
        {
            RectTransform track = Kit.At(Kit.Box(root, "VolumeSlider", Kit.Inset, Kit.Line, 8f), x, y, width, 16f);
            Slider slider = track.gameObject.AddComponent<Slider>();

            RectTransform fillArea = Kit.Stretch(Kit.CreateRect("Fill Area", track));
            RectTransform fill = Kit.Stretch(Kit.Box(fillArea, "Fill", Kit.Gold, Color.clear, 8f));
            fill.GetComponent<Image>().raycastTarget = false;

            // The handle area is inset by half a handle, so the handle never pokes past the ends.
            RectTransform handleArea = Kit.Stretch(Kit.CreateRect("Handle Area", track));
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            RectTransform handle = Kit.Circle(handleArea, "Handle", 28f, Kit.GoldBright, Kit.GoldLine, 2f);
            handle.sizeDelta = new Vector2(28f, 12f); // 16 tall track + 12 = a 28 px circle

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }
    }

    /// <summary>The settings rows in a box over the match, opened from the in-game Menu.</summary>
    public sealed class SettingsPopup : MonoBehaviour
    {
        /// <summary>Opens the popup on this canvas (made the first time).</summary>
        public static void Open(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return;
            Transform existing = canvasRoot.Find("SettingsPopup");
            GameObject popup = existing != null ? existing.gameObject : Create(canvasRoot);
            popup.SetActive(true);
            popup.transform.SetAsLastSibling();
        }

        private static GameObject Create(Transform canvasRoot)
        {
            // A dim layer over everything; tapping outside the box closes it.
            RectTransform dim = Kit.Stretch(Kit.CreateRect("SettingsPopup", canvasRoot));
            Image shade = dim.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.6f);
            SettingsPopup popup = dim.gameObject.AddComponent<SettingsPopup>();
            Button outside = dim.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(popup.Close);

            RectTransform box = Kit.Box(dim, "Box", Kit.Panel, Kit.GoldLine, 14f, 2f);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(600f, 330f);

            TMP_Text title = Kit.Label(box, "Title", "Settings", 32f, Kit.Display, Kit.Title);
            Kit.At(title.rectTransform, 0f, 22f, 600f, 44f);

            SettingsPanel.Build(box, 40f, 92f, 520f);

            TMP_Text close = Kit.Button(box, "Close", "Close", Kit.ButtonStyle.Primary, popup.Close, 20f);
            Kit.At((RectTransform)close.transform.parent, 200f, 254f, 200f, 52f);
            return dim.gameObject;
        }

        private void Close() => gameObject.SetActive(false);
    }
}
