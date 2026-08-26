using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class CardInfoV2ImplementationTests
    {
        [Test]
        public void Burn_CreatesTwoTickFireZoneWithoutImmediateDamage()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Burn, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 3, 1, out var zone), Is.True);
            Assert.That(zone.Power, Is.EqualTo(GameRules.FireZoneTick));
            Assert.That(zone.RemainingTurns, Is.EqualTo(2));
        }

        [Test]
        public void FlamePillar_DamagesImmediatelyAndLeavesFireZone()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.FlamePillar, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
            Assert.That(WorldEffectSystem.IsFrosted(ref state, 3, 1), Is.False);
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 3, 1, out _), Is.True);
        }

        [Test]
        public void FireRain_DamagesCrossAndLeavesCenterFireZone()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 3;

            Assert.That(PlaySingle(ref state, CardId.FireRain, 3, 3), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 3, 3, out _), Is.True);
        }

        [Test]
        public void Explosion_DamagesThreeByThreeAndCreatesNineFireZones()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 4;
            state.p1X = 5;
            state.p1Y = 5;

            Assert.That(PlaySingle(ref state, CardId.Explosion, 4, 4), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 16));
            Assert.That(CountEffects(ref state, WorldEffectKind.FireZone), Is.EqualTo(9));
        }

        [Test]
        public void Iceball_DamagesAndLocksTheOpponentNextTurnBasicMove()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Iceball, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 3));
            Assert.That(state.p1MoveLocked, Is.EqualTo(1));
            Assert.That(GameRules.HasTag(ref state, 1, PlayerTagId.MoveLocked), Is.True);
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void IceWall_CreatesThreeBlockingStructuresWithTwentyHp()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 3;

            Assert.That(PlaySingle(ref state, CardId.IceWall, 3, 3), Is.True);
            Assert.That(CountStructures(ref state, StructureKind.IceWall), Is.EqualTo(3));
            Assert.That(GameRules.IsBlocked(ref state, 3, 3), Is.True);
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref state, 3, 3, out var wall), Is.True);
            Assert.That(wall.Power, Is.EqualTo(20));
        }

        [Test]
        public void Chill_DamagesAndCreatesOneTurnFrostZone()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 3;
            state.p1X = 3;
            state.p1Y = 3;

            Assert.That(PlaySingle(ref state, CardId.Chill, 3, 3), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 8));
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FrostZone, 3, 3, out var frost), Is.True);
            Assert.That(frost.RemainingTurns, Is.EqualTo(1));
        }

        [Test]
        public void Frostbite_DamagesAndCreatesTwoTurnFrostZone()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Frostbite, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 15));
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FrostZone, 3, 1, out var frost), Is.True);
            Assert.That(frost.RemainingTurns, Is.EqualTo(2));
        }

        [Test]
        public void IceAge_DamagesCenterAndCreatesThreeByThreeFrost()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 4;
            state.p1X = 4;
            state.p1Y = 4;

            Assert.That(PlaySingle(ref state, CardId.IceAge, 4, 4), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 18));
            Assert.That(CountEffects(ref state, WorldEffectKind.FrostZone), Is.EqualTo(9));
        }

        [Test]
        public void Breath_PushesOneAndDealsHitDamage()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Breath, 3, 1), Is.True);
            Assert.That(state.p1X, Is.EqualTo(4));
            Assert.That(state.p1Y, Is.EqualTo(1));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
        }

        [Test]
        public void Wind_PushesTwoAndRecoilsTheCaster()
        {
            var state = NewState();
            state.p0X = 2;
            state.p0Y = 1;
            state.p1X = 4;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Wind, 4, 1), Is.True);
            Assert.That(state.p1X, Is.EqualTo(6));
            Assert.That(state.p0X, Is.EqualTo(1));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
        }

        [Test]
        public void Pull_DragsTargetTwoTilesTowardCaster()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 4;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Pull, 4, 1), Is.True);
            Assert.That(state.p1X, Is.EqualTo(2));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
        }

        [Test]
        public void Collision_DamagesUnitAndWallWhenPushHitsObstacle()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 3;
            state.p1Y = 1;
            Block(ref state, 4, 1);

            Assert.That(PlaySingle(ref state, CardId.Collision, 3, 1), Is.True);
            Assert.That(state.p1X, Is.EqualTo(3));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
            // 벽 박힘 피해 12 > 맵 장애물 HP 3 이라 그 칸은 부서진다.
            Assert.That(GameRules.MapObstacleHp(ref state, 4, 1), Is.EqualTo(0));
        }

        [Test]
        public void Cyclone_PushesThreeTiles()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Cyclone, 3, 1), Is.True);
            Assert.That(state.p1X, Is.EqualTo(6));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
        }

        [Test]
        public void Discharge_UsesTwoTileLineAndDealsTen()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Discharge, 3, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 10));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
        }

        [Test]
        public void Thunderbolt_IgnoresWallAndDealsTwelve()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 5;
            state.p1Y = 1;
            Block(ref state, 3, 1);

            Assert.That(PlaySingle(ref state, CardId.Thunderbolt, 5, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
            Assert.That(GameRules.MapObstacleHp(ref state, 3, 1), Is.EqualTo(3));
        }

        [Test]
        public void LightningStrike_IgnoresWallAndUsesBaseSix()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 5;
            state.p1Y = 1;
            Block(ref state, 3, 1);

            Assert.That(PlaySingle(ref state, CardId.LightningStrike, 5, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
        }

        [Test]
        public void Lightning_UsesFiveTileRangeAndTwelveDamage()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 6;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Lightning, 6, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
        }

        [Test]
        public void MasterSpark_HitsEveryUnitAndStructureOnOneLine()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 1;
            state.p1Y = 6;
            Assert.That(WorldEffectSystem.TryAddStructure(ref state, 0, StructureKind.Totem, 1, 3, 3, 2), Is.True);

            Assert.That(PlaySingle(ref state, CardId.MasterSpark, 1, 7), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 16));
            Assert.That(state.p0LightningStack, Is.EqualTo(1));
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref state, 1, 3, out _), Is.False);
        }

        [Test]
        public void Warmth_HealsTenUpToMaximum()
        {
            var state = NewState();
            state.p0Hp = 5;

            Assert.That(PlaySingle(ref state, CardId.Warmth, 0, 0), Is.True);
            Assert.That(state.p0Hp, Is.EqualTo(15));
        }

        [Test]
        public void Regeneration_HealsImmediatelyAndTwiceAtOwnerTurnStart()
        {
            var state = NewState();
            state.p0Hp = 5;

            Assert.That(PlaySingle(ref state, CardId.Regeneration, 0, 0), Is.True);
            Assert.That(state.p0Hp, Is.EqualTo(13));
            EndRoundForPlayerZero(ref state);
            Assert.That(state.p0Hp, Is.EqualTo(23));
            EndRoundForPlayerZero(ref state);
            Assert.That(state.p0Hp, Is.EqualTo(33));
        }

        [Test]
        public void Purify_RemovesNearbyHazardsButNotDistantHazardsOrStructures()
        {
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 3;
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 3, 3), Is.True);
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 4, 3), Is.True);
            Assert.That(WorldEffectSystem.TryAddFrostZone(ref state, 1, 3, 4, 2), Is.True);
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 5, 3), Is.True);
            Assert.That(WorldEffectSystem.TryAddStructure(ref state, 1, StructureKind.Totem, 3, 5, 3, 2), Is.True);

            Assert.That(PlaySingle(ref state, CardId.Purify, 0, 0), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 3, 3, out _), Is.False);
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 4, 3, out _), Is.False);
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FrostZone, 3, 4, out _), Is.False);
            Assert.That(WorldEffectSystem.TryGetTileEffect(ref state, WorldEffectKind.FireZone, 5, 3, out _), Is.True);
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref state, 3, 5, out _), Is.True);
        }

        [Test]
        public void Baptism_HealsSixteenAndPurifiesNearbyHazards()
        {
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 3;
            state.p0Hp = 5;
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 3, 3), Is.True);
            Assert.That(WorldEffectSystem.TryAddFrostZone(ref state, 1, 4, 3, 1), Is.True);

            Assert.That(PlaySingle(ref state, CardId.Baptism, 0, 0), Is.True);
            Assert.That(state.p0Hp, Is.EqualTo(21));
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void Draw_DrawsTwoCardsFromOwnDeck()
        {
            var state = NewState();
            GameRules.Deck(ref state, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Iceball);

            Assert.That(PlaySingle(ref state, CardId.Draw, 0, 0), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Fireball), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Iceball), Is.True);
        }

        [Test]
        public void Divination_SelectsOneOfThreeAndDiscardsTheOtherTwo()
        {
            var state = NewState();
            GameRules.Deck(ref state, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Wind);

            Assert.That(PlaySingle(ref state, CardId.Divination, 1, 0), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Iceball), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Fireball), Is.False);
            Assert.That(ContainsCard(ref state, 0, CardId.Wind), Is.False);
            Assert.That(GameRules.Disc(ref state, 0).Length, Is.EqualTo(3));
        }

        [Test]
        public void Exchange_DiscardsSelectedCardAndDrawsTwo()
        {
            var state = NewState();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Exchange);
            GameRules.Hand(ref state, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Wind);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 1, 0), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Fireball), Is.False);
            Assert.That(ContainsCard(ref state, 0, CardId.Iceball), Is.True);
            Assert.That(ContainsCard(ref state, 0, CardId.Wind), Is.True);
            Assert.That(ContainsDiscard(ref state, 0, CardId.Exchange), Is.True);
        }

        [Test]
        public void Supply_AddsOneDrawToEachOfTheNextThreeOwnerTurns()
        {
            var state = NewState();
            for (int i = 0; i < 20; i++) GameRules.Deck(ref state, 0).Add((byte)CardId.Fireball);

            Assert.That(PlaySingle(ref state, CardId.Supply, 0, 0), Is.True);
            // 2~14라운드 기본 1장 + 보급 1장 = 3라운드 동안 2장씩, 그 뒤로 1장.
            EndRoundForPlayerZero(ref state);
            Assert.That(GameRules.Deck(ref state, 0).Length, Is.EqualTo(18));
            EndRoundForPlayerZero(ref state);
            Assert.That(GameRules.Deck(ref state, 0).Length, Is.EqualTo(16));
            EndRoundForPlayerZero(ref state);
            Assert.That(GameRules.Deck(ref state, 0).Length, Is.EqualTo(14));
            EndRoundForPlayerZero(ref state);
            Assert.That(GameRules.Deck(ref state, 0).Length, Is.EqualTo(13));
        }

        [Test]
        public void Harvest_DrawsThreeCards()
        {
            var state = NewState();
            GameRules.Deck(ref state, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref state, 0).Add((byte)CardId.Wind);

            Assert.That(PlaySingle(ref state, CardId.Harvest, 0, 0), Is.True);
            Assert.That(GameRules.Hand(ref state, 0).Length, Is.EqualTo(3));
        }

        [Test]
        public void Sprint_MovesIndependentlyTwoTiles()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Sprint, 3, 1), Is.True);
            Assert.That(state.p0X, Is.EqualTo(3));
            Assert.That(state.p0Y, Is.EqualTo(1));
        }

        [Test]
        public void Step_MovesIndependentlyOneTile()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Step, 2, 1), Is.True);
            Assert.That(state.p0X, Is.EqualTo(2));
        }

        [Test]
        public void Charge_StopsBeforeEnemyAndDealsTwelveDamage()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Charge, 3, 1), Is.True);
            Assert.That(state.p0X, Is.EqualTo(2));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
        }

        [Test]
        public void Acceleration_AddsOneToTheCurrentTurnMovement()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            GameRules.Hand(ref state, 0).Add((byte)CardId.Acceleration);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.True);
            GameRules.Hand(ref state, 0).Add((byte)CardId.Sprint);
            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.True);
            Assert.That(state.p0X, Is.EqualTo(3));
        }

        [Test]
        public void Leap_AllowsOneMovementCardToCrossOneObstacle()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            Block(ref state, 2, 1);
            GameRules.Hand(ref state, 0).Add((byte)CardId.Leap);
            GameRules.Hand(ref state, 0).Add((byte)CardId.Sprint);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.True);
            Assert.That(GameRules.TryPlay(ref state, 0, 0, 3, 1), Is.True);
            Assert.That(state.p0X, Is.EqualTo(3));
            Assert.That(GameRules.HasTag(ref state, 0, PlayerTagId.Leap), Is.False);
        }

        [Test]
        public void Totem_SniperDealsEightAtOwnerTurnEndWithinTwoTiles()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 4;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Totem, 3, 1), Is.True);
            WorldEffectSystem.ResolveTurnEnd(ref state, 0);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 8));
        }

        [Test]
        public void Guardian_OnlyFiresAtOneTile()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 5;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Guardian, 3, 1), Is.True);
            WorldEffectSystem.ResolveTurnEnd(ref state, 0);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
            state.p1X = 4;
            WorldEffectSystem.ResolveTurnEnd(ref state, 0);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 6));
        }

        [Test]
        public void Thorn_DamagesAdjacentEnemyAtEnemyTurnEnd()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 4;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Thorn, 3, 1), Is.True);
            WorldEffectSystem.ResolveTurnEnd(ref state, 0);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
            WorldEffectSystem.ResolveTurnEnd(ref state, 1);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
        }

        [Test]
        public void Blessing_HealsFourteenAtTheNextOwnerTurnStart()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p0Hp = 5;
            Assert.That(PlaySingle(ref state, CardId.Blessing, 3, 1), Is.True);

            EndRoundForPlayerZero(ref state);
            Assert.That(state.p0Hp, Is.EqualTo(19));
        }

        [Test]
        public void Detonation_ExplodesForSixteenWhenDestroyedAndNotWhenExpired()
        {
            var state = NewState();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 4;
            state.p1Y = 1;

            Assert.That(PlaySingle(ref state, CardId.Detonation, 3, 1), Is.True);
            Assert.That(WorldEffectSystem.DamageStructureAt(ref state, 3, 1, 20, 1), Is.True);
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 16));
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref state, 3, 1, out _), Is.False);

            var expired = NewState();
            expired.p1X = 7;
            expired.p1Y = 7;
            Assert.That(PlaySingle(ref expired, CardId.Detonation, 3, 1), Is.True);
            // 지속은 라운드 단위라 턴 번호가 짝수일 때만 줄어든다.
            expired.turnCount = 2;
            WorldEffectSystem.ResolveTurnEnd(ref expired, 0);
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref expired, 3, 1, out _), Is.True);
            expired.turnCount = 4;
            WorldEffectSystem.ResolveTurnEnd(ref expired, 0);
            Assert.That(WorldEffectSystem.TryGetStructureAt(ref expired, 3, 1, out _), Is.False);
            Assert.That(expired.p1Hp, Is.EqualTo(GameRules.MaxHp));
        }

        static GameState NewState()
        {
            var state = GameRules.NewGame(0x2468ACE1u);
            state.obstacles = 0;
            state.obstacleHp.Clear();
            state.worldEffects.Clear();
            state.p0X = 1;
            state.p0Y = 1;
            state.p1X = 7;
            state.p1Y = 7;
            state.p0Hp = GameRules.MaxHp;
            state.p1Hp = GameRules.MaxHp;
            state.turnPlayer = 0;
            state.turnCount = 1;
            state.actionLeft = (byte)GameRules.MaxCost;
            state.p0BaseMove = 1;
            state.p1BaseMove = 1;
            state.p0MoveLocked = 0;
            state.p1MoveLocked = 0;
            state.p0LightningStack = 0;
            state.p1LightningStack = 0;
            state.winner = 0;
            state.p0Hand.Clear();
            state.p1Hand.Clear();
            state.p0Deck.Clear();
            state.p1Deck.Clear();
            state.p0Disc.Clear();
            state.p1Disc.Clear();
            state.p0Tags.Clear();
            state.p1Tags.Clear();
            return state;
        }

        static bool PlaySingle(ref GameState state, CardId card, int targetX, int targetY)
        {
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)card);
            return GameRules.TryPlay(ref state, 0, 0, targetX, targetY);
        }

        static void Block(ref GameState state, int x, int y)
        {
            int index = y * GameRules.Size + x;
            state.obstacles |= 1UL << index;
            while (state.obstacleHp.Length <= index) state.obstacleHp.Add(0);
            state.obstacleHp[index] = GameRules.DefaultMapObstacleHp;
        }

        static int CountEffects(ref GameState state, WorldEffectKind kind)
        {
            int count = 0;
            for (int i = 0; i < WorldEffectSystem.Count(ref state); i++)
            {
                Assert.That(WorldEffectSystem.TryGet(ref state, i, out var effect), Is.True);
                if (effect.Kind == kind) count++;
            }
            return count;
        }

        static int CountStructures(ref GameState state, StructureKind kind)
        {
            int count = 0;
            for (int i = 0; i < WorldEffectSystem.Count(ref state); i++)
            {
                Assert.That(WorldEffectSystem.TryGet(ref state, i, out var effect), Is.True);
                if (effect.Kind == WorldEffectKind.Structure && effect.Structure == kind) count++;
            }
            return count;
        }

        static bool ContainsCard(ref GameState state, int player, CardId card)
        {
            ref var hand = ref GameRules.Hand(ref state, player);
            for (int i = 0; i < hand.Length; i++)
                if (hand[i] == (byte)card) return true;
            return false;
        }

        static bool ContainsDiscard(ref GameState state, int player, CardId card)
        {
            ref var discard = ref GameRules.Disc(ref state, player);
            for (int i = 0; i < discard.Length; i++)
                if (discard[i] == (byte)card) return true;
            return false;
        }

        static void EndRoundForPlayerZero(ref GameState state)
        {
            Assert.That(state.turnPlayer, Is.EqualTo(0));
            GameRules.EndTurn(ref state);
            Assert.That(state.turnPlayer, Is.EqualTo(1));
            GameRules.EndTurn(ref state);
            Assert.That(state.turnPlayer, Is.EqualTo(0));
        }
    }
}
