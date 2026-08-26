using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public class DefectRegressionTests
    {
        [Test]
        public void Cards_Get_WithInvalidId_ReturnsNull()
        {
            Assert.DoesNotThrow(() => Cards.Get(255));
            Assert.That(Cards.Get(255), Is.Null);
        }

        [Test]
        public void CardText_WithInvalidId_ReturnsEmptyString()
        {
            Assert.DoesNotThrow(() => CardText.GetName((CardId)255));
            Assert.That(CardText.GetName((CardId)255), Is.EqualTo(string.Empty));

            var dummyDef = Cards.Get(0);
            Assert.DoesNotThrow(() => CardText.GetDescription((CardId)255, dummyDef));
            Assert.That(CardText.GetDescription((CardId)255, dummyDef), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GameRules_GetTag_WithInvalidPlayer_ReturnsFalse()
        {
            GameState state = new GameState();
            bool result = GameRules.GetTag(ref state, 99, PlayerTagId.MoveBoost, out _);
            Assert.IsFalse(result);
        }

        [Test]
        public void GameRules_HandImmediateDamagePower_WithInvalidPlayer_ReturnsZero()
        {
            GameState state = new GameState();
            int power = GameRules.HandImmediateDamagePower(ref state, -1);
            Assert.That(power, Is.EqualTo(0));
        }

        [Test]
        public void GameRules_InvalidCardInHand_IsRejectedAndIgnored()
        {
            GameState state = new GameState();
            state.p0Hand.Add(255);

            Assert.That(GameRules.CanPlay(ref state, 0, 0, 0, 0), Is.False);
            Assert.That(GameRules.HandImmediateDamagePower(ref state, 0), Is.Zero);
        }
    }
}
