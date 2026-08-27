using Unity.Collections;

namespace SpellThrower
{
    /// Server-authoritative, pure game rules.  Card definitions call into this
    /// class through CardEffectRuntime so the same rules can be exercised in
    /// EditMode without a scene or a NetworkBehaviour.
    public static class GameRules
    {
        public const int Size = 8;
        public const int MaxHp = 50;
        public const int StartCost = 4;        // 첫 자기 턴 최대 코스트
        public const int MaxCost = 10;         // 코스트 UI 한 칸 = 2, 반 칸 = 1 → 다섯 칸
        public const int MaxHand = 7;          // 일반 카드 기준
        public const int HandSlots = MaxHand + 1;  // 이동 카드가 한 장 더 들어온다
        public const int StartHand = 3;        // 첫 턴 일반 카드. 게임 시작 시 1회
        public const int DeckSize = 25;
        public const int MaxCopies = 2;        // 예외: 듀얼을 신청하지는 1장
        public const int MinDistinct = 13;     // 25장 / 2장 제한이 강제하는 최소 종수
        public const int TurnSeconds = 45;     // 자기 행동 턴 제한 시간
        public const byte DefaultMapObstacleHp = 3;
        public const byte FireZoneTick = 10;    // 불길 장판이 턴 종료마다 넣는 피해
        public const byte RegenerationTick = 10;  // 재생이 자기 턴 시작마다 회복하는 양
        public const int LightningStackStep = 4;  // 번개 스택 한 장당 추가 피해
        public const int DefaultMaxTurns = 25;     // 라운드 수
        public static int MaxTurns = DefaultMaxTurns;

        /// 라운드는 나와 상대의 행동 턴이 모두 끝나야 1 증가한다. turnCount 는 행동 턴
        /// 번호(1부터, 홀수가 선공)라서 라운드는 그 절반이고, 자기 턴 수와 같다.
        public static int Round(byte turnCount) => (turnCount + 1) / 2;

        /// 서로 한 번씩 두고 나야 1턴이다. '몇 턴 지속'을 세는 쪽은 이 경계에서만 줄인다.
        public static bool IsRoundEnd(byte turnCount) => turnCount % 2 == 0;

        /// 첫 자기 턴 4, 턴마다 +1, 상한 10.
        public static int CostFor(int round)
        {
            int cost = StartCost + round - 1;
            return cost > MaxCost ? MaxCost : cost;
        }

        /// 턴 시작 일반 드로우. 1턴 3장은 게임 시작 시 이미 나눠 줬으므로 0이다.
        public static int DrawFor(int round) => round <= 1 ? 0 : round >= 15 ? 2 : 1;

        /// 덱에 넣을 수 있는 같은 카드의 최대 장수.
        public static int CopyLimitOf(byte card) => card == (byte)CardId.Duel ? 1 : MaxCopies;

        public const ulong DemoObstacles =
            (1UL << (1 * Size + 0)) |
            (1UL << (4 * Size + 0)) |
            (1UL << (6 * Size + 0)) |
            (1UL << (3 * Size + 4)) |
            (1UL << (3 * Size + 5)) |
            (1UL << (1 * Size + 6)) |
            (1UL << (7 * Size + 7));

        // ---- Player state accessors ----
        public static ref byte X(ref GameState s, int p) => ref (p == 0 ? ref s.p0X : ref s.p1X);
        public static ref byte Y(ref GameState s, int p) => ref (p == 0 ? ref s.p0Y : ref s.p1Y);
        public static ref byte Hp(ref GameState s, int p) => ref (p == 0 ? ref s.p0Hp : ref s.p1Hp);
        public static ref byte MoveLocked(ref GameState s, int p) => ref (p == 0 ? ref s.p0MoveLocked : ref s.p1MoveLocked);
        public static ref byte BaseMove(ref GameState s, int p) => ref (p == 0 ? ref s.p0BaseMove : ref s.p1BaseMove);
        public static ref byte Burned(ref GameState s, int p) => ref (p == 0 ? ref s.p0Burned : ref s.p1Burned);
        public static ref byte BurnSeq(ref GameState s, int p) => ref (p == 0 ? ref s.p0BurnSeq : ref s.p1BurnSeq);
        public static ref byte LightningStack(ref GameState s, int p) => ref (p == 0 ? ref s.p0LightningStack : ref s.p1LightningStack);
        public static ref FixedList64Bytes<byte> Hand(ref GameState s, int p) => ref (p == 0 ? ref s.p0Hand : ref s.p1Hand);
        public static ref FixedList64Bytes<byte> Deck(ref GameState s, int p) => ref (p == 0 ? ref s.p0Deck : ref s.p1Deck);
        public static ref FixedList64Bytes<byte> Disc(ref GameState s, int p) => ref (p == 0 ? ref s.p0Disc : ref s.p1Disc);
        public static ref FixedList32Bytes<PlayerTag> Tags(ref GameState s, int p) => ref (p == 0 ? ref s.p0Tags : ref s.p1Tags);
        public static ref FixedList128Bytes<byte> ObstacleHp(ref GameState s) => ref s.obstacleHp;
        public static ref FixedList512Bytes<WorldEffectRecord> WorldEffects(ref GameState s) => ref s.worldEffects;

        public static int AbsValue(int value) => value < 0 ? -value : value;
        public static int SignValue(int value) => value > 0 ? 1 : (value < 0 ? -1 : 0);
        public static int Dist(int ax, int ay, int bx, int by) => AbsValue(ax - bx) + AbsValue(ay - by);
        public static bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

        public static bool IsMapObstacle(ref GameState s, int x, int y)
        {
            return InBounds(x, y) && (s.obstacles & (1UL << (y * Size + x))) != 0;
        }

        public static byte MapObstacleHp(ref GameState s, int x, int y)
        {
            if (!IsMapObstacle(ref s, x, y)) return 0;
            int index = y * Size + x;
            ref var hp = ref ObstacleHp(ref s);
            if (index < hp.Length && hp[index] > 0) return hp[index];
            return DefaultMapObstacleHp;
        }

        public static bool IsOccupied(ref GameState s, int x, int y)
        {
            return (X(ref s, 0) == x && Y(ref s, 0) == y) ||
                   (X(ref s, 1) == x && Y(ref s, 1) == y);
        }

        /// A structure is a blocking tile; fire/frost hazards are not.
        public static bool IsBlocked(ref GameState s, int x, int y)
        {
            return InBounds(x, y) &&
                   (IsMapObstacle(ref s, x, y) || WorldEffectSystem.IsStructureAt(ref s, x, y));
        }

        static uint NextRand(ref uint value)
        {
            if (value == 0) value = 2463534242u;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        // ---- HP and damage ----
        public static void DamagePlayer(ref GameState s, int player, int amount)
        {
            if (!IsPlayer(player) || amount <= 0) return;
            ref byte hp = ref Hp(ref s, player);
            hp = (byte)(hp > amount ? hp - amount : 0);
            // 체력이 0이 되는 순간 승패가 난다. 턴 종료를 눌러야만 끝나면
            // 자기 차례에 죽은 쪽이 그대로 붙잡고 있을 수 있다.
            CheckWin(ref s);
        }

        public static void HealPlayer(ref GameState s, int player, int amount)
        {
            if (!IsPlayer(player) || amount <= 0) return;
            ref byte hp = ref Hp(ref s, player);
            hp = (byte)(hp + amount > MaxHp ? MaxHp : hp + amount);
        }

        public static void DamageAt(ref GameState s, int x, int y, int amount, int sourcePlayer = -1)
        {
            DamageAt(ref s, x, y, amount, sourcePlayer, true);
        }

        public static void DamageAt(ref GameState s, int x, int y, int amount, int sourcePlayer, bool damageMapObstacle)
        {
            if (!InBounds(x, y) || amount <= 0) return;
            for (int player = 0; player < 2; player++)
            {
                if (X(ref s, player) == x && Y(ref s, player) == y)
                    DamagePlayer(ref s, player, amount);
            }
            WorldEffectSystem.DamageStructureAt(ref s, x, y, amount, sourcePlayer);
            if (damageMapObstacle) DamageMapObstacleAt(ref s, x, y, amount);
        }

        public static bool DamageMapObstacleAt(ref GameState s, int x, int y, int amount)
        {
            if (amount <= 0 || !IsMapObstacle(ref s, x, y)) return false;

            int index = y * Size + x;
            byte current = MapObstacleHp(ref s, x, y);
            int next = current > amount ? current - amount : 0;
            ref var hp = ref ObstacleHp(ref s);
            while (hp.Length <= index) hp.Add(0);
            hp[index] = (byte)next;
            if (next == 0) s.obstacles &= ~(1UL << index);
            return true;
        }

        /// Returns the current hit's damage and consumes one same-turn stack.
        /// Callers only invoke this after confirming that the lightning hit a
        /// player, structure, or map wall; a miss therefore does not stack.
        public static int NextLightningDamage(ref GameState s, int player, int basePower, int step = LightningStackStep)
        {
            if (basePower <= 0) return 0;
            int stack = LightningStack(ref s, player);
            LightningStack(ref s, player) = (byte)(stack < byte.MaxValue ? stack + 1 : byte.MaxValue);
            return basePower + stack * step;
        }

        public static bool IsLightningHit(ref GameState s, int x, int y)
        {
            return InBounds(x, y) && (IsOccupied(ref s, x, y) ||
                   IsMapObstacle(ref s, x, y) || WorldEffectSystem.IsStructureAt(ref s, x, y));
        }

        // ---- Tags ----
        public static bool HasTag(ref GameState s, int player, PlayerTagId tagId)
        {
            return GetTag(ref s, player, tagId, out _);
        }

        public static bool GetTag(ref GameState s, int player, PlayerTagId tagId, out PlayerTag tag)
        {
            tag = default;
            if (player != 0 && player != 1) return false;
            ref var tags = ref Tags(ref s, player);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Id == tagId && tags[i].DurationTurns > 0)
                {
                    tag = tags[i];
                    return true;
                }
            }
            tag = default;
            return false;
        }

        public static bool AddOrRefreshTag(ref GameState s, int player, PlayerTagId tagId, byte duration, byte value = 0)
        {
            if (!IsPlayer(player) || tagId == PlayerTagId.None || duration == 0) return false;
            ref var tags = ref Tags(ref s, player);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Id == tagId)
                {
                    tags[i] = new PlayerTag(tagId, duration, value);
                    return true;
                }
            }
            if (tags.Length >= tags.Capacity) return false;
            tags.Add(new PlayerTag(tagId, duration, value));
            return true;
        }

        public static bool RemoveTag(ref GameState s, int player, PlayerTagId tagId)
        {
            if (!IsPlayer(player)) return false;
            ref var tags = ref Tags(ref s, player);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Id == tagId)
                {
                    tags.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// Durations belong to their owner, not to the opponent's turns.
        public static void TickOwnerTags(ref GameState s, int player)
        {
            if (!IsPlayer(player)) return;
            ref var tags = ref Tags(ref s, player);
            for (int i = tags.Length - 1; i >= 0; i--)
            {
                var tag = tags[i];
                if (tag.DurationTurns <= 1) tags.RemoveAt(i);
                else
                {
                    tag.DurationTurns--;
                    tags[i] = tag;
                }
            }
        }

        // ---- Deck and hand ----
        public static int HandImmediateDamagePower(ref GameState s, int player)
        {
            if (player != 0 && player != 1) return 0;
            int total = 0;
            ref var hand = ref Hand(ref s, player);
            for (int i = 0; i < hand.Length; i++)
            {
                var card = Cards.Get(hand[i]);
                if (card != null) total += card.ImmediateDamagePower;
            }
            return total;
        }

        static bool IsNormalCard(byte card)
        {
            // Move is a synthetic card supplied at a turn boundary. It never
            // belongs to the deck, the normal hand limit, or draw effects.
            return card < Cards.All.Length && card != (byte)CardId.Move;
        }

        /// 손패에 든 일반 카드 수. 이동 카드는 매 턴 따로 지급되므로 최대치에서 뺀다.
        static int NormalHandCount(ref GameState s, int player)
        {
            ref var hand = ref Hand(ref s, player);
            int count = 0;
            for (int i = 0; i < hand.Length; i++)
                if (IsNormalCard(hand[i])) count++;
            return count;
        }

        static bool CanReceiveNormalCard(ref GameState s, int player)
        {
            if (!IsPlayer(player)) return false;
            ref var hand = ref Hand(ref s, player);
            return NormalHandCount(ref s, player) < MaxHand &&
                   hand.Length < HandSlots;
        }

        static void AddToDiscard(ref GameState s, int player, byte card)
        {
            if (!IsPlayer(player)) return;
            ref var disc = ref Disc(ref s, player);
            if (disc.Length < disc.Capacity) disc.Add(card);
        }

        static void ReceiveDrawnCard(ref GameState s, int player, byte card)
        {
            // A malformed state may contain the synthetic movement card in a
            // deck. Do not let a draw turn it into a regular hand card.
            if (!IsNormalCard(card)) return;

            ref var hand = ref Hand(ref s, player);
            if (CanReceiveNormalCard(ref s, player)) hand.Add(card);
            else DiscardDrawnCard(ref s, player, card);
        }

        /// 이동 카드를 손패 맨 앞에 한 장 놓는다. 이미 있으면 그대로 둔다.
        static void GiveMoveCard(ref GameState s, int player)
        {
            ref var hand = ref Hand(ref s, player);
            for (int i = 0; i < hand.Length; i++)
                if (hand[i] == (byte)CardId.Move) return;
            if (hand.Length >= HandSlots || hand.Length >= hand.Capacity) return;

            hand.Add((byte)CardId.Move);
            for (int i = hand.Length - 1; i > 0; i--)
            {
                byte tmp = hand[i];
                hand[i] = hand[i - 1];
                hand[i - 1] = tmp;
            }
        }

        /// 미사용 이동 카드는 턴 종료 시 사라진다 (휴지통에 넣지 않는다).
        static void DropMoveCards(ref GameState s, int player)
        {
            ref var hand = ref Hand(ref s, player);
            for (int i = hand.Length - 1; i >= 0; i--)
                if (hand[i] == (byte)CardId.Move) hand.RemoveAt(i);
        }

        public static bool IsValidDeck(byte[] cards)
        {
            if (cards == null || cards.Length != DeckSize) return false;
            var counts = new byte[Cards.All.Length];
            int distinct = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                byte card = cards[i];
                if (card >= Cards.All.Length || card == (byte)CardId.Move) return false;
                if (counts[card] == 0) distinct++;
                if (++counts[card] > CopyLimitOf(card)) return false;
            }
            return distinct >= MinDistinct;
        }

        public static GameState NewGame(uint seed, byte[] p0Cards = null, byte[] p1Cards = null)
        {
            p0Cards = p0Cards ?? Cards.DeckList;
            p1Cards = p1Cards ?? Cards.DeckList;
            if (!IsValidDeck(p0Cards) || !IsValidDeck(p1Cards))
                throw new System.ArgumentException(
                    "덱은 25장, 종류당 최대 2장(듀얼을 신청하지는 1장), 최소 13종이어야 합니다.");

            var s = default(GameState);
            s.obstacles = DemoObstacles;
            for (int i = 0; i < Size * Size; i++)
                s.obstacleHp.Add((s.obstacles & (1UL << i)) != 0 ? DefaultMapObstacleHp : (byte)0);
            s.rng = seed == 0 ? 1u : seed;
            s.p0X = 3; s.p0Y = 0;
            s.p1X = 4; s.p1Y = 7;
            s.p0Hp = MaxHp; s.p1Hp = MaxHp;
            s.turnPlayer = 0;
            s.turnCount = 1;
            s.actionLeft = (byte)CostFor(1);
            s.p0BaseMove = 1;
            s.p0Burned = byte.MaxValue;
            s.p1Burned = byte.MaxValue;
            s.reservedLegacyRangeBonus = 0;
            s.p0LightningStack = 0;
            s.p1LightningStack = 0;

            for (int player = 0; player < 2; player++)
            {
                ref var deck = ref Deck(ref s, player);
                byte[] source = player == 0 ? p0Cards : p1Cards;
                for (int i = 0; i < source.Length; i++) deck.Add(source[i]);
                Shuffle(ref deck, ref s.rng);
                DrawCards(ref s, player, StartHand);
            }
            GiveMoveCard(ref s, 0);   // 첫 턴은 선공만 이동 카드를 받는다
            return s;
        }

        static void Shuffle(ref FixedList64Bytes<byte> list, ref uint rng)
        {
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = (int)(NextRand(ref rng) % (uint)(i + 1));
                byte tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        static bool TakeTopCard(ref GameState s, int player, out byte card)
        {
            ref var deck = ref Deck(ref s, player);
            if (deck.Length == 0)
            {
                ref var disc = ref Disc(ref s, player);
                if (disc.Length == 0)
                {
                    card = 0;
                    return false;
                }
                for (int i = 0; i < disc.Length; i++) deck.Add(disc[i]);
                disc.Clear();
                Shuffle(ref deck, ref s.rng);
            }

            card = deck[deck.Length - 1];
            deck.RemoveAt(deck.Length - 1);
            return true;
        }

        static void DiscardDrawnCard(ref GameState s, int player, byte card)
        {
            AddToDiscard(ref s, player, card);
            Burned(ref s, player) = card;
            BurnSeq(ref s, player)++;
        }

        public static void DrawCards(ref GameState s, int player, int count)
        {
            if (!IsPlayer(player) || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                if (!TakeTopCard(ref s, player, out byte card)) return;
                ReceiveDrawnCard(ref s, player, card);
            }
        }

        public static void Divination(ref GameState s, int player)
        {
            Divination(ref s, player, 0);
        }

        public static void Divination(ref GameState s, int player, int choiceIndex)
        {
            if (!IsPlayer(player)) return;
            byte[] shown = new byte[3];
            int shownCount = 0;
            while (shownCount < shown.Length && TakeTopCard(ref s, player, out byte card))
            {
                // Movement is not a deck card, but keep the rule explicit for
                // callers that construct a state manually.
                if (IsNormalCard(card)) shown[shownCount++] = card;
            }

            if (shownCount == 0) return;
            if (choiceIndex < 0) choiceIndex = 0;
            if (choiceIndex >= shownCount) choiceIndex = shownCount - 1;
            ReceiveDrawnCard(ref s, player, shown[choiceIndex]);
            for (int i = 0; i < shownCount; i++)
            {
                if (i == choiceIndex) continue;
                AddToDiscard(ref s, player, shown[i]);
            }
        }

        public static void Exchange(ref GameState s, int player)
        {
            Exchange(ref s, player, 0);
        }

        public static void Exchange(ref GameState s, int player, int discardIndex)
        {
            if (!IsPlayer(player)) return;
            ref var hand = ref Hand(ref s, player);
            if (discardIndex < 0 || discardIndex >= hand.Length ||
                hand[discardIndex] == (byte)CardId.Move)
            {
                discardIndex = FirstExchangeCandidate(ref s, player);
                if (discardIndex < 0) return;
            }
            byte discarded = hand[discardIndex];
            hand.RemoveAt(discardIndex);
            AddToDiscard(ref s, player, discarded);
            DrawCards(ref s, player, 2);
        }

        // ---- Movement ----
        public static bool CanMove(ref GameState s, int player, int x, int y)
        {
            if (s.winner != 0 || !IsPlayer(player) || player != s.turnPlayer) return false;
            if (BaseMove(ref s, player) == 0 || !InBounds(x, y)) return false;
            if (IsBlocked(ref s, x, y) || IsOccupiedByOther(ref s, player, x, y)) return false;
            int maxDistance = MovementDistance(ref s, player, 1);
            int distance = Dist(X(ref s, player), Y(ref s, player), x, y);
            return distance > 0 && distance <= maxDistance &&
                   FindMovementPath(ref s, player, x, y, distance, HasTag(ref s, player, PlayerTagId.Leap),
                                    out _, out _, out _, out _);
        }

        public static bool TryMove(ref GameState s, int player, int x, int y)
        {
            if (!CanMove(ref s, player, x, y)) return false;
            int distance = Dist(X(ref s, player), Y(ref s, player), x, y);
            bool allowLeap = HasTag(ref s, player, PlayerTagId.Leap);
            if (!FindMovementPath(ref s, player, x, y, distance, allowLeap,
                                  out _, out _, out _, out bool usedLeap)) return false;
            X(ref s, player) = (byte)x;
            Y(ref s, player) = (byte)y;
            BaseMove(ref s, player) = 0;
            if (usedLeap) RemoveTag(ref s, player, PlayerTagId.Leap);
            return true;
        }

        static int MovementDistance(ref GameState s, int player, int baseDistance)
        {
            int distance = baseDistance;
            if (GetTag(ref s, player, PlayerTagId.MoveBoost, out var boost)) distance += boost.Value;
            if (WorldEffectSystem.IsFrosted(ref s, X(ref s, player), Y(ref s, player))) distance--;
            if (HasTag(ref s, player, PlayerTagId.WindFrostSlow)) distance--;
            return distance < 1 ? 1 : distance;
        }

        public static int RangeOf(ref GameState s, byte cardId)
        {
            var card = Cards.Get(cardId);
            return card != null ? card.Range : 0;
        }

        public static bool TryCardMove(ref GameState s, int player, int x, int y, int baseDistance, bool leap)
        {
            if (!CanCardMove(ref s, player, x, y, baseDistance, leap)) return false;
            int distance = Dist(X(ref s, player), Y(ref s, player), x, y);
            bool allowLeap = leap || HasTag(ref s, player, PlayerTagId.Leap);
            if (!FindMovementPath(ref s, player, x, y, distance, allowLeap,
                                  out _, out _, out _, out bool usedLeap)) return false;
            X(ref s, player) = (byte)x;
            Y(ref s, player) = (byte)y;
            if (usedLeap) RemoveTag(ref s, player, PlayerTagId.Leap);
            return true;
        }

        static bool CanCardMove(ref GameState s, int player, int x, int y, int baseDistance, bool leap)
        {
            if (s.winner != 0 || !IsPlayer(player) || player != s.turnPlayer || !InBounds(x, y)) return false;
            if (IsBlocked(ref s, x, y) || IsOccupiedByOther(ref s, player, x, y)) return false;
            int distance = Dist(X(ref s, player), Y(ref s, player), x, y);
            int maxDistance = MovementDistance(ref s, player, baseDistance);
            if (distance == 0 || distance > maxDistance) return false;
            return FindMovementPath(ref s, player, x, y, distance,
                                    leap || HasTag(ref s, player, PlayerTagId.Leap),
                                    out _, out _, out _, out _);
        }

        static bool FindMovementPath(
            ref GameState s,
            int player,
            int targetX,
            int targetY,
            int distance,
            bool allowLeap,
            out int[] pathX,
            out int[] pathY,
            out int pathLength,
            out bool usedLeap)
        {
            pathX = null;
            pathY = null;
            pathLength = 0;
            usedLeap = false;
            int startX = X(ref s, player);
            int startY = Y(ref s, player);
            if (distance <= 0) return false;

            // A movement card can use either axis order. A leap consumes the
            // obstacle/enemy tile and the empty landing tile from the budget.
            for (int route = 0; route < 2; route++)
            {
                int x = startX, y = startY;
                bool valid = true;
                bool routeUsedLeap = false;
                int budget = 0;
                int[] routeX = new int[distance + 1];
                int[] routeY = new int[distance + 1];
                int routeLength = 0;
                bool horizontalFirst = route == 0;

                while (x != targetX || y != targetY)
                {
                    if (budget >= distance)
                    {
                        valid = false;
                        break;
                    }

                    bool horizontalRemaining = x != targetX;
                    bool verticalRemaining = y != targetY;
                    bool takeHorizontal = horizontalFirst ? horizontalRemaining : !verticalRemaining;
                    int stepX = takeHorizontal ? SignValue(targetX - x) : 0;
                    int stepY = takeHorizontal ? 0 : SignValue(targetY - y);
                    int nextX = x + stepX;
                    int nextY = y + stepY;

                    if (IsBlocked(ref s, nextX, nextY) || IsOccupiedByOther(ref s, player, nextX, nextY))
                    {
                        if (!allowLeap || routeUsedLeap || budget + 2 > distance ||
                            !TryLeapLanding(ref s, player, nextX, nextY, stepX, stepY,
                                            out int landingX, out int landingY))
                        {
                            valid = false;
                            break;
                        }

                        x = landingX;
                        y = landingY;
                        budget += 2;
                        routeUsedLeap = true;
                    }
                    else
                    {
                        x = nextX;
                        y = nextY;
                        budget++;
                    }

                    routeX[routeLength] = x;
                    routeY[routeLength] = y;
                    routeLength++;
                }

                if (valid && x == targetX && y == targetY)
                {
                    pathX = routeX;
                    pathY = routeY;
                    pathLength = routeLength;
                    usedLeap = routeUsedLeap;
                    return true;
                }
            }
            return false;
        }

        static bool TryLeapLanding(
            ref GameState s,
            int player,
            int jumpedX,
            int jumpedY,
            int stepX,
            int stepY,
            out int landingX,
            out int landingY)
        {
            landingX = jumpedX + stepX;
            landingY = jumpedY + stepY;
            return InBounds(landingX, landingY) &&
                   !IsBlocked(ref s, landingX, landingY) &&
                   !IsOccupiedByOther(ref s, player, landingX, landingY);
        }

        public static bool TryCharge(ref GameState s, int player, int targetX, int targetY, int damage = 12)
        {
            if (!CanChargeTarget(ref s, player, targetX, targetY)) return false;
            int startX = X(ref s, player), startY = Y(ref s, player);
            int dx = targetX - startX, dy = targetY - startY;
            int distance = AbsValue(dx) + AbsValue(dy);
            int stepX = SignValue(dx), stepY = SignValue(dy);
            int currentX = startX, currentY = startY;
            bool allowLeap = HasTag(ref s, player, PlayerTagId.Leap);
            bool usedLeap = false;
            int budget = 0;
            while (budget < distance)
            {
                int nextX = currentX + stepX;
                int nextY = currentY + stepY;
                int opponent = OtherPlayerAt(ref s, nextX, nextY);
                if (opponent >= 0)
                {
                    if (allowLeap && budget + 2 <= distance &&
                        TryLeapLanding(ref s, player, nextX, nextY, stepX, stepY,
                                       out int landingX, out int landingY))
                    {
                        DamagePlayer(ref s, opponent, 2);
                        currentX = landingX;
                        currentY = landingY;
                        budget += 2;
                        allowLeap = false;
                        usedLeap = true;
                        continue;
                    }

                    DamagePlayer(ref s, opponent, damage);
                    X(ref s, player) = (byte)currentX;
                    Y(ref s, player) = (byte)currentY;
                    if (usedLeap) RemoveTag(ref s, player, PlayerTagId.Leap);
                    return true;
                }

                if (IsBlocked(ref s, nextX, nextY))
                {
                    if (!allowLeap || budget + 2 > distance ||
                        !TryLeapLanding(ref s, player, nextX, nextY, stepX, stepY,
                                        out int landingX, out int landingY)) return false;
                    currentX = landingX;
                    currentY = landingY;
                    budget += 2;
                    allowLeap = false;
                    usedLeap = true;
                    continue;
                }

                currentX = nextX;
                currentY = nextY;
                budget++;
            }

            X(ref s, player) = (byte)targetX;
            Y(ref s, player) = (byte)targetY;
            if (usedLeap) RemoveTag(ref s, player, PlayerTagId.Leap);
            return true;
        }

        static bool CanChargeTarget(ref GameState s, int player, int targetX, int targetY)
        {
            if (s.winner != 0 || !IsPlayer(player) || player != s.turnPlayer || !InBounds(targetX, targetY)) return false;
            int startX = X(ref s, player), startY = Y(ref s, player);
            int dx = targetX - startX, dy = targetY - startY;
            if (dx != 0 && dy != 0) return false;
            int distance = AbsValue(dx) + AbsValue(dy);
            int maxDistance = MovementDistance(ref s, player, 2);
            if (distance == 0 || distance > maxDistance) return false;

            int stepX = SignValue(dx), stepY = SignValue(dy);
            int currentX = startX, currentY = startY;
            bool allowLeap = HasTag(ref s, player, PlayerTagId.Leap);
            int budget = 0;
            while (budget < distance)
            {
                int nextX = currentX + stepX;
                int nextY = currentY + stepY;
                if (OtherPlayerAt(ref s, nextX, nextY) >= 0)
                {
                    if (allowLeap && budget + 2 <= distance &&
                        TryLeapLanding(ref s, player, nextX, nextY, stepX, stepY,
                                       out int landingX, out int landingY))
                    {
                        currentX = landingX;
                        currentY = landingY;
                        budget += 2;
                        allowLeap = false;
                        continue;
                    }
                    return true; // The charge stops before the opponent.
                }

                if (IsBlocked(ref s, nextX, nextY))
                {
                    if (!allowLeap || budget + 2 > distance ||
                        !TryLeapLanding(ref s, player, nextX, nextY, stepX, stepY,
                                        out int obstacleLandingX, out int obstacleLandingY)) return false;
                    currentX = obstacleLandingX;
                    currentY = obstacleLandingY;
                    budget += 2;
                    allowLeap = false;
                    continue;
                }

                currentX = nextX;
                currentY = nextY;
                budget++;
            }

            return currentX == targetX && currentY == targetY;
        }

        // ---- Card targeting and use ----
        public static bool CanPlay(ref GameState s, int player, int handIndex, int tx, int ty)
        {
            if (s.winner != 0 || !IsPlayer(player) || player != s.turnPlayer) return false;
            ref var hand = ref Hand(ref s, player);
            if (handIndex < 0 || handIndex >= hand.Length) return false;
            CardDef def = Cards.Get(hand[handIndex]);
            if (def == null) return false;
            if (s.actionLeft < def.Cost) return false;
            // 이동 카드는 기본 이동을 손패로 보여 주는 것뿐이다. 판정을 그대로 넘긴다.
            if (hand[handIndex] == (byte)CardId.Move) return CanMove(ref s, player, tx, ty);

            switch (def.TargetKind)
            {
                case CardTargetKind.Self:
                    return def.Effect != CardEffectKind.Exchange || CanExchange(ref s, player, handIndex);
                case CardTargetKind.Enemy:
                    return CanEnemyTarget(ref s, player, tx, ty, def);
                case CardTargetKind.Direction:
                    return CanDirectionTarget(ref s, player, tx, ty, def);
                case CardTargetKind.MoveTile:
                    return def.Effect == CardEffectKind.Charge
                        ? CanChargeTarget(ref s, player, tx, ty)
                        : CanCardMove(ref s, player, tx, ty, def.Power, def.Effect == CardEffectKind.Leap);
                case CardTargetKind.Tile:
                    return CanTileTarget(ref s, player, tx, ty, def);
                default:
                    return false;
            }
        }

        /// 사거리 안의 다른 칸인지만 본다. 상대가 그 칸에 서 있는지는 보지 않는다 —
        /// 사거리 표시가 상대 위치에 따라 나타났다 사라지면 안 되고,
        /// 상대가 사거리 밖이라고 카드를 아예 못 쓰게 되어서도 안 된다.
        static bool InRange(ref GameState s, int player, int tx, int ty, int range)
        {
            if (!InBounds(tx, ty)) return false;
            int distance = Dist(X(ref s, player), Y(ref s, player), tx, ty);
            return distance > 0 && distance <= range;
        }

        static bool CanEnemyTarget(ref GameState s, int player, int tx, int ty, CardDef def)
        {
            if (!InRange(ref s, player, tx, ty, def.Range)) return false;
            return def.Arc || HasLineOfSight(ref s, X(ref s, player), Y(ref s, player), tx, ty);
        }

        static bool CanDirectionTarget(ref GameState s, int player, int tx, int ty, CardDef def)
        {
            if (!InBounds(tx, ty)) return false;
            int dx = tx - X(ref s, player), dy = ty - Y(ref s, player);
            if (dx != 0 && dy != 0) return false;
            int distance = AbsValue(dx) + AbsValue(dy);
            return distance > 0 && distance <= def.Range;
        }

        static bool CanTileTarget(ref GameState s, int player, int tx, int ty, CardDef def)
        {
            if (def.Effect == CardEffectKind.LightningLine)
                return CanDirectionTarget(ref s, player, tx, ty, def);
            if (!InRange(ref s, player, tx, ty, def.Range)) return false;

            bool placement = def.Effect == CardEffectKind.IceWall || def.Effect == CardEffectKind.Structure;
            if (placement)
                return !IsBlocked(ref s, tx, ty) && !IsOccupied(ref s, tx, ty);

            // Direct attacks must resolve against a player. Cards that create
            // a zone or otherwise affect an area explicitly opt into empty
            // tiles through their catalog metadata.
            if (!def.AllowEmptyTile && !IsOccupied(ref s, tx, ty)) return false;

            if (def.Arc) return true;   // 낙뢰류는 장애물을 무시한다
            if (IsMapObstacle(ref s, tx, ty)) return false;
            return HasLineOfSight(ref s, X(ref s, player), Y(ref s, player), tx, ty);
        }

        static bool CanExchange(ref GameState s, int player, int exchangeIndex)
        {
            ref var hand = ref Hand(ref s, player);
            for (int i = 0; i < hand.Length; i++)
                if (i != exchangeIndex && IsNormalCard(hand[i]))
                    return true;
            return false;
        }

        static int FirstExchangeCandidate(ref GameState s, int player)
        {
            ref var hand = ref Hand(ref s, player);
            for (int i = 0; i < hand.Length; i++)
                if (IsNormalCard(hand[i]))
                    return i;
            return -1;
        }

        public static bool TryPlay(ref GameState s, int player, int handIndex, int tx, int ty)
        {
            if (!CanPlay(ref s, player, handIndex, tx, ty)) return false;
            byte card = Hand(ref s, player)[handIndex];
            CardDef def = Cards.Get(card);
            if (def == null) return false;
            // 이동 카드는 기본 이동을 소모하고 휴지통에 남지 않는다.
            if (card == (byte)CardId.Move)
            {
                if (!TryMove(ref s, player, tx, ty)) return false;
                Hand(ref s, player).RemoveAt(handIndex);
                RecordCardAction(ref s, player, card, tx, ty);
                return true;
            }
            int effectTargetX = tx;
            Hand(ref s, player).RemoveAt(handIndex);
            AddToDiscard(ref s, player, card);
            s.actionLeft -= def.Cost;

            // Exchange's choice is sent as the selected pre-removal hand
            // index. Normalize it after removing the exchange card itself.
            if (def.Effect == CardEffectKind.Exchange)
            {
                // Selecting the Exchange card itself (the default target in
                // the card UI) means "pick the first valid normal card".
                if (effectTargetX == handIndex) effectTargetX = -1;
                else if (effectTargetX > handIndex) effectTargetX--;
            }

            var runtime = new CardEffectRuntime(s);
            var instigator = new RulePlayer(runtime, player);
            ICardPlayer target = null;
            if ((def.TargetKind == CardTargetKind.Enemy || def.TargetKind == CardTargetKind.Tile) &&
                IsOpponentTarget(ref runtime.State, player, tx, ty))
                target = new RulePlayer(runtime, 1 - player);
            var context = new CardUseContext(def, instigator, target, effectTargetX, ty, runtime, runtime);
            def.OnUse(context);
            s = runtime.State;
            RecordCardAction(ref s, player, card, tx, ty);
            return true;
        }

        static void RecordCardAction(ref GameState s, int player, byte card, int tx, int ty)
        {
            s.lastActionKind = GameplayActionKind.CardUsed;
            s.lastActionPlayer = (byte)player;
            s.lastActionCardId = card;
            s.lastActionTargetX = (byte)(InBounds(tx, ty) ? tx : 0);
            s.lastActionTargetY = (byte)(InBounds(tx, ty) ? ty : 0);
            s.lastActionSequence++;
            if (s.lastActionSequence == 0) s.lastActionSequence = 1;
        }

        sealed class RulePlayer : ICardPlayer
        {
            readonly CardEffectRuntime _runtime;
            public int PlayerIndex { get; }

            public RulePlayer(CardEffectRuntime runtime, int player)
            {
                _runtime = runtime;
                PlayerIndex = player;
            }

            public int X => GameRules.X(ref _runtime.State, PlayerIndex);
            public int Y => GameRules.Y(ref _runtime.State, PlayerIndex);
            public int Hp => GameRules.Hp(ref _runtime.State, PlayerIndex);
            public int HandImmediateDamagePower => GameRules.HandImmediateDamagePower(ref _runtime.State, PlayerIndex);

            public void Damage(int amount) => DamagePlayer(ref _runtime.State, PlayerIndex, amount);
            public void Heal(int amount) => HealPlayer(ref _runtime.State, PlayerIndex, amount);
            public void MoveTo(int x, int y)
            {
                if (InBounds(x, y))
                {
                    X(ref _runtime.State, PlayerIndex) = (byte)x;
                    Y(ref _runtime.State, PlayerIndex) = (byte)y;
                }
            }
            public void PushFrom(int sourceX, int sourceY, int tiles) =>
                Push(ref _runtime.State, sourceX, sourceY, PlayerIndex, tiles);
            public void LockMove()
            {
                MoveLocked(ref _runtime.State, PlayerIndex) = 1;
                AddOrRefreshTag(ref _runtime.State, PlayerIndex, PlayerTagId.MoveLocked, 1);
            }
            public void DrawCards(int count) => GameRules.DrawCards(ref _runtime.State, PlayerIndex, count);
            public bool AddTag(PlayerTagId tagId, byte duration, byte value = 0) =>
                AddOrRefreshTag(ref _runtime.State, PlayerIndex, tagId, duration, value);
            public bool HasTag(PlayerTagId tagId) => GameRules.HasTag(ref _runtime.State, PlayerIndex, tagId);
            public bool RemoveTag(PlayerTagId tagId) => GameRules.RemoveTag(ref _runtime.State, PlayerIndex, tagId);
        }

        public static bool HasLineOfSight(ref GameState s, int x0, int y0, int x1, int y1)
        {
            int dx = AbsValue(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -AbsValue(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (x0 != x1 || y0 != y1)
            {
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
                if ((x0 != x1 || y0 != y1) && IsBlocked(ref s, x0, y0)) return false;
            }
            return true;
        }

        // Compatibility helper for old card implementations and tests.
        public static void Push(ref GameState s, int player, int opp, int tiles)
        {
            Push(ref s, X(ref s, player), Y(ref s, player), opp, tiles);
        }

        public static void Push(ref GameState s, int sourceX, int sourceY, int targetPlayer, int tiles)
        {
            if (!IsPlayer(targetPlayer) || tiles <= 0) return;
            int targetX = X(ref s, targetPlayer), targetY = Y(ref s, targetPlayer);
            int dx = targetX - sourceX, dy = targetY - sourceY;
            int stepX = 0, stepY = 0;
            if (AbsValue(dx) == AbsValue(dy)) { stepX = SignValue(dx); stepY = SignValue(dy); }   // 정대각선 - 대각으로 민다
            else if (AbsValue(dx) > AbsValue(dy)) stepX = SignValue(dx);
            else stepY = SignValue(dy);
            for (int i = 0; i < tiles; i++)
            {
                int nextX = targetX + stepX, nextY = targetY + stepY;
                if (!InBounds(nextX, nextY) || IsBlocked(ref s, nextX, nextY) ||
                    (nextX == sourceX && nextY == sourceY)) break;
                targetX = nextX;
                targetY = nextY;
            }
            X(ref s, targetPlayer) = (byte)targetX;
            Y(ref s, targetPlayer) = (byte)targetY;
        }

        // ---- Turn and judgement ----
        public static void CheckWin(ref GameState s)
        {
            if (s.winner != 0) return;
            if (s.p1Hp == 0) s.winner = 1;
            else if (s.p0Hp == 0) s.winner = 2;
        }

        /// 항복. 자기 턴이 아니어도 낼 수 있다. 이미 끝난 판은 건드리지 않는다.
        /// 승자 표기는 접속이 끊겼을 때와 같다 — 남은 쪽이 이긴다.
        public static bool Surrender(ref GameState s, int player)
        {
            if (!IsPlayer(player) || s.winner != 0) return false;
            s.winner = (byte)(2 - player);
            return true;
        }

        public static void EndTurn(ref GameState s)
        {
            if (s.winner != 0) return;
            CheckWin(ref s);
            if (s.winner != 0) return;

            int current = s.turnPlayer;
            WorldEffectSystem.ResolveTurnEnd(ref s, current);
            CheckWin(ref s);
            if (s.winner != 0) return;

            BaseMove(ref s, current) = 0;
            DropMoveCards(ref s, current);   // 미사용 이동 카드는 사라진다
            TickOwnerTags(ref s, current);
            LightningStack(ref s, current) = 0;

            // 라운드는 후공까지 끝나야 채워진다. 제한 라운드는 그 마지막 행동 턴에 닫는다.
            if (s.turnCount >= MaxTurns * 2)
            {
                Judge(ref s);
                return;
            }

            int next = 1 - current;
            s.turnCount++;
            s.turnPlayer = (byte)next;
            int round = Round(s.turnCount);
            s.actionLeft = (byte)CostFor(round);

            WorldEffectSystem.ResolveTurnStart(ref s, next);
            if (s.winner != 0) return;

            bool locked = MoveLocked(ref s, next) != 0 || HasTag(ref s, next, PlayerTagId.MoveLocked);
            BaseMove(ref s, next) = locked ? (byte)0 : (byte)1;
            MoveLocked(ref s, next) = 0;
            if (!locked) GiveMoveCard(ref s, next);   // 얼음에 묶였으면 이동 카드도 못 받는다
            ApplyTurnStartTags(ref s, next);
            DrawCards(ref s, next, DrawFor(round) + SupplyDrawBonus(ref s, next));
        }

        static void ApplyTurnStartTags(ref GameState s, int player)
        {
            if (GetTag(ref s, player, PlayerTagId.Regeneration, out var regen))
                HealPlayer(ref s, player, regen.Value);
        }

        static int SupplyDrawBonus(ref GameState s, int player)
        {
            return GetTag(ref s, player, PlayerTagId.Supply, out var supply) ? supply.Value : 0;
        }

        /// 제한 라운드까지 둘 다 살아남으면 체력이 높은 쪽이 이긴다. 무승부는 없어서
        /// 체력이 같으면 후공(P2)이 가져간다.
        /// 체력이 0이 된 경우는 여기 오기 전에 CheckWin 이 이미 승자를 정한다.
        static void Judge(ref GameState s)
        {
            s.winner = s.p0Hp > s.p1Hp ? (byte)1 : (byte)2;
        }

        static bool IsOpponentTarget(ref GameState s, int player, int x, int y)
        {
            int other = 1 - player;
            return X(ref s, other) == x && Y(ref s, other) == y;
        }

        static bool IsOccupiedByOther(ref GameState s, int player, int x, int y)
        {
            return IsOpponentTarget(ref s, player, x, y);
        }

        static int OtherPlayerAt(ref GameState s, int x, int y)
        {
            int other = 1;
            if (X(ref s, other) == x && Y(ref s, other) == y) return other;
            if (X(ref s, 0) == x && Y(ref s, 0) == y) return 0;
            return -1;
        }

        static bool IsPlayer(int player) => player == 0 || player == 1;
    }
}
