using NUnit.Framework;
using UnityEngine;

namespace SpellThrower.Tests
{
    public sealed class SfxContractTests
    {
        [Test]
        public void SfxId_PlayableValuesAreContiguous_AndCountIsReservedBoundary()
        {
            Assert.That(SfxId.None, Is.EqualTo((SfxId)0));
            Assert.That(SfxLibrary.IsPlayable(SfxId.None), Is.False);
            Assert.That(SfxLibrary.IsPlayable(SfxId.Count), Is.False);

            for (int i = 1; i < (int)SfxId.Count; i++)
                Assert.That(SfxLibrary.IsPlayable((SfxId)i), Is.True);
        }

        [Test]
        public void SfxLibrary_CreatesOneSlotForEveryPlayableIdentifier()
        {
            var library = ScriptableObject.CreateInstance<SfxLibrary>();
            try
            {
                Assert.That(library.SlotCount, Is.EqualTo((int)SfxId.Count));
                for (int i = 1; i < (int)SfxId.Count; i++)
                    Assert.That(library.HasSlot((SfxId)i), Is.True, $"Missing slot for {(SfxId)i}");
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void SfxLibrary_MissingClip_IsSafeToQuery()
        {
            var library = ScriptableObject.CreateInstance<SfxLibrary>();
            try
            {
                Assert.That(library.TryGet(SfxId.CardFire, out var clip, out var volume), Is.False);
                Assert.That(clip, Is.Null);
                Assert.That(volume, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void SfxPlayer_WithoutLibraryOrClip_FailsWithoutThrowing()
        {
            var go = new GameObject("SfxPlayerContractTest");
            try
            {
                var player = go.AddComponent<SfxPlayer>();
                Assert.That(go.GetComponent<AudioSource>(), Is.Not.Null);
                Assert.That(player.Play(SfxId.CardFire), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryCardDefinition_UsesTheCardSfxRange()
        {
            // 41종 + 기본 이동 카드. 이동은 질주 계열 소리를 쓴다.
            Assert.That(Cards.All.Length, Is.EqualTo(42));
            Assert.That(Cards.Get((byte)CardId.Move).SfxCue, Is.EqualTo(SfxId.CardSprint));
            for (int i = 0; i < 5; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardFire));
            for (int i = 5; i < 10; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardIce));
            for (int i = 10; i < 15; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardWind));
            for (int i = 15; i < 20; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardLightning));
            for (int i = 20; i < 25; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardHeal));
            for (int i = 25; i < 30; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardDraw));
            for (int i = 30; i < 35; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardSprint));
            for (int i = 35; i < 40; i++) Assert.That(Cards.Get((byte)i).SfxCue, Is.EqualTo(SfxId.CardTotem));
            Assert.That(Cards.Get((byte)CardId.Duel).SfxCue, Is.EqualTo(SfxId.CardSpecial));
        }

        [Test]
        public void TryPlay_RecordsCardActionForPresentation()
        {
            var state = GameRules.NewGame(6001);
            GameRules.Hand(ref state, 0).Clear();
            GameRules.Hand(ref state, 0).Add((byte)CardId.Heal);

            Assert.That(GameRules.TryPlay(ref state, 0, 0, 0, 0), Is.True);
            Assert.That(state.lastActionKind, Is.EqualTo(GameplayActionKind.CardUsed));
            Assert.That(state.lastActionPlayer, Is.EqualTo((byte)0));
            Assert.That(state.lastActionCardId, Is.EqualTo((byte)CardId.Heal));
            Assert.That(state.lastActionSequence, Is.EqualTo((ushort)1));
        }
    }
}
