using Unity.Collections;

namespace SpellThrower
{
    /// Server-authoritative turn and map effects. Hazard tiles and structures
    /// share the same serialized list so clients see exactly the state the
    /// server resolves.
    public static class WorldEffectSystem
    {
        public const byte AnyPlayer = byte.MaxValue;
        public const int MaxActiveEffects = 32;

        // 턴 종료 피해·회복은 토템마다 다르다. 카드 정보2 DB가 원본이다.
        const int TotemAttackDamage = 8;
        const int GuardianAttackDamage = 6;
        const int ThornAttackDamage = 12;
        const int TotemAttackRange = 2;
        const int GuardianAttackRange = 1;
        const int ThornAttackRange = 1;
        const int BlessingHeal = 14;
        const int DetonationDamage = 16;
        const byte IceWallHp = 20;

        public static ref FixedList512Bytes<WorldEffectRecord> Effects(ref GameState state) => ref state.worldEffects;

        public static int Count(ref GameState state) => Effects(ref state).Length;

        public static bool TryGet(ref GameState state, int index, out WorldEffectRecord effect)
        {
            ref var effects = ref Effects(ref state);
            if (index < 0 || index >= effects.Length)
            {
                effect = default;
                return false;
            }

            effect = effects[index];
            return true;
        }

        public static bool TryGetTileEffect(
            ref GameState state,
            WorldEffectKind kind,
            int x,
            int y,
            out WorldEffectRecord effect)
        {
            ref var effects = ref Effects(ref state);
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].Kind == kind && effects[i].X == x && effects[i].Y == y)
                {
                    effect = effects[i];
                    return true;
                }
            }

            effect = default;
            return false;
        }

        public static bool IsFrosted(ref GameState state, int x, int y)
        {
            return TryGetTileEffect(ref state, WorldEffectKind.FrostZone, x, y, out _);
        }

        public static bool IsStructureAt(ref GameState state, int x, int y)
        {
            ref var effects = ref Effects(ref state);
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].Kind == WorldEffectKind.Structure &&
                    effects[i].X == x && effects[i].Y == y)
                    return true;
            }

            return false;
        }

        public static bool TryGetStructureAt(ref GameState state, int x, int y, out WorldEffectRecord structure)
        {
            ref var effects = ref Effects(ref state);
            int found = -1;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].Kind == WorldEffectKind.Structure &&
                    effects[i].X == x && effects[i].Y == y)
                    if (found < 0 || IsSequenceBefore(effects[i].Sequence, effects[found].Sequence) ||
                        (effects[i].Sequence == effects[found].Sequence && i < found))
                        found = i;
            }

            if (found >= 0)
            {
                structure = effects[found];
                return true;
            }

            structure = default;
            return false;
        }

        public static bool TryAddFireZone(
            ref GameState state,
            int sourcePlayer,
            int x,
            int y,
            byte power = 2,
            byte ticks = 2)
        {
            if (!IsPlayer(sourcePlayer) || !GameRules.InBounds(x, y))
                return false;
            if (power == 0 || ticks == 0)
                return false;

            // 불길은 '상대 턴 종료 2회'다. 시전자 턴이 끝날 때는 타지도, 횟수가 줄지도 않는다.
            var effect = new WorldEffectRecord(
                WorldEffectKind.FireZone,
                WorldEffectPhase.TurnEnd,
                (byte)sourcePlayer,
                (byte)(1 - sourcePlayer),
                AnyPlayer,
                (byte)x,
                (byte)y,
                ticks,
                power);
            return TryAdd(ref state, effect, refreshSameTile: true);
        }

        public static bool TryScheduleTeleport(
            ref GameState state,
            int targetPlayer,
            int x,
            int y,
            byte delayTurns = 2)
        {
            if (!IsPlayer(targetPlayer) || !GameRules.InBounds(x, y) || delayTurns == 0)
                return false;

            var effect = new WorldEffectRecord(
                WorldEffectKind.DelayedTeleport,
                WorldEffectPhase.TurnStart,
                (byte)targetPlayer,
                (byte)targetPlayer,
                (byte)targetPlayer,
                (byte)x,
                (byte)y,
                delayTurns,
                0);
            return TryAdd(ref state, effect);
        }

        public static bool TryAddFrostZone(
            ref GameState state,
            int sourcePlayer,
            int x,
            int y,
            byte turns = 1)
        {
            if (!IsPlayer(sourcePlayer) || !GameRules.InBounds(x, y))
                return false;
            if (turns == 0) return false;

            var effect = new WorldEffectRecord(
                WorldEffectKind.FrostZone,
                WorldEffectPhase.TurnEnd,
                (byte)sourcePlayer,
                AnyPlayer,
                AnyPlayer,
                (byte)x,
                (byte)y,
                turns,
                0);
            return TryAdd(ref state, effect, refreshSameTile: true);
        }

        public static bool TryAddIceWall(
            ref GameState state,
            int sourcePlayer,
            int centerX,
            int centerY,
            bool horizontal = true,
            byte hp = IceWallHp)
        {
            if (!IsPlayer(sourcePlayer) || !GameRules.InBounds(centerX, centerY) ||
                GameRules.IsBlocked(ref state, centerX, centerY) ||
                GameRules.IsOccupied(ref state, centerX, centerY))
                return false;

            int dx = horizontal ? 1 : 0;
            int dy = horizontal ? 0 : 1;

            // 세 칸을 한꺼번에 잡을 수 있을 때만 놓으면, 판에 장판이 30칸쯤 깔린 순간
            // 카드만 소모되고 방벽이 통째로 안 생긴다. 가운데부터 한 칸씩 놓아
            // 자리가 모자라면 양옆만 빠지게 한다. 점유·장애물 칸을 건너뛰는 것과 같은 규칙이다.
            bool placed = AddIceWallTile(ref state, sourcePlayer, centerX, centerY, horizontal, hp);
            for (int offset = -1; offset <= 1; offset += 2)
            {
                int x = centerX + dx * offset;
                int y = centerY + dy * offset;
                if (!GameRules.InBounds(x, y) ||
                    GameRules.IsBlocked(ref state, x, y) ||
                    GameRules.IsOccupied(ref state, x, y))
                    continue;
                AddIceWallTile(ref state, sourcePlayer, x, y, horizontal, hp);
            }

            return placed;
        }

        static bool AddIceWallTile(ref GameState state, int sourcePlayer, int x, int y, bool horizontal, byte hp)
        {
            var wall = new WorldEffectRecord(
                WorldEffectKind.Structure,
                WorldEffectPhase.TurnEnd,
                (byte)sourcePlayer,
                AnyPlayer,
                AnyPlayer,
                (byte)x,
                (byte)y,
                2,
                hp);
            wall.Structure = StructureKind.IceWall;
            wall.Aux = (byte)(horizontal ? 1 : 0);
            return TryAdd(ref state, wall);
        }

        public static bool TryAddStructure(
            ref GameState state,
            int sourcePlayer,
            StructureKind structureKind,
            int x,
            int y,
            byte hp,
            byte duration = 0,
            byte aux = 0)
        {
            if (!IsPlayer(sourcePlayer) || structureKind == StructureKind.None ||
                !GameRules.InBounds(x, y) || GameRules.IsBlocked(ref state, x, y) ||
                GameRules.IsOccupied(ref state, x, y) || hp == 0)
                return false;

            var structure = new WorldEffectRecord(
                WorldEffectKind.Structure,
                WorldEffectPhase.TurnEnd,
                (byte)sourcePlayer,
                AnyPlayer,
                AnyPlayer,
                (byte)x,
                (byte)y,
                duration,
                hp);
            structure.Structure = structureKind;
            structure.Aux = aux;
            return TryAdd(ref state, structure);
        }

        public static bool TryAdd(
            ref GameState state,
            WorldEffectRecord effect,
            bool refreshSameTile = false)
        {
            if (!IsValid(effect)) return false;

            if (GetCreatedTurn(effect) == 0)
                SetCreatedTurn(ref effect, state.turnCount == 0 ? (byte)1 : state.turnCount);

            ref var effects = ref Effects(ref state);
            if (refreshSameTile && (effect.Kind == WorldEffectKind.FireZone ||
                                    effect.Kind == WorldEffectKind.FrostZone))
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i].Kind != effect.Kind || effects[i].X != effect.X || effects[i].Y != effect.Y)
                        continue;

                    effect.Sequence = effects[i].Sequence;
                    effects[i] = effect;
                    return true;
                }
            }

            if (effects.Length >= MaxActiveEffects || effects.Length >= effects.Capacity)
                return false;

            effect.Sequence = NextSequence(ref state);
            effects.Add(effect);
            return true;
        }

        public static bool DamageStructureAt(ref GameState state, int x, int y, int amount, int sourcePlayer = -1)
        {
            if (amount <= 0) return false;

            ref var effects = ref Effects(ref state);
            int hitIndex = -1;
            for (int i = 0; i < effects.Length; i++)
            {
                var structure = effects[i];
                if (structure.Kind != WorldEffectKind.Structure || structure.X != x || structure.Y != y)
                    continue;

                // Placement normally guarantees one structure per tile. If a
                // legacy or hand-authored state contains more than one, the
                // oldest sequence is the deterministic hit target.
                if (hitIndex < 0 || IsSequenceBefore(structure.Sequence, effects[hitIndex].Sequence) ||
                    (structure.Sequence == effects[hitIndex].Sequence && i < hitIndex))
                    hitIndex = i;
            }

            if (hitIndex < 0)
                return false;

            var hit = effects[hitIndex];
            hit.Power = (byte)(hit.Power > amount ? hit.Power - amount : 0);
            if (hit.Power > 0)
            {
                effects[hitIndex] = hit;
                return true;
            }

            effects.RemoveAt(hitIndex);
            if (ShouldDetonateOnDestroy(hit.Structure))
            {
                // A damage source is only a fallback for malformed legacy
                // records. A valid bomb always uses its stored owner, so the
                // blast cannot be redirected by the card that hit it.
                int owner = IsPlayer(hit.SourcePlayer) ? hit.SourcePlayer : sourcePlayer;
                Detonate(ref state, x, y, DetonationDamage, owner);
            }
            return true;
        }

        public static void RemoveHazardsAround(ref GameState state, int centerX, int centerY, int radius = 1)
        {
            ref var effects = ref Effects(ref state);
            for (int i = effects.Length - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if ((effect.Kind != WorldEffectKind.FireZone && effect.Kind != WorldEffectKind.FrostZone) ||
                    GameRules.Dist(centerX, centerY, effect.X, effect.Y) > radius)
                    continue;
                effects.RemoveAt(i);
            }
        }

        public static void ResolveTurnStart(ref GameState state, int player)
        {
            if (!IsPlayer(player)) return;
            ResolveScheduled(ref state, player, WorldEffectPhase.TurnStart);

            ref var effects = ref Effects(ref state);
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect.Kind == WorldEffectKind.Structure &&
                    effect.SourcePlayer == player && effect.Structure == StructureKind.Blessing)
                    GameRules.HealPlayer(ref state, player, BlessingHeal);
            }
        }

        public static void ResolveTurnEnd(ref GameState state, int player)
        {
            if (!IsPlayer(player)) return;
            ResolveScheduled(ref state, player, WorldEffectPhase.TurnEnd);
            if (state.winner != 0) return;

            ResolveStructureTurnEnd(ref state, player);
            // 지속 '2턴'은 라운드 단위다. 후공까지 두고 나야 한 칸 줄어든다.
            if (GameRules.IsRoundEnd(state.turnCount)) TickDurations(ref state);
        }

        static void ResolveScheduled(ref GameState state, int player, WorldEffectPhase phase)
        {
            ref var effects = ref Effects(ref state);
            int index = 0;
            while (index < effects.Length)
            {
                var effect = effects[index];
                if (effect.Kind == WorldEffectKind.Structure || effect.Phase != phase ||
                    (effect.TriggerPlayer != player && effect.TriggerPlayer != AnyPlayer))
                {
                    index++;
                    continue;
                }

                switch (effect.Kind)
                {
                    case WorldEffectKind.FireZone:
                        ApplyFireZone(ref state, index, effect, player);
                        break;
                    case WorldEffectKind.FrostZone:
                        ApplyHazardTick(ref state, index, effect);
                        break;
                    case WorldEffectKind.DelayedTeleport:
                        ApplyDelayedTeleport(ref state, index, effect, player);
                        break;
                    default:
                        index++;
                        break;
                }

                if (index < effects.Length && effects[index].Sequence == effect.Sequence)
                    index++;
            }
        }

        static void ApplyFireZone(ref GameState state, int index, WorldEffectRecord effect, int player)
        {
            // 장판은 상대 턴이 끝날 때마다 그 칸에 있는 것을 태운다.
            // DamageAt 이 빈 칸이면 알아서 아무것도 하지 않는다.
            GameRules.DamageAt(ref state, effect.X, effect.Y, effect.Power, effect.SourcePlayer);

            effect.RemainingTurns--;
            if (effect.RemainingTurns == 0) Effects(ref state).RemoveAt(index);
            else Effects(ref state)[index] = effect;
        }

        static void ApplyHazardTick(ref GameState state, int index, WorldEffectRecord effect)
        {
            // A newly placed frost tile must survive the caster's current turn
            // end. It starts counting down at the next relevant boundary.
            if (WasCreatedThisTurn(ref state, effect)) return;
            // 서리 '1턴/2턴'도 라운드 단위다.
            if (!GameRules.IsRoundEnd(state.turnCount)) return;
            effect.RemainingTurns--;
            if (effect.RemainingTurns == 0) Effects(ref state).RemoveAt(index);
            else Effects(ref state)[index] = effect;
        }

        static void ApplyDelayedTeleport(ref GameState state, int index, WorldEffectRecord effect, int player)
        {
            if (player != effect.TriggerPlayer) return;

            if (effect.RemainingTurns > 1)
            {
                effect.RemainingTurns--;
                Effects(ref state)[index] = effect;
                return;
            }

            bool destinationValid =
                GameRules.InBounds(effect.X, effect.Y) &&
                !GameRules.IsBlocked(ref state, effect.X, effect.Y) &&
                !IsOccupiedByOtherPlayer(ref state, effect.TargetPlayer, effect.X, effect.Y);

            if (destinationValid)
            {
                GameRules.X(ref state, effect.TargetPlayer) = effect.X;
                GameRules.Y(ref state, effect.TargetPlayer) = effect.Y;
            }

            Effects(ref state).RemoveAt(index);
        }

        static void ResolveStructureTurnEnd(ref GameState state, int player)
        {
            ref var effects = ref Effects(ref state);
            ulong resolved = 0;
            while (true)
            {
                int index = FindNextStructureTurnEnd(ref state, player, resolved);
                if (index < 0) return;

                resolved |= 1UL << index;
                var structure = effects[index];
                int attackRange;
                int attackDamage;
                bool enemyTurnTrigger = false;

                switch (structure.Structure)
                {
                    case StructureKind.Totem:
                    case StructureKind.Detonation:
                        // 저격/폭탄: 소유자의 턴 종료에 맨해튼 2칸 안의
                        // 가장 가까운 적 하나를 공격한다.
                        if (structure.SourcePlayer != player) continue;
                        attackRange = TotemAttackRange;
                        attackDamage = TotemAttackDamage;
                        break;

                    case StructureKind.Guardian:
                        // 경계: 소유자의 턴 종료에 맨해튼 1칸 안을 감시한다.
                        if (structure.SourcePlayer != player) continue;
                        attackRange = GuardianAttackRange;
                        attackDamage = GuardianAttackDamage;
                        break;

                    case StructureKind.Thorn:
                        // 감지: 적이 자기 턴을 끝냈을 때 인접해 있으면 공격한다.
                        if (structure.SourcePlayer == player) continue;
                        attackRange = ThornAttackRange;
                        attackDamage = ThornAttackDamage;
                        enemyTurnTrigger = true;
                        break;

                    default:
                        // 축복은 TurnStart, 얼음 방벽은 지속/점유만 담당한다.
                        continue;
                }

                int target = FindNearestEnemy(
                    ref state, structure.SourcePlayer, structure.X, structure.Y, attackRange);
                if (target < 0 || (enemyTurnTrigger && target != player)) continue;
                GameRules.DamagePlayer(ref state, target, attackDamage);
            }
        }

        static int FindNextStructureTurnEnd(ref GameState state, int player, ulong resolved)
        {
            ref var effects = ref Effects(ref state);
            int found = -1;
            for (int i = 0; i < effects.Length; i++)
            {
                if ((resolved & (1UL << i)) != 0) continue;
                var effect = effects[i];
                if (effect.Kind != WorldEffectKind.Structure || effect.Power == 0) continue;

                if (found < 0 || IsSequenceBefore(effect.Sequence, effects[found].Sequence) ||
                    (effect.Sequence == effects[found].Sequence && i < found))
                    found = i;
            }

            return found;
        }

        static int FindNearestEnemy(
            ref GameState state,
            int sourcePlayer,
            int x,
            int y,
            int maxDistance)
        {
            if (!IsPlayer(sourcePlayer)) return -1;

            int nearest = -1;
            int nearestDistance = int.MaxValue;
            for (int candidate = 0; candidate < 2; candidate++)
            {
                if (candidate == sourcePlayer) continue;

                int distance = GameRules.Dist(
                    x, y, GameRules.X(ref state, candidate), GameRules.Y(ref state, candidate));
                if (distance > maxDistance) continue;
                if (nearest < 0 || distance < nearestDistance ||
                    (distance == nearestDistance && candidate < nearest))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        static void TickDurations(ref GameState state)
        {
            ref var effects = ref Effects(ref state);
            for (int i = effects.Length - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if (effect.Kind != WorldEffectKind.Structure || effect.RemainingTurns == 0)
                    continue;
                if (WasCreatedThisTurn(ref state, effect))
                    continue;
                if (effect.RemainingTurns <= 1) effects.RemoveAt(i);
                else
                {
                    effect.RemainingTurns--;
                    effects[i] = effect;
                }
            }
        }

        static void Detonate(ref GameState state, int centerX, int centerY, int damage, int sourcePlayer)
        {
            // Detonation is described as enemy damage, not generic tile damage.
            // Use the bomb owner's stored source so collateral cannot hit the
            // owner, structures, or map walls through GameRules.DamageAt.
            DamageEnemyAt(ref state, centerX, centerY, damage, sourcePlayer);
            DamageEnemyAt(ref state, centerX - 1, centerY, damage, sourcePlayer);
            DamageEnemyAt(ref state, centerX + 1, centerY, damage, sourcePlayer);
            DamageEnemyAt(ref state, centerX, centerY - 1, damage, sourcePlayer);
            DamageEnemyAt(ref state, centerX, centerY + 1, damage, sourcePlayer);
        }

        static bool ShouldDetonateOnDestroy(StructureKind structure)
        {
            return structure == StructureKind.Detonation;
        }

        static void DamageEnemyAt(ref GameState state, int x, int y, int amount, int sourcePlayer)
        {
            if (!IsPlayer(sourcePlayer) || !GameRules.InBounds(x, y) || amount <= 0)
                return;

            int enemy = 1 - sourcePlayer;
            if (GameRules.X(ref state, enemy) == x && GameRules.Y(ref state, enemy) == y)
                GameRules.DamagePlayer(ref state, enemy, amount);
        }

        static bool IsOccupiedByOtherPlayer(ref GameState state, int player, int x, int y)
        {
            int other = 1 - player;
            return GameRules.X(ref state, other) == x && GameRules.Y(ref state, other) == y;
        }

        static ushort NextSequence(ref GameState state)
        {
            state.nextWorldEffectSequence++;
            if (state.nextWorldEffectSequence == 0) state.nextWorldEffectSequence = 1;
            return state.nextWorldEffectSequence;
        }

        static bool IsSequenceBefore(ushort candidate, ushort current)
        {
            if (candidate == current) return false;
            if (candidate == 0) return current != 0;
            if (current == 0) return false;

            // Active effects are bounded to a small window, so modular
            // comparison remains deterministic across the ushort wrap.
            return (ushort)(current - candidate) < 0x8000;
        }

        static bool WasCreatedThisTurn(ref GameState state, WorldEffectRecord effect)
        {
            // NewGame starts at turn 1, but hand-authored/unit-test states can
            // still use the zero default. TryAdd normalizes that state to the
            // first turn, so resolution must use the same normalized value.
            byte currentTurn = state.turnCount == 0 ? (byte)1 : state.turnCount;
            return GetCreatedTurn(effect) == currentTurn;
        }

        // Aux is part of the existing wire contract. Its low bit stores the
        // ice-wall orientation; the remaining bits store the creation turn so
        // adding duration bookkeeping does not reduce FixedList capacity.
        static byte GetCreatedTurn(WorldEffectRecord effect)
        {
            return effect.Kind == WorldEffectKind.Structure && effect.Structure == StructureKind.IceWall
                ? (byte)(effect.Aux >> 1)
                : effect.Aux;
        }

        static void SetCreatedTurn(ref WorldEffectRecord effect, byte turn)
        {
            if (effect.Kind == WorldEffectKind.Structure && effect.Structure == StructureKind.IceWall)
                effect.Aux = (byte)((effect.Aux & 1) | (turn << 1));
            else
                effect.Aux = turn;
        }

        static bool IsValid(WorldEffectRecord effect)
        {
            if (!GameRules.InBounds(effect.X, effect.Y) || effect.Kind == WorldEffectKind.Structure &&
                effect.Structure == StructureKind.None)
                return false;
            if (effect.Kind != WorldEffectKind.Structure && effect.RemainingTurns == 0)
                return false;
            if (effect.Kind == WorldEffectKind.DelayedTeleport && !IsPlayer(effect.TargetPlayer))
                return false;
            return effect.Kind == WorldEffectKind.FireZone ||
                   effect.Kind == WorldEffectKind.FrostZone ||
                   effect.Kind == WorldEffectKind.Structure ||
                   effect.Kind == WorldEffectKind.DelayedTeleport;
        }

        static bool IsPlayer(int player) => player == 0 || player == 1;
    }
}
