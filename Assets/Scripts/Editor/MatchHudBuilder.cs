using SoulBind.Managers;
using SoulBind.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string CardPrefabFolder = "Assets/Prefabs/Cards";
        private const string CardPrefabPath = CardPrefabFolder + "/CardView.prefab";
        private const float CardWidth = 180f;
        private const float CardHeight = 252f;
        private const float ActionWidth = 350f;
        private const float PreviewScale = 1.6f;

        private static readonly Color FrameColor = new Color(0.62f, 0.48f, 0.24f);
        private static readonly Color CardInnerColor = new Color(0.12f, 0.11f, 0.18f);
        private static readonly Color ParchmentColor = new Color(0.88f, 0.80f, 0.58f);
        private static readonly Color InkColor = new Color(0.12f, 0.10f, 0.08f);
        private static readonly Color BadgeColor = new Color(0.08f, 0.07f, 0.10f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.18f, 0.30f);
        private static readonly Color StrikeColor = new Color(0.65f, 0.15f, 0.20f);
        private const string HudName = "MatchHud";
        private const string PileName = "DiscardPile";
        private const string DrawPileName = "DrawPile";
        private const float PileOffset = 0.65f; // both piles sit this many card heights from the side area's middle

        private const float PortraitWidth = 280f;
        private const float PortraitHeight = 96f;
        private const float EdgeMargin = 20f;
        private const float HandHeight = 150f;
        private const float PlayerScale = 1.25f;

        private static readonly Color BoxColor = new Color(0.07f, 0.06f, 0.09f, 0.85f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color VesselColor = new Color(0.80f, 0.20f, 0.25f);
        private static readonly Color CorruptionColor = new Color(0.55f, 0.30f, 0.70f);

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
            CardView cardPrefab = BuildCardPrefab();

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

            // Above it: hint text and the action buttons, stacked from the bottom up.
            RectTransform actionArea = CreateRect("ActionArea", root);
            Corner(actionArea, new Vector2(1f, 0f),
                new Vector2(-EdgeMargin, EdgeMargin + PortraitHeight * PlayerScale + 10f), new Vector2(ActionWidth, 380f));
            VerticalLayoutGroup column = actionArea.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6f;
            column.childAlignment = TextAnchor.LowerCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            TMP_Text hintText = CreateText("HintText", actionArea, "", 18f, TextAlignmentOptions.BottomRight);
            hintText.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            Button forwardButton = CreateButton("ForwardButton", actionArea, "Turn direction: Forward", ButtonColor, out _);
            Button backwardButton = CreateButton("BackwardButton", actionArea, "Turn direction: Backward", ButtonColor, out _);
            Button strikeButton = CreateButton("StrikeButton", actionArea, "Strike", StrikeColor, out TMP_Text strikeLabel);
            Button noAttackButton = CreateButton("NoAttackButton", actionArea, "Play without attacking", ButtonColor, out _);
            Button cancelButton = CreateButton("CancelButton", actionArea, "Cancel", ButtonColor, out _);
            Button endTurnButton = CreateButton("EndTurnButton", actionArea, "End Turn", ButtonColor, out _);

            // Bottom center: the hand. Leaves room for the portrait and buttons on the right.
            RectTransform handArea = CreateRect("HandArea", root);
            handArea.anchorMin = new Vector2(0f, 0f);
            handArea.anchorMax = new Vector2(1f, 0f);
            handArea.pivot = new Vector2(0.5f, 0f);
            handArea.offsetMin = new Vector2(EdgeMargin, 0f);
            handArea.offsetMax = new Vector2(-(EdgeMargin * 2f + ActionWidth), HandHeight);

            // The large preview of a hovered card, left of the buttons.
            var previewObject = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, root);
            previewObject.name = "CardPreview";
            var previewRect = (RectTransform)previewObject.transform;
            Corner(previewRect, new Vector2(1f, 0.5f),
                new Vector2(-(EdgeMargin * 2f + ActionWidth), 60f), new Vector2(CardWidth, CardHeight));
            previewRect.pivot = new Vector2(1f, 0.5f);
            previewRect.localScale = Vector3.one * PreviewScale;
            CardView preview = previewObject.GetComponent<CardView>();

            TMP_Text previewReason = CreateText("PreviewReason", root, "", 22f, TextAlignmentOptions.Center);
            Corner(previewReason.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(-(EdgeMargin * 2f + ActionWidth), 60f - CardHeight * PreviewScale / 2f - 10f),
                new Vector2(CardWidth * PreviewScale, 36f));
            previewReason.rectTransform.pivot = new Vector2(1f, 1f);
            previewReason.color = new Color(1f, 0.55f, 0.45f);

            HandView handView = handArea.gameObject.AddComponent<HandView>();
            var handSo = new SerializedObject(handView);
            handSo.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            handSo.FindProperty("preview").objectReferenceValue = preview;
            handSo.FindProperty("previewReason").objectReferenceValue = previewReason;
            handSo.ApplyModifiedPropertiesWithoutUndo();

            ActionBar actionBar = actionArea.gameObject.AddComponent<ActionBar>();
            var actionSo = new SerializedObject(actionBar);
            actionSo.FindProperty("hintText").objectReferenceValue = hintText;
            actionSo.FindProperty("strikeButton").objectReferenceValue = strikeButton;
            actionSo.FindProperty("strikeLabel").objectReferenceValue = strikeLabel;
            actionSo.FindProperty("endTurnButton").objectReferenceValue = endTurnButton;
            actionSo.FindProperty("noAttackButton").objectReferenceValue = noAttackButton;
            actionSo.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            actionSo.FindProperty("forwardButton").objectReferenceValue = forwardButton;
            actionSo.FindProperty("backwardButton").objectReferenceValue = backwardButton;
            actionSo.ApplyModifiedPropertiesWithoutUndo();

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

            MatchController controller = controllerObject != null ? controllerObject.GetComponent<MatchController>() : null;
            foreach (Object scriptWithController in new Object[] { handView, actionBar })
            {
                var wiring = new SerializedObject(scriptWithController);
                wiring.FindProperty("matchController").objectReferenceValue = controller;
                wiring.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildDiscardPile(root, cardPrefab, handView, controller);
            BuildDrawPile(root.gameObject.scene, controller);

            if (controllerObject == null)
                Debug.LogWarning("MatchHudBuilder: no GameObject named 'MatchController' was found. Drag it into the " +
                                 "Match Controller field of MatchHud, HandArea (HandView) and ActionArea (ActionBar).", hud);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("MatchHudBuilder: built the Match HUD, " + PanelPrefabPath + " and " + CardPrefabPath + ". Save the scene.");
        }

        /// <summary>
        /// Adds only the discard pile (on the table, right of the board) to a HUD that was already built,
        /// so nothing else is rebuilt. Select the Canvas first. Running it again replaces the old pile.
        /// </summary>
        [MenuItem("SoulBind/Add Discard Pile")]
        private static void AddDiscardPile()
        {
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            Transform root = canvas != null ? canvas.transform.Find(HudName) : null;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Add Discard Pile",
                    "Select the Canvas that holds the MatchHud in the Hierarchy first, then run this again.", "OK");
                return;
            }

            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);
            HandView handView = root.GetComponentInChildren<HandView>(true);
            if (cardPrefab == null || handView == null)
            {
                EditorUtility.DisplayDialog("Add Discard Pile",
                    "Couldn't find " + CardPrefabPath + " or the HandArea. Run SoulBind > Build Match HUD instead.", "OK");
                return;
            }

            GameObject controllerObject = GameObject.Find("MatchController");
            MatchController controller = controllerObject != null ? controllerObject.GetComponent<MatchController>() : null;
            BuildDiscardPile((RectTransform)root, cardPrefab, handView, controller);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("MatchHudBuilder: added the DiscardPile on the table. Save the scene.");
        }

        // The pile is a World Space canvas lying on the table; DiscardPileView moves it next to the board
        // when the match starts. The board is also kept clear of the action buttons on the right.
        private static void BuildDiscardPile(RectTransform hudRoot, CardView cardPrefab, HandView handView,
            MatchController controller)
        {
            // Remove an old pile: the screen version under the HUD, or an earlier table version.
            Transform oldScreenPile = hudRoot.Find(PileName);
            if (oldScreenPile != null)
                Undo.DestroyObjectImmediate(oldScreenPile.gameObject);
            foreach (GameObject sceneRoot in hudRoot.gameObject.scene.GetRootGameObjects())
            {
                if (sceneRoot.GetComponent<DiscardPileView>() != null)
                    Undo.DestroyObjectImmediate(sceneRoot);
            }

            var pileObject = new GameObject(PileName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(pileObject, hudRoot.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(pileObject, "Add Discard Pile");
            Canvas pileCanvas = pileObject.AddComponent<Canvas>();
            pileCanvas.renderMode = RenderMode.WorldSpace;
            pileCanvas.sortingOrder = 5; // above the table and mat sprites
            var pile = (RectTransform)pileObject.transform;
            pile.sizeDelta = new Vector2(CardWidth + 60f, CardHeight + 80f);
            pile.localScale = Vector3.one * (2.4f / CardHeight); // DiscardPileView sets the real size and place

            // The glow sits behind everything, a little larger than a card.
            RectTransform glowRect = CreateRect("Glow", pile);
            Centered(glowRect, new Vector2(CardWidth + 36f, CardHeight + 36f));
            Image glow = AddImage(glowRect.gameObject, new Color(1f, 0.85f, 0.4f, 0.6f));

            // The empty card-sized slot printed on the table.
            RectTransform slot = CreateRect("Slot", pile);
            Centered(slot, new Vector2(CardWidth, CardHeight));
            AddImage(slot.gameObject, new Color(0f, 0f, 0f, 0.35f));
            Outline outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = FrameColor;
            outline.effectDistance = new Vector2(3f, -3f);

            TMP_Text emptyText = CreateText("EmptyText", slot, "Discard pile\n\nDrag a card here to play it", 18f,
                TextAlignmentOptions.Center);
            Stretch(emptyText.rectTransform, 12f, 12f, 12f, 12f);
            emptyText.color = new Color(1f, 1f, 1f, 0.6f);

            // The played cards are created under here at runtime.
            RectTransform stack = CreateRect("Stack", pile);
            Centered(stack, new Vector2(CardWidth, CardHeight));

            TMP_Text countText = CreateText("CountText", pile, "", 26f, TextAlignmentOptions.Center);
            countText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            countText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            countText.rectTransform.pivot = new Vector2(0.5f, 0f);
            countText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            countText.rectTransform.sizeDelta = new Vector2(CardWidth, 32f);

            DiscardPileView view = pileObject.AddComponent<DiscardPileView>();
            var so = new SerializedObject(view);
            so.FindProperty("matchController").objectReferenceValue = controller;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("stack").objectReferenceValue = stack;
            so.FindProperty("slot").objectReferenceValue = slot;
            so.FindProperty("countText").objectReferenceValue = countText;
            so.FindProperty("emptyText").objectReferenceValue = emptyText;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();

            var handSo = new SerializedObject(handView);
            handSo.FindProperty("discardPile").objectReferenceValue = view;
            handSo.ApplyModifiedProperties();

        }

        /// <summary>
        /// Adds the face-down draw pile on the table, below the discard pile. Running it again replaces it.
        /// </summary>
        [MenuItem("SoulBind/Add Draw Pile")]
        private static void AddDrawPile()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject controllerObject = GameObject.Find("MatchController");
            MatchController controller = controllerObject != null ? controllerObject.GetComponent<MatchController>() : null;
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Add Draw Pile", "No GameObject named 'MatchController' was found in the open scene.", "OK");
                return;
            }

            BuildDrawPile(scene, controller);

            // Make room for it: move an existing discard pile up to its matching place.
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                DiscardPileView discard = sceneRoot.GetComponent<DiscardPileView>();
                if (discard == null)
                    continue;
                var discardSo = new SerializedObject(discard);
                discardSo.FindProperty("heightOffset").floatValue = PileOffset;
                discardSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("MatchHudBuilder: added the DrawPile on the table. Save the scene.");
        }

        // Like the discard pile: a World Space canvas lying on the table, placed by DrawPileView at runtime.
        private static void BuildDrawPile(Scene scene, MatchController controller)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.GetComponent<DrawPileView>() != null)
                    Undo.DestroyObjectImmediate(sceneRoot);
            }

            var pileObject = new GameObject(DrawPileName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(pileObject, scene);
            Undo.RegisterCreatedObjectUndo(pileObject, "Add Draw Pile");
            Canvas pileCanvas = pileObject.AddComponent<Canvas>();
            pileCanvas.renderMode = RenderMode.WorldSpace;
            pileCanvas.sortingOrder = 5; // above the table and mat sprites
            var pile = (RectTransform)pileObject.transform;
            pile.sizeDelta = new Vector2(CardWidth + 60f, CardHeight + 80f);
            pile.localScale = Vector3.one * (2.4f / CardHeight); // DrawPileView sets the real size and place

            RectTransform slot = CreateRect("Slot", pile);
            Centered(slot, new Vector2(CardWidth, CardHeight));
            AddImage(slot.gameObject, new Color(0f, 0f, 0f, 0.35f));
            Outline outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = FrameColor;
            outline.effectDistance = new Vector2(3f, -3f);

            TMP_Text emptyText = CreateText("EmptyText", slot, "Draw pile\n\nEmpty: the discard pile is shuffled in on the next draw",
                16f, TextAlignmentOptions.Center);
            Stretch(emptyText.rectTransform, 12f, 12f, 12f, 12f);
            emptyText.color = new Color(1f, 1f, 1f, 0.6f);

            RectTransform stack = CreateRect("Stack", pile);
            Centered(stack, new Vector2(CardWidth, CardHeight));

            // The placeholder card back: gold frame, dark inside, a name in the middle.
            RectTransform back = CreateRect("CardBackTemplate", pile);
            Centered(back, new Vector2(CardWidth, CardHeight));
            AddImage(back.gameObject, FrameColor);
            RectTransform inner = CreateRect("Inner", back);
            Stretch(inner, 8f, 8f, 8f, 8f);
            AddImage(inner.gameObject, new Color(0.20f, 0.12f, 0.28f));
            RectTransform emblem = CreateRect("Emblem", inner);
            Centered(emblem, new Vector2(90f, 90f));
            emblem.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(emblem.gameObject, new Color(0.62f, 0.48f, 0.24f, 0.5f));
            TMP_Text backTitle = CreateText("Title", inner, "SoulBind", 26f, TextAlignmentOptions.Center);
            Stretch(backTitle.rectTransform, 0f, 0f, 0f, 0f);
            backTitle.color = ParchmentColor;
            back.gameObject.SetActive(false);

            TMP_Text countText = CreateText("CountText", pile, "", 26f, TextAlignmentOptions.Center);
            countText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            countText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            countText.rectTransform.pivot = new Vector2(0.5f, 0f);
            countText.rectTransform.anchoredPosition = Vector2.zero;
            countText.rectTransform.sizeDelta = new Vector2(CardWidth, 32f);

            DrawPileView view = pileObject.AddComponent<DrawPileView>();
            var so = new SerializedObject(view);
            so.FindProperty("matchController").objectReferenceValue = controller;
            so.FindProperty("cardBackTemplate").objectReferenceValue = back;
            so.FindProperty("stack").objectReferenceValue = stack;
            so.FindProperty("slot").objectReferenceValue = slot;
            so.FindProperty("countText").objectReferenceValue = countText;
            so.FindProperty("emptyText").objectReferenceValue = emptyText;
            so.FindProperty("heightOffset").floatValue = PileOffset;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Drawn cards fly from here into the hand.
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                HandView handView = sceneRoot.GetComponentInChildren<HandView>(true);
                if (handView == null)
                    continue;
                var handSo = new SerializedObject(handView);
                handSo.FindProperty("drawPile").objectReferenceValue = view;
                handSo.ApplyModifiedProperties();
            }
        }

        // A fixed-size box in the middle of the parent.
        private static void Centered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
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

        // Builds the card layout (placeholders) in a temporary object, saves it as a prefab and returns the asset.
        private static CardView BuildCardPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(CardPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Cards");

            RectTransform card = CreateRect("CardView", null);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);

            // Drawn first, so it shows as a glow around the card when selected.
            RectTransform glow = CreateRect("Highlight", card);
            Stretch(glow, -6f, -6f, -6f, -6f);
            Image highlight = AddImage(glow.gameObject, new Color(1f, 0.85f, 0.30f));
            highlight.enabled = false;

            RectTransform frame = CreateRect("Frame", card);
            Stretch(frame, 0f, 0f, 0f, 0f);
            AddImage(frame.gameObject, FrameColor).raycastTarget = true; // catches hover and clicks

            RectTransform inner = CreateRect("Inner", card);
            Stretch(inner, 6f, 6f, 6f, 6f);
            AddImage(inner.gameObject, CardInnerColor);

            RectTransform art = CreateRect("ArtWindow", card);
            TopRow(art, 10f, 48f, 30f, 120f);
            Image artWindow = AddImage(art.gameObject, Color.gray);

            RectTransform medallionRect = CreateRect("Medallion", card);
            Corner(medallionRect, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(34f, 34f));
            Image medallion = AddImage(medallionRect.gameObject, Color.white);
            TMP_Text medallionText = CreateText("ElementText", medallionRect, "R", 14f, TextAlignmentOptions.Center);
            Stretch(medallionText.rectTransform, 0f, 0f, 0f, 0f);
            medallionText.color = InkColor;
            medallionText.fontStyle = FontStyles.Bold;

            TMP_Text attackText = CreateBadge(card, "AtkBadge", "ATK", 0);
            TMP_Text defenseText = CreateBadge(card, "DefBadge", "DEF", 1);
            TMP_Text capText = CreateBadge(card, "CapBadge", "CAP", 2);

            RectTransform plate = CreateRect("NamePlate", card);
            TopRow(plate, 8f, 8f, 156f, 30f);
            AddImage(plate.gameObject, ParchmentColor);
            TMP_Text nameText = CreateText("NameText", plate, "Card Name", 16f, TextAlignmentOptions.Center);
            Stretch(nameText.rectTransform, 4f, 4f, 2f, 2f);
            nameText.color = InkColor;
            nameText.fontStyle = FontStyles.Bold;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 10f;
            nameText.fontSizeMax = 16f;

            RectTransform effectBox = CreateRect("EffectBox", card);
            Stretch(effectBox, 8f, 8f, 190f, 8f);
            AddImage(effectBox.gameObject, ParchmentColor);
            TMP_Text effectText = CreateText("EffectText", effectBox, "Effect text", 12f, TextAlignmentOptions.Center);
            Stretch(effectText.rectTransform, 5f, 5f, 3f, 3f);
            effectText.color = InkColor;
            effectText.enableAutoSizing = true;
            effectText.fontSizeMin = 7f;
            effectText.fontSizeMax = 12f;

            RectTransform dim = CreateRect("DimOverlay", card);
            Stretch(dim, 0f, 0f, 0f, 0f);
            Image dimOverlay = AddImage(dim.gameObject, new Color(0f, 0f, 0f, 0.55f));
            dimOverlay.enabled = false;

            CardView view = card.gameObject.AddComponent<CardView>();
            var so = new SerializedObject(view);
            so.FindProperty("highlight").objectReferenceValue = highlight;
            so.FindProperty("artWindow").objectReferenceValue = artWindow;
            so.FindProperty("medallion").objectReferenceValue = medallion;
            so.FindProperty("medallionText").objectReferenceValue = medallionText;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("effectText").objectReferenceValue = effectText;
            so.FindProperty("attackText").objectReferenceValue = attackText;
            so.FindProperty("defenseText").objectReferenceValue = defenseText;
            so.FindProperty("capText").objectReferenceValue = capText;
            so.FindProperty("dimOverlay").objectReferenceValue = dimOverlay;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(card.gameObject, CardPrefabPath);
            Object.DestroyImmediate(card.gameObject);
            return asset.GetComponent<CardView>();
        }

        // A stat badge on the card's right edge: a small label over the number. Returns the number text.
        private static TMP_Text CreateBadge(RectTransform card, string name, string label, int index)
        {
            RectTransform badge = CreateRect(name, card);
            Corner(badge, new Vector2(1f, 1f), new Vector2(-6f, -(30f + index * 42f)), new Vector2(38f, 38f));
            AddImage(badge.gameObject, BadgeColor);

            TMP_Text labelText = CreateText("Label", badge, label, 9f, TextAlignmentOptions.Center);
            TopRow(labelText.rectTransform, 0f, 0f, 1f, 11f);
            labelText.color = new Color(1f, 1f, 1f, 0.7f);

            TMP_Text valueText = CreateText("Value", badge, "0", 18f, TextAlignmentOptions.Center);
            TopRow(valueText.rectTransform, 0f, 0f, 12f, 24f);
            valueText.fontStyle = FontStyles.Bold;
            return valueText;
        }

        // A button with a label, sized for the action column.
        private static Button CreateButton(string name, Transform parent, string label, Color color, out TMP_Text labelText)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect.gameObject, color);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            labelText = CreateText("Label", rect, label, 20f, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 6f, 6f, 2f, 2f);
            return button;
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