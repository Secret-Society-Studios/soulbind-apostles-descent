using UnityEngine;

namespace SoulBind.Entities
{
    /// <summary>Display for one Apostle. It only shows state; the rules live in SoulBind.Core.</summary>
    public sealed class ApostleView : MonoBehaviour
    {
        private const int SortingOrder = 10;
        private const float ActiveTurnScale = 1.25f;

        public int ApostleId { get; private set; }

        public void Initialize(int apostleId, Sprite sprite, Color color)
        {
            ApostleId = apostleId;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("ApostleView: the Apostle prefab needs a SpriteRenderer.", this);
                return;
            }

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = SortingOrder;
        }

        /// <summary>The Apostle whose turn it is is drawn larger.</summary>
        public void SetActiveTurn(bool isActive)
        {
            transform.localScale = Vector3.one * (isActive ? ActiveTurnScale : 1f);
        }
    }
}