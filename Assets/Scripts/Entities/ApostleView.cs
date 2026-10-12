using UnityEngine;

namespace SoulBind.Entities
{
    /// <summary>Display for one Apostle. It only shows state; the rules live in SoulBind.Core.</summary>
    public sealed class ApostleView : MonoBehaviour
    {
        private const int SortingOrder = 10;
        private const int HaloSortingOrder = 9;
        private const float ActiveTurnScale = 1.25f;
        private const float HaloScale = 1.45f;
        private static readonly Color HaloColor = new Color(1f, 0.97f, 0.80f);

        private SpriteRenderer _renderer;
        private SpriteRenderer _halo;

        public int ApostleId { get; private set; }

        public void Initialize(int apostleId, Sprite sprite, Color color)
        {
            ApostleId = apostleId;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                Debug.LogError("ApostleView: the Apostle prefab needs a SpriteRenderer.", this);
                return;
            }

            _renderer.sprite = sprite;
            _renderer.color = color;
            _renderer.sortingOrder = SortingOrder;
        }

        /// <summary>The Apostle whose turn it is is drawn larger.</summary>
        public void SetActiveTurn(bool isActive)
        {
            transform.localScale = Vector3.one * (isActive ? ActiveTurnScale : 1f);
        }

        /// <summary>Shows or hides the halo that marks the Awakened Apostle.</summary>
        public void SetAwakened(bool isAwakened)
        {
            if (_halo == null)
            {
                if (!isAwakened)
                    return;
                _halo = CreateHalo();
                if (_halo == null)
                    return;
            }
            _halo.enabled = isAwakened;
        }

        // Builds the halo once: the same disc sprite, larger, behind the Apostle.
        private SpriteRenderer CreateHalo()
        {
            if (_renderer == null || _renderer.sprite == null)
                return null;

            var haloObject = new GameObject("Awakened Halo");
            haloObject.transform.SetParent(transform, false);
            haloObject.transform.localScale = Vector3.one * HaloScale;

            var halo = haloObject.AddComponent<SpriteRenderer>();
            halo.sprite = _renderer.sprite;
            halo.color = HaloColor;
            halo.sortingLayerID = _renderer.sortingLayerID;
            halo.sortingOrder = HaloSortingOrder;
            return halo;
        }
    }
}