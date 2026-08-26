using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class GameRulesBoundaryContractTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [Test]
        public void CanPlay_RejectsInvalidHandIndexAndInsufficientActions()
        {
            var state = GameRules.NewGame(0x2468ACE0u);
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Heal);

            Assert.That(GameRules.CanPlay(ref state, 0, -1, 0, 0), Is.False);
            Assert.That(GameRules.CanPlay(ref state, 0, 1, 0, 0), Is.False);

            state.actionLeft = 0;
            Assert.That(GameRules.CanPlay(ref state, 0, 0, 0, 0), Is.False);
        }

        [Test]
        public void CanPlay_RejectsOutOfBoundsAndBlockedTargets()
        {
            var state = GameRules.NewGame(0x2468ACE1u);
            state.p1X = 3;
            state.p1Y = 1;
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Fireball);

            Assert.That(GameRules.CanPlay(ref state, 0, 0, -1, 1), Is.False);
            Assert.That(GameRules.CanPlay(ref state, 0, 0, GameRules.Size, 1), Is.False);

            state.obstacles |= 1UL << (1 * GameRules.Size + 3);
            Assert.That(GameRules.CanPlay(ref state, 0, 0, 3, 1), Is.False);
        }

        [Test]
        public void CanMove_RejectsOccupiedDestinationAndUnavailableBasicMove()
        {
            var state = GameRules.NewGame(0x31415926u);
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(GameRules.CanMove(ref state, 0, 3, 1), Is.False);

            state.p1X = 4;
            state.p1Y = 7;
            state.p0BaseMove = 0;
            Assert.That(GameRules.CanMove(ref state, 0, 3, 1), Is.False);
        }

        [Test]
        public void CanPlay_RejectsAnyCardAfterWinnerIsRecorded()
        {
            var state = GameRules.NewGame(0xABCDEF01u);
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Heal);
            state.winner = 2;

            Assert.That(GameRules.CanPlay(ref state, 0, 0, 0, 0), Is.False);
            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.False);
        }
    }
}
