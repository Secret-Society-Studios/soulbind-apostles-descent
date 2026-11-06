using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

public class ActiveBuffsTests
{
    private readonly List<ActiveBuff> _buffs = new List<ActiveBuff>();
    private ApostleState _apostle;

    [SetUp]
    public void SetUp()
    {
        _apostle = new ApostleState(1, new HexCoord(0, 0), 5);
        _apostle.BeginTurn();
    }

    private List<ActiveBuff> Collect()
    {
        ActiveBuffs.Collect(_apostle, _buffs);
        return _buffs;
    }

    [Test]
    public void NoEffects_ListsNothing()
    {
        Assert.IsEmpty(Collect());
    }

    [Test]
    public void ActiveEffect_CountsTheCurrentRoundAndDownToItsLast()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);

        Assert.AreEqual("+4 DEF (2r)", ActiveBuffs.Describe(Collect()[0]));

        _apostle.BeginTurn();
        Assert.AreEqual("+4 DEF (1r)", ActiveBuffs.Describe(Collect()[0]));

        _apostle.BeginTurn();
        Assert.IsEmpty(Collect());
    }

    [Test]
    public void DelayedEffect_IsUpcomingUntilItsRoundStarts()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Attack, 5, 1, delayTurns: 1);

        ActiveBuff buff = Collect()[0];
        Assert.IsTrue(buff.IsUpcoming);
        Assert.AreEqual("Next +5 ATK (1r)", ActiveBuffs.Describe(buff));

        _apostle.BeginTurn();
        buff = Collect()[0];
        Assert.IsFalse(buff.IsUpcoming);
        Assert.AreEqual("+5 ATK (1r)", ActiveBuffs.Describe(buff));
    }

    [Test]
    public void EffectStartingAfterNextRound_IsNotListed()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Attack, 5, 1, delayTurns: 2);

        Assert.IsEmpty(Collect());
    }

    [Test]
    public void ActiveEffects_ComeBeforeUpcomingOnes()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Attack, 5, 1, delayTurns: 1);
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 2, 1);

        List<ActiveBuff> buffs = Collect();
        Assert.AreEqual(2, buffs.Count);
        Assert.AreEqual(TimedEffectKind.Defense, buffs[0].Kind);
        Assert.AreEqual(TimedEffectKind.Attack, buffs[1].Kind);
    }

    [Test]
    public void SameKindEndingTogether_MergesIntoOneLine()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 2, 1);
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 3, 1);

        List<ActiveBuff> buffs = Collect();
        Assert.AreEqual(1, buffs.Count);
        Assert.AreEqual(5, buffs[0].Amount);
    }

    [Test]
    public void SameKindEndingApart_StaysOnSeparateLines()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 2, 1);
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);

        Assert.AreEqual(2, Collect().Count);
    }

    [Test]
    public void DefenseUsedUpByAnAttack_ShowsWhatIsLeft()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);

        _apostle.AbsorbAttack(3);
        Assert.AreEqual(1, Collect()[0].Amount);

        _apostle.AbsorbAttack(1);
        Assert.IsEmpty(Collect());
    }

    [Test]
    public void CapacityPenalty_ReadsAsALoss()
    {
        _apostle.AddTimedEffect(TimedEffectKind.CapacityPenalty, 2, 1);

        Assert.AreEqual("-2 CAP (1r)", ActiveBuffs.Describe(Collect()[0]));
    }

    [Test]
    public void EliminatedApostle_ListsNothing()
    {
        _apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);
        _apostle.TakeDamage(GameConstants.MaxVessel);

        Assert.IsEmpty(Collect());
    }
}
