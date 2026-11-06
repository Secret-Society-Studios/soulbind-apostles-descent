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
        [Tooltip("On: the animated avatar from Resources/AvatarSkin. Off: the 3D pieces above.")]
        [SerializeField] private bool useAnimatedAvatar = true;

        private SpriteRenderer _renderer;
        private SpriteRenderer _halo;

        // The standee's renderers, found once, so TopPoint allocates nothing each frame.
        private Renderer[] _pieceRenderers;

        // The animated avatar, or null while the 3D piece (or just the disc) is shown.
        private AvatarAnimator _avatar;

        public int ApostleId { get; private set; }

        // Sets up the piece for one Apostle: id, art and colour.
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

            if (!SpawnAvatar(color))
                SpawnPiece(apostleId);
        }

        /// <summary>The avatar swings towards a world direction.</summary>
        public void PlayAttack(Vector3 direction)
        {
            if (_avatar != null)
                _avatar.PlayAttack(direction);
        }

        /// <summary>The avatar flinches, facing the world direction the hit came from.</summary>
        public void PlayDamaged(Vector3 towardAttacker)
        {
            if (_avatar != null)
                _avatar.PlayDamaged(towardAttacker);
        }

        /// <summary>Holds the avatar still, for The World's stopped time.</summary>
        public void SetFrozen(bool frozen)
        {
            if (_avatar != null)
                _avatar.SetFrozen(frozen);
        }

        /// <summary>Shows the avatar's Corrupted look, or back to normal.</summary>
        public void SetCorrupted(bool corrupted)
        {
            if (_avatar != null)
                _avatar.SetCorrupted(corrupted);
        }

        /// <summary>While it is this Apostle's turn, the avatar keeps facing the way it walked.</summary>
        public void SetHoldsPosture(bool holds)
        {
            if (_avatar != null)
                _avatar.SetHoldsPosture(holds);
        }

        /// <summary>
        /// The top of the piece in world space: the avatar's head, or for a 3D piece the point nearest
        /// the camera, since the board faces -Z and pieces stand toward it. Name plates float above this.
        /// </summary>
        public Vector3 TopPoint
        {
            get
            {
                if (_avatar != null)
                    return _avatar.TopPoint;
                Vector3 position = transform.position;
                float top = position.z + pieceLift;
                if (_pieceRenderers != null)
                {
                    for (int i = 0; i < _pieceRenderers.Length; i++)
                    {
                        if (_pieceRenderers[i] != null)
                            top = Mathf.Min(top, _pieceRenderers[i].bounds.min.z);
                    }
                }
                return new Vector3(position.x, position.y, top);
            }
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

        // Stands the animated avatar on the disc, tinted with the Apostle's colour. False if there are no frames.
        private bool SpawnAvatar(Color color)
        {
            if (!useAnimatedAvatar)
                return false;
            _avatar = AvatarAnimator.Create(transform, AvatarSkin.Current, color, PieceSortingOrder);
            if (_avatar == null)
                return false;
            _avatar.transform.localPosition = new Vector3(0f, 0f, pieceLift);
            return true;
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

            _pieceRenderers = piece.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in _pieceRenderers)
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
