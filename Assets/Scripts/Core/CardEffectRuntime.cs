namespace SpellThrower
{
    /// Mutable card-effect facade. It owns a value copy of GameState while a
    /// card resolves, allowing CardDef implementations to stay independent of
    /// the server transport and still perform compound area effects.
    public sealed class CardEffectRuntime : IWorldEffectSink
    {
        // 공통 바람 규칙과 오의 ： 익스플로전의 예외 틱. 카드 정보2 DB가 원본이다.
        const int WindHitDamage = 6;
        const int WindCollisionDamage = 12;
        const byte ExplosionFireTick = 8;
        const int MasterSparkStackStep = 6;

        public GameState State;

        public CardEffectRuntime(GameState state)
        {
            State = state;
        }

        public bool TryAddFireZone(int sourcePlayer, int x, int y, byte power = 2, byte ticks = 2)
        {
            return WorldEffectSystem.TryAddFireZone(ref State, sourcePlayer, x, y, power, ticks);
        }

        public bool TryScheduleTeleport(int targetPlayer, int x, int y, byte delayTurns = 2)
        {
            return WorldEffectSystem.TryScheduleTeleport(ref State, targetPlayer, x, y, delayTurns);
        }

        public void DamageAt(int x, int y, int amount, int sourcePlayer = -1, bool damageMapObstacle = true)
        {
            GameRules.DamageAt(ref State, x, y, amount, sourcePlayer, damageMapObstacle);
        }

        public void FireRain(int sourcePlayer, int centerX, int centerY, int damage)
        {
            DamageAt(centerX, centerY, damage, sourcePlayer);
            DamageFireRainAdjacent(centerX - 1, centerY, damage, sourcePlayer);
            DamageFireRainAdjacent(centerX + 1, centerY, damage, sourcePlayer);
            DamageFireRainAdjacent(centerX, centerY - 1, damage, sourcePlayer);
            DamageFireRainAdjacent(centerX, centerY + 1, damage, sourcePlayer);
            TryAddFireZone(sourcePlayer, centerX, centerY, GameRules.FireZoneTick, 2);
        }

        void DamageFireRainAdjacent(int x, int y, int damage, int sourcePlayer)
        {
            // FireRain excludes every adjacent obstacle cell, including
            // dynamic structures. DamageAt's map-wall flag alone is not
            // sufficient because it still damages structures.
            if (!GameRules.InBounds(x, y) || GameRules.IsBlocked(ref State, x, y)) return;
            DamageAt(x, y, damage, sourcePlayer, false);
        }

        public void FireExplosion(int sourcePlayer, int centerX, int centerY, int damage)
        {
            for (int y = centerY - 1; y <= centerY + 1; y++)
            {
                for (int x = centerX - 1; x <= centerX + 1; x++)
                {
                    if (!GameRules.InBounds(x, y)) continue;
                    DamageAt(x, y, damage, sourcePlayer);
                    TryAddFireZone(sourcePlayer, x, y, ExplosionFireTick, 2);
                }
            }
        }

        public void IceAge(int sourcePlayer, int centerX, int centerY, int damage = 18)
        {
            DamageAt(centerX, centerY, damage, sourcePlayer);
            for (int y = centerY - 1; y <= centerY + 1; y++)
            {
                for (int x = centerX - 1; x <= centerX + 1; x++)
                {
                    if (!GameRules.InBounds(x, y)) continue;
                    TryAddFrostZone(sourcePlayer, x, y, 2);
                }
            }
        }

        public bool TryAddFrostZone(int sourcePlayer, int x, int y, int turns)
        {
            if (turns <= 0 || turns > byte.MaxValue) return false;
            return WorldEffectSystem.TryAddFrostZone(ref State, sourcePlayer, x, y, (byte)turns);
        }

        public bool TryAddIceWall(int sourcePlayer, int centerX, int centerY, byte hp)
        {
            if ((sourcePlayer != 0 && sourcePlayer != 1) ||
                !GameRules.InBounds(centerX, centerY)) return false;

            int dx = centerX - GameRules.X(ref State, sourcePlayer);
            int dy = centerY - GameRules.Y(ref State, sourcePlayer);
            bool horizontal = GameRules.AbsValue(dx) >= GameRules.AbsValue(dy);
            return WorldEffectSystem.TryAddIceWall(ref State, sourcePlayer, centerX, centerY, horizontal, hp);
        }

        public void WindPush(int sourcePlayer, int targetPlayer, int tiles, bool recoil)
        {
            WindMove(sourcePlayer, targetPlayer, tiles, false, recoil);
        }

        public void WindPull(int sourcePlayer, int targetPlayer, int tiles)
        {
            WindMove(sourcePlayer, targetPlayer, tiles, true, false);
        }

        public void ResolveDuel(int sourcePlayer)
        {
            if (sourcePlayer != 0 && sourcePlayer != 1) return;

            int targetPlayer = 1 - sourcePlayer;
            int instigatorPower = GameRules.HandImmediateDamagePower(ref State, sourcePlayer);
            int targetPower = GameRules.HandImmediateDamagePower(ref State, targetPlayer);
            if (instigatorPower > targetPower)
                GameRules.DamagePlayer(ref State, targetPlayer, instigatorPower / 2);
            else if (targetPower > instigatorPower)
                GameRules.DamagePlayer(ref State, sourcePlayer, targetPower / 2);
        }

        void WindMove(int sourcePlayer, int targetPlayer, int tiles, bool pull, bool recoil)
        {
            int sourceX = GameRules.X(ref State, sourcePlayer);
            int sourceY = GameRules.Y(ref State, sourcePlayer);
            int targetX = GameRules.X(ref State, targetPlayer);
            int targetY = GameRules.Y(ref State, targetPlayer);
            int dx = targetX - sourceX;
            int dy = targetY - sourceY;
            int axisX = 0, axisY = 0;
            // 정대각선이면 대각으로 민다. 축 하나만 고르면 대각으로 맞은 상대가 옆으로 밀린다.
            if (GameRules.AbsValue(dx) == GameRules.AbsValue(dy))
            {
                axisX = GameRules.SignValue(dx);
                axisY = GameRules.SignValue(dy);
            }
            else if (GameRules.AbsValue(dx) > GameRules.AbsValue(dy)) axisX = GameRules.SignValue(dx);
            else axisY = GameRules.SignValue(dy);
            if (pull) { axisX = -axisX; axisY = -axisY; }

            bool frostHit = WorldEffectSystem.IsFrosted(ref State, targetX, targetY);
            bool collision = false;
            int[] pathX = new int[tiles + 1];
            int[] pathY = new int[tiles + 1];
            int pathLength = 1;
            pathX[0] = targetX;
            pathY[0] = targetY;
            int firstFire = WorldEffectSystem.TryGetTileEffect(
                ref State, WorldEffectKind.FireZone, targetX, targetY, out _) ? 0 : -1;
            for (int step = 0; step < tiles; step++)
            {
                int nextX = targetX + axisX;
                int nextY = targetY + axisY;
                if (!GameRules.InBounds(nextX, nextY)) break;

                if (GameRules.IsBlocked(ref State, nextX, nextY) ||
                    (nextX == sourceX && nextY == sourceY))
                {
                    GameRules.DamagePlayer(ref State, targetPlayer, WindCollisionDamage);
                    WorldEffectSystem.DamageStructureAt(ref State, nextX, nextY, WindCollisionDamage, sourcePlayer);
                    GameRules.DamageMapObstacleAt(ref State, nextX, nextY, WindCollisionDamage);
                    collision = true;
                    break;
                }

                targetX = nextX;
                targetY = nextY;
                pathX[pathLength] = targetX;
                pathY[pathLength] = targetY;
                pathLength++;

                if (firstFire < 0 && WorldEffectSystem.TryGetTileEffect(
                        ref State, WorldEffectKind.FireZone, targetX, targetY, out _))
                    firstFire = pathLength - 1;
            }

            GameRules.X(ref State, targetPlayer) = (byte)targetX;
            GameRules.Y(ref State, targetPlayer) = (byte)targetY;
            if (!collision)
                GameRules.DamagePlayer(ref State, targetPlayer, WindHitDamage);

            if (frostHit)
                GameRules.AddOrRefreshTag(ref State, targetPlayer, PlayerTagId.WindFrostSlow, 1, 1);

            if (firstFire >= 0)
            {
                for (int i = firstFire; i < pathLength; i++)
                    TryAddFireZone(sourcePlayer, pathX[i], pathY[i], GameRules.FireZoneTick, 1);
            }

            if (recoil)
            {
                int recoilX = sourceX - axisX;
                int recoilY = sourceY - axisY;
                if (GameRules.InBounds(recoilX, recoilY) &&
                    !GameRules.IsBlocked(ref State, recoilX, recoilY) &&
                    !(GameRules.X(ref State, targetPlayer) == recoilX && GameRules.Y(ref State, targetPlayer) == recoilY))
                {
                    GameRules.X(ref State, sourcePlayer) = (byte)recoilX;
                    GameRules.Y(ref State, sourcePlayer) = (byte)recoilY;
                }
            }
        }

        public void LightningStrike(int sourcePlayer, int x, int y, int basePower)
        {
            if (!GameRules.IsLightningHit(ref State, x, y)) return;
            int damage = GameRules.NextLightningDamage(ref State, sourcePlayer, basePower);
            DamageAt(x, y, damage, sourcePlayer);
        }

        public void LightningLine(int sourcePlayer, int targetX, int targetY, int basePower, bool stopsAtWall = false)
        {
            int sourceX = GameRules.X(ref State, sourcePlayer);
            int sourceY = GameRules.Y(ref State, sourcePlayer);
            int dx = targetX - sourceX;
            int dy = targetY - sourceY;
            int stepX = 0, stepY = 0;
            if (GameRules.AbsValue(dx) >= GameRules.AbsValue(dy)) stepX = GameRules.SignValue(dx);
            else stepY = GameRules.SignValue(dy);
            if (stepX == 0 && stepY == 0) return;

            bool hit = false;
            for (int i = 1; i <= 2; i++)
            {
                int x = sourceX + stepX * i;
                int y = sourceY + stepY * i;
                if (!GameRules.InBounds(x, y)) break;
                if (stopsAtWall && GameRules.IsBlocked(ref State, x, y))
                {
                    hit |= GameRules.IsLightningHit(ref State, x, y);
                    DamageAt(x, y, 0, sourcePlayer);
                    break;
                }
                if (GameRules.IsLightningHit(ref State, x, y)) hit = true;
            }

            if (!hit) return;
            int damage = GameRules.NextLightningDamage(ref State, sourcePlayer, basePower);
            for (int i = 1; i <= 2; i++)
            {
                int x = sourceX + stepX * i;
                int y = sourceY + stepY * i;
                if (!GameRules.InBounds(x, y)) break;
                if (stopsAtWall && GameRules.IsBlocked(ref State, x, y))
                {
                    DamageAt(x, y, damage, sourcePlayer);
                    break;
                }
                DamageAt(x, y, damage, sourcePlayer);
            }
        }

        public void MasterSpark(int sourcePlayer, int targetX, int targetY, int basePower)
        {
            int sourceX = GameRules.X(ref State, sourcePlayer);
            int sourceY = GameRules.Y(ref State, sourcePlayer);
            int stepX = 0, stepY = 0;
            if (targetX == sourceX) stepY = GameRules.SignValue(targetY - sourceY);
            else if (targetY == sourceY) stepX = GameRules.SignValue(targetX - sourceX);
            if (stepX == 0 && stepY == 0) return;

            int x = sourceX + stepX;
            int y = sourceY + stepY;
            bool hit = false;
            while (GameRules.InBounds(x, y))
            {
                if (GameRules.IsLightningHit(ref State, x, y)) hit = true;
                x += stepX;
                y += stepY;
            }
            if (!hit) return;

            int damage = GameRules.NextLightningDamage(
                ref State, sourcePlayer, basePower, MasterSparkStackStep);
            x = sourceX + stepX;
            y = sourceY + stepY;
            while (GameRules.InBounds(x, y))
            {
                DamageAt(x, y, damage, sourcePlayer);
                x += stepX;
                y += stepY;
            }
        }

        public void Purify(int player)
        {
            int centerX = GameRules.X(ref State, player);
            int centerY = GameRules.Y(ref State, player);
            WorldEffectSystem.RemoveHazardsAround(ref State, centerX, centerY, 1);
        }

        public void Divination(int player, int choiceIndex = 0)
        {
            GameRules.Divination(ref State, player, choiceIndex);
        }

        public void Exchange(int player, int discardIndex = 0)
        {
            GameRules.Exchange(ref State, player, discardIndex);
        }

        public void AddTag(int player, PlayerTagId tagId, byte duration, byte value = 0)
        {
            GameRules.AddOrRefreshTag(ref State, player, tagId, duration, value);
        }

        public bool TryMoveCard(int player, int x, int y, int baseDistance, bool leap)
        {
            return GameRules.TryCardMove(ref State, player, x, y, baseDistance, leap);
        }

        public void Charge(int player, int x, int y, int damage)
        {
            GameRules.TryCharge(ref State, player, x, y, damage);
        }

        public bool TryAddStructure(int player, StructureKind kind, int x, int y, byte hp)
        {
            if (kind == StructureKind.None || hp == 0) return false;
            return WorldEffectSystem.TryAddStructure(ref State, player, kind, x, y, hp, 2);
        }
    }
}
