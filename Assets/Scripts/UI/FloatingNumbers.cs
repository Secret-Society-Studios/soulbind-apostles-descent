using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Numbers that rise above an Apostle when something changes: damage, healing, Souls and Corruption.
    /// They fade in, drift up slowly with a soft glow in their colour, and fade out: no bounce.
    /// A delay lets a number wait for its moment (damage appears when the strike lands).
    /// The texts are pooled, so showing a number does not create garbage after the first few.
    /// </summary>
    public sealed class FloatingNumbers : MonoBehaviour
    {
        private const float FadeIn = 0.12f; // shares of the popup's time
        private const float FadeOutFrom = 0.6f;

        private sealed class Popup
        {
            public TextMeshPro Text;
            public Material Material;
            public Vector3 Start;
            public float Age;
            public float Duration;
            public Color Color;
            public bool Active;
        }

        private readonly List<Popup> _popups = new List<Popup>(16);
        private float _duration = 1.3f;
        private float _rise = 0.9f;
        private float _fontSize = 5f;

        public static FloatingNumbers Create(Transform parent, float duration, float rise, float fontSize)
        {
            var go = new GameObject("FloatingNumbers");
            go.transform.SetParent(parent, false);
            FloatingNumbers numbers = go.AddComponent<FloatingNumbers>();
            numbers._duration = Mathf.Max(0.1f, duration);
            numbers._rise = rise;
            numbers._fontSize = fontSize;
            return numbers;
        }

        /// <summary>
        /// Shows a rising number at this world position. With a delay (seconds at normal speed), it
        /// appears only after that long.
        /// </summary>
        public void Show(Vector3 worldPosition, string text, Color color, float delay = 0f)
        {
            Popup popup = Take();
            popup.Start = worldPosition;
            popup.Age = -MotionStyle.Seconds(delay);
            popup.Duration = MotionStyle.Seconds(_duration * 1.5f); // slower and steadier than before
            popup.Color = color;
            popup.Active = true;
            popup.Text.text = text;

            // A soft glow behind the number, in its own colour.
            if (popup.Material != null)
            {
                Color glow = color;
                glow.a = 0.55f;
                popup.Material.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
            }

            popup.Text.gameObject.SetActive(popup.Age >= 0f);
            Place(popup, 0f);
        }

        private Popup Take()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                if (!_popups[i].Active)
                    return _popups[i];
            }

            var go = new GameObject("Popup");
            go.transform.SetParent(transform, false);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.fontSize = _fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
            text.outlineWidth = 0.3f;
            text.outlineColor = new Color32(8, 4, 10, 255);
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sortingOrder = 70; // above the board, pieces, piles and strike effects

            // Its own material (once per pooled text), so each number can glow in its colour.
            Material material = text.fontMaterial;
            if (material != null && material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
            }
            else
            {
                material = null;
            }

            var popup = new Popup { Text = text, Material = material };
            _popups.Add(popup);
            return popup;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                if (!popup.Active)
                    continue;

                bool waiting = popup.Age < 0f;
                popup.Age += delta;
                if (popup.Age < 0f)
                    continue;
                if (waiting)
                    popup.Text.gameObject.SetActive(true);

                float t = popup.Age / popup.Duration;
                if (t >= 1f)
                {
                    popup.Active = false;
                    popup.Text.gameObject.SetActive(false);
                    continue;
                }
                Place(popup, t);
            }
        }

        private void Place(Popup popup, float t)
        {
            Camera cam = Camera.main;
            Transform text = popup.Text.transform;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            text.position = popup.Start + up * (_rise * MotionStyle.Out(t));
            if (cam != null)
                text.rotation = cam.transform.rotation; // always faces the camera

            // Settles from slightly small to full size as it appears; never overshoots.
            float scale = Mathf.Lerp(0.8f, 1f, MotionStyle.Out(Mathf.Clamp01(t / (FadeIn * 2f))));
            text.localScale = new Vector3(scale, scale, 1f);

            float alpha = t < FadeIn ? t / FadeIn
                : t > FadeOutFrom ? 1f - MotionStyle.InOut((t - FadeOutFrom) / (1f - FadeOutFrom))
                : 1f;
            Color color = popup.Color;
            color.a = alpha;
            popup.Text.color = color;
        }
    }
}