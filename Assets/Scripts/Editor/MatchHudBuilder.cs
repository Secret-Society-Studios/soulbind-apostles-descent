using SoulBind.Managers;
using SoulBind.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// Editor-only tool: SoulBind > Build Match HUD. Builds the HUD hierarchy under the selected
    /// Canvas and saves the Apostle portrait box as a prefab, all wired up. Everything it makes is an
    /// ordinary GameObject or prefab, so it can be restyled by hand afterwards.
    /// Running it again replaces the HUD (and the panel prefab) with fresh placeholders.
    /// </summary>
    public static class MatchHudBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string PanelPrefabPath = PrefabFolder + "/ApostlePanel.prefab";
        private const string HudName = "MatchHud";

        private const float PortraitWidth = 280f;
        private const float PortraitHeight = 96f;
        private const float EdgeMargin = 20f;
        private const float HandHeight = 150f;
        private const float PlayerScale = 1.25f;

        private static readonly Color BoxColor = new Color(0.07f, 0.06f, 0.09f, 0.85f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color VesselColor = new Color(0.80f, 0.20f, 0.25f);
        private static readonly Color CorruptionColor = new Color(0.55f, 0.30f, 0.70f);
        private static readonly Color PlaceholderTextColor = new Color(1f, 1f, 1f, 0.35f);

        [MenuItem("SoulBind/Build Match HUD")]
        private static void Build()
        {
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Build Match HUD", "Select the Canvas in the Hierarchy first, then run this again.", "OK");
                return;
            }
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                Debug.LogWarning("MatchHudBuilder: the Canvas is not Screen Space - Overlay. The HUD expects Overlay.", canvas);

            ConfigureScaler(canvas);
            ApostlePanelView panelPrefab = BuildPanelPrefab();

            Transform old = canvas.transform.Find(HudName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            // Root: fills the whole Canvas. Nothing in it blocks clicks on the board.
            RectTransform root = CreateRect(HudName, canvas.transform);
            Stretch(root, 0f, 0f, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Match HUD");

            // Top-left: the opponents' portraits in a row.
            RectTransform opponentRow = CreateRect("OpponentRow", root);
            Corner(opponentRow, new Vector2(0f, 1f), new Vector2(EdgeMargin, -EdgeMargin),
                new Vector2(5f * PortraitWidth + 4f * 10f, PortraitHeight));
            AddRowLayout(opponentRow.gameObject, 10f, TextAnchor.UpperLeft);

            // Top-right: round, Phase, whose turn and Capacity.
            RectTransform turnInfo = CreateRect("TurnInfo", root);
            Corner(turnInfo, new Vector2(1f, 1f), new Vector2(-EdgeMargin, -EdgeMargin), new Vector2(330f, PortraitHeight));
            AddImage(turnInfo.gameObject, BoxColor);
            TMP_Text roundText = CreateText("RoundText", turnInfo, "Round 1   Phase 1", 20f, TextAlignmentOptions.Right);
            TopRow(roundText.rectTransform, 12f, 12f, 6f, 26f);
            TMP_Text turnText = CreateText("TurnText", turnInfo, "Apostle 1's turn", 26f, TextAlignmentOptions.Right);
            TopRow(turnText.rectTransform, 12f, 12f, 32f, 32f);
            TMP_Text capacityText = CreateText("CapacityText", turnInfo, "Capacity 8/8", 20f, TextAlignmentOptions.Right);
            TopRow(capacityText.rectTransform, 12f, 12f, 64f, 26f);

            // Bottom-right: the current Apostle's portrait, a little larger.
            RectTransform playerSlot = CreateRect("PlayerSlot", root);
            Corner(playerSlot, new Vector2(1f, 0f), new Vector2(-EdgeMargin, EdgeMargin),
                new Vector2(PortraitWidth, PortraitHeight));
            playerSlot.localScale = Vector3.one * PlayerScale;
            AddRowLayout(playerSlot.gameObject, 0f, TextAnchor.MiddleCenter);

            // Above it: Strike / End Turn (U2).
            RectTransform actionArea = CreateRect("ActionArea", root);
            Corner(actionArea, new Vector2(1f, 0f),
                new Vector2(-EdgeMargin, EdgeMargin + PortraitHeight * PlayerScale + 10f), new Vector2(PortraitWidth * PlayerScale, 60f));
            TMP_Text actionLabel = CreateText("Placeholder", actionArea, "Strike / End Turn (U2)", 20f, TextAlignmentOptions.Center);
            Stretch(actionLabel.rectTransform, 0f, 0f, 0f, 0f);
            actionLabel.color = PlaceholderTextColor;

            // Bottom center: the hand (U2). Leaves room for the portrait on the right.
            RectTransform handArea = CreateRect("HandArea", root);
            handArea.anchorMin = new Vector2(0f, 0f);
            handArea.anchorMax = new Vector2(1f, 0f);
            handArea.pivot = new Vector2(0.5f, 0f);
            handArea.offsetMin = new Vector2(EdgeMargin, 0f);
            handArea.offsetMax = new Vector2(-(EdgeMargin * 2f + PortraitWidth * PlayerScale), HandHeight);
            TMP_Text handLabel = CreateText("Placeholder", handArea, "Hand (coming in U2)", 24f, TextAlignmentOptions.Center);
            Stretch(handLabel.rectTransform, 0f, 0f, 0f, 0f);
            handLabel.color = PlaceholderTextColor;

            // The script that drives it all.
            MatchHud hud = root.gameObject.AddComponent<MatchHud>();
            var so = new SerializedObject(hud);
            GameObject controllerObject = GameObject.Find("MatchController");
            so.FindProperty("matchController").objectReferenceValue =
                controllerObject != null ? controllerObject.GetComponent<MatchController>() : null;
            so.FindProperty("topReservedArea").objectReferenceValue = opponentRow;
            so.FindProperty("bottomReservedArea").objectReferenceValue = handArea;
            so.FindProperty("opponentRow").objectReferenceValue = opponentRow;
            so.FindProperty("playerSlot").objectReferenceValue = playerSlot;
            so.FindProperty("panelPrefab").objectReferenceValue = panelPrefab;
            so.FindProperty("roundText").objectReferenceValue = roundText;
            so.FindProperty("turnText").objectReferenceValue = turnText;
            so.FindProperty("capacityText").objectReferenceValue = capacityText;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (controllerObject == null)
                Debug.LogWarning("MatchHudBuilder: no GameObject named 'MatchController' was found. " +
                                 "Drag it into MatchHud's Match Controller field.", hud);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("MatchHudBuilder: built the Match HUD and " + PanelPrefabPath + ". Save the scene.");
        }

        // 1920x1080 reference, scaling evenly between width and height.
        private static void ConfigureScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Builds the Apostle portrait box in a temporary object, saves it as a prefab and returns the asset.
        private static ApostlePanelView BuildPanelPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            RectTransform panel = CreateRect("ApostlePanel", null);
            panel.sizeDelta = new Vector2(PortraitWidth, PortraitHeight);
            Image background = AddImage(panel.gameObject, new Color(0.16f, 0.14f, 0.20f, 0.95f));
            LayoutElement layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = PortraitWidth;
            layout.preferredHeight = PortraitHeight;

            // Portrait square on the left (placeholder: the Apostle's color with "A1" on it).
            RectTransform portrait = CreateRect("Portrait", panel);
            portrait.anchorMin = new Vector2(0f, 0f);
            portrait.anchorMax = new Vector2(0f, 1f);
            portrait.pivot = new Vector2(0f, 0.5f);
            portrait.offsetMin = new Vector2(8f, 8f);
            portrait.offsetMax = new Vector2(88f, -8f);
            Image portraitImage = AddImage(portrait.gameObject, Color.white);
            TMP_Text portraitText = CreateText("PortraitText", portrait, "A1", 30f, TextAlignmentOptions.Center);
            Stretch(portraitText.rectTransform, 0f, 0f, 0f, 0f);
            portraitText.fontStyle = FontStyles.Bold;
            portraitText.color = new Color(0f, 0f, 0f, 0.6f);

            TMP_Text nameText = CreateText("NameText", panel, "Apostle 1", 18f, TextAlignmentOptions.Left);
            TopRow(nameText.rectTransform, 98f, 90f, 4f, 22f);
            nameText.fontStyle = FontStyles.Bold;
            TMP_Text statusText = CreateText("StatusText", panel, "", 14f, TextAlignmentOptions.Right);
            TopRow(statusText.rectTransform, 170f, 8f, 6f, 20f);
            statusText.color = new Color(1f, 0.85f, 0.35f);

            CreateBar(panel, "VesselBar", 28f, 18f, VesselColor, "Vessel 50/50",
                out RectTransform vesselFill, out _, out TMP_Text vesselText);
            CreateBar(panel, "CorruptionBar", 50f, 16f, CorruptionColor, "Corruption 0",
                out RectTransform corruptionFill, out Image corruptionFillImage, out TMP_Text corruptionText);
            corruptionFill.anchorMax = new Vector2(0f, 1f);

            TMP_Text statsText = CreateText("StatsText", panel, "Souls 7/20   DEF 0", 14f, TextAlignmentOptions.Left);
            TopRow(statsText.rectTransform, 98f, 8f, 70f, 22f);

            ApostlePanelView view = panel.gameObject.AddComponent<ApostlePanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("colorStripe").objectReferenceValue = portraitImage;
            so.FindProperty("portraitText").objectReferenceValue = portraitText;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("vesselFill").objectReferenceValue = vesselFill;
            so.FindProperty("vesselText").objectReferenceValue = vesselText;
            so.FindProperty("corruptionFill").objectReferenceValue = corruptionFill;
            so.FindProperty("corruptionFillImage").objectReferenceValue = corruptionFillImage;
            so.FindProperty("corruptionText").objectReferenceValue = corruptionText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, PanelPrefabPath);
            Object.DestroyImmediate(panel.gameObject);
            return asset.GetComponent<ApostlePanelView>();
        }

        // A bar: dark background, a colored fill (its width set by script), and a label on top.
        private static void CreateBar(RectTransform parent, string name, float top, float height, Color fillColor,
            string label, out RectTransform fill, out Image fillImage, out TMP_Text text)
        {
            RectTransform bar = CreateRect(name, parent);
            TopRow(bar, 98f, 8f, top, height);
            AddImage(bar.gameObject, BarBackColor);

            fill = CreateRect("Fill", bar);
            Stretch(fill, 0f, 0f, 0f, 0f);
            fillImage = AddImage(fill.gameObject, fillColor);

            text = CreateText("Label", bar, label, 13f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        }

        // ---- Small layout helpers ----

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void AddRowLayout(GameObject go, float spacing, TextAnchor alignment)
        {
            HorizontalLayoutGroup row = go.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = alignment;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
        }

        // Fills the parent, inset by these margins.
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // A fixed-size box pinned to a corner of the parent (corner (0,1) is top-left, (1,0) bottom-right).
        // offset moves it in from that corner, in pixels.
        private static void Corner(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        // A row of fixed height, a given distance below the parent's top edge.
        private static void TopRow(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}