namespace SoulBind.Core
{
    /// <summary>
    /// The 22 Major Arcana, numbered as in the rulebook. This number is what gets saved
    /// and sent over the network, so never reorder or renumber these values.
    /// </summary>
    public enum ArcanaId : byte
    {
        Fool = 0,
        Magician = 1,
        HighPriestess = 2,
        Empress = 3,
        Emperor = 4,
        Hierophant = 5,
        Lovers = 6,
        Chariot = 7,
        Strength = 8,
        Hermit = 9,
        WheelOfFortune = 10,
        Justice = 11,
        HangedMan = 12,
        Death = 13,
        Temperance = 14,
        Devil = 15,
        Tower = 16,
        Star = 17,
        Moon = 18,
        Sun = 19,
        Judgement = 20,
        World = 21
    }
}