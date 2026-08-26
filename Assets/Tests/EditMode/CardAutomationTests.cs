using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellThrower;

namespace SpellThrower.Tests
{
    /// <summary>
    /// Deterministic, test-only scenarios for the complete current card catalog.
    /// The production rules and network transport are intentionally not changed;
    /// each case arranges a GameState and then drives the same GameRules.TryPlay
    /// and GameRules.EndTurn entry points used by NetGame.
    /// </summary>
    public sealed class CardAutomationTests
    {
        [Test]
        public void ScenarioTable_CoversEveryCatalogCardExactlyOnce()
        {
            var seen = new HashSet<byte>();
            var cases = BuildCases();

            foreach (var testCase in cases)
            {
                Assert.That(seen.Add((byte)testCase.Card), Is.True,
                    $"Duplicate scenario for {testCase.Card}");
                Assert.That(Cards.Get((byte)testCase.Card), Is.Not.Null,
                    $"No catalog definition for {testCase.Card}");
            }

            Assert.That(cases.Length, Is.EqualTo(Cards.All.Length));
            Assert.That(seen.Count, Is.EqualTo(Cards.All.Length));
        }

        [TestCaseSource(nameof(AllCardCases))]
        public void EveryCard_HasControlledUseAndExpectedResult(CardAutomationCase testCase)
        {
            testCase.Run(CardAutomationScenario.New());
        }

        [Test]
        public void FireZone_TriggersOnTheOpponentsTurnEnd()
        {
            var scenario = CardAutomationScenario.New();
            scenario.SetPosition(0, 1, 1);
            scenario.SetPosition(1, 3, 1);
            scenario.PlayAndAssert(CardId.Burn, 3, 1);

            scenario.EndTurn();
            Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp),
                "A fire zone must not tick at the caster's turn end");

            scenario.EndTurn();
            Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - GameRules.FireZoneTick));
            Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 3, 1, out var zone), Is.True);
            Assert.That(zone.RemainingTurns, Is.EqualTo(1));
        }

        [Test]
        public void FrostZone_SurvivesTheCreationTurnAndExpiresAtRoundBoundary()
        {
            var scenario = CardAutomationScenario.New();
            scenario.SetPosition(0, 1, 1);
            scenario.SetPosition(1, 3, 1);
            scenario.PlayAndAssert(CardId.Chill, 3, 1);

            scenario.EndTurn();
            Assert.That(scenario.TryGetEffect(WorldEffectKind.FrostZone, 3, 1, out var frost), Is.True);
            Assert.That(frost.RemainingTurns, Is.EqualTo(1));

            scenario.EndTurn();
            Assert.That(scenario.TryGetEffect(WorldEffectKind.FrostZone, 3, 1, out _), Is.False);
        }

        [Test]
        public void LightningStack_ResetsWhenTheCurrentPlayerEndsTheirTurn()
        {
            var scenario = CardAutomationScenario.New();
            scenario.SetPosition(0, 1, 1);
            scenario.SetPosition(1, 5, 1);
            scenario.PlayAndAssert(CardId.LightningStrike, 5, 1);
            Assert.That(scenario.LightningStack(0), Is.EqualTo(1));

            scenario.EndTurn();
            Assert.That(scenario.LightningStack(0), Is.Zero);
        }

        [Test]
        public void Wind_CarriesFireZoneEffectsAlongItsPushPath()
        {
            var scenario = CardAutomationScenario.New();
            scenario.SetPosition(0, 1, 1);
            scenario.SetPosition(1, 3, 1);
            Assert.That(scenario.AddFireZone(0, 4, 1, GameRules.FireZoneTick, 1), Is.True);

            scenario.PlayAndAssert(CardId.Wind, 3, 1);

            Assert.That(scenario.Position(1), Is.EqualTo((5, 1)));
            Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 4, 1, out _), Is.True);
            Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 5, 1, out _), Is.True);
        }

        static IEnumerable<TestCaseData> AllCardCases()
        {
            foreach (var testCase in BuildCases())
                yield return new TestCaseData(testCase).SetName($"CardAutomation_{testCase.Card}");
        }

        static CardAutomationCase[] BuildCases()
        {
            return new[]
            {
                Case(CardId.Fireball, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Fireball, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 5));
                }),
                Case(CardId.Burn, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Burn, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp));
                    scenario.AssertEffect(WorldEffectKind.FireZone, 3, 1, GameRules.FireZoneTick, 2);
                }),
                Case(CardId.FlamePillar, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.FlamePillar, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                    scenario.AssertEffect(WorldEffectKind.FireZone, 3, 1, GameRules.FireZoneTick, 2);
                }),
                Case(CardId.FireRain, scenario =>
                {
                    scenario.SetPosition(0, 1, 3);
                    scenario.SetPosition(1, 4, 3);
                    scenario.PlayAndAssert(CardId.FireRain, 3, 3);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                    scenario.AssertEffect(WorldEffectKind.FireZone, 3, 3, GameRules.FireZoneTick, 2);
                }),
                Case(CardId.Explosion, scenario =>
                {
                    scenario.SetPosition(0, 1, 4);
                    scenario.SetPosition(1, 4, 4);
                    scenario.PlayAndAssert(CardId.Explosion, 4, 4);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 16));
                    Assert.That(scenario.CountEffects(WorldEffectKind.FireZone), Is.EqualTo(9));
                    scenario.AssertEffect(WorldEffectKind.FireZone, 4, 4, 8, 2);
                }),

                Case(CardId.Iceball, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Iceball, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 3));
                    Assert.That(scenario.HasTag(1, PlayerTagId.MoveLocked), Is.True);
                    Assert.That(scenario.MoveLocked(1), Is.EqualTo(1));
                }),
                Case(CardId.IceWall, scenario =>
                {
                    scenario.SetPosition(0, 1, 3);
                    scenario.PlayAndAssert(CardId.IceWall, 3, 3);
                    Assert.That(scenario.CountStructures(StructureKind.IceWall), Is.EqualTo(3));
                    Assert.That(scenario.IsBlocked(3, 3), Is.True);
                    scenario.AssertStructure(3, 3, StructureKind.IceWall, 20);
                }),
                Case(CardId.Chill, scenario =>
                {
                    scenario.SetPosition(0, 1, 3);
                    scenario.SetPosition(1, 3, 3);
                    scenario.PlayAndAssert(CardId.Chill, 3, 3);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 8));
                    scenario.AssertEffect(WorldEffectKind.FrostZone, 3, 3, 0, 1);
                }),
                Case(CardId.Frostbite, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Frostbite, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 15));
                    scenario.AssertEffect(WorldEffectKind.FrostZone, 3, 1, 0, 2);
                }),
                Case(CardId.IceAge, scenario =>
                {
                    scenario.SetPosition(0, 1, 4);
                    scenario.SetPosition(1, 4, 4);
                    scenario.PlayAndAssert(CardId.IceAge, 4, 4);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 18));
                    Assert.That(scenario.CountEffects(WorldEffectKind.FrostZone), Is.EqualTo(9));
                }),

                Case(CardId.Breath, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Breath, 3, 1);
                    Assert.That(scenario.Position(1), Is.EqualTo((4, 1)));
                    Assert.That(scenario.Position(0), Is.EqualTo((1, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                }),
                Case(CardId.Wind, scenario =>
                {
                    scenario.SetPosition(0, 2, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Wind, 4, 1);
                    Assert.That(scenario.Position(1), Is.EqualTo((6, 1)));
                    Assert.That(scenario.Position(0), Is.EqualTo((1, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                }),
                Case(CardId.Pull, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Pull, 4, 1);
                    Assert.That(scenario.Position(1), Is.EqualTo((2, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                }),
                Case(CardId.Collision, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.AddObstacle(4, 1);
                    scenario.PlayAndAssert(CardId.Collision, 3, 1);
                    Assert.That(scenario.Position(1), Is.EqualTo((3, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                    Assert.That(scenario.ObstacleHp(4, 1), Is.Zero);
                }),
                Case(CardId.Cyclone, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Cyclone, 3, 1);
                    Assert.That(scenario.Position(1), Is.EqualTo((6, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                }),

                Case(CardId.Discharge, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Discharge, 3, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 10));
                    Assert.That(scenario.LightningStack(0), Is.EqualTo(1));
                }),
                Case(CardId.Thunderbolt, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 5, 1);
                    scenario.AddObstacle(3, 1);
                    scenario.PlayAndAssert(CardId.Thunderbolt, 5, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                    Assert.That(scenario.LightningStack(0), Is.EqualTo(1));
                    Assert.That(scenario.ObstacleHp(3, 1), Is.EqualTo(GameRules.DefaultMapObstacleHp));
                }),
                Case(CardId.LightningStrike, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 5, 1);
                    scenario.PlayAndAssert(CardId.LightningStrike, 5, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                    Assert.That(scenario.LightningStack(0), Is.EqualTo(1));
                }),
                Case(CardId.Lightning, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 6, 1);
                    scenario.PlayAndAssert(CardId.Lightning, 6, 1);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                    Assert.That(scenario.LightningStack(0), Is.EqualTo(1));
                }),
                Case(CardId.MasterSpark, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 1, 6);
                    Assert.That(scenario.AddStructure(0, StructureKind.Totem, 1, 3, 3), Is.True);
                    scenario.PlayAndAssert(CardId.MasterSpark, 1, 7);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 16));
                    Assert.That(scenario.LightningStack(0), Is.EqualTo(1));
                    Assert.That(scenario.TryGetStructure(1, 3, out _), Is.False);
                }),

                Case(CardId.Heal, scenario =>
                {
                    scenario.SetHealth(0, 10);
                    scenario.PlayAndAssert(CardId.Heal, 0, 0);
                    Assert.That(scenario.Health(0), Is.EqualTo(26));
                }),
                Case(CardId.Warmth, scenario =>
                {
                    scenario.SetHealth(0, 10);
                    scenario.PlayAndAssert(CardId.Warmth, 0, 0);
                    Assert.That(scenario.Health(0), Is.EqualTo(20));
                }),
                Case(CardId.Regeneration, scenario =>
                {
                    scenario.SetHealth(0, 10);
                    scenario.PlayAndAssert(CardId.Regeneration, 0, 0);
                    Assert.That(scenario.Health(0), Is.EqualTo(18));
                    scenario.AssertTag(0, PlayerTagId.Regeneration, 3, GameRules.RegenerationTick);
                    scenario.EndRoundForPlayerZero();
                    Assert.That(scenario.Health(0), Is.EqualTo(28));
                }),
                Case(CardId.Purify, scenario =>
                {
                    scenario.SetPosition(0, 3, 3);
                    Assert.That(scenario.AddFireZone(1, 3, 3, GameRules.FireZoneTick, 2), Is.True);
                    Assert.That(scenario.AddFireZone(1, 4, 3, GameRules.FireZoneTick, 2), Is.True);
                    Assert.That(scenario.AddFrostZone(1, 3, 4, 2), Is.True);
                    Assert.That(scenario.AddFireZone(1, 5, 3, GameRules.FireZoneTick, 2), Is.True);
                    Assert.That(scenario.AddStructure(1, StructureKind.Totem, 3, 5, 3), Is.True);

                    scenario.PlayAndAssert(CardId.Purify, 0, 0);
                    Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 3, 3, out _), Is.False);
                    Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 4, 3, out _), Is.False);
                    Assert.That(scenario.TryGetEffect(WorldEffectKind.FrostZone, 3, 4, out _), Is.False);
                    Assert.That(scenario.TryGetEffect(WorldEffectKind.FireZone, 5, 3, out _), Is.True);
                    Assert.That(scenario.TryGetStructure(3, 5, out _), Is.True);
                }),
                Case(CardId.Baptism, scenario =>
                {
                    scenario.SetPosition(0, 3, 3);
                    scenario.SetHealth(0, 10);
                    Assert.That(scenario.AddFireZone(1, 3, 3, GameRules.FireZoneTick, 2), Is.True);
                    Assert.That(scenario.AddFrostZone(1, 4, 3, 1), Is.True);

                    scenario.PlayAndAssert(CardId.Baptism, 0, 0);
                    Assert.That(scenario.Health(0), Is.EqualTo(26));
                    Assert.That(scenario.CountEffects(WorldEffectKind.FireZone), Is.Zero);
                    Assert.That(scenario.CountEffects(WorldEffectKind.FrostZone), Is.Zero);
                }),

                Case(CardId.Draw, scenario =>
                {
                    scenario.AddDeck(0, CardId.Fireball);
                    scenario.AddDeck(0, CardId.Iceball);
                    scenario.PlayAndAssert(CardId.Draw, 0, 0);
                    Assert.That(scenario.InHand(0, CardId.Fireball), Is.True);
                    Assert.That(scenario.InHand(0, CardId.Iceball), Is.True);
                }),
                Case(CardId.Divination, scenario =>
                {
                    scenario.AddDeck(0, CardId.Fireball);
                    scenario.AddDeck(0, CardId.Iceball);
                    scenario.AddDeck(0, CardId.Wind);
                    scenario.PlayAndAssert(CardId.Divination, 1, 0);
                    Assert.That(scenario.InHand(0, CardId.Iceball), Is.True);
                    Assert.That(scenario.InHand(0, CardId.Fireball), Is.False);
                    Assert.That(scenario.InHand(0, CardId.Wind), Is.False);
                    Assert.That(scenario.DiscardCount(0), Is.EqualTo(3));
                }),
                Case(CardId.Exchange, scenario =>
                {
                    scenario.ClearHand(0);
                    scenario.AddCard(0, CardId.Exchange);
                    scenario.AddCard(0, CardId.Fireball);
                    scenario.AddDeck(0, CardId.Iceball);
                    scenario.AddDeck(0, CardId.Wind);
                    Assert.That(scenario.PlayFirst(1, 0), Is.True);
                    Assert.That(scenario.InHand(0, CardId.Fireball), Is.False);
                    Assert.That(scenario.InHand(0, CardId.Iceball), Is.True);
                    Assert.That(scenario.InHand(0, CardId.Wind), Is.True);
                    Assert.That(scenario.InDiscard(0, CardId.Exchange), Is.True);
                    Assert.That(scenario.InDiscard(0, CardId.Fireball), Is.True);
                }),
                Case(CardId.Supply, scenario =>
                {
                    for (int i = 0; i < 6; i++) scenario.AddDeck(0, CardId.Fireball);
                    scenario.PlayAndAssert(CardId.Supply, 0, 0);
                    scenario.AssertTag(0, PlayerTagId.Supply, 4, 1);
                    scenario.EndRoundForPlayerZero();
                    Assert.That(scenario.DeckCount(0), Is.EqualTo(4));
                    scenario.AssertTag(0, PlayerTagId.Supply, 3, 1);
                }),
                Case(CardId.Harvest, scenario =>
                {
                    scenario.AddDeck(0, CardId.Fireball);
                    scenario.AddDeck(0, CardId.Iceball);
                    scenario.AddDeck(0, CardId.Wind);
                    scenario.PlayAndAssert(CardId.Harvest, 0, 0);
                    Assert.That(scenario.HandCount(0), Is.EqualTo(3));
                }),

                Case(CardId.Sprint, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.PlayAndAssert(CardId.Sprint, 3, 1);
                    Assert.That(scenario.Position(0), Is.EqualTo((3, 1)));
                }),
                Case(CardId.Step, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.PlayAndAssert(CardId.Step, 2, 1);
                    Assert.That(scenario.Position(0), Is.EqualTo((2, 1)));
                }),
                Case(CardId.Charge, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 3, 1);
                    scenario.PlayAndAssert(CardId.Charge, 3, 1);
                    Assert.That(scenario.Position(0), Is.EqualTo((2, 1)));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                }),
                Case(CardId.Acceleration, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.PlayAndAssert(CardId.Acceleration, 0, 0);
                    scenario.AssertTag(0, PlayerTagId.MoveBoost, 1, 1);
                    scenario.AddCard(0, CardId.Sprint);
                    Assert.That(scenario.PlayFirst(4, 1), Is.True);
                    Assert.That(scenario.Position(0), Is.EqualTo((4, 1)));
                }),
                Case(CardId.Leap, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.AddObstacle(2, 1);
                    scenario.PlayAndAssert(CardId.Leap, 0, 0);
                    scenario.AssertTag(0, PlayerTagId.Leap, 1, 0);
                    scenario.AddCard(0, CardId.Sprint);
                    Assert.That(scenario.PlayFirst(3, 1), Is.True);
                    Assert.That(scenario.Position(0), Is.EqualTo((3, 1)));
                    Assert.That(scenario.HasTag(0, PlayerTagId.Leap), Is.False);
                }),

                Case(CardId.Totem, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Totem, 3, 1);
                    scenario.AssertStructure(3, 1, StructureKind.Totem, 20);
                    scenario.EndTurn();
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 8));
                }),
                Case(CardId.Guardian, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Guardian, 3, 1);
                    scenario.EndTurn();
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 6));
                }),
                Case(CardId.Thorn, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Thorn, 3, 1);
                    scenario.EndTurn();
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp));
                    scenario.EndTurn();
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 12));
                }),
                Case(CardId.Blessing, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetHealth(0, 10);
                    scenario.PlayAndAssert(CardId.Blessing, 3, 1);
                    scenario.EndRoundForPlayerZero();
                    Assert.That(scenario.Health(0), Is.EqualTo(24));
                }),
                Case(CardId.Detonation, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.SetPosition(1, 4, 1);
                    scenario.PlayAndAssert(CardId.Detonation, 3, 1);
                    Assert.That(scenario.DamageStructure(3, 1, 20, 1), Is.True);
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp - 16));
                    Assert.That(scenario.TryGetStructure(3, 1, out _), Is.False);
                }),

                Case(CardId.Duel, scenario =>
                {
                    scenario.ClearHand(0);
                    scenario.ClearHand(1);
                    scenario.AddCard(0, CardId.Duel);
                    scenario.AddCard(0, CardId.Fireball);
                    scenario.AddCard(1, CardId.Lightning);
                    Assert.That(scenario.PlayFirst(0, 0), Is.True);
                    Assert.That(scenario.Health(0), Is.EqualTo(GameRules.MaxHp - 6));
                    Assert.That(scenario.Health(1), Is.EqualTo(GameRules.MaxHp));
                    Assert.That(scenario.InDiscard(0, CardId.Duel), Is.True);
                }),
                Case(CardId.Move, scenario =>
                {
                    scenario.SetPosition(0, 1, 1);
                    scenario.ClearHand(0);
                    scenario.AddCard(0, CardId.Move);
                    Assert.That(scenario.PlayFirst(2, 1), Is.True);
                    Assert.That(scenario.Position(0), Is.EqualTo((2, 1)));
                    Assert.That(scenario.BaseMove(0), Is.Zero);
                    Assert.That(scenario.InDiscard(0, CardId.Move), Is.False);
                }),
            };
        }

        static CardAutomationCase Case(CardId card, Action<CardAutomationScenario> run)
        {
            return new CardAutomationCase(card, run);
        }
    }

    public sealed class CardAutomationCase
    {
        readonly Action<CardAutomationScenario> _run;

        public CardId Card { get; }

        public CardAutomationCase(CardId card, Action<CardAutomationScenario> run)
        {
            Card = card;
            _run = run;
        }

        public void Run(CardAutomationScenario scenario)
        {
            try
            {
                _run(scenario);
            }
            catch (AssertionException exception)
            {
                throw new AssertionException($"{Card}: {exception.Message}");
            }
        }
    }

    public sealed class CardAutomationScenario
    {
        GameState _state;

        CardAutomationScenario(GameState state)
        {
            _state = state;
        }

        public static CardAutomationScenario New()
        {
            var state = GameRules.NewGame(0x5A17C0DEu);
            state.obstacles = 0;
            state.obstacleHp.Clear();
            state.worldEffects.Clear();
            state.nextWorldEffectSequence = 0;
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
            return new CardAutomationScenario(state);
        }

        public void SetPosition(int player, int x, int y)
        {
            Assert.That(GameRules.InBounds(x, y), Is.True, $"Invalid position ({x},{y})");
            GameRules.X(ref _state, player) = (byte)x;
            GameRules.Y(ref _state, player) = (byte)y;
        }

        public (int x, int y) Position(int player)
        {
            return (GameRules.X(ref _state, player), GameRules.Y(ref _state, player));
        }

        public void SetHealth(int player, int health)
        {
            Assert.That(health, Is.InRange(0, GameRules.MaxHp));
            GameRules.Hp(ref _state, player) = (byte)health;
            _state.winner = 0;
        }

        public int Health(int player) => GameRules.Hp(ref _state, player);

        public int MoveLocked(int player) => GameRules.MoveLocked(ref _state, player);

        public int BaseMove(int player) => GameRules.BaseMove(ref _state, player);

        public int LightningStack(int player) => GameRules.LightningStack(ref _state, player);

        public void ClearHand(int player) => GameRules.Hand(ref _state, player).Clear();

        public void AddCard(int player, CardId card) => GameRules.Hand(ref _state, player).Add((byte)card);

        public void AddDeck(int player, CardId card) => GameRules.Deck(ref _state, player).Add((byte)card);

        public int HandCount(int player) => GameRules.Hand(ref _state, player).Length;

        public int DeckCount(int player) => GameRules.Deck(ref _state, player).Length;

        public int DiscardCount(int player) => GameRules.Disc(ref _state, player).Length;

        public bool InHand(int player, CardId card) => Contains(ref GameRules.Hand(ref _state, player), card);

        public bool InDiscard(int player, CardId card) => Contains(ref GameRules.Disc(ref _state, player), card);

        public bool Play(CardId card, int targetX, int targetY)
        {
            ClearHand(0);
            AddCard(0, card);
            return PlayFirst(targetX, targetY);
        }

        public void PlayAndAssert(CardId card, int targetX, int targetY)
        {
            Assert.That(Play(card, targetX, targetY), Is.True, $"{card} could not be played");
            Assert.That(_state.lastActionCardId, Is.EqualTo((byte)card),
                $"{card} was not recorded as the last card action");
            if (card != CardId.Move)
                Assert.That(InDiscard(0, card), Is.True, $"{card} was not discarded after use");
        }

        public bool PlayFirst(int targetX, int targetY)
        {
            return GameRules.TryPlay(ref _state, 0, 0, targetX, targetY);
        }

        public bool HasTag(int player, PlayerTagId tagId) => GameRules.HasTag(ref _state, player, tagId);

        public void AssertTag(int player, PlayerTagId tagId, int duration, int value)
        {
            Assert.That(GameRules.GetTag(ref _state, player, tagId, out var tag), Is.True,
                $"Missing tag {tagId} on player {player}");
            Assert.That(tag.DurationTurns, Is.EqualTo(duration));
            Assert.That(tag.Value, Is.EqualTo(value));
        }

        public bool AddObstacle(int x, int y, byte hp = GameRules.DefaultMapObstacleHp)
        {
            if (!GameRules.InBounds(x, y)) return false;
            int index = y * GameRules.Size + x;
            _state.obstacles |= 1UL << index;
            ref var obstacleHp = ref GameRules.ObstacleHp(ref _state);
            while (obstacleHp.Length <= index) obstacleHp.Add(0);
            obstacleHp[index] = hp;
            return true;
        }

        public bool IsBlocked(int x, int y) => GameRules.IsBlocked(ref _state, x, y);

        public int ObstacleHp(int x, int y) => GameRules.MapObstacleHp(ref _state, x, y);

        public bool AddFireZone(int sourcePlayer, int x, int y, byte power, byte ticks)
        {
            return WorldEffectSystem.TryAddFireZone(ref _state, sourcePlayer, x, y, power, ticks);
        }

        public bool AddFrostZone(int sourcePlayer, int x, int y, byte turns)
        {
            return WorldEffectSystem.TryAddFrostZone(ref _state, sourcePlayer, x, y, turns);
        }

        public bool AddStructure(int sourcePlayer, StructureKind kind, int x, int y, byte hp)
        {
            return WorldEffectSystem.TryAddStructure(ref _state, sourcePlayer, kind, x, y, hp, 2);
        }

        public bool DamageStructure(int x, int y, int amount, int sourcePlayer)
        {
            return WorldEffectSystem.DamageStructureAt(ref _state, x, y, amount, sourcePlayer);
        }

        public int CountEffects(WorldEffectKind kind)
        {
            int count = 0;
            for (int i = 0; i < WorldEffectSystem.Count(ref _state); i++)
            {
                Assert.That(WorldEffectSystem.TryGet(ref _state, i, out var effect), Is.True);
                if (effect.Kind == kind) count++;
            }
            return count;
        }

        public int CountStructures(StructureKind kind)
        {
            int count = 0;
            for (int i = 0; i < WorldEffectSystem.Count(ref _state); i++)
            {
                Assert.That(WorldEffectSystem.TryGet(ref _state, i, out var effect), Is.True);
                if (effect.Kind == WorldEffectKind.Structure && effect.Structure == kind) count++;
            }
            return count;
        }

        public bool TryGetEffect(WorldEffectKind kind, int x, int y, out WorldEffectRecord effect)
        {
            return WorldEffectSystem.TryGetTileEffect(ref _state, kind, x, y, out effect);
        }

        public bool TryGetStructure(int x, int y, out WorldEffectRecord structure)
        {
            return WorldEffectSystem.TryGetStructureAt(ref _state, x, y, out structure);
        }

        public void AssertEffect(WorldEffectKind kind, int x, int y, int power, int remainingTurns)
        {
            Assert.That(TryGetEffect(kind, x, y, out var effect), Is.True,
                $"Missing {kind} at ({x},{y})");
            if (power > 0) Assert.That(effect.Power, Is.EqualTo(power));
            Assert.That(effect.RemainingTurns, Is.EqualTo(remainingTurns));
        }

        public void AssertStructure(int x, int y, StructureKind kind, int hp)
        {
            Assert.That(TryGetStructure(x, y, out var structure), Is.True,
                $"Missing {kind} at ({x},{y})");
            Assert.That(structure.Structure, Is.EqualTo(kind));
            Assert.That(structure.Power, Is.EqualTo(hp));
        }

        public void EndTurn() => GameRules.EndTurn(ref _state);

        public void EndRoundForPlayerZero()
        {
            Assert.That(_state.turnPlayer, Is.EqualTo(0));
            EndTurn();
            Assert.That(_state.turnPlayer, Is.EqualTo(1));
            EndTurn();
            Assert.That(_state.turnPlayer, Is.EqualTo(0));
        }

        static bool Contains(ref Unity.Collections.FixedList64Bytes<byte> list, CardId card)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i] == (byte)card) return true;
            return false;
        }
    }
}
