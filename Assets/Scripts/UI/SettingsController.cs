using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpellThrower
{
    /// MenuCanvas/SettingsUI 안에 BGM·SFX 볼륨 슬라이더 두 줄을 만든다.
    /// 값은 LocalPrefs 에 바로 저장되고, BGM 은 그 자리에서 반영된다.
    public sealed class SettingsController : MonoBehaviour
    {
        void Awake()
        {
            var settingsUi = transform.Find("MenuCanvas/SettingsUI") as RectTransform;
            if (settingsUi == null) return;

            // 내용은 Panel 안에 넣는다. 판이 떨어질 때 같이 내려오고,
            // MenuLobbyController 가 판 밖 자식을 숨기는 연출과도 부딪히지 않는다.
            var panel = settingsUi.Find("Panel") as RectTransform ?? settingsUi;

            // 이전에 만들어 둔 판이 남아 있으면 지우고 지금 값으로 다시 만든다.
            var previous = panel.Find("VolumeSettings");
            if (previous != null)
            {
                previous.SetParent(null);
                Destroy(previous.gameObject);
            }

            var root = NewRect("VolumeSettings", panel);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(560f, 220f);

            BuildVolumeRows(root, Resources.Load<Font>("neodgm"));
        }

        /// BGM·SFX 두 줄을 만들어 붙인다. 로비 설정 화면과 게임 중 ESC 패널이 같이 쓴다.
        /// 값은 LocalPrefs 에 바로 저장되므로 어느 쪽에서 돌려도 양쪽에 같이 반영된다.
        public static void BuildVolumeRows(RectTransform root, Font font)
        {
            BuildRow(root, font, "BGM", 45f, LocalPrefs.BgmVolume, value =>
            {
                LocalPrefs.BgmVolume = value;
                MusicPlayer.ApplyVolume();
            });
            BuildRow(root, font, "SFX", -45f, LocalPrefs.SfxVolume, value => LocalPrefs.SfxVolume = value);
        }

        static void BuildRow(RectTransform parent, Font font, string label, float y,
                             float value, UnityAction<float> onChange)
        {
            var row = NewRect("Row_" + label, parent);
            row.anchorMin = row.anchorMax = new Vector2(.5f, .5f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(520f, 60f);

            var name = NewText(row, font, label, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, .5f);
            name.rectTransform.pivot = new Vector2(0f, .5f);
            name.rectTransform.anchoredPosition = Vector2.zero;
            name.rectTransform.sizeDelta = new Vector2(110f, 40f);

            var percent = NewText(row, font, "", TextAnchor.MiddleRight);
            percent.rectTransform.anchorMin = percent.rectTransform.anchorMax = new Vector2(1f, .5f);
            percent.rectTransform.pivot = new Vector2(1f, .5f);
            percent.rectTransform.anchoredPosition = Vector2.zero;
            percent.rectTransform.sizeDelta = new Vector2(100f, 40f);

            var bar = NewRect("Slider", row);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f);
            bar.anchoredPosition = new Vector2(5f, 0f);
            bar.sizeDelta = new Vector2(300f, 26f);
            var back = bar.gameObject.AddComponent<Image>();
            back.color = new Color(.10f, .08f, .06f, .9f);

            // Slider 는 손잡이가 없으면 채움 칸을 클릭 영역으로 쓴다. 손잡이 없이 띄만 둔다.
            var fillArea = NewRect("Fill Area", bar);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);

            var fill = NewRect("Fill", fillArea);
            Stretch(fill);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(.83f, .65f, .32f, 1f);

            var slider = bar.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.targetGraphic = back;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            percent.text = Percent(value);
            slider.onValueChanged.AddListener(v =>
            {
                percent.text = Percent(v);
                onChange(v);
            });
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Text NewText(Transform parent, Font font, string value, TextAnchor anchor)
        {
            var rect = NewRect("Label", parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            if (font != null) text.font = font;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = anchor;
            text.raycastTarget = false;
            CardView.AddPixelOutline(text, 1f);
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
