using System;
using System.Collections.Generic;
using System.IO;

namespace SoulBind.Core
{
    /// <summary>
    /// Everything one player may know about a match at one moment, as plain data that can be sent over
    /// the network. The host makes one per player after every change (Match.CaptureSnapshot); each
    /// player's game loads it into its own copy of the match (Match.ApplySnapshot).
    /// Hidden information never leaves the host: other Apostles' hands and the draw pile only keep
    /// their size, with every card replaced by HiddenCard.
    /// </summary>
    public sealed class MatchSnapshot
    {
        /// <summary>Stands in for every card the viewer is not allowed to see.</summary>
        public const ArcanaId HiddenCard = (ArcanaId)0;

        private const byte FormatVersion = 1;

        public sealed class Apostle
        {
            public int Id;
            public HexCoord Position;
            public int Vessel;
            public int Souls;
            public int Corruption;
            public int Shield;
            public int TurnsStarted;
            public readonly List<TimedEffect> TimedEffects = new List<TimedEffect>();
            public readonly List<ArcanaId> Hand = new List<ArcanaId>();
        }

        /// <summary>Counts up with every snapshot the host sends, so an old one arriving late can be ignored.</summary>
        public int Sequence;
        /// <summary>Whose view this is (their own hand is the only one filled in). 0 = everything (tests).</summary>
        public int ViewerId;

        public readonly List<Apostle> Apostles = new List<Apostle>();

        // Turn order
        public readonly List<int> Seats = new List<int>();
        public readonly List<int> TurnSequence = new List<int>();
        public int SequenceIndex;
        public int StartSeat;
        public int RoundNumber;
        public TurnDirection Direction;
        public bool AwaitingPhaseStart;

        // Cards
        public readonly List<ArcanaId> DrawPile = new List<ArcanaId>();
        public readonly List<ArcanaId> DiscardPile = new List<ArcanaId>();

        // Board
        public readonly List<HexCoord> SpecialTiles = new List<HexCoord>();
        public readonly List<SpecialTileType> SpecialTypes = new List<SpecialTileType>();
        public int AwakenedId;
        public HexCoord AscensionTile;

        // The current turn
        public int CurrentApostleId;
        public int CapacityMaximum;
        public int CapacityRemaining;
        public EngagementState Engagement;
        public int EngagedTargetId;
        public int PendingAttack;
        public int ExecuteBelowVessel;
        public int CorruptionReducersThisTurn;
        public bool StrengthAwaitingPartner;
        public bool EmperorArmed;
        public bool SkipRestOfRound;
        public readonly int[] ElementsPlayedThisTurn = new int[ElementCombo.ElementCount];
        public readonly List<ArcanaId> AttackCards = new List<ArcanaId>();
        public readonly List<StrikeHit> LastStrikeHits = new List<StrikeHit>();

        // The end
        public bool IsOver;
        public int WinnerId;
        public MatchEndReason EndReason;

        // ---- Bytes ----

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream(512))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(FormatVersion);
                w.Write(Sequence);
                w.Write((byte)ViewerId);

                w.Write((byte)Apostles.Count);
                foreach (Apostle a in Apostles)
                {
                    w.Write((byte)a.Id);
                    WriteHex(w, a.Position);
                    w.Write((short)a.Vessel);
                    w.Write((short)a.Souls);
                    w.Write((short)a.Corruption);
                    w.Write((short)a.Shield);
                    w.Write((short)a.TurnsStarted);
                    w.Write((byte)a.TimedEffects.Count);
                    foreach (TimedEffect e in a.TimedEffects)
                    {
                        w.Write((byte)e.Kind);
                        w.Write((short)e.Amount);
                        w.Write((short)e.StartsOnTurn);
                        w.Write((short)e.ExpiresOnTurn);
                    }
                    WriteCards(w, a.Hand);
                }

                WriteBytes(w, Seats);
                WriteBytes(w, TurnSequence);
                w.Write((byte)SequenceIndex);
                w.Write((byte)StartSeat);
                w.Write((short)RoundNumber);
                w.Write((byte)Direction);
                w.Write(AwaitingPhaseStart);

                WriteCards(w, DrawPile);
                WriteCards(w, DiscardPile);

                w.Write((byte)SpecialTiles.Count);
                for (int i = 0; i < SpecialTiles.Count; i++)
                {
                    WriteHex(w, SpecialTiles[i]);
                    w.Write((byte)SpecialTypes[i]);
                }
                w.Write((byte)AwakenedId);
                WriteHex(w, AscensionTile);

                w.Write((byte)CurrentApostleId);
                w.Write((short)CapacityMaximum);
                w.Write((short)CapacityRemaining);
                w.Write((byte)Engagement);
                w.Write((byte)EngagedTargetId);
                w.Write((short)PendingAttack);
                w.Write((short)ExecuteBelowVessel);
                w.Write((byte)CorruptionReducersThisTurn);
                w.Write(StrengthAwaitingPartner);
                w.Write(EmperorArmed);
                w.Write(SkipRestOfRound);
                for (int i = 0; i < ElementsPlayedThisTurn.Length; i++)
                    w.Write((byte)ElementsPlayedThisTurn[i]);
                WriteCards(w, AttackCards);
                w.Write((byte)LastStrikeHits.Count);
                foreach (StrikeHit hit in LastStrikeHits)
                {
                    w.Write((byte)hit.TargetId);
                    w.Write((short)hit.Attack);
                    w.Write((short)hit.Damage);
                    w.Write((byte)hit.SoulsStolen);
                    w.Write(hit.Eliminated);
                }

                w.Write(IsOver);
                w.Write((byte)WinnerId);
                w.Write((byte)EndReason);
                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Reads bytes made by ToBytes. Returns null if they are damaged or from another version.</summary>
        public static MatchSnapshot FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var r = new BinaryReader(stream))
                {
                    if (r.ReadByte() != FormatVersion)
                        return null;

                    var s = new MatchSnapshot { Sequence = r.ReadInt32(), ViewerId = r.ReadByte() };

                    int apostles = r.ReadByte();
                    for (int i = 0; i < apostles; i++)
                    {
                        var a = new Apostle
                        {
                            Id = r.ReadByte(),
                            Position = ReadHex(r),
                            Vessel = r.ReadInt16(),
                            Souls = r.ReadInt16(),
                            Corruption = r.ReadInt16(),
                            Shield = r.ReadInt16(),
                            TurnsStarted = r.ReadInt16(),
                        };
                        int effects = r.ReadByte();
                        for (int e = 0; e < effects; e++)
                        {
                            var kind = (TimedEffectKind)r.ReadByte();
                            int amount = r.ReadInt16();
                            int starts = r.ReadInt16();
                            int expires = r.ReadInt16();
                            a.TimedEffects.Add(new TimedEffect(kind, amount, starts, expires));
                        }
                        ReadCards(r, a.Hand);
                        s.Apostles.Add(a);
                    }

                    ReadBytes(r, s.Seats);
                    ReadBytes(r, s.TurnSequence);
                    s.SequenceIndex = r.ReadByte();
                    s.StartSeat = r.ReadByte();
                    s.RoundNumber = r.ReadInt16();
                    s.Direction = (TurnDirection)r.ReadByte();
                    s.AwaitingPhaseStart = r.ReadBoolean();

                    ReadCards(r, s.DrawPile);
                    ReadCards(r, s.DiscardPile);

                    int specials = r.ReadByte();
                    for (int i = 0; i < specials; i++)
                    {
                        s.SpecialTiles.Add(ReadHex(r));
                        s.SpecialTypes.Add((SpecialTileType)r.ReadByte());
                    }
                    s.AwakenedId = r.ReadByte();
                    s.AscensionTile = ReadHex(r);

                    s.CurrentApostleId = r.ReadByte();
                    s.CapacityMaximum = r.ReadInt16();
                    s.CapacityRemaining = r.ReadInt16();
                    s.Engagement = (EngagementState)r.ReadByte();
                    s.EngagedTargetId = r.ReadByte();
                    s.PendingAttack = r.ReadInt16();
                    s.ExecuteBelowVessel = r.ReadInt16();
                    s.CorruptionReducersThisTurn = r.ReadByte();
                    s.StrengthAwaitingPartner = r.ReadBoolean();
                    s.EmperorArmed = r.ReadBoolean();
                    s.SkipRestOfRound = r.ReadBoolean();
                    for (int i = 0; i < s.ElementsPlayedThisTurn.Length; i++)
                        s.ElementsPlayedThisTurn[i] = r.ReadByte();
                    ReadCards(r, s.AttackCards);
                    int hits = r.ReadByte();
                    for (int i = 0; i < hits; i++)
                    {
                        int target = r.ReadByte();
                        int attack = r.ReadInt16();
                        int damage = r.ReadInt16();
                        int stolen = r.ReadByte();
                        bool eliminated = r.ReadBoolean();
                        s.LastStrikeHits.Add(new StrikeHit(target, attack, damage, stolen, eliminated));
                    }

                    s.IsOver = r.ReadBoolean();
                    s.WinnerId = r.ReadByte();
                    s.EndReason = (MatchEndReason)r.ReadByte();
                    return s;
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private static void WriteHex(BinaryWriter w, HexCoord tile)
        {
            w.Write((short)tile.Q);
            w.Write((short)tile.R);
        }

        private static HexCoord ReadHex(BinaryReader r) => new HexCoord(r.ReadInt16(), r.ReadInt16());

        private static void WriteCards(BinaryWriter w, List<ArcanaId> cards)
        {
            w.Write((byte)cards.Count);
            foreach (ArcanaId card in cards)
                w.Write((byte)card);
        }

        private static void ReadCards(BinaryReader r, List<ArcanaId> cards)
        {
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                byte card = r.ReadByte();
                if (card >= CardCatalog.All.Count)
                    throw new EndOfStreamException("Unknown card.");
                cards.Add((ArcanaId)card);
            }
        }

        private static void WriteBytes(BinaryWriter w, List<int> values)
        {
            w.Write((byte)values.Count);
            foreach (int value in values)
                w.Write((byte)value);
        }

        private static void ReadBytes(BinaryReader r, List<int> values)
        {
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
                values.Add(r.ReadByte());
        }
    }
}