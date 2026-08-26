using System;
using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    /// <summary>
    /// Behavior contracts for the current eight-card prototype.
    /// Scenario setup is kept behind RuleTestDriver so runtime state changes
    /// remain localized to one test seam.
    /// </summary>
    public sealed class GameRulesContractTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [Test]
        public void Surrender_GivesTheWinToTheOtherPlayer_RegardlessOfTurn()
        {
            var s = GameRules.NewGame(0x51A2B3C4u);
            s.turnPlayer = 0;

            // 자기 턴이 아닌 쪽도 항복할 수 있다.
            Assert.That(GameRules.Surrender(ref s, 1), Is.True);
            Assert.That(s.winner, Is.EqualTo(1));   // winner - 1 == 이긴 플레이어
        }

        [Test]
        public void Surrender_IsIgnoredOnAFinishedGameOrAnInvalidPlayer()
        {
            var s = GameRules.NewGame(0x51A2B3C4u);

            Assert.That(GameRules.Surrender(ref s, 2), Is.False);
            Assert.That(s.winner, Is.Zero);

            Assert.That(GameRules.Surrender(ref s, 0), Is.True);
            Assert.That(s.winner, Is.EqualTo(2));

            // 이미 끝난 판은 두 번째 항복으로 승자가 바뀌지 않는다.
            Assert.That(GameRules.Surrender(ref s, 1), Is.False);
            Assert.That(s.winner, Is.EqualTo(2));
        }

        [Test]
        public void RejectedTargetDoesNotConsumeCardOrAction()
        {
            var game = new RuleTestDriver();
            game.ClearHand(0);
            int fire = game.AddCard(0, CardId.Fireball);
            game.BlockTile(3, 1);

            int handBefore = game.HandCount(0);
            int discardBefore = game.DiscardCount(0);
            byte actionsBefore = game.ActionsRemaining;

            Assert.That(game.Play(0, fire, 3, 1), Is.False);
            Assert.That(game.HandCount(0), Is.EqualTo(handBefore));
            Assert.That(game.DiscardCount(0), Is.EqualTo(discardBefore));
            Assert.That(game.ActionsRemaining, Is.EqualTo(actionsBefore));
        }

        [Test]
        public void RegularCardUsesConfiguredPowerAndMovesToDiscard()
        {
            var game = new RuleTestDriver();
            game.ClearHand(0);
            int fire = game.AddCard(0, CardId.Fireball);
            game.SetPosition(1, 3, 1);

            byte healthBefore = game.Health(1);
            int expectedHealth = Math.Max(0, healthBefore - Cards.Get((byte)CardId.Fireball).Power);
            int discardBefore = game.DiscardCount(0);

            Assert.That(game.Play(0, fire, 3, 1), Is.True);
            Assert.That(game.Health(1), Is.EqualTo(expectedHealth));
            Assert.That(game.HandCount(0), Is.EqualTo(0));
            Assert.That(game.DiscardCount(0), Is.EqualTo(discardBefore + 1));
            Assert.That(game.ActionsRemaining, Is.EqualTo(GameRules.StartCost - Cards.Get((byte)CardId.Fireball).Cost));
        }

        [Test]
        public void CardUseResolvesDeathImmediately()
        {
            var game = new RuleTestDriver();
            game.ClearHand(0);
            int fire = game.AddCard(0, CardId.Fireball);
            game.SetPosition(1, 3, 1);
            game.SetHealth(1, Cards.Get((byte)CardId.Fireball).Power);

            Assert.That(game.Play(0, fire, 3, 1), Is.True);
            Assert.That(game.Winner, Is.EqualTo(1));
        }

        [Test]
        public void HealUsesConfiguredPowerWithoutExceedingMaximum()
        {
            var game = new RuleTestDriver();
            game.ClearHand(0);
            int heal = game.AddCard(0, CardId.Heal);
            game.SetHealth(0, (byte)(GameRules.MaxHp - 1));

            Assert.That(game.Play(0, heal), Is.True);
            Assert.That(game.Health(0), Is.EqualTo(GameRules.MaxHp));
        }

        [Test]
        public void AccelerationAddsOneMovementForTheCurrentTurn()
        {
            var game = new RuleTestDriver();
            game.ClearHand(0);
            int acceleration = game.AddCard(0, CardId.Acceleration);

            Assert.That(game.Play(0, acceleration), Is.True);
            Assert.That(game.Move(0, 4, 1), Is.True);
        }

        [Test]
        public void InactivePlayerCannotMoveOrPlay()
        {
            var game = new RuleTestDriver();
            game.ClearHand(1);
            int fire = game.AddCard(1, CardId.Fireball);

            Assert.That(game.CurrentPlayer, Is.EqualTo(0));
            Assert.That(game.Move(1, 4, 6), Is.False);
            Assert.That(game.Play(1, fire, 3, 1), Is.False);
        }
    }
}
