using UnityEngine;

namespace SpellThrower
{
    /// 배경음. 씬이 바뀌어도 끊기지 않도록 첫 씬에서 스스로 만들어지고 살아남는다.
    /// 클립은 Resources 에서 이름으로 찾으므로 씬에 연결해 둘 것이 없다.
    public sealed class MusicPlayer : MonoBehaviour
    {
        public const string ClipPath = "BGM/MoonlitDuel";

        public static MusicPlayer I;

        AudioSource _source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = LocalPrefs.BgmVolume;

            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogWarning("[스펠 스로워] BGM 클립을 찾지 못했습니다: Resources/" + ClipPath);
                return;
            }

            _source.clip = clip;
            _source.Play();
        }

        /// 설정 슬라이더를 움직이면 바로 반영된다.
        public static void ApplyVolume()
        {
            if (I != null && I._source != null) I._source.volume = LocalPrefs.BgmVolume;
        }
    }
}
