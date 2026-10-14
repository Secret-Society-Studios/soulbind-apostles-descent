using System;
using System.Collections.Generic;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Cards flying across the screen: from a hand to the discard pile when played, and from the
    /// draw pile to a hand when drawn. Opponents' cards fly face-down, and turn face-up only when they
    /// land on the (public) discard pile. A flight is only a picture on top of everything: the rules have
    /// already moved the card, and the piles and hand hide it until it lands.
    /// HandView creates this layer at runtime as the last child of the Canvas, so it needs no setup.
    /// </summary>
    public sealed class CardFlightLayer : MonoBehaviour
    {
        private sealed class Flight
        {
            public CardView Card;
            public RectTransform Back;
            public RectTransform Follow;
            public Vector3 From;
            public Vector3 To;
            public Quaternion FromRotation;
            public Quaternion ToRotation;
            public float FromScale;
            public float ToScale;
            public float Elapsed;
            public float Duration;
            public bool StartFaceDown;
            public bool EndFaceDown;
            public Action Landed;
            public bool Active;
        }

        private readonly List<Flight> _flights = new List<Flight>(8);
        private CardView _cardPrefab;
        private RectTransform _backTemplate;
        private float _arcHeight;

        /// <summary>How many cards are in the air.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Makes a full-screen layer on top of the canvas that never catches the mouse.</summary>
        public static CardFlightLayer Create(Canvas canvas, CardView cardPrefab, RectTransform backTemplate, float arcHeight)
        {
            var go = new GameObject("CardFlights", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = canvas.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            CardFlightLayer layer = go.AddComponent<CardFlightLayer>();
            layer._cardPrefab = cardPrefab;
            layer._backTemplate = backTemplate;
            layer._arcHeight = arcHeight;
            return layer;
        }

        /// <summary>The scale that makes a flying card this many screen pixels tall.</summary>
        public float ScaleForPixels(float pixelHeight)
        {
            float height = ((RectTransform)_cardPrefab.transform).sizeDelta.y * transform.lossyScale.y;
            return height > 0f ? pixelHeight / height : 1f;
        }

        /// <summary>
        /// Flies a face-up card from a hand card's place to a fixed point (the discard pile).
        /// Positions are in the canvas's world space, which for an Overlay canvas is screen pixels.
        /// </summary>
        public void FlyTo(ArcanaId card, Vector3 from, Quaternion fromRotation, float fromScale,
            Vector3 to, float toScale, float duration, Action landed)
        {
            Flight flight = Take(card, false, false);
            flight.To = to;
            flight.ToScale = toScale;
            flight.ToRotation = Quaternion.identity;
            flight.Follow = null;
            Begin(flight, from, fromRotation, fromScale, duration, landed);
        }

        /// <summary>
        /// Flies a card from a point (the draw pile) to a hand card that is still moving into place.
        /// It starts face-down and turns over halfway.
        /// </summary>
        public void FlyToFollow(ArcanaId card, Vector3 from, float fromScale, RectTransform follow,
            float duration, Action landed)
        {
            Flight flight = Take(card, _backTemplate != null, false);
            flight.Follow = follow;
            Begin(flight, from, Quaternion.identity, fromScale, duration, landed);
        }

        /// <summary>
        /// Flies a card between two fixed points, face-down or face-up at each end; it turns over
        /// halfway if the two differ. Used for opponents' cards: their draws stay face-down all the way,
        /// and the cards they play turn face-up as they reach the discard pile.
        /// A face-down flight needs the card back; without it, nothing flies (the card is never revealed).
        /// </summary>
        public void Fly(ArcanaId card, Vector3 from, float fromScale, Vector3 to, float toScale,
            bool startFaceDown, bool endFaceDown, float duration, Action landed)
        {
            Fly(card, from, 0f, fromScale, to, 0f, toScale, startFaceDown, endFaceDown, duration, landed);
        }

        /// <summary>
        /// As above, also turning from one on-screen angle to another (degrees, 0 = upright), for cards
        /// that lie turned along the sides of the mat.
        /// </summary>
        public void Fly(ArcanaId card, Vector3 from, float fromAngle, float fromScale, Vector3 to, float toAngle,
            float toScale, bool startFaceDown, bool endFaceDown, float duration, Action landed)
        {
            if ((startFaceDown || endFaceDown) && _backTemplate == null)
            {
                landed?.Invoke();
                return;
            }

            Flight flight = Take(card, startFaceDown, endFaceDown);
            flight.To = to;
            flight.ToScale = toScale;
            flight.ToRotation = Quaternion.Euler(0f, 0f, toAngle);
            flight.Follow = null;
            Begin(flight, from, Quaternion.Euler(0f, 0f, fromAngle), fromScale, duration, landed);
        }

        private void Begin(Flight flight, Vector3 from, Quaternion fromRotation, float fromScale, float duration, Action landed)
        {
            flight.From = from;
            flight.FromRotation = fromRotation;
            flight.FromScale = fromScale;
            flight.Elapsed = 0f;
            flight.Duration = Mathf.Max(0.05f, duration);
            flight.Landed = landed;
            flight.Active = true;
            ActiveCount++;

            RectTransform rect = flight.Card.Rect;
            rect.position = from;
            rect.rotation = fromRotation;
            rect.localScale = Vector3.one * fromScale;
            flight.Card.gameObject.SetActive(true);
            rect.SetAsLastSibling();
            Place(flight, 0f);
        }

        // Reuses a finished flight, or makes a new one.
        private Flight Take(ArcanaId card, bool startFaceDown, bool endFaceDown)
        {
            Flight flight = null;
            for (int i = 0; i < _flights.Count; i++)
            {
                if (!_flights[i].Active)
                {
                    flight = _flights[i];
                    break;
                }
            }

            if (flight == null)
            {
                flight = new Flight { Card = Instantiate(_cardPrefab, transform) };
                RectTransform rect = flight.Card.Rect;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                if (_backTemplate != null)
                {
                    flight.Back = Instantiate(_backTemplate, rect);
                    flight.Back.anchorMin = Vector2.zero;
                    flight.Back.anchorMax = Vector2.one;
                    flight.Back.offsetMin = Vector2.zero;
                    flight.Back.offsetMax = Vector2.zero;
                    flight.Back.localScale = Vector3.one;
                    flight.Back.localRotation = Quaternion.identity;
                    flight.Back.anchoredPosition3D = Vector3.zero;
                }
                _flights.Add(flight);
            }

            flight.Card.Show(CardCatalog.Get(card));
            flight.Card.SetSelected(false);
            flight.Card.SetDimmed(false);
            flight.StartFaceDown = startFaceDown && flight.Back != null;
            flight.EndFaceDown = endFaceDown && flight.Back != null;
            if (flight.Back != null)
                flight.Back.gameObject.SetActive(flight.StartFaceDown);
            return flight;
        }

        private void Update()
        {
            if (ActiveCount == 0)
                return;

            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < _flights.Count; i++)
            {
                Flight flight = _flights[i];
                if (!flight.Active)
                    continue;

                // The hand card being followed was hidden or removed: land straight away.
                if (flight.Follow != null && !flight.Follow.gameObject.activeInHierarchy)
                    flight.Elapsed = flight.Duration;
                else
                    flight.Elapsed += delta;

                float t = Mathf.Clamp01(flight.Elapsed / flight.Duration);
                Place(flight, t);

                if (t >= 1f)
                {
                    flight.Active = false;
                    flight.Card.gameObject.SetActive(false);
                    ActiveCount--;
                    Action landed = flight.Landed;
                    flight.Landed = null;
                    landed?.Invoke();
                }
            }
        }

        private void Place(Flight flight, float t)
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t); // fast start, gentle landing

            Vector3 to = flight.Follow != null ? flight.Follow.position : flight.To;
            Quaternion toRotation = flight.Follow != null ? flight.Follow.rotation : flight.ToRotation;
            float toScale = flight.Follow != null ? flight.Follow.localScale.x : flight.ToScale;

            // A small arc upward, scaled with the canvas so it looks the same at any resolution.
            float arc = Mathf.Sin(t * Mathf.PI) * _arcHeight * transform.lossyScale.y;

            RectTransform rect = flight.Card.Rect;
            rect.position = Vector3.Lerp(flight.From, to, eased) + Vector3.up * arc;
            rect.rotation = Quaternion.Slerp(flight.FromRotation, toRotation, eased);

            float scale = Mathf.Lerp(flight.FromScale, toScale, eased);
            if (flight.StartFaceDown != flight.EndFaceDown)
            {
                // Turns over halfway: one side shrinks to an edge, then the other grows out of it.
                float turn = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
                rect.localScale = new Vector3(scale * turn, scale, 1f);
                if (flight.Back != null)
                    flight.Back.gameObject.SetActive(t < 0.5f ? flight.StartFaceDown : flight.EndFaceDown);
            }
            else
            {
                rect.localScale = new Vector3(scale, scale, 1f);
                if (flight.Back != null)
                    flight.Back.gameObject.SetActive(flight.StartFaceDown);
            }
        }
    }
}