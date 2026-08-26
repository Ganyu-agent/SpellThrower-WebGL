using System.Collections;
using UnityEngine;

namespace SpellThrower
{
    /// 식별자로 SfxLibrary의 AudioClip을 찾아 재생하는 선택적 UI 계층 컴포넌트.
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] SfxLibrary _library;
        [SerializeField] AudioSource _audioSource;
        [SerializeField, Range(0f, 1f)] float _masterVolume = 1f;
        [SerializeField, Min(0.02f)] float _previewGap = 0.08f;

        Coroutine _preview;

        public SfxLibrary Library => _library;

        void Awake()
        {
            EnsureAudioSource();
        }

        void OnValidate()
        {
            _masterVolume = Mathf.Clamp01(_masterVolume);
            _previewGap = Mathf.Max(0.02f, _previewGap);
            EnsureAudioSource();
        }

        /// 성공적으로 클립을 찾고 재생 요청을 보냈으면 true.
        public bool Play(SfxId id)
        {
            if (_library == null || !SfxLibrary.IsPlayable(id)) return false;
            if (!_library.TryGet(id, out var clip, out var volume)) return false;

            EnsureAudioSource();
            if (_audioSource == null) return false;

            // 설정의 SFX 볼륨은 여기서 한 번만 곱한다.
            _audioSource.PlayOneShot(clip, volume * _masterVolume * LocalPrefs.SfxVolume);
            return true;
        }

        /// Play Mode에서 enum 순서대로 모든 슬롯을 한 번씩 재생한다.
        /// 클립이 비어 있는 슬롯은 건너뛰고 다음 enum으로 진행한다.
        [ContextMenu("Play All SFX In Enum Order")]
        public void PlayAllInEnumOrder()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SFX] Play All은 Play Mode에서 실행하세요.");
                return;
            }

            StopPreview();
            _preview = StartCoroutine(PlayAllRoutine());
        }

        [ContextMenu("Stop SFX Preview")]
        public void StopPreview()
        {
            if (_preview == null) return;
            StopCoroutine(_preview);
            _preview = null;
        }

        IEnumerator PlayAllRoutine()
        {
            for (int i = (int)SfxId.None + 1; i < (int)SfxId.Count; i++)
            {
                var id = (SfxId)i;
                if (_library != null && _library.TryGet(id, out var clip, out _))
                {
                    Play(id);
                    yield return new WaitForSecondsRealtime(Mathf.Max(_previewGap, clip.length) + _previewGap);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(_previewGap);
                }
            }

            _preview = null;
        }

        void EnsureAudioSource()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) return;

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
        }
    }
}
