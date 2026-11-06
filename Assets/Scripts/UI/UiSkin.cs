using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoulBind.UI
{
    /// <summary>
    /// The art slots for the HUD's bars (Capacity, Vessel (HP), Corruption and Souls) and the quick chat emotes.
    /// It lives at Assets/Resources/UiSkin.asset. Drag a sprite into a slot and that part of the bar
    /// uses it; an empty slot keeps the code-drawn placeholder, so the art can arrive one piece at a time.
    /// Frames and backs should be 9-sliced in the Sprite Editor; fills are drawn as Filled, Horizontal.
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/UI Skin", fileName = "UiSkin")]
    public sealed class UiSkin : ScriptableObject
    {
        [Header("Capacity bar (above your hand)")]
        [Tooltip("One unspent Capacity point.")]
        [SerializeField] private Sprite capacityPipLit;
        [Tooltip("One spent Capacity point.")]
        [SerializeField] private Sprite capacityPipSpent;
        [Tooltip("Optional frame around the whole row of pips.")]
        [SerializeField] private Sprite capacityFrame;

        [Header("Vessel (HP) bar")]
        [SerializeField] private Sprite vesselBack;
        [SerializeField] private Sprite vesselFill;
        [SerializeField] private Sprite vesselFrame;

        [Header("Corruption bar")]
        [SerializeField] private Sprite corruptionBack;
        [SerializeField] private Sprite corruptionFill;
        [SerializeField] private Sprite corruptionFrame;

        [Header("Souls bar")]
        [SerializeField] private Sprite soulsBack;
        [SerializeField] private Sprite soulsFill;
        [SerializeField] private Sprite soulsFrame;

        [Header("Quick chat emotes")]
        [Tooltip("One sprite per emote slot (QuickChat.EmoteCount = 8). An empty slot shows a numbered placeholder.")]
        [SerializeField] private Sprite[] emotes = new Sprite[8];

        [Header("Main menu design (Art/UI/MainMenu)")]
        [Tooltip("Full-screen still background. An animated one goes in FxLibrary > Main Menu Background.")]
        [SerializeField] private Sprite menuBackground;
        [SerializeField] private Sprite menuLogo;
        [Tooltip("Boxes and cards behind menu content. 9-slice it.")]
        [SerializeField] private Sprite menuPanel;
        [SerializeField] private Sprite menuButton;
        [SerializeField] private Sprite menuButtonPressed;
        [SerializeField] private Sprite menuBackButton;

        [Header("In-game menu design (Art/UI/InGame)")]
        [Tooltip("The Menu, Settings, Help and result boxes. 9-slice it.")]
        [SerializeField] private Sprite gamePanel;
        [SerializeField] private Sprite gameButton;
        [SerializeField] private Sprite gameButtonPressed;
        [SerializeField] private Sprite gameMenuIcon;
        [SerializeField] private Sprite gameHelpIcon;
        [SerializeField] private Sprite gameBanner;

        [Header("Studio opening (Art/UI/StudioIntro)")]
        [Tooltip("Shown after Made with Unity, before the main menu. A video wins over the logo.")]
        [SerializeField] private VideoClip studioVideo;
        [Tooltip("Still logo that fades in and out when there is no video.")]
        [SerializeField] private Sprite studioLogo;
        [SerializeField] private Color studioBackground = Color.black;
        [Tooltip("How long the logo stays up, in seconds (fades included). Videos play to the end.")]
        [SerializeField] private float studioSeconds = 3f;

        [Header("Colours")]
        [Tooltip("Off: fill sprites show their own colours. On: the game still tints them " +
                 "(Corruption turns red when Corrupted, for example). Draw fills white to tint them.")]
        [SerializeField] private bool tintFills;

        public Sprite CapacityPipLit => capacityPipLit;
        public Sprite CapacityPipSpent => capacityPipSpent;
        public Sprite CapacityFrame => capacityFrame;
        public bool TintFills => tintFills;
        public VideoClip StudioVideo => studioVideo;
        public Sprite StudioLogo => studioLogo;
        public Color StudioBackground => studioBackground;
        public float StudioSeconds => studioSeconds;
        public Sprite MenuBackground => menuBackground;
        public Sprite MenuLogo => menuLogo;
        public Sprite MenuPanel => menuPanel;
        public Sprite MenuButton => menuButton;
        public Sprite MenuButtonPressed => menuButtonPressed;
        public Sprite MenuBackButton => menuBackButton;
        public Sprite GamePanel => gamePanel;
        public Sprite GameButton => gameButton;
        public Sprite GameButtonPressed => gameButtonPressed;
        public Sprite GameMenuIcon => gameMenuIcon;
        public Sprite GameHelpIcon => gameHelpIcon;
        public Sprite GameBanner => gameBanner;

        /// <summary>The art for emote slot index, or null to keep its placeholder.</summary>
        public Sprite GetEmote(int index) =>
            emotes != null && index >= 0 && index < emotes.Length ? emotes[index] : null;

        private static UiSkin _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The skin at Resources/UiSkin, or null if there is none (everything stays placeholder).</summary>
        public static UiSkin Current
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _current = Resources.Load<UiSkin>("UiSkin");
                }
                return _current;
            }
        }

        // ---------- Applying it ----------

        /// <summary>Puts the skin's sprites on a portrait box's bars (call after GothicTheme styled it).</summary>
        public void ApplyToPortrait(Transform portrait)
        {
            if (portrait == null)
                return;
            ApplyBar(portrait.Find("VesselBar"), vesselBack, vesselFill, vesselFrame);
            ApplyBar(portrait.Find("CorruptionBar"), corruptionBack, corruptionFill, corruptionFrame);
            ApplyBar(portrait.Find("SoulsBar"), soulsBack, soulsFill, soulsFrame);
        }

        // A bar is a back Image, a "Fill" child and the theme's "ThemeFrame" child; each slot is optional.
        private void ApplyBar(Transform bar, Sprite back, Sprite fill, Sprite frame)
        {
            if (bar == null)
                return;

            var backImage = (Image)bar.GetComponent(typeof(Image));
            if (back != null && backImage != null)
            {
                backImage.sprite = back;
                backImage.type = Image.Type.Sliced;
                backImage.color = Color.white;
            }

            Transform fillChild = bar.Find("Fill");
            var fillImage = fillChild != null ? (Image)fillChild.GetComponent(typeof(Image)) : null;
            if (fill != null && fillImage != null)
            {
                // Filled keeps the art from squashing as the bar empties; the view sets fillAmount.
                fillImage.sprite = fill;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                var rect = (RectTransform)fillChild;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                if (!tintFills)
                    fillImage.color = Color.white;
            }

            Transform frameChild = bar.Find("ThemeFrame");
            var frameImage = frameChild != null ? (Image)frameChild.GetComponent(typeof(Image)) : null;
            if (frame != null && frameImage != null)
            {
                frameImage.sprite = frame;
                frameImage.type = Image.Type.Sliced;
                frameImage.color = Color.white;
            }
        }
    }
}
