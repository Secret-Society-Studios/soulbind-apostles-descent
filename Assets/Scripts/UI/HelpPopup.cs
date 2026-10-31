using System.Collections.Generic;
using SoulBind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    // Inside the namespace, so it wins over the old SoulBind.Element in Card.cs.
    using Element = SoulBind.Core.Element;

    /// <summary>
    /// The How to Play popup, opened from the main menu's ? button and the match's Menu.
    /// Tabs across the top (Goal, Turn, Cards, Combos, Corruption, Tiles); each has a small
    /// demo to tap, a Reset button, a short rules paragraph, and Back / Close / Next.
    /// Built at runtime under the canvas it is opened on, once, and reused.
    /// The numbers in the text come from GameConstants and the card list, so they stay true.
    /// </summary>
    public sealed class HelpPopup : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080).
        private const float BoxWidth = 1160f;
        private const float BoxHeight = 780f;
        private const float DemoWidth = 1060f;
        private const float DemoHeight = 300f;
        private const float ChipWidth = 190f;
        private const float ChipHeight = 92f;
        private const int ChipCount = 4;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color DemoColor = new Color(0.05f, 0.04f, 0.07f, 0.95f);
        private static readonly Color ChipColor = new Color(0.17f, 0.12f, 0.22f, 1f);
        private static readonly Color SubText = new Color(0.81f, 0.76f, 0.68f, 1f);
        private static readonly Color SoulsColor = new Color(0.95f, 0.78f, 0.35f);
        private static readonly Color CorruptionColor = new Color(0.55f, 0.30f, 0.70f);
        private static readonly Color CorruptedColor = new Color(0.85f, 0.15f, 0.30f);

        private enum Demo { Goal, Turn, Cards, Combos, Corruption, Tiles }

        private sealed class Tab
        {
            public string Name;
            public string Instruction;
            public string Rules;
            public Demo Demo;
        }

        private sealed class Chip
        {
            public RectTransform Rect;
            public Image Background;
            public Outline Ring;
            public TMP_Text Label;
        }

        // The demo cards, read from the card list.
        private static readonly ArcanaId[] CardsDemo = { ArcanaId.Magician, ArcanaId.Death, ArcanaId.Star, ArcanaId.Fool };
        private static readonly ArcanaId[] CombosDemo = { ArcanaId.Magician, ArcanaId.Empress, ArcanaId.Chariot, ArcanaId.Strength };

        private Tab[] _tabs;
        private readonly Chip[] _chips = new Chip[ChipCount];
        private readonly Image[] _tabImages = new Image[6];
        private readonly List<int> _queue = new List<int>(ChipCount); // chip indices, in play order
        private TMP_Text _instruction;
        private TMP_Text _info;
        private TMP_Text _rules;
        private TMP_Text _nextLabel;
        private GameObject _back;
        private GameObject _bar;
        private RectTransform _barFill;
        private Image _barFillImage;
        private TMP_Text _barText;
        private int _tab;
        private int _step;

        /// <summary>Opens the popup on this canvas (made the first time), at the first tab.</summary>
        public static void Open(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return;
            Transform existing = canvasRoot.Find("HelpPopup");
            HelpPopup popup = existing != null ? existing.GetComponent<HelpPopup>() : Create(canvasRoot);
            popup.gameObject.SetActive(true);
            popup.transform.SetAsLastSibling();
            popup.ShowTab(0);
        }

        private static HelpPopup Create(Transform canvasRoot)
        {
            var go = new GameObject("HelpPopup", typeof(RectTransform));
            go.layer = canvasRoot.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRoot, false);
            Stretch(rect);
            HelpPopup popup = go.AddComponent<HelpPopup>();
            popup._tabs = BuildTabs();
            popup.Build(rect);
            return popup;
        }

        // ---------- The text ----------

        private static Tab[] BuildTabs()
        {
            return new[]
            {
                new Tab
                {
                    Name = "Goal", Demo = Demo.Goal,
                    Instruction = "1. Tap the enemy to steal a Soul",
                    Rules = $"Win by being the last Apostle standing, or by reaching {GameConstants.SoulsToAwaken} Souls: " +
                            "you become Awakened, the Ascension tile appears, and stepping onto it wins the match. " +
                            $"Everyone starts with {GameConstants.StartingVessel} Vessel (health); at 0 you are out.",
                },
                new Tab
                {
                    Name = "Turn", Demo = Demo.Turn,
                    Instruction = "Tap each step of a turn",
                    Rules = $"Each turn you get Capacity to spend: {GameConstants.Phase1Capacity} in Phase 1, " +
                            $"{GameConstants.Phase2To3Capacity} in Phases 2-3, {GameConstants.Phase4PlusCapacity} after. " +
                            $"A move costs {GameConstants.MoveCapacityCost}; cards cost their CAP. You attack at most once. " +
                            $"Ending your turn draws {GameConstants.CardsDrawnPerTurn} card; holding more than " +
                            $"{GameConstants.MaxHandSize} gives +{GameConstants.HandOverLimitCorruption} Corruption.",
                },
                new Tab
                {
                    Name = "Cards", Demo = Demo.Cards,
                    Instruction = "1. Tap a card to queue it   2. Tap it again to take it back",
                    Rules = "Cards wait in the queue with their Capacity held, so you can take them back. Drag one onto " +
                            "an adjacent enemy to attack, add more, then press ATTACK: the ATK is shared between every " +
                            "adjacent enemy and their DEF blocks it. Each broken DEF steals " +
                            $"{GameConstants.SoulsStolenPerAttack} Soul and gives you +{GameConstants.AttackCorruption} Corruption. " +
                            "Without an attack, PLAY gives just their DEF and effects.",
                },
                new Tab
                {
                    Name = "Combos", Demo = Demo.Combos,
                    Instruction = "Queue two cards of the same element",
                    Rules = "Two cards of one element in a turn trigger its combo, once: " +
                            $"Radiance +{ElementCombo.RadianceShield} DEF, Celeste -{ElementCombo.CelesteCorruptionReduction} Corruption, " +
                            $"Crimson +{ElementCombo.CrimsonAttack} ATK in an attack, Nocturne +{ElementCombo.NocturneCorruption} " +
                            "Corruption to an opponent you pick. The queue glows when a combo is on.",
                },
                new Tab
                {
                    Name = "Corruption", Demo = Demo.Corruption,
                    Instruction = "Tap to gain or lose Corruption",
                    Rules = $"At {GameConstants.CorruptionThreshold} Corruption you are Corrupted: you lose " +
                            $"{GameConstants.CorruptedDamage} Vessel each turn and cannot gain Souls (Soullocked). At " +
                            $"{GameConstants.SevereCorruptionThreshold} it is {GameConstants.SevereCorruptionDamage} a turn. " +
                            $"Dropping to 0 Souls adds {GameConstants.ZeroSoulCorruption} Corruption. Celeste cards and Purify tiles clean it.",
                },
                new Tab
                {
                    Name = "Tiles", Demo = Demo.Tiles,
                    Instruction = "Tap a tile to see what it does",
                    Rules = $"Special tiles: Heal +{GameConstants.HealTileAmount} Vessel, Purify -{GameConstants.PurifyTileAmount} " +
                            $"Corruption, Soul +{GameConstants.SoulTileAmount} Souls. Step on one to use it. Used tiles come back " +
                            "on free tiles at the start of every Phase.",
                },
            };
        }

        // ---------- Tabs and buttons ----------

        private void ShowTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _tabs.Length - 1);
            Tab tab = _tabs[_tab];
            for (int i = 0; i < _tabImages.Length; i++)
                _tabImages[i].color = i == _tab ? GothicTheme.StrikeButtonColor : GothicTheme.ButtonColor;
            _instruction.text = tab.Instruction;
            _rules.text = tab.Rules;
            _back.SetActive(_tab > 0);
            _nextLabel.text = _tab == _tabs.Length - 1 ? "Done" : "Next";
            ResetDemo();
        }

        private void Next()
        {
            if (_tab >= _tabs.Length - 1)
                Close();
            else
                ShowTab(_tab + 1);
        }

        private void Close() => gameObject.SetActive(false);

        // ---------- The demo ----------

        private void ResetDemo()
        {
            _step = 0;
            _queue.Clear();
            Render();
        }

        private void OnChip(int index)
        {
            switch (_tabs[_tab].Demo)
            {
                case Demo.Goal:
                    if (13 + _step < GameConstants.SoulsToAwaken)
                        _step++;
                    break;
                case Demo.Turn:
                case Demo.Tiles:
                    _step = index + 1;
                    break;
                case Demo.Cards:
                case Demo.Combos:
                    ToggleQueued(index);
                    break;
                case Demo.Corruption:
                    _step = Mathf.Clamp(_step + (index == 0 ? 1 : -1), 0, 15);
                    break;
            }
            Render();
        }

        // Queues a hand card, or takes a queued one back. The Cards demo keeps to Phase 1's Capacity.
        private void ToggleQueued(int index)
        {
            if (_queue.Remove(index))
                return;
            if (_tabs[_tab].Demo == Demo.Cards && QueuedCap() + CardCatalog.Get(CardsDemo[index]).Cap > GameConstants.Phase1Capacity)
            {
                _step = -1; // "not enough Capacity", until the next tap
                return;
            }
            _step = 0;
            _queue.Add(index);
        }

        private int QueuedCap()
        {
            int cap = 0;
            for (int i = 0; i < _queue.Count; i++)
                cap += CardCatalog.Get(CardsDemo[_queue[i]]).Cap;
            return cap;
        }

        private void Render()
        {
            for (int i = 0; i < _chips.Length; i++)
                _chips[i].Rect.gameObject.SetActive(false);
            _bar.SetActive(false);

            switch (_tabs[_tab].Demo)
            {
                case Demo.Goal: RenderGoal(); break;
                case Demo.Turn: RenderTurn(); break;
                case Demo.Cards: RenderQueue(CardsDemo, true); break;
                case Demo.Combos: RenderQueue(CombosDemo, false); break;
                case Demo.Corruption: RenderCorruption(); break;
                case Demo.Tiles: RenderTiles(); break;
            }
        }

        private void RenderGoal()
        {
            int souls = Mathf.Min(13 + _step, GameConstants.SoulsToAwaken);
            ShowBar($"Souls {souls}/{GameConstants.SoulsToAwaken}", (float)souls / GameConstants.SoulsToAwaken, SoulsColor);
            ShowChip(0, "Enemy\nDEF 0", new Color(0.42f, 0.07f, 0.10f, 1f), 330f, -10f, false);
            _info.text = souls >= GameConstants.SoulsToAwaken ? "AWAKENED! Now reach the Ascension tile to win."
                : _step == 0 ? "Break an enemy's DEF in an attack to steal a Soul."
                : $"Stolen! {GameConstants.SoulsToAwaken - souls} more to Awaken.";
        }

        private void RenderTurn()
        {
            string[] steps = { "1  Move", "2  Play cards", "3  Attack", "4  End turn" };
            string[] notes =
            {
                "Tap a step.",
                $"Move to a neighbouring tile for {GameConstants.MoveCapacityCost} Capacity. Not while attacking.",
                "Play cards into the queue: tap them to take them back until you commit.",
                "Drag a card onto an adjacent enemy, add more, then press ATTACK. Once per turn.",
                "Anything still queued resolves, you draw a card, and the next Apostle goes.",
            };
            for (int i = 0; i < steps.Length; i++)
                ShowChip(i, steps[i], ChipColor, -3f * (ChipWidth + 30f) / 2f + i * (ChipWidth + 30f), 10f, _step == i + 1);
            _info.text = notes[Mathf.Clamp(_step, 0, notes.Length - 1)];
        }

        // Hand on the top row, the queue in play order on the bottom row.
        private void RenderQueue(ArcanaId[] cards, bool countCapacity)
        {
            float step = ChipWidth + 24f;
            float left = -1.5f * step;
            int attack = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                CardDefinition card = CardCatalog.Get(cards[i]);
                int slot = _queue.IndexOf(i);
                bool queued = slot >= 0;
                float x = queued ? left + slot * step : left + i * step;
                float y = queued ? -75f : 70f;
                string label = $"<b>{card.Name}</b>\nA{card.Attack} D{card.Defense} C{card.Cap}";
                ShowChip(i, label, CardView.GetElementColor(card.Element) * 0.75f, x, y, queued);
                if (queued)
                    attack += card.Attack;
            }

            if (countCapacity)
            {
                _info.text = _step < 0 ? "Not enough Capacity for that one."
                    : _queue.Count == 0 ? "Hand above, queue below."
                    : $"Queued {_queue.Count} · ATK {attack} · Capacity left {GameConstants.Phase1Capacity - QueuedCap()}/{GameConstants.Phase1Capacity}";
                return;
            }

            // Combos: any element queued twice.
            string combos = string.Empty;
            for (int e = 0; e < ElementCombo.ElementCount; e++)
            {
                var element = (Element)e;
                int count = 0;
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (CardCatalog.Get(cards[_queue[i]]).Element == element)
                        count++;
                }
                if (count >= ElementCombo.CardsNeeded)
                    combos += (combos.Length > 0 ? "   " : string.Empty) + ComboText(element);
            }
            _info.text = combos.Length > 0 ? combos : "Hand above, queue below. Match two elements.";
        }

        private static string ComboText(Element element)
        {
            switch (element)
            {
                case Element.Radiance: return $"RADIANCE COMBO: +{ElementCombo.RadianceShield} DEF";
                case Element.Celeste: return $"CELESTE COMBO: -{ElementCombo.CelesteCorruptionReduction} Corruption";
                case Element.Crimson: return $"CRIMSON COMBO: +{ElementCombo.CrimsonAttack} ATK";
                default: return $"NOCTURNE COMBO: +{ElementCombo.NocturneCorruption} Corruption to a foe";
            }
        }

        private void RenderCorruption()
        {
            int corruption = _step;
            bool corrupted = corruption >= GameConstants.CorruptionThreshold;
            ShowBar($"Corruption {corruption}", corruption / 15f, corrupted ? CorruptedColor : CorruptionColor);
            ShowChip(0, "Break a DEF\n+1", ChipColor, -150f, -10f, false);
            ShowChip(1, "Celeste combo\n-1", CardView.GetElementColor(Element.Celeste) * 0.75f, 150f, -10f, false);
            _info.text = corruption >= GameConstants.SevereCorruptionThreshold
                    ? $"Severely Corrupted: -{GameConstants.SevereCorruptionDamage} Vessel every turn, no Souls."
                : corrupted ? $"Corrupted: -{GameConstants.CorruptedDamage} Vessel every turn, and no Souls."
                : $"Safe below {GameConstants.CorruptionThreshold}.";
        }

        private void RenderTiles()
        {
            ShowChip(0, "Heal", new Color(0.20f, 0.55f, 0.30f, 1f), -260f, 30f, _step == 1);
            ShowChip(1, "Purify", new Color(0.25f, 0.45f, 0.75f, 1f), 0f, 30f, _step == 2);
            ShowChip(2, "Soul", new Color(0.70f, 0.55f, 0.20f, 1f), 260f, 30f, _step == 3);
            switch (_step)
            {
                case 1: _info.text = $"Heal: +{GameConstants.HealTileAmount} Vessel."; break;
                case 2: _info.text = $"Purify: -{GameConstants.PurifyTileAmount} Corruption."; break;
                case 3: _info.text = $"Soul: +{GameConstants.SoulTileAmount} Souls (not while Soullocked)."; break;
                default: _info.text = "Tap a tile."; break;
            }
        }

        private void ShowChip(int index, string label, Color color, float x, float y, bool lit)
        {
            Chip chip = _chips[index];
            chip.Rect.gameObject.SetActive(true);
            chip.Rect.anchoredPosition = new Vector2(x, y);
            chip.Background.color = color;
            chip.Ring.effectColor = lit ? GothicTheme.GoldBright : GothicTheme.GoldDim;
            chip.Label.text = label;
        }

        private void ShowBar(string label, float fraction, Color color)
        {
            _bar.SetActive(true);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            _barFillImage.color = color;
            _barText.text = label;
        }

        // ---------- Building ----------

        private void Build(RectTransform root)
        {
            // Dims the screen and catches every tap behind the box.
            Image dim = AddImage(root.gameObject, DimColor);
            dim.raycastTarget = true;

            RectTransform box = NewRect("Box", root);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(BoxWidth, BoxHeight);
            Image panel = AddImage(box.gameObject, Color.white);
            panel.raycastTarget = true;
            GothicTheme.StylePanel(panel, true);

            TMP_Text title = NewText("Title", box, "HOW TO PLAY", 36f, FontStyles.Bold);
            Place(title.rectTransform, 0f, -40f, BoxWidth, 50f);
            title.color = GothicTheme.Gold;
            title.characterSpacing = 6f;

            // The tabs.
            float tabWidth = (DemoWidth - 5f * 10f) / 6f;
            for (int i = 0; i < _tabs.Length && i < _tabImages.Length; i++)
            {
                int index = i;
                Button tab = NewButton("Tab" + _tabs[i].Name, box, _tabs[i].Name, 22f,
                    -DemoWidth / 2f + tabWidth / 2f + i * (tabWidth + 10f), -110f, tabWidth, 50f, () => ShowTab(index));
                _tabImages[i] = (Image)tab.targetGraphic;
            }

            // The demo panel, with its instruction and Reset.
            RectTransform demo = NewRect("Demo", box);
            Place(demo, 0f, -165f - DemoHeight / 2f, DemoWidth, DemoHeight);
            AddImage(demo.gameObject, DemoColor);
            _instruction = NewText("Instruction", demo, "", 22f, FontStyles.Bold);
            Place(_instruction.rectTransform, -60f, DemoHeight / 2f - 26f, DemoWidth - 160f, 36f);
            _instruction.alignment = TextAlignmentOptions.MidlineLeft;
            _instruction.color = GothicTheme.GoldBright;
            NewButton("Reset", demo, "Reset", 20f, DemoWidth / 2f - 70f, DemoHeight / 2f - 28f, 120f, 40f, ResetDemo);

            for (int i = 0; i < _chips.Length; i++)
                _chips[i] = BuildChip(demo, i);

            _bar = NewRect("Bar", demo).gameObject;
            var bar = (RectTransform)_bar.transform;
            Place(bar, -110f, -10f, 560f, 36f);
            AddImage(_bar, new Color(0f, 0f, 0f, 0.6f));
            _barFill = NewRect("Fill", bar);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = Vector2.one;
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            _barFillImage = AddImage(_barFill.gameObject, SoulsColor);
            _barText = NewText("Label", bar, "", 20f, FontStyles.Bold);
            Stretch(_barText.rectTransform);
            _barText.alignment = TextAlignmentOptions.Center;

            _info = NewText("Info", demo, "", 22f, FontStyles.Normal);
            Place(_info.rectTransform, 0f, -DemoHeight / 2f + 26f, DemoWidth - 40f, 40f);
            _info.color = SubText;

            // The rules paragraph.
            _rules = NewText("Rules", box, "", 24f, FontStyles.Normal);
            Place(_rules.rectTransform, 0f, -165f - DemoHeight - 90f, DemoWidth, 150f);
            _rules.alignment = TextAlignmentOptions.TopLeft;
            _rules.color = GothicTheme.TextColor;
            _rules.textWrappingMode = TextWrappingModes.Normal;

            // Back, Close, Next.
            float y = -BoxHeight + 55f;
            _back = NewButton("Back", box, "Back", 26f, -300f, y, 200f, 60f, () => ShowTab(_tab - 1)).gameObject;
            NewButton("Close", box, "Close", 26f, 0f, y, 200f, 60f, Close);
            Button next = NewButton("Next", box, "Next", 26f, 300f, y, 200f, 60f, Next);
            _nextLabel = next.GetComponentInChildren<TMP_Text>();
        }

        private Chip BuildChip(RectTransform demo, int index)
        {
            var chip = new Chip { Rect = NewRect("Chip" + (index + 1), demo) };
            chip.Rect.anchorMin = chip.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            chip.Rect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            chip.Background = AddImage(chip.Rect.gameObject, ChipColor);
            chip.Background.raycastTarget = true;
            chip.Ring = chip.Rect.gameObject.AddComponent<Outline>();
            chip.Ring.effectDistance = new Vector2(2.5f, -2.5f);
            Button button = chip.Rect.gameObject.AddComponent<Button>();
            button.targetGraphic = chip.Background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnChip(index));
            chip.Label = NewText("Label", chip.Rect, "", 20f, FontStyles.Normal);
            Stretch(chip.Label.rectTransform);
            chip.Label.alignment = TextAlignmentOptions.Center;
            return chip;
        }

        // ---------- Layout helpers (positions from the parent's top centre, in pixels) ----------

        // Box children are placed from the box's top centre; demo children from the demo's centre.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            bool inBox = rect.parent != null && rect.parent.name == "Box";
            rect.anchorMin = rect.anchorMax = inBox ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private Button NewButton(string name, RectTransform parent, string label, float size, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, y, width, height);
            Image image = AddImage(rect.gameObject, GothicTheme.ButtonColor);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TMP_Text text = NewText("Label", rect, label, size, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            GothicTheme.StyleButton(button);
            return button;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var image = (Image)go.AddComponent(typeof(Image));
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
        {
            RectTransform rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (GothicTheme.TitleFont != null && style == FontStyles.Bold)
                tmp.font = GothicTheme.TitleFont;
            return tmp;
        }
    }
}
