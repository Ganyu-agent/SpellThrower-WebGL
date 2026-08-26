using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class GameActionContractTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [Test]
        public void SuccessfulCardUses_UpdateLatestActionAndIncrementSequence()
        {
            var state = GameRules.NewGame(0x10203040u);
            state.p1X = 3;
            state.p1Y = 1;
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Fireball);
            GameRules.Hand(ref state, 0).Add((byte)CardId.Heal);

            Assert.That(state.lastActionKind, Is.EqualTo(GameplayActionKind.None));
            Assert.That(state.lastActionSequence, Is.Zero);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.True);
            Assert.That(state.lastActionKind, Is.EqualTo(GameplayActionKind.CardUsed));
            Assert.That(state.lastActionPlayer, Is.EqualTo((byte)0));
            Assert.That(state.lastActionCardId, Is.EqualTo((byte)CardId.Fireball));
            Assert.That(state.lastActionSequence, Is.EqualTo((ushort)1));

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.True);
            Assert.That(state.lastActionPlayer, Is.EqualTo((byte)0));
            Assert.That(state.lastActionCardId, Is.EqualTo((byte)CardId.Heal));
            Assert.That(state.lastActionSequence, Is.EqualTo((ushort)2));
        }

        [Test]
        public void RejectedCardUse_DoesNotChangeActionMetadata()
        {
            var state = GameRules.NewGame(0x11223344u);
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Fireball);
            state.lastActionKind = GameplayActionKind.CardUsed;
            state.lastActionPlayer = 1;
            state.lastActionCardId = (byte)CardId.Lightning;
            state.lastActionSequence = 23;
            state.obstacles |= 1UL << (1 * GameRules.Size + 3);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.False);
            Assert.That(state.lastActionKind, Is.EqualTo(GameplayActionKind.CardUsed));
            Assert.That(state.lastActionPlayer, Is.EqualTo((byte)1));
            Assert.That(state.lastActionCardId, Is.EqualTo((byte)CardId.Lightning));
            Assert.That(state.lastActionSequence, Is.EqualTo((ushort)23));
        }

        [Test]
        public void ActionSequence_SkipsZeroAfterUshortWrap()
        {
            var state = GameRules.NewGame(0x55667788u);
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Heal);
            state.lastActionSequence = ushort.MaxValue;

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.True);
            Assert.That(state.lastActionSequence, Is.EqualTo((ushort)1));
        }
    }
}
