using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Small numbers that rise and fade above an Apostle when something changes: damage, healing,
    /// Souls and Corruption. They make it easy to follow what bots (and you) just did.
    /// The texts are pooled, so showing a number does not create garbage after the first few.
    /// </summary>
    public sealed class FloatingNumbers : MonoBehaviour
    {
        private sealed class Popup
        {
            public TextMeshPro Text;
            public Vector3 Start;
            public float Age;
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

        /// <summary>Shows a rising number at this world position (it starts there and floats up).</summary>
        public void Show(Vector3 worldPosition, string text, Color color)
        {
            Popup popup = Take();
            popup.Start = worldPosition;
            popup.Age = 0f;
            popup.Color = color;
            popup.Active = true;
            popup.Text.text = text;
            popup.Text.color = color;
            popup.Text.gameObject.SetActive(true);
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
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sortingOrder = 50; // above the board, discs and piles

            var popup = new Popup { Text = text };
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

                popup.Age += delta;
                float t = popup.Age / _duration;
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
            float eased = 1f - (1f - t) * (1f - t);
            text.position = popup.Start + up * (_rise * eased);
            if (cam != null)
                text.rotation = cam.transform.rotation; // always faces the camera

            Color color = popup.Color;
            color.a = 1f - t * t;
            popup.Text.color = color;
        }
    }
}