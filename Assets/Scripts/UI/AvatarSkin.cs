using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The animated avatar every Apostle uses on the board: frame lists for idle, walking four ways,
    /// attacking and getting hit. It lives at Assets/Resources/AvatarSkin.asset.
    /// The frames are grey ("no colour"), so each Apostle tints them with its own colour.
    /// Empty Idle frames turn the avatar off and the 3D pieces in ApostleArtLibrary come back.
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/Avatar Skin", fileName = "AvatarSkin")]
    public sealed class AvatarSkin : ScriptableObject
    {
        [Header("Frames (in order)")]
        [SerializeField] private Sprite[] idle = new Sprite[0];
        [SerializeField] private Sprite[] walkUp = new Sprite[0];
        [SerializeField] private Sprite[] walkDown = new Sprite[0];
        [SerializeField] private Sprite[] walkLeft = new Sprite[0];
        [SerializeField] private Sprite[] walkRight = new Sprite[0];
        [SerializeField] private Sprite[] attackLeft = new Sprite[0];
        [SerializeField] private Sprite[] attackRight = new Sprite[0];
        [SerializeField] private Sprite[] damagedLeft = new Sprite[0];
        [SerializeField] private Sprite[] damagedRight = new Sprite[0];

        [Header("Speed (frames per second)")]
        [SerializeField, Min(1f)] private float idleFps = 10f;
        [SerializeField, Min(1f)] private float walkFps = 10f;
        [SerializeField, Min(1f)] private float attackFps = 12f;
        [SerializeField, Min(1f)] private float damagedFps = 10f;

        [Header("Look")]
        [Tooltip("How tall the avatar stands, in world units (a tile is about 1 across).")]
        [SerializeField, Min(0.1f)] private float height = 1.1f;
        [Tooltip("How much the Apostle's colour tints the grey frames: 0 = stays grey, 1 = full colour.")]
        [SerializeField, Range(0f, 1f)] private float tint = 0.7f;

        public bool HasFrames => idle != null && idle.Length > 0;
        public Sprite[] Idle => idle;
        public Sprite[] WalkUp => walkUp;
        public Sprite[] WalkDown => walkDown;
        public Sprite[] WalkLeft => walkLeft;
        public Sprite[] WalkRight => walkRight;
        public Sprite[] AttackLeft => attackLeft;
        public Sprite[] AttackRight => attackRight;
        public Sprite[] DamagedLeft => damagedLeft;
        public Sprite[] DamagedRight => damagedRight;
        public float IdleFps => idleFps;
        public float WalkFps => walkFps;
        public float AttackFps => attackFps;
        public float DamagedFps => damagedFps;
        public float Height => height;
        public float Tint => tint;

        private static AvatarSkin _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The skin at Resources/AvatarSkin, or null if there is none.</summary>
        public static AvatarSkin Current
        {
            get
            {
                if (_looked)
                    return _current;
                _looked = true;
                _current = Resources.Load<AvatarSkin>("AvatarSkin");
                return _current;
            }
        }
    }
}
