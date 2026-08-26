using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class WorldEffectSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRules.MaxTurns = 20;
        }

        [TearDown]
        public void RestoreTurnLimit() => GameRules.MaxTurns = GameRules.DefaultMaxTurns;

        [Test]
        public void FireZone_BurnsAtTheOpponentTurnEnd_AndExpiresAfterTwoTicks()
        {
            var state = NewState();
            state.p1X = 3;
            state.p1Y = 1;

            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 0, 3, 1), Is.True);

            GameRules.EndTurn(ref state); // 시전자(0) 턴 종료: 불길은 타지 않는다.
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(1));

            GameRules.EndTurn(ref state); // 상대(1) 턴 종료: 첫 번째 틱.
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 2));
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(1));

            GameRules.EndTurn(ref state); // 시전자 턴 종료: 그대로.
            GameRules.EndTurn(ref state); // 상대 턴 종료: 두 번째이자 마지막 틱.
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 4));
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void FireZone_ConsumesATickEvenWhenTheTileIsEmpty()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 0, 3, 2), Is.True);

            GameRules.EndTurn(ref state); // 시전자 턴 종료는 틱이 아니다.
            GameRules.EndTurn(ref state); // 상대 턴 종료: 빈 칸이어도 횟수는 줄어든다.

            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp));
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 3, 2, out var remaining), Is.True);
            Assert.That(remaining.RemainingTurns, Is.EqualTo(1));

            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state);
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void FireZone_OnSameTileRefreshesWithoutDuplicating()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 0, 3, 1, 2, 2), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 3, 1, out var first), Is.True);

            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 3, 1, 3, 4), Is.True);
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(1));
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 3, 1, out var refreshed), Is.True);
            Assert.That(refreshed.Sequence, Is.EqualTo(first.Sequence));
            Assert.That(refreshed.SourcePlayer, Is.EqualTo(1));
            Assert.That(refreshed.Power, Is.EqualTo(3));
            Assert.That(refreshed.RemainingTurns, Is.EqualTo(4));
        }

        [Test]
        public void DelayedTeleport_ExecutesAtTheSecondTargetTurnStart()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryScheduleTeleport(ref state, 1, 3, 3, 2), Is.True);

            GameRules.EndTurn(ref state); // Player 1's first TurnStart.
            Assert.That(state.p1X, Is.EqualTo(4));
            Assert.That(state.p1Y, Is.EqualTo(7));
            Assert.That(WorldEffectSystem.TryGet(
                ref state, 0, out var countingDown), Is.True);
            Assert.That(countingDown.RemainingTurns, Is.EqualTo(1));

            GameRules.EndTurn(ref state); // Player 0's TurnStart.
            Assert.That(state.p1X, Is.EqualTo(4));
            Assert.That(state.p1Y, Is.EqualTo(7));

            GameRules.EndTurn(ref state); // Player 1's second TurnStart.
            Assert.That(state.p1X, Is.EqualTo(3));
            Assert.That(state.p1Y, Is.EqualTo(3));
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void DelayedTeleport_WhenDestinationBecomesBlocked_IsCancelledAndRemoved()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryScheduleTeleport(ref state, 1, 3, 3, 2), Is.True);
            state.obstacles |= 1UL << (3 * GameRules.Size + 3);

            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state);

            Assert.That(state.p1X, Is.EqualTo(4));
            Assert.That(state.p1Y, Is.EqualTo(7));
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void DelayedTeleport_WhenDestinationIsOccupied_IsCancelledAndRemoved()
        {
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 3;
            Assert.That(WorldEffectSystem.TryScheduleTeleport(ref state, 1, 3, 3, 2), Is.True);

            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state);

            Assert.That(state.p1X, Is.EqualTo(4));
            Assert.That(state.p1Y, Is.EqualTo(7));
            Assert.That(WorldEffectSystem.Count(ref state), Is.Zero);
        }

        [Test]
        public void WorldEffects_RejectRegistrationAfterCapacityWithoutChangingExistingState()
        {
            var state = NewState();
            for (int i = 0; i < WorldEffectSystem.MaxActiveEffects; i++)
            {
                Assert.That(WorldEffectSystem.TryScheduleTeleport(
                    ref state, i % 2, 3, 2, 2), Is.True);
            }

            int countBefore = WorldEffectSystem.Count(ref state);
            ushort sequenceBefore = state.nextWorldEffectSequence;

            Assert.That(WorldEffectSystem.TryScheduleTeleport(ref state, 0, 3, 2, 2), Is.False);
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(countBefore));
            Assert.That(state.nextWorldEffectSequence, Is.EqualTo(sequenceBefore));
        }

        [Test]
        public void TurnEndEffectsAreResolvedBeforeFinalTurnJudgement()
        {
            GameRules.MaxTurns = 1;
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 1;
            state.p0Hp = 10;
            state.p1Hp = 9;

            Assert.That(WorldEffectSystem.TryAddFireZone(ref state, 1, 3, 1, 2, 1), Is.True);
            GameRules.EndTurn(ref state);
            Assert.That(state.p0Hp, Is.EqualTo(8));
            GameRules.EndTurn(ref state);   // 라운드는 후공까지 끝나야 닫힌다

            Assert.That(state.p0Hp, Is.EqualTo(8));
            Assert.That(state.winner, Is.EqualTo(2));   // 제한 라운드 도달: 체력이 높은 쪽 승
        }

        [Test]
        public void FrostZone_DoesNotLoseItsFirstTurnAtTheCasterTurnEnd()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryAddFrostZone(ref state, 0, 3, 2, 1), Is.True);

            GameRules.EndTurn(ref state); // Caster turn end: still present.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 3, 2, out var active), Is.True);
            Assert.That(active.RemainingTurns, Is.EqualTo(1));

            GameRules.EndTurn(ref state); // Next relevant boundary: expires.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 3, 2, out _), Is.False);
        }

        [Test]
        public void MapWall_HasThreeHp_AndIsRemovedAtZero()
        {
            var state = NewState();
            state.obstacles = 1UL << (2 * GameRules.Size + 2);
            state.obstacleHp[2 * GameRules.Size + 2] = 3;

            GameRules.DamageAt(ref state, 2, 2, 2);
            Assert.That(GameRules.IsMapObstacle(ref state, 2, 2), Is.True);
            Assert.That(GameRules.MapObstacleHp(ref state, 2, 2), Is.EqualTo(1));

            GameRules.DamageAt(ref state, 2, 2, 1);
            Assert.That(GameRules.IsMapObstacle(ref state, 2, 2), Is.False);
            Assert.That(GameRules.MapObstacleHp(ref state, 2, 2), Is.Zero);
        }

        [Test]
        public void AreaHazard_StoresWallTile_ButContinuesAcrossTheArea()
        {
            var state = NewState();
            state.obstacles = 1UL << (2 * GameRules.Size + 2);
            state.obstacleHp[2 * GameRules.Size + 2] = 3;
            var runtime = new CardEffectRuntime(state);

            runtime.FireExplosion(0, 3, 2, 1);
            state = runtime.State;

            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 2, 2, out _), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 3, 2, out _), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 4, 2, out _), Is.True);
            Assert.That(GameRules.MapObstacleHp(ref state, 2, 2), Is.EqualTo(2));
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);
        }

        [Test]
        public void IceAge_StoresFrostOnWallTile_ButContinuesAcrossTheArea()
        {
            var state = NewState();
            state.obstacles = 1UL << (2 * GameRules.Size + 2);
            state.obstacleHp[2 * GameRules.Size + 2] = 3;
            var runtime = new CardEffectRuntime(state);

            runtime.IceAge(0, 3, 2);
            state = runtime.State;

            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 2, 2, out _), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 4, 2, out _), Is.True);
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);
        }

        [Test]
        public void HazardOnWall_TicksDownAndExpiresWithoutOpeningTheTile()
        {
            var state = NewState();
            state.obstacles = 1UL << (2 * GameRules.Size + 2);
            state.obstacleHp[2 * GameRules.Size + 2] = 3;

            Assert.That(WorldEffectSystem.TryAddFrostZone(ref state, 0, 2, 2, 2), Is.True);
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 2, 2, out var placed), Is.True);
            Assert.That(placed.RemainingTurns, Is.EqualTo(2));
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);

            GameRules.EndTurn(ref state); // Placement turn: duration is preserved.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 2, 2, out placed), Is.True);
            Assert.That(placed.RemainingTurns, Is.EqualTo(2));

            GameRules.EndTurn(ref state); // 라운드가 닫히는 경계: 첫 틱.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 2, 2, out placed), Is.True);
            Assert.That(placed.RemainingTurns, Is.EqualTo(1));

            GameRules.EndTurn(ref state); // 라운드 중간이라 그대로.
            GameRules.EndTurn(ref state); // 다음 라운드 경계: 소멸.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FrostZone, 2, 2, out _), Is.False);
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);
        }

        [Test]
        public void FireZoneOnWall_TicksDownAndExpiresWithoutOpeningTheTile()
        {
            var state = NewState();
            state.obstacles = 1UL << (2 * GameRules.Size + 2);
            state.obstacleHp[2 * GameRules.Size + 2] = 3;

            Assert.That(WorldEffectSystem.TryAddFireZone(
                ref state, 0, 2, 2, 1, 2), Is.True);

            GameRules.EndTurn(ref state); // 시전자 턴 종료는 틱이 아니다.
            GameRules.EndTurn(ref state); // 상대 턴 종료: 첫 틱, 벽이 탄다.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 2, 2, out var active), Is.True);
            Assert.That(active.RemainingTurns, Is.EqualTo(1));
            Assert.That(GameRules.MapObstacleHp(ref state, 2, 2), Is.EqualTo(2));
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);

            GameRules.EndTurn(ref state);
            GameRules.EndTurn(ref state); // 두 번째 틱: 소멸.
            Assert.That(WorldEffectSystem.TryGetTileEffect(
                ref state, WorldEffectKind.FireZone, 2, 2, out _), Is.False);
            Assert.That(GameRules.MapObstacleHp(ref state, 2, 2), Is.EqualTo(1));
            Assert.That(GameRules.IsBlocked(ref state, 2, 2), Is.True);
        }

        [Test]
        public void TotemStructure_UsesHpAndSurvivesThePlacementTurnEnd()
        {
            var state = NewState();
            Assert.That(WorldEffectSystem.TryAddStructure(
                ref state, 0, StructureKind.Detonation, 2, 2, 3, 2), Is.True);
            Assert.That(WorldEffectSystem.TryGetStructureAt(
                ref state, 2, 2, out var structure), Is.True);
            Assert.That(structure.Power, Is.EqualTo(3));
            Assert.That(structure.RemainingTurns, Is.EqualTo(2));

            GameRules.EndTurn(ref state);
            Assert.That(WorldEffectSystem.TryGetStructureAt(
                ref state, 2, 2, out structure), Is.True);
            Assert.That(structure.RemainingTurns, Is.EqualTo(2));
        }

        [Test]
        public void Detonation_DamagesOnlyTheEnemyOnceAndLeavesStructuresUntouched()
        {
            var state = NewState();
            state.p0X = 2;
            state.p0Y = 1;
            state.p1X = 2;
            state.p1Y = 3;
            state.p0Hp = GameRules.MaxHp;
            state.p1Hp = GameRules.MaxHp;

            Assert.That(WorldEffectSystem.TryAddStructure(
                ref state, 0, StructureKind.Guardian, 1, 2, 2, 2), Is.True);
            Assert.That(WorldEffectSystem.TryAddStructure(
                ref state, 0, StructureKind.Detonation, 2, 2, 3, 2), Is.True);

            // The attacker/source argument must not replace the bomb owner's
            // ownership when the detonation is triggered.
            Assert.That(WorldEffectSystem.DamageStructureAt(ref state, 2, 2, 3), Is.True);

            Assert.That(state.p0Hp, Is.EqualTo(GameRules.MaxHp));
            Assert.That(state.p1Hp, Is.EqualTo(GameRules.MaxHp - 16));
            Assert.That(WorldEffectSystem.TryGetStructureAt(
                ref state, 1, 2, out var guardian), Is.True);
            Assert.That(guardian.Power, Is.EqualTo(2));
            Assert.That(WorldEffectSystem.TryGetStructureAt(
                ref state, 2, 2, out _), Is.False);
        }

        [Test]
        public void WorldEffectResolution_JudgesWinnerAsSoonAsHpHitsZero()
        {
            var state = NewState();
            state.p0X = 3;
            state.p0Y = 1;
            state.p0Hp = 1;

            Assert.That(WorldEffectSystem.TryAddFireZone(
                ref state, 1, 3, 1, 1, 1), Is.True);

            WorldEffectSystem.ResolveTurnEnd(ref state, 0);

            Assert.That(state.p0Hp, Is.Zero);
            Assert.That(state.winner, Is.EqualTo(2));
        }

        [Test]
        public void IceWall_StillPlacesItsCenter_WhenEffectSlotsCannotHoldAllThreeTiles()
        {
            var state = NewState();
            state.obstacles = 0;
            // 양옆까지 놓을 자리는 없고 한 칸만 남은 상태.
            for (int i = 0; i < WorldEffectSystem.MaxActiveEffects - 1; i++)
                Assert.That(WorldEffectSystem.TryScheduleTeleport(ref state, i % 2, 3, 2, 2), Is.True);

            Assert.That(WorldEffectSystem.TryAddIceWall(ref state, 0, 5, 3), Is.True);
            Assert.That(WorldEffectSystem.IsStructureAt(ref state, 5, 3), Is.True);
            Assert.That(WorldEffectSystem.Count(ref state), Is.EqualTo(WorldEffectSystem.MaxActiveEffects));
        }

        static GameState NewState() => GameRules.NewGame(0x2468ACE1u);
    }
}
