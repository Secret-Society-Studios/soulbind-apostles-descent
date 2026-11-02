using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Quick chat ids map to lines and emotes, and the limiter stops spam.</summary>
    public class QuickChatTests
    {
        [Test]
        public void Ids_CoverLinesThenEmotes()
        {
            Assert.AreEqual(QuickChat.Lines.Length + QuickChat.EmoteCount, QuickChat.Count);
            Assert.AreEqual("GG", QuickChat.GetLine(0));
            Assert.IsFalse(QuickChat.IsEmote(0));

            int first = QuickChat.GetEmoteId(0);
            Assert.IsTrue(QuickChat.IsEmote(first));
            Assert.AreEqual(0, QuickChat.GetEmoteIndex(first));
            Assert.AreEqual(string.Empty, QuickChat.GetLine(first));

            Assert.IsFalse(QuickChat.IsValid(-1));
            Assert.IsFalse(QuickChat.IsValid(QuickChat.Count));
            // Ids travel as one byte.
            Assert.LessOrEqual(QuickChat.Count, 255);
        }

        [Test]
        public void Limiter_OneMessagePerCooldown_PerSeat()
        {
            var limiter = new ChatLimiter();
            Assert.IsTrue(limiter.TryUse(1, 0f));
            Assert.IsFalse(limiter.TryUse(1, QuickChat.CooldownSeconds - 0.1f));
            Assert.IsTrue(limiter.TryUse(2, 0.5f), "Another seat has its own wait");
            Assert.IsTrue(limiter.TryUse(1, QuickChat.CooldownSeconds));
        }

        [Test]
        public void Limiter_RefusesBadSeats()
        {
            var limiter = new ChatLimiter();
            Assert.IsFalse(limiter.TryUse(0, 0f));
            Assert.IsFalse(limiter.TryUse(GameConstants.MaxPlayers + 1, 0f));
        }
    }
}
