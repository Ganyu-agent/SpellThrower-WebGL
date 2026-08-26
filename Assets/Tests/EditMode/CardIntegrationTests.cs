using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class CardIntegrationTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [Test]
        public void Duel_UsesOnlyImmediateDamageCards_AndDamagesTheLowerHand()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 1).Clear();
            GiveCard(ref state, 0, CardId.Duel);
            GiveCard(ref state, 0, CardId.Fireball);
            GiveCard(ref state, 0, CardId.Heal);
            GiveCard(ref state, 1, CardId.Lightning);

            // 내 손패 즉시 피해 합 5(파이어볼) < 상대 12(번개) → 12/2 = 6 을 내가 맞는다.
            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.True);
            Assert.That(state.p0Hp, Is.EqualTo(GameRules.MaxHp - 6));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
        }

        [Test]
        public void Duel_DelaysWinnerJudgementUntilTurnEnd()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;
            state.p1Hp = 1;
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 1).Clear();
            GiveCard(ref state, 0, CardId.Duel);
            GiveCard(ref state, 0, CardId.Fireball);
            GiveCard(ref state, 0, CardId.Fireball);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.Zero);
            Assert.That(state.winner, Is.EqualTo(1));
        }

        [Test]
        public void Lightning_StacksOnlyWhenAPlayerOrStructureIsHit()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 2;
            GameRules.Hand(ref state, 0).Clear();
            GiveCard(ref state, 0, CardId.Lightning);
            GiveCard(ref state, 0, CardId.Lightning);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 2), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 1, 1), Is.True);
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
        }

        [Test]
        public void IceWall_OccupiesThreeTilesAndBlocksMovement()
        {
            var state = NewState();
            GameRules.Hand(ref state, 0).Clear();
            GiveCard(ref state, 0, CardId.IceWall);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 2), Is.True);
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(3));
            Assert.That(GameRules.IsBlocked(ref state, 3, 2), Is.True);
        }

        [Test]
        public void Duel_UsesSelfTargetMetadata_AndIgnoresTargetTile()
        {
            var state = NewState();
            var duel = Cards.Get((byte)CardId.Duel);

            Assert.That(duel.TargetKind, Is.EqualTo(CardTargetKind.Self));
            Assert.That(duel.Range, Is.Zero);

            GameRules.Hand(ref state, 0).Clear();
            GiveCard(ref state, 0, CardId.Duel);

            Assert.That(GameRules.CanPlay(ref state, 0, 0, 0, 0), Is.True);
        }

        [Test]
        public void Wind_RecoilsCaster_WhileOtherWindCardsDoNot()
        {
            var windState = NewWindState(CardId.Wind);
            Assert.That(GameRules.TryPlay(ref windState, 0, 0, 5, 2), Is.True);
            Assert.That(windState.p0X, Is.EqualTo(2));

            AssertNoWindRecoil(CardId.Breath);
            AssertNoWindRecoil(CardId.Pull);
            AssertNoWindRecoil(CardId.Collision);
            AssertNoWindRecoil(CardId.Cyclone);
        }

        [Test]
        public void SprintCards_UseCurrentValueTiers()
        {
            // 카드 정보2 DB 기준 단보가 기본, 질주가 저다.
            Assert.That(Cards.Get((byte)CardId.Sprint).Tier, Is.EqualTo(CardValueTier.Low));
            Assert.That(Cards.Get((byte)CardId.Step).Tier, Is.EqualTo(CardValueTier.Basic));
        }

        [Test]
        public void FireRain_DoesNotDamageAdjacentMapObstacleOrStructure()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 3;
            state.obstacles &= ~(1UL << (2 * GameRules.Size + 3));
            state.obstacles |= 1UL << (2 * GameRules.Size + 3);
            state.obstacleHp[2 * GameRules.Size + 3] = GameRules.DefaultMapObstacleHp;

            Assert.That(WorldEffectSystem.TryAddStructure(
                ref state, 0, StructureKind.Totem, 3, 4, 3), Is.True);

            GameRules.Hand(ref state, 0).Clear();
            GiveCard(ref state, 0, CardId.FireRain);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 3), Is.True);
            Assert.That(GameRules.IsMapObstacle(ref state, 3, 2), Is.True);
            Assert.That(GameRules.MapObstacleHp(ref state, 3, 2), Is.EqualTo(3));
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref state, 3, 4, out var structure), Is.True);
            Assert.That(structure.Power, Is.EqualTo(3));
        }

        static void AssertNoWindRecoil(CardId cardId)
        {
            var state = NewWindState(cardId);
            Assert.That(GameRules.TryPlay(ref state, 0, 0, 5, 2), Is.True);
            Assert.That(state.p0X, Is.EqualTo(3));
        }

        static GameState NewWindState(CardId card)
        {
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 2;
            state.p1X = 5;
            state.p1Y = 2;
            GameRules.Hand(ref state, 0).Clear();
            GiveCard(ref state, 0, card);
            return state;
        }

        /// 카드 동작만 보는 테스트라 첫 턴 코스트 4에 막히지 않게 상한까지 채운다.
        static GameState NewState()
        {
            var state = GameRules.NewGame(0x13572468u);
            state.actionLeft = (byte)GameRules.MaxCost;
            return state;
        }

        static void GiveCard(ref GameState state, int player, CardId card)
        {
            GameRules.Hand(ref state, player).Add((byte)card);
        }
    }
}
