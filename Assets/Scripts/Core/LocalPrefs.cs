using UnityEngine;

namespace SpellThrower
{
    /// 닉네임·덱·볼륨을 PlayerPrefs 에 담아 둔다. 게임을 껐다 켜도, 한 판 끝내고
    /// 로비로 돌아와도 이름과 덱을 다시 만들지 않게 하는 것이 목적이다.
    public static class LocalPrefs
    {
        const string NickKey = "spellthrower.nick";
        const string DeckKey = "spellthrower.deck";
        const string BgmKey = "spellthrower.bgm";
        const string SfxKey = "spellthrower.sfx";

        public static string Nickname
        {
            get { return PlayerPrefs.GetString(NickKey, ""); }
            set
            {
                PlayerPrefs.SetString(NickKey, value == null ? "" : value);
                PlayerPrefs.Save();
            }
        }

        /// 저장된 덱. 없거나 형식이 깨졌으면 null.
        public static byte[] Deck
        {
            get
            {
                var raw = PlayerPrefs.GetString(DeckKey, "");
                if (raw.Length == 0) return null;

                var parts = raw.Split(',');
                var cards = new byte[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                    if (!byte.TryParse(parts[i], out cards[i])) return null;
                return cards;
            }
            set
            {
                PlayerPrefs.SetString(DeckKey, value == null ? "" : string.Join(",", value));
                PlayerPrefs.Save();
            }
        }

        public static float BgmVolume
        {
            get { return PlayerPrefs.GetFloat(BgmKey, 0.6f); }
            set
            {
                PlayerPrefs.SetFloat(BgmKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static float SfxVolume
        {
            get { return PlayerPrefs.GetFloat(SfxKey, 1f); }
            set
            {
                PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }
    }
}
