using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    public sealed class CardCatalogContractTests
    {
        static readonly string[] Names =
        {
            "파이어볼", "불사르기", "화염 기둥", "파이어 레인", "오의 ： 익스플로전",
            "아이스볼", "얼음 방벽", "냉기", "동상", "회귀 : 빙하기의 시작",
            "숨결", "바람", "흡인", "격돌", "발생 : 사이클론",
            "전류 방출", "썬더볼트", "낙뢰", "번개", "연부 ： 마스터 스파크",
            "회복", "온기", "재생", "정화", "부여 : 세례",
            "드로우", "점술", "묻고 더블로 가", "보급", "이 중에 하나는 쓸만하겠지",
            "질주", "단보", "돌진", "가속", "곡예 : 뛰어넘기",
            "토템 : 저격", "토템 : 경계", "토템 : 감지", "토템 : 축복", "토템 : 폭탄",
            "듀얼을 신청하지",
        };

        /// 코스트는 기획 DB(카드 정보2)가 원본이다. 값이 바뀌면 여기도 같이 바뀌어야 한다.
        /// 순서는 CardText.Names 와 동일하며, 기본 이동 카드(CardId.Move)는 제외한다.
        static readonly byte[] ExpectedCosts =
        {
            1, 4, 4, 5, 7,   // 화염
            1, 4, 2, 4, 7,   // 얼음
            2, 2, 2, 3, 5,   // 바람
            2, 3, 2, 4, 6,   // 번개
            3, 2, 4, 2, 5,   // 회복
            2, 2, 2, 3, 5,   // 드로우
            3, 2, 4, 3, 5,   // 질주
            3, 2, 4, 4, 6,   // 토템
            7,               // 듀얼을 신청하지
        };

        [Test]
        public void FinalCatalog_HasExactlyTheEightAttributesAndSpecialCard()
        {
            Assert.That(Cards.All, Is.Not.Null);
            // 41종 + 덱에 넣을 수 없는 기본 이동 카드 한 장.
            Assert.That(Cards.All.Length, Is.EqualTo(42));
            Assert.That(Cards.Get((byte)CardId.Move).Name, Is.EqualTo("이동"));
            Assert.That(Cards.Get((byte)CardId.Move).Cost, Is.EqualTo(0));
            Assert.That(ExpectedCosts.Length, Is.EqualTo((int)CardId.Move));
            for (byte id = 0; id < (byte)CardId.Move; id++)
            {
                var definition = Cards.Get(id);
                Assert.That(definition.Name, Is.EqualTo(Names[id]), "Card {0} name", id);
                Assert.That(definition.Cost, Is.EqualTo(ExpectedCosts[id]), "Card {0} cost", id);
                Assert.That(definition.Name, Is.Not.EqualTo("집중"));
                Assert.That(definition.Name, Is.Not.EqualTo("HOOK"));
            }
        }

        [Test]
        public void CardText_IsTheSharedNameAndDescriptionSource()
        {
            Assert.That(CardText.Names.Length, Is.EqualTo(Cards.All.Length));
            Assert.That(CardText.Descriptions.Length, Is.EqualTo(Cards.All.Length));

            for (byte id = 0; id < (byte)Cards.All.Length; id++)
            {
                var card = Cards.Get(id);
                Assert.That(card.Name, Is.EqualTo(CardText.GetName((CardId)id)));
                Assert.That(CardText.GetDescription((CardId)id, card), Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void CardText_CoversEveryAttributeValueAndTargetKind()
        {
            Assert.That(CardText.AttributeNames.Length,
                Is.EqualTo(System.Enum.GetValues(typeof(CardAttribute)).Length));
            Assert.That(CardText.ValueNames.Length,
                Is.EqualTo(System.Enum.GetValues(typeof(CardValueTier)).Length));
            Assert.That(CardText.TargetKindNames.Length,
                Is.EqualTo(System.Enum.GetValues(typeof(CardTargetKind)).Length));

            for (byte id = 0; id < (byte)Cards.All.Length; id++)
            {
                var stats = CardText.GetStats(Cards.Get(id));
                Assert.That(stats, Is.Not.Null.And.Not.Empty, "Card {0} stats", id);
                Assert.That(stats, Does.Not.Contain("{"), "Card {0} left a format hole", id);
            }

            var fireball = Cards.Get((byte)CardId.Fireball);
            Assert.That(CardText.GetStats(fireball), Does.Contain("화염"));
            // CardValueTier 는 등급이 아니라 밸류다. 표기가 등급으로 되돌아가지 않게 잠근다.
            Assert.That(CardText.GetStats(fireball), Does.Contain("밸류"));
            Assert.That(CardText.ValueNames, Has.None.Contains("급"));
            Assert.That(CardText.GetStats(fireball), Does.Contain(CardText.BlockedText));
            Assert.That(CardText.GetStats(Cards.Get((byte)CardId.Thunderbolt)), Does.Contain(CardText.ArcText));
            // 자신에게 쓰는 카드는 장애물 문구가 뜻이 없으므로 빠진다.
            var heal = CardText.GetStats(Cards.Get((byte)CardId.Heal));
            Assert.That(heal, Does.Not.Contain(CardText.BlockedText));
            Assert.That(heal, Does.Contain("자신"));
        }

        [Test]
        public void CardText_UsesCardInfoV2NamesAndRuleText()
        {
            Assert.That(CardText.GetName(CardId.Exchange), Is.EqualTo("묻고 더블로 가"));
            Assert.That(CardText.GetName(CardId.Harvest), Is.EqualTo("이 중에 하나는 쓸만하겠지"));
            Assert.That(CardText.GetName(CardId.Leap), Is.EqualTo("곡예 : 뛰어넘기"));

            Assert.That(CardText.GetDescription(CardId.Pull, Cards.Get((byte)CardId.Pull)),
                        Does.Contain("피해 6"));
            Assert.That(CardText.GetDescription(CardId.Cyclone, Cards.Get((byte)CardId.Cyclone)),
                        Does.Contain("피해 6"));
            Assert.That(CardText.GetDescription(CardId.Regeneration, Cards.Get((byte)CardId.Regeneration)),
                        Does.Contain("자기 턴 시작마다 HP 10을 2회"));
            Assert.That(CardText.GetDescription(CardId.Supply, Cards.Get((byte)CardId.Supply)),
                        Does.Contain("다음 자기 턴 시작 3회"));
            Assert.That(CardText.GetDescription(CardId.Harvest, Cards.Get((byte)CardId.Harvest)),
                        Does.Contain("코스트 변경 없음"));
            Assert.That(CardText.GetDescription(CardId.Guardian, Cards.Get((byte)CardId.Guardian)),
                        Does.Contain("2턴"));
            Assert.That(CardText.GetDescription(CardId.Blessing, Cards.Get((byte)CardId.Blessing)),
                        Does.Contain("자기 턴 시작 시 HP 14"));
            Assert.That(CardText.GetDescription(CardId.Leap, Cards.Get((byte)CardId.Leap)),
                        Does.Contain("장애물 또는 적 1개"));
            Assert.That(CardText.GetDescription(CardId.Detonation, Cards.Get((byte)CardId.Detonation)),
                        Does.Contain("자기 턴 종료 시"));
        }

        [Test]
        public void Metadata_MatchesThePrototypeNumbersInCardInfo2()
        {
            AssertCard(CardId.Fireball, 4, 5, 5, true, false);
            AssertCard(CardId.FireRain, 4, 12, 12, true, false);
            AssertCard(CardId.Explosion, 4, 16, 16, true, false);
            AssertCard(CardId.Iceball, 3, 3, 3, true, false);
            AssertCard(CardId.Discharge, 2, 10, 10, true, false);
            AssertCard(CardId.Thunderbolt, 6, 12, 12, true, true);
            AssertCard(CardId.MasterSpark, 100, 16, 16, true, true);
            AssertCard(CardId.Heal, 0, 16, 0, false, false);
            AssertCard(CardId.Regeneration, 0, 8, 0, false, false);
            AssertCard(CardId.Supply, 0, 1, 0, false, false);
            AssertCard(CardId.Duel, 0, 0, 0, false, false);
        }

        [Test]
        public void CardsUseOneSharedSfxPerAttribute()
        {
            for (int id = 0; id < 5; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardFire));
            for (int id = 5; id < 10; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardIce));
            for (int id = 10; id < 15; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardWind));
            for (int id = 15; id < 20; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardLightning));
            for (int id = 20; id < 25; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardHeal));
            for (int id = 25; id < 30; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardDraw));
            for (int id = 30; id < 35; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardSprint));
            for (int id = 35; id < 40; id++) Assert.That(Cards.Get((byte)id).SfxCue, Is.EqualTo(SfxId.CardTotem));
            Assert.That(Cards.Get((byte)CardId.Duel).SfxCue, Is.EqualTo(SfxId.CardSpecial));
        }

        [Test]
        public void DefaultDeckIsValidAgainstTheCurrentCatalog()
        {
            Assert.That(GameRules.IsValidDeck(Cards.DeckList), Is.True);
            foreach (byte card in Cards.DeckList)
                Assert.That(card, Is.LessThan(Cards.All.Length));
        }

        static void AssertCard(CardId id, int range, int power, int immediate, bool targeted, bool arc)
        {
            var definition = Cards.Get((byte)id);
            Assert.That(definition.Range, Is.EqualTo(range), "{0} range", id);
            Assert.That(definition.Power, Is.EqualTo(power), "{0} power", id);
            Assert.That(definition.ImmediateDamagePower, Is.EqualTo(immediate), "{0} immediate", id);
            Assert.That(definition.Targeted, Is.EqualTo(targeted), "{0} targeted", id);
            Assert.That(definition.Arc, Is.EqualTo(arc), "{0} arc", id);
        }
    }
}
