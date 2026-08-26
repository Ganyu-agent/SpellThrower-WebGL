using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class DuelCardContractTests
    {
        [Test]
        public void Metadata_MatchesDuelCardDefinition()
        {
            var card = new DuelCardDef();

            Assert.That(card.Name, Is.EqualTo("듀얼을 신청하지"));
            Assert.That(card.Cost, Is.EqualTo(7));
            Assert.That(card.Range, Is.Zero);
            Assert.That(card.Power, Is.Zero);
            Assert.That(card.Targeted, Is.False);
            Assert.That(card.TargetKind, Is.EqualTo(CardTargetKind.Self));
            Assert.That(card.Arc, Is.False);
        }

        [Test]
        public void OnUse_WhenInstigatorHasHigherHandDamage_DamagesTargetByHalf()
        {
            var card = new DuelCardDef();
            var instigator = new FakeCardPlayer(handImmediateDamagePower: 7, hp: 10);
            var target = new FakeCardPlayer(handImmediateDamagePower: 3, hp: 10);

            card.OnUse(new CardUseContext(card, instigator, target, 2, 1));

            Assert.That(target.DamageCalls, Is.EqualTo(1));
            Assert.That(target.LastDamageAmount, Is.EqualTo(3));
            Assert.That(instigator.DamageCalls, Is.Zero);
        }

        [Test]
        public void OnUse_WhenTargetHasHigherHandDamage_DamagesInstigatorByHalf()
        {
            var card = new DuelCardDef();
            var instigator = new FakeCardPlayer(handImmediateDamagePower: 2, hp: 10);
            var target = new FakeCardPlayer(handImmediateDamagePower: 8, hp: 10);

            card.OnUse(new CardUseContext(card, instigator, target, 2, 1));

            Assert.That(instigator.DamageCalls, Is.EqualTo(1));
            Assert.That(instigator.LastDamageAmount, Is.EqualTo(4));
            Assert.That(target.DamageCalls, Is.Zero);
        }

        [Test]
        public void OnUse_WhenHandDamageIsTied_DoesNotDamageEitherPlayer()
        {
            var card = new DuelCardDef();
            var instigator = new FakeCardPlayer(handImmediateDamagePower: 4, hp: 10);
            var target = new FakeCardPlayer(handImmediateDamagePower: 4, hp: 10);

            card.OnUse(new CardUseContext(card, instigator, target, 2, 1));

            Assert.That(instigator.DamageCalls, Is.Zero);
            Assert.That(target.DamageCalls, Is.Zero);
            Assert.That(instigator.Hp, Is.EqualTo(10));
            Assert.That(target.Hp, Is.EqualTo(10));
        }

        [Test]
        public void OnUse_WhenTargetIsNull_DoesNothing()
        {
            var card = new DuelCardDef();
            var instigator = new FakeCardPlayer(handImmediateDamagePower: 7, hp: 10);

            Assert.DoesNotThrow(() =>
                card.OnUse(new CardUseContext(card, instigator, null, 2, 1)));

            Assert.That(instigator.DamageCalls, Is.Zero);
        }

        [Test]
        public void OnUse_WhenWinningPowerIsOdd_RoundsDamageDown()
        {
            var card = new DuelCardDef();
            var instigator = new FakeCardPlayer(handImmediateDamagePower: 1, hp: 10);
            var target = new FakeCardPlayer(handImmediateDamagePower: 5, hp: 10);

            card.OnUse(new CardUseContext(card, instigator, target, 2, 1));

            Assert.That(instigator.DamageCalls, Is.EqualTo(1));
            Assert.That(instigator.LastDamageAmount, Is.EqualTo(2));
        }

        private sealed class FakeCardPlayer : ICardPlayer
        {
            public int PlayerIndex { get; }
            public int X { get; }
            public int Y { get; }
            public int Hp { get; private set; }
            public int HandImmediateDamagePower { get; }

            public int DamageCalls { get; private set; }
            public int LastDamageAmount { get; private set; }

            public FakeCardPlayer(int handImmediateDamagePower, int hp)
            {
                HandImmediateDamagePower = handImmediateDamagePower;
                Hp = hp;
            }

            public void Damage(int amount)
            {
                DamageCalls++;
                LastDamageAmount = amount;
                Hp = Hp > amount ? Hp - amount : 0;
            }

            public void Heal(int amount) { }
            public void MoveTo(int x, int y) { }
            public void PushFrom(int sourceX, int sourceY, int tiles) { }
            public void LockMove() { }
            public void DrawCards(int count) { }
            public bool AddTag(PlayerTagId tagId, byte duration, byte value = 0) => true;
            public bool HasTag(PlayerTagId tagId) => false;
            public bool RemoveTag(PlayerTagId tagId) => false;
        }
    }
}
