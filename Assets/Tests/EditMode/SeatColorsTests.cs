using NUnit.Framework;
using SoulBind.Core;

public class SeatColorsTests
{
    private const int N = SeatColors.None;

    [Test]
    public void Reservations_CannotStealEvenWhenTheRequesterIsTheHost()
    {
        var reservations = new System.Collections.Generic.Dictionary<string, int>();
        Assert.IsTrue(SeatColors.TryReserve(reservations, "host", 0));
        Assert.IsTrue(SeatColors.TryReserve(reservations, "guest", 1));
        Assert.IsFalse(SeatColors.TryReserve(reservations, "host", 1));
        Assert.AreEqual(0, reservations["host"]);
        Assert.AreEqual(1, reservations["guest"]);
    }

    [Test]
    public void Reservations_TwoRequestsForOneColour_FirstGrantKeepsIt()
    {
        var reservations = new System.Collections.Generic.Dictionary<string, int>();
        SeatColors.TryReserve(reservations, "first", 0);
        SeatColors.TryReserve(reservations, "second", 1);
        Assert.IsTrue(SeatColors.TryReserve(reservations, "second", 4));
        Assert.IsFalse(SeatColors.TryReserve(reservations, "first", 4));
        Assert.IsTrue(SeatColors.TryReserve(reservations, "first", 1));
        Assert.IsFalse(SeatColors.TryReserve(reservations, "first", 99));
        Assert.AreEqual(1, reservations["first"]);
        Assert.AreEqual(4, reservations["second"]);
    }

    [Test]
    public void Reservations_LeavingFreesColour_KeepingYourOwnColourIsAllowed()
    {
        var reservations = new System.Collections.Generic.Dictionary<string, int>();
        SeatColors.TryReserve(reservations, "guest", 2);
        Assert.IsTrue(SeatColors.TryReserve(reservations, "guest", 2));
        reservations.Remove("guest");
        Assert.IsTrue(SeatColors.TryReserve(reservations, "newcomer", 2));
    }

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
