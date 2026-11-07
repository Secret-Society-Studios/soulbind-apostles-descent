using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// The slots for every music track and sound effect. It lives at Assets/Resources/SoundLibrary.asset.
    /// Drag a clip into a slot and the game plays it; an empty slot stays silent, so the sounds can
    /// arrive one at a time. Put the files in Assets/Audio (Music, SFX/Game, SFX/UI, SFX/Cards).
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        // ---------- Music ----------

        [Header("Music (at least 3 tracks)")]
        [Tooltip("Loops on the main menu.")]
        [SerializeField] private AudioClip mainMenuMusic;
        [Tooltip("Loops during a match.")]
        [SerializeField] private AudioClip matchMusic;
        [Tooltip("Plays on the result screen.")]
        [SerializeField] private AudioClip resultMusic;
        [Tooltip("Spare tracks for later (lobby, tutorial, Awakened...).")]
        [SerializeField] private AudioClip[] extraMusic = new AudioClip[0];

        // ---------- In-game SFX ----------

        [Header("In-game SFX")]
        [SerializeField] private AudioClip move;
        [SerializeField] private AudioClip spawnLand;
        [SerializeField] private AudioClip attackHit;
        [SerializeField] private AudioClip shieldBreak;
        [Tooltip("A Heal tile is picked up.")]
        [SerializeField] private AudioClip heal;
        [Tooltip("A Purify tile is picked up.")]
        [SerializeField] private AudioClip purify;
        [Tooltip("A Soul tile is picked up.")]
        [SerializeField] private AudioClip soulGained;
        [SerializeField] private AudioClip awaken;
        [SerializeField] private AudioClip death;
        [SerializeField] private AudioClip cardDraw;
        [Tooltip("A card is played and has no sound of its own in Cards below.")]
        [SerializeField] private AudioClip cardPlay;
        [Tooltip("A card strengthens the Apostle who played it.")]
        [SerializeField] private AudioClip buff;
        [Tooltip("An opponent's card puts a lasting penalty on an Apostle.")]
        [SerializeField] private AudioClip debuff;
        [Tooltip("An opponent's card gives an Apostle Corruption.")]
        [SerializeField] private AudioClip corrupt;
        [Tooltip("Your turn starts.")]
        [SerializeField] private AudioClip turnStart;
        [SerializeField] private AudioClip victory;
        [SerializeField] private AudioClip defeat;

        // ---------- UI SFX ----------

        [Header("UI interaction SFX")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip buttonHover;
        [SerializeField] private AudioClip popupOpen;
        [SerializeField] private AudioClip popupClose;
        [SerializeField] private AudioClip notAllowed;

        // ---------- Card SFX ----------

        [Header("Card SFX (one per card in the Arcana)")]
        [Tooltip("Index = card id: 0 Fool, 1 Magician ... 21 World. Plays when that card is played, instead " +
                 "of Card Play. The Emperor, Moon, Sun and World also have their moment's sound in FxLibrary.")]
        [SerializeField] private AudioClip[] cards = new AudioClip[ArcanaCount];

        private const int ArcanaCount = 22; // The Fool to The World

        public AudioClip MainMenuMusic => mainMenuMusic;
        public AudioClip MatchMusic => matchMusic;
        public AudioClip ResultMusic => resultMusic;
        public AudioClip Move => move;
        public AudioClip SpawnLand => spawnLand;
        public AudioClip AttackHit => attackHit;
        public AudioClip ShieldBreak => shieldBreak;
        public AudioClip Heal => heal;
        public AudioClip Purify => purify;
        public AudioClip SoulGained => soulGained;
        public AudioClip Awaken => awaken;
        public AudioClip Death => death;
        public AudioClip CardDraw => cardDraw;
        public AudioClip CardPlay => cardPlay;
        public AudioClip Buff => buff;
        public AudioClip Debuff => debuff;
        public AudioClip Corrupt => corrupt;
        public AudioClip TurnStart => turnStart;
        public AudioClip Victory => victory;
        public AudioClip Defeat => defeat;
        public AudioClip ButtonClick => buttonClick;
        public AudioClip ButtonHover => buttonHover;
        public AudioClip PopupOpen => popupOpen;
        public AudioClip PopupClose => popupClose;
        public AudioClip NotAllowed => notAllowed;

        // A spare music track, or null when the slot is empty.
        public AudioClip GetExtraMusic(int index) =>
            extraMusic != null && index >= 0 && index < extraMusic.Length ? extraMusic[index] : null;

        // The sound for a card, or null to stay silent.
        public AudioClip GetCard(ArcanaId card) =>
            cards != null && (int)card < cards.Length ? cards[(int)card] : null;

        // ---------- Finding it ----------

        private static SoundLibrary _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The library at Resources/SoundLibrary, or null (the game stays silent).</summary>
        public static SoundLibrary Current
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _current = Resources.Load<SoundLibrary>("SoundLibrary");
                }
                return _current;
            }
        }
    }
}
