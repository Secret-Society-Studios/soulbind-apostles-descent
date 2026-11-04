using SoulBind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The Collections' 360 view: one card, large, turning slowly. Drag anywhere to spin it (it keeps
    /// spinning, then settles back to a slow turn) and up or down to tip it. Its back shows when it faces
    /// away. Tap outside the card, or Close, to go back. Built once under the screen's frame and reused.
    /// </summary>
    public sealed class CardViewer360 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        private const float CardWidth = 300f, CardHeight = 420f; // design pixels, about the art's 5:7
        private const float AutoSpin = 25f;      // degrees per second while nobody drags
        private const float DragSpin = 0.45f;    // degrees per pixel dragged sideways
        private const float DragTip = 0.25f;     // degrees per pixel dragged up or down
        private const float MaxTip = 25f;        // the most it tips forward or back
        private const float Settle = 2.5f;       // how fast a flick slows back to the slow turn

        private RectTransform _card;
        private Image _front;
        private Image _back;
        private TMP_Text _missing;
        private GameObject _plain; // a plain card shape (with its border), for a side that has no art
        private TMP_Text _name;
        private TMP_Text _stats;
        private TMP_Text _effect;
        private float _yaw;      // degrees around the card's upright axis; 0 = front facing you
        private float _tip;      // degrees forward or back
        private float _spin;     // degrees per second
        private bool _dragging;

        /// <summary>Shows this card in the viewer over frame (made the first time).</summary>
        public static void Open(RectTransform frame, CardDefinition card)
        {
            if (frame == null || card == null)
                return;
            Transform existing = frame.Find("CardViewer360");
            CardViewer360 viewer = existing != null ? existing.GetComponent<CardViewer360>() : Create(frame);
            viewer.Show(card);
            viewer.gameObject.SetActive(true);
            viewer.transform.SetAsLastSibling();
        }

        // Puts a card's front and back art on the viewer and resets the spin.
        private void Show(CardDefinition card)
        {
            CardArtLibrary art = CardArtLibrary.Current;
            Sprite front = art != null ? art.GetFront(card.Id) : null;
            Sprite back = art != null ? art.Back : null;
            _front.sprite = front;
            _back.sprite = back;
            _missing.text = front == null ? card.Name : "";
            _name.text = card.Name;
            _stats.text = $"{card.Element.ToString().ToUpperInvariant()}   ATK {card.Attack}   " +
                          $"DEF {card.Defense}   CAP {card.Cap}";
            _effect.text = card.Effect;

            // Every card starts face on, with a little spin so it is clearly turnable.
            _yaw = 0f;
            _tip = 0f;
            _spin = AutoSpin * 4f;
            _dragging = false;
            Apply();
        }

        // ---------- Spinning ----------

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (!_dragging)
            {
                // A flick slows down to the slow turn; the tip eases back to upright.
                _spin = Mathf.Lerp(_spin, AutoSpin, 1f - Mathf.Exp(-Settle * delta));
                _yaw += _spin * delta;
                _tip = Mathf.Lerp(_tip, 0f, 1f - Mathf.Exp(-Settle * delta));
            }
            Apply();
        }

        // Turns the card and shows whichever face points at the camera.
        private void Apply()
        {
            _yaw = Mathf.Repeat(_yaw, 360f);
            _card.localRotation = Quaternion.Euler(_tip, _yaw, 0f);
            // UI images draw both sides, so show whichever side faces you.
            bool facing = Mathf.Cos(_yaw * Mathf.Deg2Rad) >= 0f;
            if (_front.enabled != (facing && _front.sprite != null))
                _front.enabled = facing && _front.sprite != null;
            if (_back.enabled != !facing)
                _back.enabled = !facing;
            _missing.enabled = facing;
            bool plain = facing ? _front.sprite == null : _back.sprite == null;
            if (_plain.activeSelf != plain)
                _plain.SetActive(plain);
        }

        // Stops the free spin while the player drags.
        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _spin = 0f;
        }

        // Turns the card with the drag and remembers the speed for the spin after release.
        public void OnDrag(PointerEventData eventData)
        {
            float turn = eventData.delta.x * DragSpin;
            _yaw -= turn;
            _tip = Mathf.Clamp(_tip + eventData.delta.y * DragTip, -MaxTip, MaxTip);
            float delta = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            _spin = Mathf.Lerp(_spin, -turn / delta, 0.5f); // keeps the flick's speed for the let-go
        }

        // Lets go: the card keeps spinning at the drag speed and slows down.
        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _spin = Mathf.Clamp(_spin, -900f, 900f);
        }

        // A tap (not the end of a drag) outside the card closes the viewer.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;
            if (RectTransformUtility.RectangleContainsScreenPoint(_card, eventData.position, eventData.pressEventCamera))
                return;
            Close();
        }

        // Hides the viewer.
        private void Close() => gameObject.SetActive(false);

        // ---------- Building ----------

        // Builds the viewer once: a dim layer, the card with two faces, and a hint.
        private static CardViewer360 Create(RectTransform frame)
        {
            // A dim layer over the whole screen: it catches drags anywhere, so spinning is easy on a phone.
            RectTransform root = Kit.Stretch(Kit.CreateRect("CardViewer360", frame));
            root.offsetMin = new Vector2(-2000f, -2000f); // past the design frame's edges, to the screen's
            root.offsetMax = new Vector2(2000f, 2000f);
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);
            CardViewer360 viewer = root.gameObject.AddComponent<CardViewer360>();

            // A holder at the design frame's size, so the parts can be placed with the design's numbers.
            RectTransform area = Kit.CreateRect("Area", root);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0.5f, 0.5f);
            area.sizeDelta = frame.sizeDelta;

            TMP_Text hint = Kit.Label(area, "Hint", "Drag to spin the card", 15f, Kit.Inter, Kit.Muted);
            Kit.At(hint.rectTransform, 0f, 70f, frame.sizeDelta.x, 22f);

            viewer._card = Kit.CreateRect("Card", area);
            viewer._card.anchorMin = viewer._card.anchorMax = new Vector2(0.5f, 1f);
            viewer._card.pivot = new Vector2(0.5f, 0.5f);
            viewer._card.anchoredPosition = new Vector2(0f, -110f - CardHeight / 2f);
            viewer._card.sizeDelta = new Vector2(CardWidth, CardHeight);

            // The back sits behind the front, turned around so it reads the right way when it shows.
            viewer._back = AddFace(viewer._card, "Back");
            viewer._back.rectTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            RectTransform plain = Kit.Stretch(Kit.Box(viewer._card, "Plain", Kit.Panel, Kit.GoldLine, 14f, 2f));
            plain.GetComponent<Image>().raycastTarget = false;
            plain.SetAsFirstSibling();
            viewer._plain = plain.gameObject;
            viewer._front = AddFace(viewer._card, "Front");
            viewer._missing = Kit.Label(viewer._card, "Name", "", 24f, Kit.Display, Kit.Title);
            Kit.Stretch(viewer._missing.rectTransform, 20f);

            float y = 110f + CardHeight + 18f;
            float width = frame.sizeDelta.x;
            viewer._name = Kit.Label(area, "CardName", "", 26f, Kit.Display, Kit.Title);
            Kit.At(viewer._name.rectTransform, 0f, y, width, 34f);
            viewer._stats = Kit.Label(area, "CardStats", "", 15f, Kit.InterSemiBold, Kit.Soft);
            Kit.At(viewer._stats.rectTransform, 0f, y + 38f, width, 22f);
            viewer._effect = Kit.Label(area, "CardEffect", "", 14f, Kit.Inter, Kit.Muted);
            Kit.At(viewer._effect.rectTransform, width / 2f - 300f, y + 64f, 600f, 44f);

            TMP_Text close = Kit.Button(area, "Close", "Close", Kit.ButtonStyle.Secondary, viewer.Close, 18f);
            Kit.At((RectTransform)close.transform.parent, width - 170f, 40f, 130f, 48f);
            return viewer;
        }

        // Adds one face of the card as a stretched image.
        private static Image AddFace(RectTransform card, string name)
        {
            RectTransform rect = Kit.Stretch(Kit.CreateRect(name, card));
            var image = (Image)rect.gameObject.AddComponent(typeof(Image));
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
