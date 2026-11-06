using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoulBind.UI
{
    /// <summary>
    /// The studio's opening billboard: plays once per launch, after Unity's own splash and over the
    /// main menu. Its art comes from UiSkin (Studio opening). With no art in the slots it never shows.
    /// A tap, click or key skips it.
    /// </summary>
    public sealed class StudioIntro : MonoBehaviour
    {
        private const float FadeSeconds = 0.6f; // logo fade in and fade out
        private const int SortOrder = 1000;     // above every menu canvas

        private static bool _played;

        private Image _logo;
        private RawImage _screen;
        private VideoPlayer _video;
        private RenderTexture _texture;
        private float _time;
        private float _length;

        // Runs again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _played = false;

        /// <summary>Shows the opening the first time the main menu loads; later visits skip it.</summary>
        public static void PlayOnce()
        {
            if (_played)
                return;
            _played = true;

            UiSkin skin = UiSkin.Current;
            if (skin == null || (skin.StudioVideo == null && skin.StudioLogo == null))
                return;

            var go = new GameObject("StudioIntro", typeof(RectTransform));
            go.AddComponent<StudioIntro>().Build(skin);
        }

        // Builds a full-screen canvas with the video or the logo on a solid background.
        private void Build(UiSkin skin)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            gameObject.AddComponent<GraphicRaycaster>(); // swallows taps meant for the menu

            var back = (Image)CreateChild("Background").AddComponent(typeof(Image));
            back.color = skin.StudioBackground;

            if (skin.StudioVideo != null)
                BuildVideo(skin.StudioVideo);
            else
                BuildLogo(skin);
        }

        // Plays the clip into a RawImage that fills the screen.
        private void BuildVideo(VideoClip clip)
        {
            _texture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            _screen = (RawImage)CreateChild("Video").AddComponent(typeof(RawImage));
            _screen.texture = _texture;
            _screen.raycastTarget = false;
            var fitter = _screen.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = clip.width / Mathf.Max(1f, clip.height);

            _video = gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.clip = clip;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _texture;
            _video.audioOutputMode = VideoAudioOutputMode.Direct;
            _video.loopPointReached += _ => Finish();
            _video.Play();
            _length = float.MaxValue; // the video ends it
        }

        // Shows the logo in the middle, fading in and out over StudioSeconds.
        private void BuildLogo(UiSkin skin)
        {
            _logo = (Image)CreateChild("Logo").AddComponent(typeof(Image));
            _logo.sprite = skin.StudioLogo;
            _logo.preserveAspect = true;
            _logo.raycastTarget = false;
            var rect = (RectTransform)_logo.transform;
            rect.anchorMin = new Vector2(0.25f, 0.25f);
            rect.anchorMax = new Vector2(0.75f, 0.75f);
            _length = Mathf.Max(skin.StudioSeconds, 2f * FadeSeconds);
            SetLogoAlpha(0f);
        }

        private void Update()
        {
            if (WasSkipPressed())
            {
                Finish();
                return;
            }
            if (_logo == null)
                return;

            _time += Time.unscaledDeltaTime;
            float fadeIn = _time / FadeSeconds;
            float fadeOut = (_length - _time) / FadeSeconds;
            SetLogoAlpha(Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut)));
            if (_time >= _length)
                Finish();
        }

        // Any tap, click or key skips the opening.
        private static bool WasSkipPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return true;
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        // Fades the logo without touching its colour.
        private void SetLogoAlpha(float alpha)
        {
            Color color = _logo.color;
            color.a = alpha;
            _logo.color = color;
        }

        // Removes the opening and frees the video texture.
        private void Finish()
        {
            if (_video != null)
                _video.Stop();
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
            Destroy(gameObject);
        }

        // A child that fills the canvas.
        private GameObject CreateChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }
    }
}
