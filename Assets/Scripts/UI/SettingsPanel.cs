using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The settings rows: Graphics (Auto, Low, Medium, High), Music, Sounds, Narrator and Voice. The main menu's Settings screen
    /// shows them in place; during a match, SettingsPopup shows them over the game.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const float RowHeight = 48f;
        private const float RowGap = 64f;
        private const float LabelWidth = 160f;

        private TMP_Text _graphics;
        private TMP_Text _narrator;

        /// <summary>Builds the rows at x, y (design pixels from the parent's top-left), width wide.</summary>
        public static SettingsPanel Build(RectTransform parent, float x, float y, float width)
        {
            RectTransform root = Kit.At(Kit.CreateRect("Settings", parent), x, y, width, 4f * RowGap + RowHeight);
            SettingsPanel panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.BuildRows(root, width);
            return panel;
        }

        // Builds the Graphics, Music, Sounds, Narrator and Voice rows.
        private void BuildRows(RectTransform root, float width)
        {
            float control = width - LabelWidth - 20f;

            // ---------- Graphics ----------
            RowLabel(root, "GraphicsLabel", "Graphics", 0f);
            _graphics = Kit.Button(root, "Graphics", GraphicsQuality.ShortLabel, Kit.ButtonStyle.Secondary,
                CycleGraphics, 18f);
            Kit.At((RectTransform)_graphics.transform.parent, LabelWidth + 20f, 0f, control, RowHeight);

            // ---------- Volume ----------
            VolumeRow(root, "Music", RowGap, width, GameAudio.MusicVolume, v => GameAudio.MusicVolume = v);
            VolumeRow(root, "Sounds", 2f * RowGap, width, GameAudio.SoundVolume, v => GameAudio.SoundVolume = v);

            VolumeRow(root, "Narrator", 3f * RowGap, width, GameAudio.NarratorVolume, v => GameAudio.NarratorVolume = v);

            // ---------- Narrator's voice ----------
            RowLabel(root, "VoiceLabel", "Voice", 4f * RowGap);
            _narrator = Kit.Button(root, "Voice", Narrator.Voice.ToString(), Kit.ButtonStyle.Secondary,
                CycleNarrator, 18f);
            Kit.At((RectTransform)_narrator.transform.parent, LabelWidth + 20f, 4f * RowGap, control, RowHeight);
        }

        // One volume row: a label, a slider and its percentage. set stores the new volume.
        private static void VolumeRow(RectTransform root, string name, float y, float width, float value,
            System.Action<float> set)
        {
            float control = width - LabelWidth - 20f;
            RowLabel(root, name + "Label", name, y);
            Slider slider = BuildSlider(root, name + "Slider", LabelWidth + 20f, y + RowHeight / 2f - 8f, control - 70f);
            TMP_Text percent = Kit.Label(root, name + "Percent", "", 18f, Kit.InterSemiBold, Kit.Text,
                TextAlignmentOptions.Right);
            Kit.At(percent.rectTransform, width - 64f, y, 64f, RowHeight);
            slider.SetValueWithoutNotify(value);
            ShowPercent(percent, value);
            slider.onValueChanged.AddListener(volume =>
            {
                set(volume);
                ShowPercent(percent, volume);
            });
        }

        private void OnEnable()
        {
            // The tier may have changed elsewhere (the other menu) since this was built.
            if (_graphics != null)
                _graphics.text = GraphicsQuality.ShortLabel;
            if (_narrator != null)
                _narrator.text = Narrator.Voice.ToString();
        }

        // The volumes are saved to disk when the rows close, not on every slider step.
        private void OnDisable() => GameAudio.Save();

        // Moves to the next graphics level and shows it.
        private void CycleGraphics()
        {
            GraphicsQuality.Cycle();
            _graphics.text = GraphicsQuality.ShortLabel;
        }

        // Moves to the next narrator voice and says a line, so the player hears what they picked.
        private void CycleNarrator()
        {
            Narrator.CycleVoice();
            _narrator.text = Narrator.Voice.ToString();
            GameAudio.StopVoice();
            Narrator.Preview();
        }

        // Shows the volume as a percentage, without allocating a string.
        private static void ShowPercent(TMP_Text percent, float volume) =>
            percent.SetText("{0}%", Mathf.RoundToInt(volume * 100f));

        // Builds a row label on the left.
        private static void RowLabel(RectTransform root, string name, string text, float y)
        {
            TMP_Text label = Kit.Label(root, name, text, 20f, Kit.InterSemiBold, Kit.Soft, TextAlignmentOptions.Left);
            Kit.At(label.rectTransform, 0f, y, LabelWidth, RowHeight);
        }

        // A gold bar with a round handle, 0 to 1.
        private static Slider BuildSlider(RectTransform root, string name, float x, float y, float width)
        {
            RectTransform track = Kit.At(Kit.Box(root, name, Kit.Inset, Kit.Line, 8f), x, y, width, 16f);
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

        // Builds the in-match Settings popup once.
        private static GameObject Create(Transform canvasRoot)
        {
            // A dim layer over everything; tapping outside the box closes it.
            RectTransform dim = Kit.Stretch(Kit.CreateRect("SettingsPopup", canvasRoot));
            Image shade = dim.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.6f);
            UiKit.OnTop(dim.gameObject, 510); // above the help popup too
            SettingsPopup popup = dim.gameObject.AddComponent<SettingsPopup>();
            Button outside = dim.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(popup.Close);

            RectTransform box = Kit.Box(dim, "Box", Kit.Panel, Kit.Line, 14f, 2f);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(600f, 522f);

            // Takes the taps inside the box, or they would pass up to the dim layer and close it.
            Button inside = box.gameObject.AddComponent<Button>();
            inside.transition = Selectable.Transition.None;
            inside.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text title = Kit.Label(box, "Title", "Settings", 32f, Kit.Display, Kit.Title);
            Kit.At(title.rectTransform, 0f, 22f, 600f, 44f);

            SettingsPanel.Build(box, 40f, 92f, 520f);

            TMP_Text close = Kit.Button(box, "Close", "Close", Kit.ButtonStyle.Primary, popup.Close, 20f);
            Kit.At((RectTransform)close.transform.parent, 200f, 446f, 200f, 52f);
            return dim.gameObject;
        }

        // Hides the popup.
        private void Close() => gameObject.SetActive(false);
    }
}
