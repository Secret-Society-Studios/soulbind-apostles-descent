using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using SoulBind.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// Puts the team's card art into the game:
    /// SoulBind > Add Card Art Support adds the art layers to the CardView prefab and creates the
    /// CardArtLibrary asset; SoulBind > Fill Card Art From Folder fills the library from a folder of sprites.
    /// </summary>
    public static class CardArtTools
    {
        private const string CardPrefabPath = "Assets/Prefabs/Cards/CardView.prefab";
        private const string LibraryPath = "Assets/Art/CardArtLibrary.asset";
        private const string DefaultArtFolder = "Assets/Art/Sprites/Cards";

        [MenuItem("SoulBind/Add Card Art Support")]
        private static void AddCardArtSupport()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) == null)
            {
                EditorUtility.DisplayDialog("Add Card Art Support",
                    "Couldn't find " + CardPrefabPath + ". Run SoulBind > Build Match HUD first.", "OK");
                return;
            }

            CardArtLibrary library = LoadOrCreateLibrary();
            GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                AddArtLayers(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Selection.activeObject = library;
            Debug.Log("CardArtTools: the CardView prefab now shows card art from " + LibraryPath +
                      ". Fill it in the Inspector, or with SoulBind > Fill Card Art From Folder.");
        }

        [MenuItem("SoulBind/Fill Card Art From Folder")]
        private static void FillFromFolder()
        {
            CardArtLibrary library = LoadOrCreateLibrary();
            EnsurePrefabSupportsArt(library);

            // The folder selected in the Project window, or the default art folder.
            string folder = DefaultArtFolder;
            if (Selection.activeObject != null)
            {
                string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (AssetDatabase.IsValidFolder(selected))
                    folder = selected;
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Fill Card Art From Folder",
                    "Select the folder with the card sprites in the Project window, then run this again.", "OK");
                return;
            }

            // Card names, simplified ("The High Priestess" and "HighPriestess" both become "highpriestess").
            var cardsByName = new Dictionary<string, ArcanaId>();
            foreach (CardDefinition card in CardCatalog.All)
            {
                cardsByName[Simplify(card.Name)] = card.Id;
                cardsByName[Simplify(card.Id.ToString())] = card.Id;
            }

            var namedSlots = new Dictionary<string, Sprite>();
            var found = new HashSet<ArcanaId>();
            var unmatched = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;
                string name = Simplify(sprite.name);

                if (cardsByName.TryGetValue(name, out ArcanaId id))
                {
                    library.SetFront(id, sprite);
                    found.Add(id);
                }
                else if (!TryNameSlot(name, sprite, namedSlots))
                {
                    unmatched.Append("\n  ").Append(sprite.name);
                }
            }
            library.EnsureEntryPerCard();
            if (namedSlots.Count > 0)
            {
                var so = new SerializedObject(library);
                foreach (KeyValuePair<string, Sprite> slot in namedSlots)
                    so.FindProperty(slot.Key).objectReferenceValue = slot.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            var missing = new StringBuilder();
            foreach (CardDefinition card in CardCatalog.All)
            {
                if (!found.Contains(card.Id) && library.GetFront(card.Id) == null)
                    missing.Append("\n  ").Append(card.Name);
            }

            Debug.Log($"CardArtTools: matched {found.Count} card fronts from {folder}." +
                      (missing.Length > 0 ? "\nStill without art:" + missing : "\nEvery card has art.") +
                      (unmatched.Length > 0 ? "\nSprites that matched nothing (rename them or drag them in by hand):" + unmatched : string.Empty));
            Selection.activeObject = library;
        }

        // Adds the art layer to the card prefab the first time (or after Build Match HUD rebuilt it).
        private static void EnsurePrefabSupportsArt(CardArtLibrary library)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (asset == null || asset.transform.Find("FullArt") != null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                AddArtLayers(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Element medallions and stat icons, recognised by name.
        private static bool TryNameSlot(string name, Sprite sprite, Dictionary<string, Sprite> slots)
        {
            string slot = null;
            if (name.Contains("radiance")) slot = "radiance";
            else if (name.Contains("celeste")) slot = "celeste";
            else if (name.Contains("crimson")) slot = "crimson";
            else if (name.Contains("nocturne")) slot = "nocturne";
            else if (name.Contains("atk") || name.Contains("attack")) slot = "attackIcon";
            else if (name.Contains("def")) slot = "defenseIcon";
            else if (name.Contains("cap")) slot = "capIcon";
            if (slot == null)
                return false;
            slots[slot] = sprite;
            return true;
        }

        // Lowercase letters only, without a leading "the" or any numbers ("02_The_High-Priestess" -> "highpriestess").
        private static string Simplify(string name)
        {
            var letters = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetter(c))
                    letters.Append(char.ToLowerInvariant(c));
            }
            string result = letters.ToString();
            if (result.StartsWith("the") && result.Length > 3)
                result = result.Substring(3);
            if (result.StartsWith("card") && result.Length > 4)
                result = result.Substring(4);
            return result;
        }

        private static CardArtLibrary LoadOrCreateLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(LibraryPath);
            if (library != null)
                return library;

            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            library = ScriptableObject.CreateInstance<CardArtLibrary>();
            library.EnsureEntryPerCard();
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            return library;
        }

        // Adds the full-card art image and the stat icon slots, and connects everything to CardView.
        private static void AddArtLayers(GameObject root, CardArtLibrary library)
        {
            CardView view = root.GetComponent<CardView>();
            Transform card = root.transform;

            Image frame = FindImage(card, "Frame");
            Image inner = FindImage(card, "Inner");
            Image artWindow = FindImage(card, "ArtWindow");
            Image namePlate = FindImage(card, "NamePlate");
            Image effectBox = FindImage(card, "EffectBox");

            // The flat front goes above the placeholder frame and art window, below every text layer.
            Transform fullArtTransform = card.Find("FullArt");
            if (fullArtTransform == null)
            {
                var go = new GameObject("FullArt", typeof(RectTransform));
                go.layer = root.layer;
                fullArtTransform = go.transform;
                fullArtTransform.SetParent(card, false);
            }
            var fullArtRect = (RectTransform)fullArtTransform;
            fullArtRect.anchorMin = Vector2.zero;
            fullArtRect.anchorMax = Vector2.one;
            fullArtRect.offsetMin = Vector2.zero;
            fullArtRect.offsetMax = Vector2.zero;
            Transform after = artWindow != null ? artWindow.transform : inner != null ? inner.transform : null;
            if (after != null)
                fullArtTransform.SetSiblingIndex(after.GetSiblingIndex() + 1);
            Image fullArt = fullArtTransform.GetComponent<Image>();
            if (fullArt == null)
                fullArt = fullArtTransform.gameObject.AddComponent<Image>();
            fullArt.raycastTarget = false;
            fullArt.enabled = false;

            var plates = new List<Graphic>();
            if (namePlate != null) plates.Add(namePlate);
            if (effectBox != null) plates.Add(effectBox);
            var labels = new List<Graphic>();
            Image attackIcon = PrepareBadge(card, "AtkBadge", plates, labels);
            Image defenseIcon = PrepareBadge(card, "DefBadge", plates, labels);
            Image capIcon = PrepareBadge(card, "CapBadge", plates, labels);

            var so = new SerializedObject(view);
            so.FindProperty("artLibrary").objectReferenceValue = library;
            so.FindProperty("fullArt").objectReferenceValue = fullArt;
            so.FindProperty("frame").objectReferenceValue = frame;
            SetArray(so.FindProperty("placeholderParts"), new Graphic[] { inner, artWindow });
            SetArray(so.FindProperty("plates"), plates.ToArray());
            so.FindProperty("attackIcon").objectReferenceValue = attackIcon;
            so.FindProperty("defenseIcon").objectReferenceValue = defenseIcon;
            so.FindProperty("capIcon").objectReferenceValue = capIcon;
            SetArray(so.FindProperty("statLabels"), labels.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A stat badge gets an Icon behind its number. Its plate and its small label word are collected.
        private static Image PrepareBadge(Transform card, string badgeName, List<Graphic> plates, List<Graphic> labels)
        {
            Transform badge = card.Find(badgeName);
            if (badge == null)
                return null;

            Image plate = badge.GetComponent<Image>();
            if (plate != null)
                plates.Add(plate);
            Transform label = badge.Find("Label");
            if (label != null && label.GetComponent<Graphic>() != null)
                labels.Add(label.GetComponent<Graphic>());

            Transform iconTransform = badge.Find("Icon");
            if (iconTransform == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform));
                go.layer = badge.gameObject.layer;
                iconTransform = go.transform;
                iconTransform.SetParent(badge, false);
            }
            iconTransform.SetSiblingIndex(0); // behind the number
            var rect = (RectTransform)iconTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image icon = iconTransform.GetComponent<Image>();
            if (icon == null)
                icon = iconTransform.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.enabled = false;
            return icon;
        }

        private static Image FindImage(Transform card, string childName)
        {
            Transform child = card.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void SetArray(SerializedProperty property, Graphic[] graphics)
        {
            var kept = new List<Graphic>();
            foreach (Graphic graphic in graphics)
            {
                if (graphic != null)
                    kept.Add(graphic);
            }
            property.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
        }
    }
}