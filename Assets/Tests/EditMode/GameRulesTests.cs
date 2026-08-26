using NUnit.Framework;

namespace SpellThrower.Tests
{
    public class GameRulesTests
    {
        GameState New() => GameRules.NewGame(12345);

        static int GiveCard(ref GameState s, int player, CardId card)
        {
            GameRules.Hand(ref s, player).Add((byte)card);
            return GameRules.Hand(ref s, player).Length - 1;
        }

        /// 이동 카드는 덱 밖에서 매 턴 지급되므로 장수 계산에서 뺀다.
        static int NormalCount(ref GameState s, int player)
        {
            ref var hand = ref GameRules.Hand(ref s, player);
            int count = 0;
            for (int i = 0; i < hand.Length; i++)
                if (hand[i] != (byte)CardId.Move) count++;
            return count;
        }

        static int IndexOfMove(ref GameState s, int player)
        {
            ref var hand = ref GameRules.Hand(ref s, player);
            for (int i = 0; i < hand.Length; i++)
                if (hand[i] == (byte)CardId.Move) return i;
            return -1;
        }

        [SetUp]
        public void Reset() => GameRules.MaxTurns = 20;

        /// MaxTurns 는 static 이라 값을 바꾼 채로 끝나면 다음 픽스처까지 오염시킨다.
        /// 1 로 낮춘 테스트 뒤에 EndTurn 을 쓰는 픽스처가 돌면 첫 턴에 판정이 나 버린다.
        [TearDown]
        public void RestoreTurnLimit() => GameRules.MaxTurns = GameRules.DefaultMaxTurns;

        [Test]
        public void Deck_MustHave25Cards_AndAtMost2Copies()
        {
            Assert.IsTrue(GameRules.IsValidDeck(Cards.DeckList));
            Assert.IsFalse(GameRules.IsValidDeck(new byte[24]));
            Assert.IsFalse(GameRules.IsValidDeck(new byte[25]));   // 파이어볼 25장

            var threeCopies = (byte[])Cards.DeckList.Clone();
            threeCopies[threeCopies.Length - 1] = threeCopies[0];  // 파이어볼 3장
            Assert.IsFalse(GameRules.IsValidDeck(threeCopies));
        }

        /// 듀얼을 신청하지만 2장 제한의 예외로 1장이다.
        [Test]
        public void Deck_AllowsOnlyOneDuelCard()
        {
            var one = (byte[])Cards.DeckList.Clone();
            one[one.Length - 1] = (byte)CardId.Duel;
            Assert.IsTrue(GameRules.IsValidDeck(one));

            var two = (byte[])Cards.DeckList.Clone();
            two[0] = (byte)CardId.Duel;
            two[two.Length - 1] = (byte)CardId.Duel;
            Assert.IsFalse(GameRules.IsValidDeck(two));
        }

        [Test]
        public void GameStartsWithThreeCards_AndBasicMoveForFirstPlayer()
        {
            var s = New();
            Assert.AreEqual(3, NormalCount(ref s, 0));
            Assert.AreEqual(3, NormalCount(ref s, 1));
            Assert.AreEqual(GameRules.DeckSize - 3, s.p0Deck.Length);
            Assert.AreEqual(1, s.p0BaseMove);
            Assert.AreEqual(0, s.p1BaseMove);
            Assert.AreEqual(GameRules.StartCost, s.actionLeft);
        }

        /// 첫 자기 턴 4에서 시작해 턴마다 +1, 상한 10.
        [Test]
        public void MaxCost_GrowsByOnePerOwnTurn_UpToTen()
        {
            var s = New();
            for (int round = 1; round <= 8; round++)
            {
                Assert.AreEqual(GameRules.CostFor(round), s.actionLeft, "P1 round " + round);
                GameRules.EndTurn(ref s);
                Assert.AreEqual(GameRules.CostFor(round), s.actionLeft, "P2 round " + round);
                GameRules.EndTurn(ref s);
            }
            Assert.AreEqual(GameRules.MaxCost, s.actionLeft);
        }

        /// 라운드는 양쪽 행동 턴이 모두 끝나야 1 오른다.
        [Test]
        public void Round_AdvancesOnlyAfterBothPlayersActed()
        {
            var s = New();
            Assert.AreEqual(1, GameRules.Round(s.turnCount));
            GameRules.EndTurn(ref s);
            Assert.AreEqual(1, GameRules.Round(s.turnCount));
            GameRules.EndTurn(ref s);
            Assert.AreEqual(2, GameRules.Round(s.turnCount));
        }

        [Test]
        public void MoveCard_ConsumesBasicMove_LeavesNoDiscard_AndUnusedOneVanishes()
        {
            var s = New();
            int idx = IndexOfMove(ref s, 0);
            Assert.GreaterOrEqual(idx, 0, "선공은 첫 턴에 이동 카드를 든다");

            int disc = s.p0Disc.Length;
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, idx, 3, 1));
            Assert.AreEqual(3, s.p0X);
            Assert.AreEqual(1, s.p0Y);
            Assert.AreEqual(0, s.p0BaseMove);                          // 기본 이동을 소모한다
            Assert.AreEqual(disc, s.p0Disc.Length);                    // 휴지통에 남지 않는다
            Assert.AreEqual(GameRules.StartCost, s.actionLeft);   // 코스트 0
            Assert.Less(IndexOfMove(ref s, 0), 0);

            GameRules.EndTurn(ref s);
            Assert.GreaterOrEqual(IndexOfMove(ref s, 1), 0, "다음 턴 플레이어가 새로 받는다");
            GameRules.EndTurn(ref s);
            Assert.Less(IndexOfMove(ref s, 1), 0, "안 쓴 이동 카드는 턴 끝에 사라진다");
        }

        [Test]
        public void BasicMove_MovesOneTileOnce_WithoutEnteringDiscard()
        {
            var s = New();
            int discard = s.p0Disc.Length;
            Assert.IsFalse(GameRules.TryMove(ref s, 0, 3, 2));
            Assert.IsTrue(GameRules.TryMove(ref s, 0, 3, 1));
            Assert.IsFalse(GameRules.TryMove(ref s, 0, 3, 2));
            Assert.AreEqual(0, s.p0BaseMove);
            Assert.AreEqual(discard, s.p0Disc.Length);
        }

        [Test]
        public void Sprint_IsIndependentTwoTileMove_AndCanBeUsedTwice()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            // 질주 두 장은 첫 턴 코스트 4로는 못 낸다. 카드 동작만 보려고 상한까지 채운다.
            s.actionLeft = (byte)GameRules.MaxCost;
            GiveCard(ref s, 0, CardId.Sprint);
            GiveCard(ref s, 0, CardId.Sprint);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 3, 2));
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 3, 4));
            Assert.AreEqual(3, s.p0X);
            Assert.AreEqual(4, s.p0Y);
            Assert.AreEqual(2, s.p0Disc.Length);
            Assert.AreEqual(GameRules.MaxCost - 2 * Cards.Get((byte)CardId.Sprint).Cost, s.actionLeft);
        }

        [Test]
        public void ActionPoints_NotCardCount_LimitUsage()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            // 카드 장수가 아니라 행동력이 상한이다. 행동력을 다 쓸 만큼 쥐여 주고 한 장 더 넣는다.
            // 피해 카드로 세면 상대가 먼저 죽어 판이 끝나 버리므로 자기 대상 카드로 센다.
            int plays = GameRules.StartCost / Cards.Get((byte)CardId.Heal).Cost;
            for (int i = 0; i < plays + 1; i++) GiveCard(ref s, 0, CardId.Heal);
            for (int i = 0; i < plays; i++)
                Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 0, 0), "play " + i);
            Assert.IsFalse(GameRules.TryPlay(ref s, 0, 0, 0, 0));
            // 코스트가 딱 나눠떨어지지 않으면 잔돈이 남는다. 한 장을 더 낼 수 없다는 것이 규칙이다.
            Assert.Less(s.actionLeft, Cards.Get((byte)CardId.Heal).Cost);
        }

        [Test]
        public void EndTurn_KeepsHand_AndGrantsBasicMove_WithoutRedrawingRoundOne()
        {
            var s = New();
            int p1Before = NormalCount(ref s, 1);
            GameRules.EndTurn(ref s);
            Assert.AreEqual(1, s.turnPlayer);
            Assert.AreEqual(p1Before, NormalCount(ref s, 1));   // 1라운드 3장은 시작 시 이미 받았다
            Assert.AreEqual(1, s.p1BaseMove);
            Assert.AreEqual(0, s.p0BaseMove);
            Assert.AreEqual(GameRules.CostFor(1), s.actionLeft);

            int p0Before = NormalCount(ref s, 0);
            GameRules.EndTurn(ref s);
            Assert.AreEqual(p0Before + 1, NormalCount(ref s, 0));   // 2~14라운드는 1장
        }

        /// 1라운드 0장(시작 시 3장), 2~14라운드 1장, 15라운드부터 2장.
        [Test]
        public void TurnStartDraw_FollowsTheRoundBrackets()
        {
            Assert.AreEqual(0, GameRules.DrawFor(1));
            Assert.AreEqual(1, GameRules.DrawFor(2));
            Assert.AreEqual(1, GameRules.DrawFor(14));
            Assert.AreEqual(2, GameRules.DrawFor(15));
            Assert.AreEqual(2, GameRules.DrawFor(25));
        }

        [Test]
        public void FullHand_DrawsCardsIntoDiscard()
        {
            var s = New();
            while (NormalCount(ref s, 0) < GameRules.MaxHand) GiveCard(ref s, 0, CardId.Fireball);
            int deckBefore = s.p0Deck.Length;
            int discardBefore = s.p0Disc.Length;
            s.p0Hand.RemoveAt(s.p0Hand.Length - 1);
            int draw = GiveCard(ref s, 0, CardId.Draw);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, draw, 0, 0));
            Assert.AreEqual(GameRules.MaxHand, NormalCount(ref s, 0));
            Assert.AreEqual(deckBefore - 2, s.p0Deck.Length);
            Assert.AreEqual(discardBefore + 2, s.p0Disc.Length);
        }

        [Test]
        public void Draw_UsesRemainingDeck_ThenRecyclesDiscard()
        {
            var s = New();
            s.p0Hand.Clear();
            s.p0Deck.Clear();
            s.p0Disc.Clear();
            s.p0Deck.Add((byte)CardId.Fireball);
            s.p0Disc.Add((byte)CardId.Iceball);
            s.p0Disc.Add((byte)CardId.Wind);
            int draw = GiveCard(ref s, 0, CardId.Draw);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, draw, 0, 0));
            Assert.AreEqual(2, s.p0Hand.Length);
            Assert.AreEqual(0, s.p0Disc.Length);
            Assert.AreEqual(2, s.p0Deck.Length);
        }

        [Test]
        public void Divination_AddsTheSelectedCardAndDiscardsTheOtherTwo()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Clear();
            GameRules.Disc(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Wind);

            GameRules.Divination(ref s, 0, 1);

            Assert.That(GameRules.Hand(ref s, 0)[0], Is.EqualTo((byte)CardId.Iceball));
            Assert.That(GameRules.Disc(ref s, 0).Length, Is.EqualTo(2));
        }

        [Test]
        public void Divination_IgnoresSyntheticMoveCardWhenCheckingHandCapacity()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Clear();
            GameRules.Disc(ref s, 0).Clear();
            GameRules.Hand(ref s, 0).Add((byte)CardId.Move);
            for (int i = 0; i < GameRules.MaxHand - 1; i++)
                GiveCard(ref s, 0, CardId.Fireball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Fireball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Wind);

            GameRules.Divination(ref s, 0, 0);

            Assert.That(NormalCount(ref s, 0), Is.EqualTo(GameRules.MaxHand));
            Assert.That(IndexOfMove(ref s, 0), Is.GreaterThanOrEqualTo(0));
            Assert.That(GameRules.Hand(ref s, 0).Length, Is.EqualTo(GameRules.HandSlots));
            Assert.That(GameRules.Disc(ref s, 0).Length, Is.EqualTo(2));
            Assert.That(s.p0BurnSeq, Is.Zero);
        }

        [Test]
        public void Exchange_UsesTheRequestedPostRemovalHandIndex()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Clear();
            GameRules.Disc(ref s, 0).Clear();
            GameRules.Hand(ref s, 0).Add((byte)CardId.Exchange);
            GameRules.Hand(ref s, 0).Add((byte)CardId.Fireball);
            GameRules.Hand(ref s, 0).Add((byte)CardId.Iceball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Wind);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Heal);

            Assert.That(GameRules.TryPlay(ref s, 0, 0, 2, 0), Is.True);
            Assert.That(ContainsCard(GameRules.Disc(ref s, 0), CardId.Iceball), Is.True);
            Assert.That(ContainsCard(GameRules.Hand(ref s, 0), CardId.Fireball), Is.True);
        }

        [Test]
        public void Exchange_SkipsSyntheticMoveCardWhenUsingDefaultUiTarget()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Clear();
            GameRules.Disc(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Exchange);
            GiveCard(ref s, 0, CardId.Move);
            GiveCard(ref s, 0, CardId.Fireball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Heal);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Wind);

            Assert.That(GameRules.CanPlay(ref s, 0, 0, 0, 0), Is.True);
            Assert.That(GameRules.TryPlay(ref s, 0, 0, 0, 0), Is.True);
            Assert.That(IndexOfMove(ref s, 0), Is.GreaterThanOrEqualTo(0));
            Assert.That(ContainsCard(GameRules.Disc(ref s, 0), CardId.Move), Is.False);
            Assert.That(ContainsCard(GameRules.Disc(ref s, 0), CardId.Fireball), Is.True);
        }

        [Test]
        public void Exchange_ExecutionDefense_FallsBackFromMoveToNormalCard()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GameRules.Deck(ref s, 0).Clear();
            GameRules.Disc(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Move);
            GiveCard(ref s, 0, CardId.Fireball);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Heal);
            GameRules.Deck(ref s, 0).Add((byte)CardId.Wind);
            int handLength = s.p0Hand.Length;

            GameRules.Exchange(ref s, 0, 0);

            Assert.That(s.p0Hand.Length, Is.EqualTo(handLength + 1));
            Assert.That(IndexOfMove(ref s, 0), Is.EqualTo(0));
            Assert.That(ContainsCard(GameRules.Hand(ref s, 0), CardId.Fireball), Is.False);
            Assert.That(ContainsCard(GameRules.Disc(ref s, 0), CardId.Fireball), Is.True);
            Assert.That(ContainsCard(GameRules.Disc(ref s, 0), CardId.Move), Is.False);
        }

        static bool ContainsCard(Unity.Collections.FixedList64Bytes<byte> cards, CardId card)
        {
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] == (byte)card) return true;
            return false;
        }

        [Test]
        public void Death_JudgesWinnerImmediately()
        {
            var s = New();
            s.p1X = 3; s.p1Y = 1; s.p1Hp = 2;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Fireball);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 3, 1));
            Assert.AreEqual(0, s.p1Hp);
            Assert.AreEqual(1, s.winner);   // 턴 종료를 기다리지 않는다
        }

        [Test]
        public void SelfDeath_JudgesWinnerWithoutEndTurn()
        {
            var s = New();
            s.p0Hp = 1;
            GameRules.DamagePlayer(ref s, 0, 1);
            Assert.AreEqual(0, s.p0Hp);
            Assert.AreEqual(2, s.winner);
        }

        [Test]
        public void EndTurn_JudgesWinnerAfterTurnEndEffects()
        {
            var s = New();
            s.p0X = 3;
            s.p0Y = 1;
            s.p0Hp = 1;
            Assert.That(WorldEffectSystem.TryAddFireZone(ref s, 1, 3, 1, 1, 1), Is.True);

            GameRules.EndTurn(ref s);

            Assert.That(s.p0Hp, Is.Zero);
            Assert.That(s.winner, Is.EqualTo(2));
        }

        [Test]
        public void TurnLimit_GivesTheWinToWhoeverHasMoreHp()
        {
            GameRules.MaxTurns = 1;
            var s = New();
            s.p0Hp = 30;
            s.p1Hp = 1;

            GameRules.EndTurn(ref s);   // 선공이 끝나도 1라운드는 아직 안 끝났다
            Assert.That(s.winner, Is.Zero);
            GameRules.EndTurn(ref s);

            Assert.That(s.winner, Is.EqualTo(1));
            Assert.That(s.turnCount, Is.EqualTo(2));   // 제한 라운드의 마지막 행동 턴에서 멈춘다
        }

        /// 무승부는 없앴다. 동점은 후공이 가져간다.
        [Test]
        public void TurnLimit_BreaksAnHpTieInFavourOfTheSecondPlayer()
        {
            GameRules.MaxTurns = 1;
            var s = New();
            s.p0Hp = 20;
            s.p1Hp = 20;

            GameRules.EndTurn(ref s);
            GameRules.EndTurn(ref s);

            Assert.That(s.winner, Is.EqualTo(2));
        }

        [Test]
        public void TurnLimit_StillLetsADeathDecideAWinner()
        {
            GameRules.MaxTurns = 1;
            var s = New();
            s.p0X = 3;
            s.p0Y = 1;
            s.p0Hp = 1;
            Assert.That(WorldEffectSystem.TryAddFireZone(ref s, 1, 3, 1, 1, 1), Is.True);

            GameRules.EndTurn(ref s);   // 마지막 턴에 죽으면 양쪽 패배가 아니라 상대 승리

            Assert.That(s.p0Hp, Is.Zero);
            Assert.That(s.winner, Is.EqualTo(2));
        }

        [Test]
        public void TotalRegularCards_AreConserved()
        {
            var s = New();
            int total = NormalCount(ref s, 0) + s.p0Deck.Length + s.p0Disc.Length;
            Assert.AreEqual(GameRules.DeckSize, total);
        }

        [Test]
        public void Obstacles_BlockMovementAndLineOfSight()
        {
            var s = New();
            s.obstacles = 1UL << (1 * GameRules.Size + 3); // (3,1)
            Assert.IsFalse(GameRules.CanMove(ref s, 0, 3, 1));

            s.p1X = 3; s.p1Y = 2;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Fireball);
            Assert.IsFalse(GameRules.CanPlay(ref s, 0, 0, 3, 2));

            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Wind);
            Assert.IsFalse(GameRules.CanPlay(ref s, 0, 0, 3, 2));
        }

        [Test]
        public void TargetingEmptyTile_DoesNotDamageOpponent()
        {
            var s = New();
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.FlamePillar);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 2, 1));
            Assert.AreEqual(GameRules.MaxHp, s.p1Hp);
        }

        [Test]
        public void OverflowDraw_RecordsRevealedCard()
        {
            var s = New();
            while (NormalCount(ref s, 0) < GameRules.MaxHand) GiveCard(ref s, 0, CardId.Fireball);
            s.p0Hand.RemoveAt(s.p0Hand.Length - 1);
            int draw = GiveCard(ref s, 0, CardId.Draw);
            Assert.IsTrue(GameRules.TryPlay(ref s, 0, draw, 0, 0));
            Assert.AreNotEqual(byte.MaxValue, s.p0Burned);
            Assert.AreEqual(1, s.p0BurnSeq);
        }

        [Test]
        public void Lightning_CanTargetBehindObstacle()
        {
            var s = New();
            s.obstacles = 1UL << (1 * GameRules.Size + 3); // (3,1)
            s.p1X = 3; s.p1Y = 2;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Lightning);
            Assert.IsTrue(GameRules.CanPlay(ref s, 0, 0, 3, 2));
        }

        [Test]
        public void Ice_BlocksOpponentNextBasicMove_ForOneTurn()
        {
            var s = New();
            s.p1X = 3; s.p1Y = 2;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Iceball);

            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 3, 2));
            GameRules.EndTurn(ref s);
            Assert.AreEqual(0, s.p1BaseMove);

            GameRules.EndTurn(ref s);
            GameRules.EndTurn(ref s);
            Assert.AreEqual(1, s.p1BaseMove);
        }

        [Test]
        public void Wind_StopsBeforeObstacle()
        {
            var s = New();
            s.p1X = 3; s.p1Y = 2;
            s.obstacles = 1UL << (3 * GameRules.Size + 3); // (3,3)
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Wind);

            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 3, 2));
            Assert.AreEqual(2, s.p1Y);
        }

        [Test]
        public void Wind_PushesDiagonally_WhenTargetIsOnTheDiagonal()
        {
            var s = New();
            s.obstacles = 0;
            s.p0X = 3; s.p0Y = 2;
            s.p1X = 5; s.p1Y = 4;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Wind);

            Assert.IsTrue(GameRules.TryPlay(ref s, 0, 0, 5, 4));
            Assert.AreEqual(7, s.p1X);   // 대각으로 2칸
            Assert.AreEqual(6, s.p1Y);
        }

        [Test]
        public void Leap_GrantsOneCurrentTurnPass_AndPlainMovementDoesNotDamageTheJumpedUnit()
        {
            var s = New();
            s.p0X = 3; s.p0Y = 0;
            s.actionLeft = (byte)GameRules.MaxCost;   // 도약 + 질주는 첫 턴 코스트 4를 넘는다
            s.obstacles = 1UL << (1 * GameRules.Size + 3);
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Leap);
            GiveCard(ref s, 0, CardId.Sprint);

            Assert.That(GameRules.TryPlay(ref s, 0, 0, 0, 0), Is.True);
            Assert.That(GameRules.HasTag(ref s, 0, PlayerTagId.Leap), Is.True);
            Assert.That(GameRules.TryPlay(ref s, 0, 0, 3, 2), Is.True);
            Assert.That(s.p0Y, Is.EqualTo(2));
            Assert.That(GameRules.HasTag(ref s, 0, PlayerTagId.Leap), Is.False);
            Assert.That(GameRules.IsMapObstacle(ref s, 3, 1), Is.True);
        }

        [Test]
        public void Leap_CanPassOnlyOneBlockedTileInOneMovement()
        {
            var s = New();
            s.p0X = 3; s.p0Y = 0;
            s.obstacles = (1UL << (1 * GameRules.Size + 3)) |
                          (1UL << (3 * GameRules.Size + 3));
            GameRules.AddOrRefreshTag(ref s, 0, PlayerTagId.Leap, 1);

            Assert.That(GameRules.TryCardMove(ref s, 0, 3, 4, 4, false), Is.False);
            Assert.That(s.p0Y, Is.Zero);
            Assert.That(GameRules.HasTag(ref s, 0, PlayerTagId.Leap), Is.True);
        }

        [Test]
        public void Charge_StopsBeforeOpponentAndDealsTwelveDamage()
        {
            var s = New();
            s.p0X = 3; s.p0Y = 0;
            s.p1X = 3; s.p1Y = 2;
            GameRules.Hand(ref s, 0).Clear();
            GiveCard(ref s, 0, CardId.Charge);

            Assert.That(GameRules.TryPlay(ref s, 0, 0, 3, 2), Is.True);
            Assert.That(s.p0Y, Is.EqualTo(1));
            Assert.That(s.p1Hp, Is.EqualTo(GameRules.MaxHp - 12));
        }

        [Test]
        public void Charge_WithLeapPassesOpponentAndDealsTwoDamage()
        {
            var s = New();
            s.p0X = 3; s.p0Y = 0;
            s.p1X = 3; s.p1Y = 1;
            GameRules.AddOrRefreshTag(ref s, 0, PlayerTagId.Leap, 1);

            Assert.That(GameRules.TryCharge(ref s, 0, 3, 2), Is.True);
            Assert.That(s.p0Y, Is.EqualTo(2));
            Assert.That(s.p1Hp, Is.EqualTo(GameRules.MaxHp - 2));
            Assert.That(GameRules.HasTag(ref s, 0, PlayerTagId.Leap), Is.False);
        }

        [Test]
        public void DeckCyclesAcrossManyTurns_WithoutLosingCards()
        {
            GameRules.MaxTurns = 100;
            var s = New();
            for (int turn = 0; turn < 30; turn++) GameRules.EndTurn(ref s);

            Assert.Greater(s.p0BurnSeq + s.p1BurnSeq, 0);
            Assert.AreEqual(GameRules.DeckSize, NormalCount(ref s, 0) + s.p0Deck.Length + s.p0Disc.Length);
            Assert.AreEqual(GameRules.DeckSize, NormalCount(ref s, 1) + s.p1Deck.Length + s.p1Disc.Length);
        }
    }
}
