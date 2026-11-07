using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>The "new version" box over everything. GameUpdater makes it when there is an update.</summary>
    public sealed class UpdatePrompt : MonoBehaviour
    {
        private const int SortingOrder = 1000; // over the studio opening and every menu
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);

        private TMP_Text _message;
        private TMP_Text _actionLabel;
        private GameObject _action;
        private GameObject _later;
        private Action _onAction;

        /// <summary>Builds the hidden box. onLater runs when the player puts the update off.</summary>
        public static UpdatePrompt Create(Action onLater)
        {
            var root = new GameObject("UpdatePrompt", typeof(RectTransform));
            DontDestroyOnLoad(root);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignSize;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            UpdatePrompt prompt = root.AddComponent<UpdatePrompt>();
            prompt.Build(onLater);
            root.SetActive(false);
            return prompt;
        }

        private void Build(Action onLater)
        {
            RectTransform dim = Kit.Stretch(Kit.CreateRect("Dim", transform));
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            RectTransform box = Kit.Box(dim, "Box", Kit.Panel, Kit.GoldLine, 14f, 2f);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(640f, 300f);

            TMP_Text title = Kit.Label(box, "Title", "Update", 32f, Kit.Display, Kit.Title);
            Kit.At(title.rectTransform, 0f, 22f, 640f, 44f);

            _message = Kit.Label(box, "Message", "", 22f, Kit.Inter, Kit.Text);
            Kit.At(_message.rectTransform, 40f, 84f, 560f, 110f);

            _actionLabel = Kit.Button(box, "Action", "Update", Kit.ButtonStyle.Primary, () => _onAction?.Invoke(), 20f);
            RectTransform action = (RectTransform)_actionLabel.transform.parent;
            Kit.At(action, 340f, 218f, 220f, 56f);
            _action = action.gameObject;

            TMP_Text later = Kit.Button(box, "Later", "Later", Kit.ButtonStyle.Secondary, () => onLater?.Invoke(), 20f);
            RectTransform laterRect = (RectTransform)later.transform.parent;
            Kit.At(laterRect, 80f, 218f, 220f, 56f);
            _later = laterRect.gameObject;
        }

        /// <summary>A message, a button and Later.</summary>
        public void Show(string message, string button, Action onAction)
        {
            gameObject.SetActive(true);
            _message.text = message;
            _actionLabel.text = button;
            _onAction = onAction;
            _action.SetActive(true);
            _later.SetActive(true);
        }

        /// <summary>A message and nothing to press: something is running.</summary>
        public void ShowBusy(string message)
        {
            _message.text = message;
            _action.SetActive(false);
            _later.SetActive(false);
        }

        /// <summary>The download's progress, 0 to 1.</summary>
        public void ShowPercent(float progress) =>
            _message.SetText("Downloading... {0}%", Mathf.RoundToInt(progress * 100f));
    }
}
