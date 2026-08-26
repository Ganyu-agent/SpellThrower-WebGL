using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class CardUsageAndTagContractTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [Test]
        public void Fireball_OnUse_DamagesTargetFakePlayer_NotInstigator()
        {
            var instigator = new FakeCardPlayer(0, 1, 1, 10);
            var target = new FakeCardPlayer(1, 4, 1, 10);
            var card = Cards.Get((byte)CardId.Fireball);

            card.OnUse(new CardUseContext(card, instigator, target, 4, 1));

            Assert.That(target.DamageCalls, Is.EqualTo(1));
            Assert.That(target.LastDamageAmount, Is.EqualTo(card.Power));
            Assert.That(target.Hp, Is.EqualTo(10 - card.Power));
            Assert.That(instigator.DamageCalls, Is.Zero);
            Assert.That(instigator.Hp, Is.EqualTo(10));
        }

        [Test]
        public void Sprint_OnUse_MovesInstigatorFakePlayer_ToContextCoordinates()
        {
            var instigator = new FakeCardPlayer(0, 1, 1, 10);
            var target = new FakeCardPlayer(1, 4, 4, 10);
            var card = Cards.Get((byte)CardId.Sprint);

            card.OnUse(new CardUseContext(card, instigator, target, 3, 2));

            Assert.That(instigator.MoveCalls, Is.EqualTo(1));
            Assert.That(instigator.LastMoveX, Is.EqualTo(3));
            Assert.That(instigator.LastMoveY, Is.EqualTo(2));
            Assert.That(instigator.X, Is.EqualTo(3));
            Assert.That(instigator.Y, Is.EqualTo(2));
            Assert.That(target.MoveCalls, Is.Zero);
            Assert.That(target.X, Is.EqualTo(4));
            Assert.That(target.Y, Is.EqualTo(4));
        }

        [Test]
        public void AddOrRefreshTag_ReplacesExistingTagWithoutStacking_AndRefreshesDurationAndValue()
        {
            var state = GameRules.NewGame(1001);

            Assert.That(GameRules.AddOrRefreshTag(
                ref state, 0, PlayerTagId.MoveBoost, 2, 3), Is.True);
            Assert.That(GameRules.AddOrRefreshTag(
                ref state, 0, PlayerTagId.MoveBoost, 5, 9), Is.True);

            Assert.That(GameRules.Tags(ref state, 0).Length, Is.EqualTo(1));
            Assert.That(GameRules.GetTag(ref state, 0, PlayerTagId.MoveBoost, out var tag), Is.True);
            Assert.That(tag.DurationTurns, Is.EqualTo(5));
            Assert.That(tag.Value, Is.EqualTo(9));
        }

        [Test]
        public void EndTurn_TicksOnlyOwnerTag_AndRemovesItWhenDurationReachesZero()
        {
            var state = GameRules.NewGame(1002);
            GameRules.AddOrRefreshTag(ref state, 0, PlayerTagId.MoveBoost, 2, 10);
            GameRules.AddOrRefreshTag(ref state, 1, PlayerTagId.MoveBoost, 2, 20);

            // Player 0 ends: only player 0's duration decreases.
            GameRules.EndTurn(ref state);
            Assert.That(GameRules.GetTag(ref state, 0, PlayerTagId.MoveBoost, out var p0AfterFirstEnd), Is.True);
            Assert.That(p0AfterFirstEnd.DurationTurns, Is.EqualTo(1));
            Assert.That(GameRules.GetTag(ref state, 1, PlayerTagId.MoveBoost, out var p1AfterFirstEnd), Is.True);
            Assert.That(p1AfterFirstEnd.DurationTurns, Is.EqualTo(2));

            // Player 1 ends: player 0 remains unchanged and player 1 decreases.
            GameRules.EndTurn(ref state);
            Assert.That(GameRules.GetTag(ref state, 0, PlayerTagId.MoveBoost, out var p0AfterSecondEnd), Is.True);
            Assert.That(p0AfterSecondEnd.DurationTurns, Is.EqualTo(1));
            Assert.That(GameRules.GetTag(ref state, 1, PlayerTagId.MoveBoost, out var p1AfterSecondEnd), Is.True);
            Assert.That(p1AfterSecondEnd.DurationTurns, Is.EqualTo(1));

            // Player 0 ends again: its duration reaches zero and the tag is removed.
            GameRules.EndTurn(ref state);
            Assert.That(GameRules.GetTag(ref state, 0, PlayerTagId.MoveBoost, out var removedTag), Is.False);
            Assert.That(removedTag.DurationTurns, Is.Zero);
            Assert.That(GameRules.GetTag(ref state, 1, PlayerTagId.MoveBoost, out var p1AfterThirdEnd), Is.True);
            Assert.That(p1AfterThirdEnd.DurationTurns, Is.EqualTo(1));
        }

        [Test]
        public void AddOrRefreshTag_WhenCapacityIsExceeded_ReturnsFalseWithoutThrowingOrChangingExistingTags()
        {
            var state = GameRules.NewGame(1003);
            ref var tags = ref GameRules.Tags(ref state, 0);

            for (int i = 0; i < tags.Capacity; i++)
                tags.Add(new PlayerTag((PlayerTagId)(10 + i), 1, (byte)i));

            int lengthBefore = tags.Length;
            Assert.That(lengthBefore, Is.EqualTo(tags.Capacity));

            bool added = false;
            Assert.DoesNotThrow(() =>
            {
                added = GameRules.AddOrRefreshTag(ref state, 0, PlayerTagId.MoveBoost, 4, 99);
            });

            Assert.That(added, Is.False);
            Assert.That(GameRules.Tags(ref state, 0).Length, Is.EqualTo(lengthBefore));
            for (int i = 0; i < lengthBefore; i++)
            {
                var existing = GameRules.Tags(ref state, 0)[i];
                Assert.That(existing.Id, Is.EqualTo((PlayerTagId)(10 + i)));
                Assert.That(existing.DurationTurns, Is.EqualTo(1));
                Assert.That(existing.Value, Is.EqualTo((byte)i));
            }
        }

        private sealed class FakeCardPlayer : ICardPlayer
        {
            public int PlayerIndex { get; }
            public int X { get; private set; }
            public int Y { get; private set; }
            public int Hp { get; private set; }
            public int HandImmediateDamagePower { get; set; }

            public int DamageCalls { get; private set; }
            public int LastDamageAmount { get; private set; }
            public int MoveCalls { get; private set; }
            public int LastMoveX { get; private set; }
            public int LastMoveY { get; private set; }

            public FakeCardPlayer(int playerIndex, int x, int y, int hp)
            {
                PlayerIndex = playerIndex;
                X = x;
                Y = y;
                Hp = hp;
            }

            public void Damage(int amount)
            {
                DamageCalls++;
                LastDamageAmount = amount;
                Hp = Hp > amount ? Hp - amount : 0;
            }

            public void Heal(int amount) { }

            public void MoveTo(int x, int y)
            {
                MoveCalls++;
                LastMoveX = x;
                LastMoveY = y;
                X = x;
                Y = y;
            }

            public void PushFrom(int sourceX, int sourceY, int tiles) { }
            public void LockMove() { }
            public void DrawCards(int count) { }
            public bool AddTag(PlayerTagId tagId, byte duration, byte value = 0) => true;
            public bool HasTag(PlayerTagId tagId) => false;
            public bool RemoveTag(PlayerTagId tagId) => false;
        }
    }
}
