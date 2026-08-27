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

        [Test]
        public void GameRules_DirectTileCard_RequiresOpponentUnlessEmptyTilesAreAllowed()
        {
            var directAttack = GameRules.NewGame(101);
            directAttack.p0X = 1;
            directAttack.p0Y = 1;
            directAttack.p1X = 3;
            directAttack.p1Y = 1;
            directAttack.p0Hand.Clear();
            directAttack.p0Hand.Add((byte)CardId.Fireball);
            directAttack.actionLeft = (byte)GameRules.MaxCost;

            Assert.That(GameRules.CanPlay(ref directAttack, 0, 0, 2, 1), Is.False,
                        "a direct attack must not be spendable on an empty tile");
            Assert.That(GameRules.TryPlay(ref directAttack, 0, 0, 2, 1), Is.False);
            Assert.That(directAttack.p0Hand.Length, Is.EqualTo(1));
            Assert.That(directAttack.actionLeft, Is.EqualTo(GameRules.MaxCost));

            Assert.That(GameRules.CanPlay(ref directAttack, 0, 0, 1, 1), Is.False,
                        "a tile-target card must not target its caster");
            Assert.That(GameRules.TryPlay(ref directAttack, 0, 0, 1, 1), Is.False);
            Assert.That(directAttack.p0Hp, Is.EqualTo(GameRules.MaxHp));
            Assert.That(directAttack.p0Hand.Length, Is.EqualTo(1));

            Assert.That(GameRules.TryPlay(ref directAttack, 0, 0, 3, 1), Is.True,
                        "the same card must resolve when the opponent occupies the tile");
            Assert.That(directAttack.p1Hp, Is.EqualTo(GameRules.MaxHp - 5));

            var areaCard = GameRules.NewGame(102);
            areaCard.p0X = 1;
            areaCard.p0Y = 1;
            areaCard.p1X = 6;
            areaCard.p1Y = 6;
            areaCard.p0Hand.Clear();
            areaCard.p0Hand.Add((byte)CardId.Burn);
            areaCard.actionLeft = (byte)GameRules.MaxCost;

            Assert.That(GameRules.CanPlay(ref areaCard, 0, 0, 1, 1), Is.False,
                        "an empty-tile area card must still reject its caster's tile");
            Assert.That(GameRules.CanPlay(ref areaCard, 0, 0, 2, 1), Is.True,
                        "an area card that opts into empty tiles must keep that behavior");
            Assert.That(GameRules.TryPlay(ref areaCard, 0, 0, 2, 1), Is.True);
            Assert.That(areaCard.worldEffects.Length, Is.EqualTo(1));
            Assert.That(areaCard.worldEffects[0].Kind, Is.EqualTo(WorldEffectKind.FireZone));
            Assert.That(areaCard.worldEffects[0].X, Is.EqualTo(2));
            Assert.That(areaCard.worldEffects[0].Y, Is.EqualTo(1));
        }
    }
}
