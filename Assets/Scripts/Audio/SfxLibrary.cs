using System;
using UnityEngine;

namespace SpellThrower
{
    [Serializable]
    public struct SfxClipSlot
    {
        public AudioClip Clip;

        [Range(0f, 1f)]
        public float Volume;
    }

    /// enum 값과 같은 인덱스에 AudioClip을 저장하는 에디터 편집용 라이브러리.
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "SpellThrower/Audio/SFX Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [SerializeField]
        SfxClipSlot[] _slots;

        public int SlotCount => _slots == null ? 0 : _slots.Length;

        void OnEnable()
        {
            EnsureSlotCount();
        }

        void OnValidate()
        {
            EnsureSlotCount();
        }

        public bool TryGet(SfxId id, out AudioClip clip, out float volume)
        {
            if (!IsPlayable(id) || _slots == null || (int)id >= _slots.Length)
            {
                clip = null;
                volume = 0f;
                return false;
            }

            var slot = _slots[(int)id];
            clip = slot.Clip;
            volume = slot.Volume;
            if (clip != null) return true;

            volume = 0f;
            return false;
        }

        public bool HasSlot(SfxId id)
        {
            return IsPlayable(id) && _slots != null && (int)id < _slots.Length;
        }

        public static bool IsPlayable(SfxId id)
        {
            return id > SfxId.None && id < SfxId.Count;
        }

        void EnsureSlotCount()
        {
            int expected = (int)SfxId.Count;
            if (_slots == null || _slots.Length == 0)
            {
                _slots = new SfxClipSlot[expected];
                SetDefaultVolumes(0, _slots.Length);
                return;
            }

            int oldLength = _slots.Length;
            if (oldLength != expected)
            {
                Array.Resize(ref _slots, expected);
                SetDefaultVolumes(oldLength, expected);
            }
        }

        void SetDefaultVolumes(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                var slot = _slots[i];
                slot.Volume = 1f;
                _slots[i] = slot;
            }
        }
    }
}
