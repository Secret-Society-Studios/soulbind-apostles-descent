using SoulBind.UI;
using UnityEngine;

namespace SoulBind.Entities
{
    /// <summary>Display for one Apostle. It only shows state; the rules live in SoulBind.Core.</summary>
    public sealed class ApostleView : MonoBehaviour
    {
        private const int SortingOrder = 10;
        private const int HaloSortingOrder = 9;
        private const int PieceSortingOrder = 11;
        private const float ActiveTurnScale = 1.25f;
        private const float HaloScale = 1.45f;
        private static readonly Color HaloColor = new Color(1f, 0.97f, 0.80f);

        [Header("Board piece (optional: empty keeps just the disc)")]
        [SerializeField] private ApostleArtLibrary art;
        [SerializeField] private float pieceScale = 0.6f;
        [Tooltip("Negative Z lifts the piece toward the camera, above the tiles.")]
        [SerializeField] private float pieceLift = -0.05f;
        [Tooltip("Set to 180 if you see the back of the standee.")]
        [SerializeField] private float pieceYaw = 0f;

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

            SpawnPiece(apostleId);
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

        // Puts the Apostle's standee (or pawn) on top of its colored disc.
        private void SpawnPiece(int apostleId)
        {
            GameObject prefab = art != null ? art.GetPiece(apostleId) : null;
            if (prefab == null)
                return;

            GameObject piece = Instantiate(prefab, transform);
            piece.name = "Piece";
            piece.transform.localPosition = new Vector3(0f, 0f, pieceLift);
            // The models stand along +Y; the board faces -Z, so tip them up toward the camera.
            piece.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, pieceYaw, 0f);
            piece.transform.localScale = Vector3.one * pieceScale;

            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
                r.sortingOrder = PieceSortingOrder;
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