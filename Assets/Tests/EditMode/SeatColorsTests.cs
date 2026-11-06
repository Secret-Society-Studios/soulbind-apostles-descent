using NUnit.Framework;
using SoulBind.Core;

public class SeatColorsTests
{
    private const int N = SeatColors.None;

    private static int[] Resolve(params int[] wanted)
    {
        var colors = new int[SeatColors.Count];
        SeatColors.Resolve(wanted, colors);
        return colors;
    }

    [Test]
    public void NoWishes_SeatsKeepPaletteOrder()
    {
        Assert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Resolve(N, N, N, N, N, N));
    }

    [Test]
    public void OneWish_OthersTakeTheRestInOrder()
    {
        Assert.AreEqual(new[] { 2, 0, 1, 3, 4, 5 }, Resolve(2, N, N, N, N, N));
    }

    [Test]
    public void Clash_EarlierSeatKeepsItAndTheLaterSeatTakesTheLowestFree()
    {
        Assert.AreEqual(new[] { 1, 0, 2, 3, 4, 5 }, Resolve(1, 1, N, N, N, N));
    }

    [Test]
    public void WishOfALaterSeat_IsNotTakenByAnEarlierSeatWithoutOne()
    {
        Assert.AreEqual(new[] { 1, 0, 2, 3, 4, 5 }, Resolve(N, 0, N, N, N, N));
    }

    [Test]
    public void BadWishes_AreTreatedAsNone()
    {
        Assert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Resolve(99, -7, N, N, N, N));
    }

    [Test]
    public void EveryoneWantsTheSame_EverySeatStillDiffers()
    {
        Assert.IsTrue(SeatColors.IsValid(Resolve(3, 3, 3, 3, 3, 3)));
    }

    [Test]
    public void IsValid_RejectsMalformedArrays()
    {
        Assert.IsFalse(SeatColors.IsValid(null));
        Assert.IsFalse(SeatColors.IsValid(new[] { 0, 1 }));
        Assert.IsFalse(SeatColors.IsValid(new[] { 0, 0, 2, 3, 4, 5 }));
        Assert.IsFalse(SeatColors.IsValid(new[] { 0, 1, 2, 3, 4, 6 }));
    }
}
