using NUnit.Framework;
using SpellThrower;
using Unity.Collections;
using Unity.Netcode;

namespace SpellThrower.Tests
{
    public sealed class GameStateSerializationContractTests
    {
        [Test]
        public void GameState_MemcpyRoundTrip_PreservesActionEffectsAndPlayerState()
        {
            var source = GameRules.NewGame(0x13579BDFu);
            source.foeLeft = 1;
            source.p0Name = new FixedString32Bytes("Alpha");
            source.p1Name = new FixedString32Bytes("Beta");
            source.lastActionKind = GameplayActionKind.CardUsed;
            source.lastActionPlayer = 1;
            source.lastActionCardId = (byte)CardId.Duel;
            source.lastActionSequence = 17;
            source.obstacleHp[1 * GameRules.Size + 0] = 2;

            Assert.That(GameRules.AddOrRefreshTag(ref source, 0, PlayerTagId.Regeneration, 3, 2), Is.True);
            Assert.That(WorldEffectSystem.TryAddFireZone(ref source, 0, 3, 1, 4, 3), Is.True);
            Assert.That(WorldEffectSystem.TryScheduleTeleport(ref source, 1, 2, 2, 2), Is.True);

            var writer = new FastBufferWriter(4096, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(source);

                var reader = new FastBufferReader(writer, Allocator.Temp);
                try
                {
                    reader.ReadValueSafe(out GameState received);

                    Assert.That(received.foeLeft, Is.EqualTo(source.foeLeft));
                    Assert.That(received.p0Name.ToString(), Is.EqualTo(source.p0Name.ToString()));
                    Assert.That(received.p1Name.ToString(), Is.EqualTo(source.p1Name.ToString()));
                    Assert.That(received.lastActionKind, Is.EqualTo(source.lastActionKind));
                    Assert.That(received.lastActionPlayer, Is.EqualTo(source.lastActionPlayer));
                    Assert.That(received.lastActionCardId, Is.EqualTo(source.lastActionCardId));
                    Assert.That(received.lastActionSequence, Is.EqualTo(source.lastActionSequence));
                    Assert.That(received.p0Hand.Length, Is.EqualTo(source.p0Hand.Length));
                    Assert.That(received.p1Deck.Length, Is.EqualTo(source.p1Deck.Length));
                    Assert.That(received.obstacleHp.Length, Is.EqualTo(source.obstacleHp.Length));
                    Assert.That(received.obstacleHp[1 * GameRules.Size + 0], Is.EqualTo(2));
                    Assert.That(received.p0Tags.Length, Is.EqualTo(source.p0Tags.Length));
                    Assert.That(received.p0Tags[0].Id, Is.EqualTo(source.p0Tags[0].Id));
                    Assert.That(received.p0Tags[0].DurationTurns, Is.EqualTo(source.p0Tags[0].DurationTurns));
                    Assert.That(received.p0Tags[0].Value, Is.EqualTo(source.p0Tags[0].Value));
                    Assert.That(received.worldEffects.Length, Is.EqualTo(source.worldEffects.Length));
                    Assert.That(received.nextWorldEffectSequence, Is.EqualTo(source.nextWorldEffectSequence));

                    for (int i = 0; i < source.worldEffects.Length; i++)
                    {
                        var expected = source.worldEffects[i];
                        var actual = received.worldEffects[i];
                        Assert.That(actual.Kind, Is.EqualTo(expected.Kind), "effect {0} kind", i);
                        Assert.That(actual.Phase, Is.EqualTo(expected.Phase), "effect {0} phase", i);
                        Assert.That(actual.SourcePlayer, Is.EqualTo(expected.SourcePlayer), "effect {0} source", i);
                        Assert.That(actual.TriggerPlayer, Is.EqualTo(expected.TriggerPlayer), "effect {0} trigger", i);
                        Assert.That(actual.TargetPlayer, Is.EqualTo(expected.TargetPlayer), "effect {0} target", i);
                        Assert.That(actual.X, Is.EqualTo(expected.X), "effect {0} x", i);
                        Assert.That(actual.Y, Is.EqualTo(expected.Y), "effect {0} y", i);
                        Assert.That(actual.RemainingTurns, Is.EqualTo(expected.RemainingTurns), "effect {0} duration", i);
                        Assert.That(actual.Power, Is.EqualTo(expected.Power), "effect {0} power", i);
                        Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence), "effect {0} sequence", i);
                        Assert.That(actual.Structure, Is.EqualTo(expected.Structure), "effect {0} structure", i);
                        Assert.That(actual.Aux, Is.EqualTo(expected.Aux), "effect {0} aux", i);
                    }
                }
                finally
                {
                    reader.Dispose();
                }
            }
            finally
            {
                writer.Dispose();
            }
        }
    }
}
