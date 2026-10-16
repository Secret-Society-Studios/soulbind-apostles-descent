using System;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Each Apostle's art in one asset: the HUD portrait and the board piece.
    /// Entry 0 is Apostle 1. Empty slots keep the placeholder look.
    /// </summary>
    [CreateAssetMenu(fileName = "ApostleArtLibrary", menuName = "SoulBind/Apostle Art Library")]
    public sealed class ApostleArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct ApostleArt
        {
            public Sprite portrait;
            public GameObject piece;
        }

        [SerializeField] private ApostleArt[] apostles = new ApostleArt[6];

        public Sprite GetPortrait(int apostleId) => Get(apostleId).portrait;
        public GameObject GetPiece(int apostleId) => Get(apostleId).piece;

        private ApostleArt Get(int apostleId)
        {
            if (apostles == null || apostles.Length == 0)
                return default;
            return apostles[(apostleId - 1 + apostles.Length) % apostles.Length];
        }
    }
}