using System.Collections.Generic;
using System.Text;
using SoulBind.UI;
using UnityEditor;
using UnityEngine;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Fix Card Art Import: sets every card image (the fronts, element medallions and stat
    /// icons in the CardArtLibrary, plus the card back on the open scene's DrawPileView) to import with
    /// mipmaps and smooth filtering, so cards drawn small on the table look clean instead of noisy.
    /// Safe to run again after adding new art.
    /// </summary>
    public static class CardArtImportTools
    {
        private const string LibraryPath = "Assets/Art/CardArtLibrary.asset";

        [MenuItem("SoulBind/Fix Card Art Import")]
        private static void FixCardArt()
        {
            var sprites = new List<Sprite>();

            var library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(LibraryPath);
            if (library != null)
            {
                foreach (CardArtLibrary.CardArt art in library.Cards)
                    sprites.Add(art.Front);
                var so = new SerializedObject(library);
                foreach (string field in new[] { "radiance", "celeste", "crimson", "nocturne", "attackIcon", "defenseIcon", "capIcon" })
                {
                    SerializedProperty property = so.FindProperty(field);
                    if (property != null)
                        sprites.Add(property.objectReferenceValue as Sprite);
                }
            }
            else
            {
                Debug.LogWarning("CardArtImportTools: no CardArtLibrary at " + LibraryPath + ", so only the card back is fixed.");
            }

            foreach (DrawPileView pile in Object.FindObjectsByType<DrawPileView>(FindObjectsSortMode.None))
            {
                SerializedProperty back = new SerializedObject(pile).FindProperty("cardBackSprite");
                if (back != null)
                    sprites.Add(back.objectReferenceValue as Sprite);
            }

            var done = new HashSet<string>();
            var report = new StringBuilder();
            foreach (Sprite sprite in sprites)
            {
                if (sprite == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(sprite);
                if (string.IsNullOrEmpty(path) || !done.Add(path))
                    continue;
                string size = Fix(path);
                if (size != null)
                    report.Append("\n  ").Append(System.IO.Path.GetFileName(path)).Append("  ").Append(size);
            }

            Debug.Log($"CardArtImportTools: fixed {done.Count} card images (mipmaps, trilinear, high-quality compression)." +
                      report);
        }

        // Returns the image's real size ("1024 x 1434"), or null if it isn't a texture.
        private static string Fix(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return null;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int maxSize = 32;
            while (maxSize < Mathf.Max(width, height) && maxSize < 4096)
                maxSize *= 2;

            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter; // keeps small copies sharper than the default
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4; // for cards lying at an angle along the hexagon's sides
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (android.overridden)
            {
                android.maxTextureSize = maxSize;
                android.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(android);
            }

            importer.SaveAndReimport();
            return $"{width} x {height}";
        }
    }
}