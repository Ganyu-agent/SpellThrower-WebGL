using UnityEngine;
using UnityEngine.UI;

namespace SpellThrower
{
    /// 손패 카드 한 장의 겉모습. 씬에는 빈 슬롯(Image + Text)만 있으므로
    /// 아이콘·프레임·비용·이름·설명을 런타임에 붙여 만든다.
    ///
    /// 슬롯은 고정된 판정용 띠이고, 실제 그림은 자식 Visual 안에 들어간다.
    /// 커서 판정 영역이 움직이지 않아야 떠오르는 연출이 자기 자신을 취소하지 않는다.
    ///
    /// 배치는 원본 카드 디자인(500x750)의 좌표를 비율로 환산해 앵커로 넣는다.
    /// 프레임 가운데가 뚫려 있으므로 아이콘을 프레임보다 뒤에 깐다.
    public sealed class CardView
    {
        // 원본 디자인에서 프레임 중심 기준으로 잰 값 (중심 x,y / 크기 w,h — 전부 500x750 대비 비율)
        // 프레임이 뚫려 있는 창(원본 500x750 기준 x 88~421, y 115~490). 아트는 이 창 안에서만 보인다.
        // 프레임은 창 위쪽 카드 끝까지 다 그려져 있지 않으므로, 넘친 아트는 직접 잘라야 한다.
        static readonly Rect WindowBox = new Rect(0.010f, 0.090f, 0.668f, 0.490f);
        static readonly Rect CostBox = new Rect(-0.3439f, 0.3686f, 0.1576f, 0.1013f);
        // 이름판은 긴 이름을 두 줄로 감싸도 픽셀 글자가 찌그러지지 않게 높이를 확보한다.
        static readonly Rect TitleBox = new Rect(0.0118f, 0.3733f, 0.5280f, 0.105f);
        static readonly Rect DesBox = new Rect(-0.0006f, -0.3043f, 0.6180f, 0.1787f);

        public static readonly Color Idle = Color.white;
        public static readonly Color Selected = new Color(1f, 0.88f, 0.45f);

        /// 회전·확대·상승은 전부 이 노드에만 준다. 슬롯 자신은 움직이지 않는다.
        public RectTransform Visual { get; private set; }

        Image _icon, _frame;
        AspectRatioFitter _iconFit;
        Text _cost, _title, _des;

        /// slot 은 씬에 있던 Card_N. slotText 는 그 자식으로 이미 있던 Text 로, 이름표로 재활용한다.
        public static CardView Build(RectTransform slot, Image slotImage, Text slotText, Vector2 cardSize,
                                     Sprite frame, Font titleFont, Font bodyFont, float outlineWidth = 1f,
                                     float titleScale = 1f)
        {
            var v = new CardView();

            // 슬롯 자신의 그래픽은 커서·클릭 판정만 맡고 보이지는 않는다.
            slotImage.sprite = null;
            slotImage.color = new Color(0f, 0f, 0f, 0f);
            slotImage.raycastTarget = true;

            // 씬에 이미 Visual 계층구조가 있으면 재사용 (에디터 미리보기와 100% 동기화)
            var existingVisual = slot.Find("Visual");
            if (existingVisual != null)
            {
                v.Visual = (RectTransform)existingVisual;
                v.Visual.sizeDelta = cardSize;
                v._icon = existingVisual.Find("Icon")?.GetComponent<Image>();
                v._frame = existingVisual.Find("Frame")?.GetComponent<Image>();
                v._title = existingVisual.Find("Title")?.GetComponent<Text>() ?? slotText;
                v._cost = existingVisual.Find("Cost")?.GetComponent<Text>();
                v._des = existingVisual.Find("Des")?.GetComponent<Text>();

                if (v._icon != null && v._frame != null && v._title != null && v._cost != null && v._des != null)
                {
                    if (v._frame.sprite == null && frame != null) v._frame.sprite = frame;
                    var titleLayer = EnsureTitleLayer(slot, cardSize, titleScale);
                    v._title.transform.SetParent(titleLayer, false);
                    v._iconFit = ClipIconToWindow(v.Visual, v._icon);
                    ApplyTitleStyle(v._title, titleFont, cardSize, outlineWidth);
                    Style(v._cost, bodyFont, Color.white, TextAnchor.MiddleCenter);
                    Style(v._des, bodyFont, Color.black, TextAnchor.UpperLeft);
                    ClipToCard(v.Visual);
                    return v;
                }
            }

            var visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(slot, false);
            v.Visual = (RectTransform)visual.transform;
            v.Visual.anchorMin = v.Visual.anchorMax = new Vector2(0.5f, 0.5f);
            v.Visual.pivot = new Vector2(0.5f, 0.5f);
            v.Visual.sizeDelta = cardSize;

            v._icon = MakeImage(v.Visual, "Icon", WindowBox);
            v._iconFit = ClipIconToWindow(v.Visual, v._icon);

            v._frame = MakeImage(v.Visual, "Frame", new Rect(0f, 0f, 1f, 1f));
            v._frame.sprite = frame;
            AddPixelOutline(v._frame, outlineWidth);

            // 이미 있던 Text 를 이름표 자리로 옮긴다. 프레임보다 앞에 와야 글씨가 보인다.
            v._title = slotText;
            var newTitleLayer = EnsureTitleLayer(slot, cardSize, titleScale);
            slotText.transform.SetParent(newTitleLayer, false);
            ApplyTitleStyle(v._title, titleFont, cardSize, outlineWidth);
            slotText.transform.SetAsLastSibling();

            v._cost = MakeText(v.Visual, "Cost", CostBox, bodyFont, Color.white, TextAnchor.MiddleCenter);
            v._des = MakeText(v.Visual, "Des", DesBox, bodyFont, Color.black, TextAnchor.UpperLeft);
            ClipToCard(v.Visual);
            return v;
        }

        /// 픽셀아트라 테두리도 픽셀 단위로 딱 떨어지게 준다. 대각선까지 채워야 계단이 안 생긴다.
        public static void AddPixelOutline(Graphic g, float thickness = 1f)
        {
            if (g == null) return;
            var o = g.GetComponent<Outline>();
            if (o == null) o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.04f, 0.08f, 1f);
            o.effectDistance = new Vector2(thickness, thickness);
            o.useGraphicAlpha = true;
        }

        // 카드명은 카드마다 best-fit하지 않고, 같은 표시 크기의 카드끼리 같은 크기를 쓴다.
        // 가장 긴 이름 때문에 픽셀 폰트가 4~5px까지 줄어들면 짧은 이름도 함께 읽을 수
        // 없게 되므로, 작은 목록 카드에서도 사람이 읽을 수 있는 하한을 둔다.
        // neodgm is a pixel font whose crisp native raster size is 16px.
        // Do not let the shared title size fall below that threshold: a
        // mathematically fitting title is still a failed UI if its strokes
        // disappear when the card is rendered at FHD.
        const int MinimumReadableTitleFontSize = PixelFontCrisp.NativeSize;

        /// 모든 카드 제목이 같은 크기를 사용하도록, 전체 카드 중 가장 긴 이름을 기준으로
        /// 현재 카드 크기에 맞는 하나의 폰트 크기를 계산한다. 카드별 문자열에 따라
        /// best-fit을 다시 적용하지 않으므로 짧은 이름만 커지는 현상이 생기지 않는다.
        public static void ApplyTitleStyle(Text title, Font font, Vector2 cardSize, float outlineWidth = 1f)
        {
            if (title == null) return;

            Place(title.rectTransform, TitleBox);
            if (font != null) title.font = font;
            title.color = Color.black;
            title.alignment = TextAnchor.MiddleCenter;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = false;
            title.fontSize = FixedTitleFontSize(font, cardSize);
            title.raycastTarget = false;

            if (outlineWidth > 0f)
                AddPixelOutline(title, outlineWidth);
        }

        /// The hand builds the art at its focused size and scales the whole Visual
        /// down while idle.  The title is on a slot-sized overlay, so this method
        /// never needs to compensate for the art scale.
        public void SetTitleFontSize(float size)
        {
            if (_title == null) return;
            _title.resizeTextForBestFit = false;
            _title.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
        }

        public void Set(CardDef card, Sprite icon, bool selected)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            if (icon != null) _iconFit.aspectRatio = icon.rect.width / icon.rect.height;
            _frame.color = selected ? Selected : Idle;
            _cost.text = card != null ? card.Cost.ToString() : "";
            _title.text = card != null ? card.Name : "";
        }

        public void SetDescription(string text) => _des.text = text;

        /// 아트를 창 크기 마스크 안에 넣고 창을 꽉 채우게 한다.
        /// 비율이 어떻든 창이 비지 않고, 창 밖으로 삐져나온 부분은 마스크가 잘라낸다.
        static AspectRatioFitter ClipIconToWindow(RectTransform visual, Image icon)
        {
            var window = visual.Find("IconWindow") as RectTransform;
            if (window == null)
            {
                window = (RectTransform)new GameObject("IconWindow", typeof(RectTransform)).transform;
                window.SetParent(visual, false);
            }
            Place(window, WindowBox);
            if (window.GetComponent<RectMask2D>() == null)
                window.gameObject.AddComponent<RectMask2D>();
            window.SetAsFirstSibling();   // 프레임보다 뒤에 깔려야 테두리 장식이 아트를 덮는다
            icon.transform.SetParent(window, false);
            icon.preserveAspect = false;  // 채우기는 Fitter 가 맡는다

            var fitter = icon.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = icon.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return fitter;
        }

        static void ClipToCard(RectTransform visual)
        {
            if (visual.GetComponent<RectMask2D>() == null)
                visual.gameObject.AddComponent<RectMask2D>();
        }

        /// Keep text out of the high-resolution art node.  GameUI creates the
        /// artwork at FocusScale and scales that node down while idle; a title
        /// under it becomes unreadable even when its nominal font size is 16.
        /// The overlay uses the displayed card size and remains at scale one.
        static RectTransform EnsureTitleLayer(RectTransform slot, Vector2 cardSize, float titleScale)
        {
            var layer = slot.Find("TitleLayer") as RectTransform;
            if (layer == null)
            {
                var go = new GameObject("TitleLayer", typeof(RectTransform));
                go.transform.SetParent(slot, false);
                layer = (RectTransform)go.transform;
            }

            layer.anchorMin = layer.anchorMax = layer.pivot = new Vector2(0.5f, 0.5f);
            layer.anchoredPosition = Vector2.zero;
            layer.localRotation = Quaternion.identity;
            layer.localScale = Vector3.one;
            layer.sizeDelta = cardSize / Mathf.Max(0.001f, titleScale);
            layer.SetAsLastSibling();
            return layer;
        }

        // ---------------- 만들기 ----------------

        static Image MakeImage(RectTransform parent, string name, Rect box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, box);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;   // 판정은 슬롯이 맡는다
            return img;
        }

        static Text MakeText(RectTransform parent, string name, Rect box,
                             Font font, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, box);
            var t = go.AddComponent<Text>();
            t.raycastTarget = false;
            Style(t, font, color, anchor);
            return t;
        }

        /// 카드 크기를 몰라도 되도록 비율 앵커로 붙인다.
        static void Place(RectTransform rt, Rect box)
        {
            float cx = 0.5f + box.x, cy = 0.5f + box.y;
            float hw = box.width * 0.5f, hh = box.height * 0.5f;
            rt.anchorMin = new Vector2(cx - hw, cy - hh);
            rt.anchorMax = new Vector2(cx + hw, cy + hh);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// 카드가 작을 때 글자를 고정 크기로 두면 넘친다 → 칸에 맞춰 자동으로 줄인다.
        static void Style(Text t, Font font, Color color, TextAnchor anchor)
        {
            if (font != null) t.font = font;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 4;
            t.resizeTextMaxSize = 40;
        }

        static int FixedTitleFontSize(Font font, Vector2 cardSize)
        {
            var availableWidth = Mathf.Max(1f, cardSize.x * TitleBox.width * 0.96f);
            var availableHeight = Mathf.Max(1f, cardSize.y * TitleBox.height * 0.98f);
            if (font == null)
                return Mathf.Max(1, Mathf.FloorToInt(availableHeight));

            var low = MinimumReadableTitleFontSize;
            var high = Mathf.Clamp(Mathf.FloorToInt(availableHeight), low, 64);
            var best = low;
            var generator = new TextGenerator();

            while (low <= high)
            {
                var candidate = (low + high) / 2;
                var settings = TitleGenerationSettings(font, candidate, availableWidth, availableHeight);
                var widest = 0f;
                var tallest = 0f;
                var names = CardText.Names;
                for (var i = 0; i < names.Length; i++)
                {
                    if (string.IsNullOrEmpty(names[i])) continue;
                    widest = Mathf.Max(widest, generator.GetPreferredWidth(names[i], settings));
                    tallest = Mathf.Max(tallest, generator.GetPreferredHeight(names[i], settings));
                }

                if (widest <= availableWidth && tallest <= availableHeight)
                {
                    best = candidate;
                    low = candidate + 1;
                }
                else
                {
                    high = candidate - 1;
                }
            }

            return best;
        }

        static TextGenerationSettings TitleGenerationSettings(Font font, int fontSize,
                                                               float availableWidth, float availableHeight)
        {
            return new TextGenerationSettings
            {
                font = font,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                scaleFactor = 1f,
                lineSpacing = 1f,
                richText = true,
                color = Color.black,
                textAnchor = TextAnchor.UpperLeft,
                alignByGeometry = false,
                horizontalOverflow = HorizontalWrapMode.Wrap,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(availableWidth, availableHeight),
                pivot = Vector2.zero,
                generateOutOfBounds = true,
                resizeTextForBestFit = false,
                resizeTextMinSize = fontSize,
                resizeTextMaxSize = fontSize,
                updateBounds = true
            };
        }
    }
}
