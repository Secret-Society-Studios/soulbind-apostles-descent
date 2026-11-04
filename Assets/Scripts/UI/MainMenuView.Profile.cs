using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    // The stats half of the profile page (the name half is in MainMenuView.cs).
    // Stats come from PlayerProfile, which every finished match adds to.
    public sealed partial class MainMenuView
    {
        // ---------- Profile stats ----------

        private const float ProfileWidth = 720f;
        private const float StatLeft = 60f;      // matches the name field's left edge
        private const float StatRowWidth = 600f; // matches the name field's width
        private const float StatGap = 16f;
        private const float StatHeight = 80f;
        private const int StatsPerRow = 4;
        private const int TopCardCount = 3;

        private readonly TMP_Text[] _statValues = new TMP_Text[8];
        private readonly TMP_Text[] _topCards = new TMP_Text[TopCardCount];
        private readonly List<ArcanaId> _topCardIds = new List<ArcanaId>(TopCardCount);
        private TMP_Text _noStats;

        private static readonly string[] StatNames =
        {
            "Matches", "Wins", "Win rate", "Best streak",
            "Ascensions", "Cards played", "Combos", "Streak now"
        };

        // Builds the grid of profile stat boxes.
        private void BuildProfileStats(RectTransform panel)
        {
            float width = (StatRowWidth - StatGap * (StatsPerRow - 1)) / StatsPerRow;
            for (int i = 0; i < StatNames.Length; i++)
            {
                float x = StatLeft + (i % StatsPerRow) * (width + StatGap);
                float y = 176f + (i / StatsPerRow) * (StatHeight + StatGap);
                _statValues[i] = BuildStatTile(panel, StatNames[i], x, y, width);
            }

            TMP_Text label = Kit.Label(panel, "TopCardsLabel", "MOST USED CARDS", 12f, Kit.InterBold, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(label.rectTransform, StatLeft, 372f, 300f, 18f);

            float chipWidth = (StatRowWidth - StatGap * (TopCardCount - 1)) / TopCardCount;
            for (int i = 0; i < TopCardCount; i++)
            {
                float x = StatLeft + i * (chipWidth + StatGap);
                RectTransform chip = Kit.Box(panel, "TopCard" + i, Kit.Inset, Kit.Line, 10f);
                Kit.At(chip, x, 396f, chipWidth, 48f);
                _topCards[i] = Kit.Label(chip, "Text", "", 15f, Kit.InterSemiBold, Kit.Soft);
                Kit.Stretch(_topCards[i].rectTransform, 6f);
                _topCards[i].textWrappingMode = TextWrappingModes.NoWrap;
                _topCards[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            _noStats = Kit.Label(panel, "NoStats", "Finish a match to start your stats.", 15f, Kit.Inter, Kit.Muted);
            Kit.At(_noStats.rectTransform, StatLeft, 396f, StatRowWidth, 48f);
        }

        // One box: a big number on top, its name underneath. Returns the number's label.
        private static TMP_Text BuildStatTile(RectTransform panel, string name, float x, float y, float width)
        {
            RectTransform tile = Kit.Box(panel, "Stat " + name, Kit.Inset, Kit.Line, 10f);
            Kit.At(tile, x, y, width, StatHeight);

            TMP_Text value = Kit.Label(tile, "Value", "0", 28f, Kit.DisplayHeavy, Kit.Title);
            Kit.At(value.rectTransform, 0f, 10f, width, 36f);
            value.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_Text caption = Kit.Label(tile, "Name", name, 12f, Kit.Inter, Kit.Muted);
            Kit.At(caption.rectTransform, 0f, 50f, width, 18f);
            return value;
        }

        // Called each time the page opens, so a match just played shows up.
        private void ShowProfileStats()
        {
            PlayerProfile.Stats stats = PlayerProfile.Load();
            _statValues[0].SetText("{0}", stats.Matches);
            _statValues[1].SetText("{0}", stats.Wins);
            _statValues[2].text = stats.Matches > 0 ? $"{stats.WinRate * 100f:0}%" : "-";
            _statValues[3].SetText("{0}", stats.BestStreak);
            _statValues[4].SetText("{0}", stats.Ascensions);
            _statValues[5].SetText("{0}", stats.CardsPlayed);
            _statValues[6].SetText("{0}", stats.Combos);
            _statValues[7].SetText("{0}", stats.Streak);

            PlayerProfile.GetTopCards(stats, TopCardCount, _topCardIds);
            _noStats.gameObject.SetActive(_topCardIds.Count == 0);
            for (int i = 0; i < TopCardCount; i++)
            {
                bool used = i < _topCardIds.Count;
                _topCards[i].transform.parent.gameObject.SetActive(used);
                if (!used)
                    continue;
                ArcanaId card = _topCardIds[i];
                string name = CardCatalog.Get(card).Name;
                _topCards[i].text = $"{name}  <color=#DBB261>x{stats.CardUses[(int)card]}</color>";
            }
        }
    }
}
