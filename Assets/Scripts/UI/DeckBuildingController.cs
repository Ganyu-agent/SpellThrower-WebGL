using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpellThrower
{
    /// MenuCanvas/DeckUI 안에서만 동작하는 로컬 덱 편집기.
    /// 네트워크 제출 및 기존 GameUI와는 의도적으로 연결하지 않는다.
    ///
    /// 배치는 전부 Panel 크기에 대한 비율이다. Panel.png 는 액자 그림이라
    /// 안쪽 나무판 범위(Inner*) 밖에 UI를 놓으면 장식에 가려진다.
    public sealed class DeckBuildingController : MonoBehaviour
    {
        [SerializeField] Font _deckListKoreanFont;

        const int DeckLimit = GameRules.DeckSize;
        const int AllFilter = -1;
        // 카드명이 가장 긴 카드까지 같은 크기로 읽혀야 하므로 목록은 2열로 둔다.
        // 3열에서는 전체 이름에 맞추는 공통 폰트가 5px까지 줄어들었다.
        const int GridColumns = 2;

        /// 글자와 픽셀 단위 치수를 한꺼번에 키우는 배율. 패널은 이미 화면 높이의 93%라
        /// 더 못 키우므로, 대신 패널 안 내용물을 이 비율로 함께 키운다. 이 값만 바꾸면
        /// 폰트·행 높이·여백·카드가 같은 비율로 따라온다.
        const float UiScale = 1.35f;

        // Panel.png 안쪽 나무판이 차지하는 범위. 글자가 커진 만큼 여백을 줄여 열을 넓힌다.
        const float InnerLeft = .07f, InnerRight = .93f;
        // 세로 칸: 아래 분포 바 / 가운데 본문 / 위 탭·버튼 줄 / 맨 위 명판.
        const float BarBottom = .19f, BarTop = .247f;
        const float BodyBottom = .258f, BodyTop = .765f;
        const float StripBottom = .778f, StripTop = .850f;
        const float PlateLeft = .33f, PlateRight = .67f, PlateBottom = .877f, PlateTop = .945f;
        // 가로 칸: 수집 / 상세 / 덱.
        const float CollectionRight = .44f;
        const float DetailLeft = .455f, DetailRight = .70f;
        const float DeckLeft = .715f;

        static readonly int TextSize = Mathf.RoundToInt(PixelFontCrisp.NativeSize * UiScale);
        const float ScrollbarWidth = 12f * UiScale;
        const float GridPadding = 8f * UiScale, GridSpacing = 10f * UiScale;
        const float RowHeight = 40f * UiScale;
        const float BannerSize = 24f * UiScale;
        const float StatsHeight = 36f * UiScale;
        const float StripeWidth = 8f * UiScale, ValueWidth = 52f * UiScale, CountWidth = 30f * UiScale;
        const float SegmentMinWidth = 60f * UiScale;

        readonly List<byte> _deck = new List<byte>(DeckLimit);
        RectTransform _deckUi;
        RectTransform _collectionContent;
        RectTransform _deckListContent;
        RectTransform _attributeBar;
        TMP_Text _deckCountText;
        GameObject[] _cardSlots;
        CanvasGroup[] _cardDimmers;
        Text[] _cardBadges;
        Image[] _filterTabs;
        CardView _detailView;
        Text _detailStats;
        Text _detailBody;
        int _filter = AllFilter;

        public byte[] CurrentDeck => _deck.ToArray();
        public bool IsComplete => _deck.Count == DeckLimit;

        /// 씬에 넣어 둔 한글 폰트가 비어 있어도 카드와 같은 픽셀 폰트로 그린다.
        Font KoreanFont => _deckListKoreanFont != null ? _deckListKoreanFont : Resources.Load<Font>("neodgm");

        void Awake()
        {
            _deckUi = transform.Find("MenuCanvas/DeckUI") as RectTransform;
            if (_deckUi == null) return;

            var existingRoot = FindDescendant(_deckUi, "DeckBuilder") as RectTransform;
            if (existingRoot == null)
            {
                if (Application.isPlaying) { LoadSavedDeck(); BuildUi(); }
                return;
            }

            if (!Application.isPlaying) return;

            // 씬에 구워 둔 카드 목록은 옛 카탈로그라 보이는 카드와 실제로 담기는 카드가
            // 어긋난다. 실행할 때는 항상 지금의 Cards.All 로 다시 만든다.
            existingRoot.SetParent(null);
            Destroy(existingRoot.gameObject);
            LoadSavedDeck();
            BuildUi();
        }

        [ContextMenu("Create Deck Builder Scene Layout")]
        public void CreateSceneLayout()
        {
            if (Application.isPlaying) return;

            _deckUi = transform.Find("MenuCanvas/DeckUI") as RectTransform;
            if (_deckUi == null) return;
            var previous = FindDescendant(_deckUi, "DeckBuilder");
            if (previous != null) DestroyImmediate(previous.gameObject);
            BuildUi();
        }

        void BuildUi()
        {
            var panel = FindDescendant(_deckUi, "Panel") as RectTransform;
            var host = panel != null ? panel : _deckUi;
            var root = CreateRect("DeckBuilder", host);
            Stretch(root);

            var hostSize = host.rect.size;
            _deckCountText = CreateLabel("DeckTitle", root, "", BannerSize);
            Anchor(_deckCountText.rectTransform, PlateLeft, PlateBottom, PlateRight, PlateTop);

            BuildFilterTabs(root);
            BuildActions(root);
            BuildCollectionScroll(root, hostSize);
            BuildCardDetail(root, hostSize);
            BuildDeckList(root);
            BuildAttributeBar(root);
            RefreshAll();
        }

        // ---------------- 속성 탭 ----------------

        void BuildFilterTabs(RectTransform root)
        {
            var tabRoot = CreateRect("FilterTabs", root);
            Anchor(tabRoot, InnerLeft, StripBottom, DetailRight, StripTop);
            var layout = tabRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            // 탭 목록은 CardAttribute 를 그대로 훑어 만든다. 속성이 늘면 탭도 따라 늘어난다.
            var attributes = System.Enum.GetValues(typeof(CardAttribute));
            _filterTabs = new Image[attributes.Length + 1];
            AddFilterTab(tabRoot, 0, AllFilter, "전체");
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = (CardAttribute)attributes.GetValue(i);
                AddFilterTab(tabRoot, i + 1, (int)attribute, CardText.AttributeNames[(byte)attribute]);
            }
        }

        void AddFilterTab(RectTransform parent, int slot, int filter, string label)
        {
            var button = CreateButton("Tab_" + label, parent, Color.clear);
            _filterTabs[slot] = button.GetComponent<Image>();
            button.onClick.AddListener(() => SetFilter(filter));
            var text = CreateKoreanText("Label", button.transform, label, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
        }

        void SetFilter(int filter)
        {
            _filter = filter;
            RefreshCollection();
        }

        // ---------------- 수집 목록 ----------------

        void BuildCollectionScroll(RectTransform root, Vector2 hostSize)
        {
            var scrollRoot = CreateRect("CardCollectionScroll", root);
            Anchor(scrollRoot, InnerLeft, BodyBottom, CollectionRight, BodyTop);
            _collectionContent = BuildScroll(scrollRoot);

            var layout = _collectionContent.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset((int)GridPadding, (int)GridPadding, 6, 6);
            layout.spacing = new Vector2(GridSpacing, GridSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = GridColumns;

            var frame = Resources.Load<Sprite>("Cards/CardFrame");
            // 카드 비율은 프레임 원본에서 읽는다. 아트가 바뀌면 칸도 따라 바뀐다.
            var cardAspect = frame != null ? frame.rect.height / frame.rect.width : 1.5f;
            var usable = (CollectionRight - InnerLeft) * hostSize.x
                       - ScrollbarWidth - GridPadding * 2f - GridSpacing * (GridColumns - 1);
            var cellWidth = usable / GridColumns;
            layout.cellSize = new Vector2(cellWidth, cellWidth * cardAspect);

            var fitter = _collectionContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleFont = Resources.Load<Font>("neodgm");
            _cardSlots = new GameObject[Cards.All.Length];
            _cardDimmers = new CanvasGroup[Cards.All.Length];
            _cardBadges = new Text[Cards.All.Length];
            for (var i = 0; i < Cards.All.Length; i++)
            {
                var id = (byte)i;
                if (id == (byte)CardId.Move) continue;   // 매 턴 자동 지급이라 덱에 넣을 수 없다
                BuildCollectionCard(id, layout.cellSize, frame, titleFont);
            }
        }

        void BuildCollectionCard(byte id, Vector2 cellSize, Sprite frame, Font titleFont)
        {
            var card = Cards.Get(id);
            var button = CreateButton("Card_" + id, _collectionContent, Color.clear);
            button.onClick.AddListener(() => { ShowDetail(id); AddCard(id); });
            HoverToShowDetail(button.gameObject, id);

            // Image와 Text는 같은 GameObject에 함께 둘 수 없으므로 제목은
            // 별도 자식으로 만든 뒤 CardView가 카드 프레임 내부로 옮긴다.
            var titleRect = CreateRect("Title", button.transform);
            var title = titleRect.gameObject.AddComponent<Text>();
            var view = CardView.Build(button.GetComponent<RectTransform>(), button.GetComponent<Image>(), title,
                cellSize, frame, titleFont, titleFont, 1f, 1f, 0f);
            // 썸네일 안의 설명은 4px까지 줄어들어 읽을 수 없다. 설명은 상세 칸에서만 보여 준다.
            view.Set(card, LoadIcon(id), false);

            _cardSlots[id] = button.gameObject;
            _cardDimmers[id] = view.Visual.gameObject.AddComponent<CanvasGroup>();

            // 설명을 뺀 자리가 프레임 아래쪽에 통째로 비므로, 보유 수를 그 종이칸에 넣는다.
            var badge = CreateKoreanText("Badge", view.Visual, "", TextAnchor.MiddleCenter);
            Anchor(badge.rectTransform, .19f, .11f, .81f, .29f);
            badge.color = new Color(.25f, .18f, .12f);
            _cardBadges[id] = badge;
        }

        void RefreshCollection()
        {
            if (_cardSlots == null) return;

            for (var i = 0; i < _filterTabs.Length; i++)
            {
                var selected = i == 0 ? _filter == AllFilter : _filter == i - 1;
                _filterTabs[i].color = selected
                    ? new Color(.83f, .65f, .32f, .95f)
                    : new Color(.27f, .21f, .14f, .92f);
            }

            for (var i = 0; i < _cardSlots.Length; i++)
            {
                if (_cardSlots[i] == null) continue;
                var card = Cards.Get((byte)i);
                _cardSlots[i].SetActive(_filter == AllFilter || (int)card.Attribute == _filter);

                var copies = CountOf((byte)i);
                var limit = GameRules.CopyLimitOf((byte)i);
                _cardBadges[i].text = copies + "/" + limit;
                // 장수를 채웠거나 덱이 가득 차면 눌러도 안 담긴다. 그 이유를 어둡게 해서 보여 준다.
                var addable = copies < limit && _deck.Count < DeckLimit;
                _cardDimmers[i].alpha = addable ? 1f : .4f;
            }
        }

        // ---------------- 상세 칸 ----------------

        void BuildCardDetail(RectTransform root, Vector2 hostSize)
        {
            var detail = CreateRect("CardDetail", root);
            Anchor(detail, DetailLeft, BodyBottom, DetailRight, BodyTop);
            var layout = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var frame = Resources.Load<Sprite>("Cards/CardFrame");
            var cardAspect = frame != null ? frame.rect.height / frame.rect.width : 1.5f;
            var cardWidth = (DetailRight - DetailLeft) * hostSize.x * .58f;
            var cardSize = new Vector2(cardWidth, cardWidth * cardAspect);

            var slot = CreateRect("DetailCard", detail);
            slot.gameObject.AddComponent<LayoutElement>().preferredHeight = cardSize.y;
            var slotImage = slot.gameObject.AddComponent<Image>();
            slotImage.raycastTarget = false;
            var titleRect = CreateRect("Title", slot);
            var title = titleRect.gameObject.AddComponent<Text>();
            var font = Resources.Load<Font>("neodgm");
            _detailView = CardView.Build(slot, slotImage, title, cardSize, frame, font, font, 1f, 1f, 0f);

            _detailStats = CreateKoreanText("Stats", detail, "", TextAnchor.UpperCenter);
            _detailStats.gameObject.AddComponent<LayoutElement>().preferredHeight = StatsHeight;
            _detailStats.color = new Color(.95f, .86f, .62f);

            _detailBody = CreateKoreanText("Body", detail, "카드에 커서를 올리면 설명이 나옵니다.", TextAnchor.UpperLeft);
            _detailBody.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // 아직 고른 카드가 없을 때 빈 아이콘이 흰 사각형으로 보이지 않게 꺼 둔다.
            _detailView.Set(null, null, false);
        }

        void ShowDetail(byte cardId)
        {
            var card = Cards.Get(cardId);
            _detailView.Set(card, LoadIcon(cardId), false);
            _detailStats.text = CardText.GetStats(card);
            _detailBody.text = CardDescription(card);
        }

        /// 커서를 올리면 상세 칸이 바뀐다. 좌클릭은 슬롯의 Button 이 그대로 맡는다.
        ///
        /// EventTrigger 는 IScrollHandler·IDragHandler 까지 전부 구현하므로 카드 위에서
        /// 휠과 드래그가 여기서 멈춰 스크롤이 안 됐다. 필요한 진입 이벤트만 받는다.
        void HoverToShowDetail(GameObject target, byte cardId)
        {
            target.AddComponent<HoverDetail>().Show = () => ShowDetail(cardId);
        }

        // ---------------- 덱 목록 ----------------

        void BuildDeckList(RectTransform root)
        {
            var scrollRoot = CreateRect("DeckListScroll", root);
            Anchor(scrollRoot, DeckLeft, BodyBottom, InnerRight, BodyTop);
            _deckListContent = BuildScroll(scrollRoot);

            var layout = _deckListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = _deckListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void RefreshDeckList()
        {
            if (_deckListContent == null) return;
            ClearChildren(_deckListContent);

            for (var i = 0; i < Cards.All.Length; i++)
            {
                var id = (byte)i;
                var copies = CountOf(id);
                if (copies == 0) continue;

                var card = Cards.Get(id);
                var row = CreateButton("DeckEntry_" + id, _deckListContent, new Color(.27f, .21f, .14f, .92f));
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                row.onClick.AddListener(() => RemoveCard(id));
                HoverToShowDetail(row.gameObject, id);

                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(0, 6, 3, 3);
                layout.spacing = 5f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandHeight = true;

                var stripe = CreateRect("Attribute", row.transform);
                stripe.gameObject.AddComponent<Image>().color = AttributeColor(card.Attribute);
                stripe.gameObject.AddComponent<LayoutElement>().preferredWidth = StripeWidth;

                var name = CreateKoreanText("Name", row.transform, card.Name, TextAnchor.MiddleLeft);
                name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                // 코스트는 전 카드가 1이라 구분에 쓸모가 없다. 밸류를 대신 보여 준다.
                var value = CreateKoreanText("Value", row.transform, CardText.ValueNames[(byte)card.Tier], TextAnchor.MiddleRight);
                value.color = new Color(.72f, .62f, .42f);
                value.gameObject.AddComponent<LayoutElement>().preferredWidth = ValueWidth;

                var count = CreateKoreanText("Count", row.transform, "x" + copies, TextAnchor.MiddleRight);
                count.gameObject.AddComponent<LayoutElement>().preferredWidth = CountWidth;
            }
        }

        // ---------------- 속성 분포 바 ----------------

        void BuildAttributeBar(RectTransform root)
        {
            _attributeBar = CreateRect("AttributeBar", root);
            Anchor(_attributeBar, InnerLeft, BarBottom, DetailRight, BarTop);
            var layout = _attributeBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        }

        void RefreshAttributeBar()
        {
            if (_attributeBar == null) return;
            ClearChildren(_attributeBar);

            var counts = new int[CardText.AttributeNames.Length];
            for (var i = 0; i < _deck.Count; i++) counts[(byte)Cards.Get(_deck[i]).Attribute]++;

            for (var a = 0; a < counts.Length; a++)
            {
                if (counts[a] == 0) continue;
                var segment = CreateRect("Segment_" + a, _attributeBar);
                segment.gameObject.AddComponent<Image>().color = AttributeColor((CardAttribute)a);
                // 폭이 장수에 비례하므로 바 자체가 덱 구성비가 된다. 다만 한 장짜리
                // 속성도 이름이 읽히도록 최소 폭은 남겨 둔다.
                var size = segment.gameObject.AddComponent<LayoutElement>();
                size.flexibleWidth = counts[a];
                size.minWidth = SegmentMinWidth;
                // 속성 9개가 모두 나와도 들어가도록 이름과 장수를 두 줄로 쪼갠다.
                var label = CreateKoreanText("Label", segment,
                    CardText.AttributeNames[a] + "\n" + counts[a], TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                CardView.AddPixelOutline(label, 0.5f);
            }
        }

        static Color AttributeColor(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Fire: return new Color(.85f, .32f, .18f);
                case CardAttribute.Ice: return new Color(.35f, .68f, .90f);
                case CardAttribute.Wind: return new Color(.42f, .78f, .74f);
                case CardAttribute.Lightning: return new Color(.92f, .78f, .26f);
                case CardAttribute.Heal: return new Color(.40f, .80f, .45f);
                case CardAttribute.Draw: return new Color(.62f, .55f, .90f);
                case CardAttribute.Sprint: return new Color(.95f, .60f, .25f);
                case CardAttribute.Totem: return new Color(.70f, .55f, .35f);
                default: return new Color(.86f, .45f, .74f);
            }
        }

        // ---------------- 비우기 / 랜덤 ----------------

        void BuildActions(RectTransform root)
        {
            var actions = CreateRect("Actions", root);
            Anchor(actions, DeckLeft, StripBottom, InnerRight, StripTop);
            var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            AddActionButton(actions, "비우기", ClearDeck);
            AddActionButton(actions, "랜덤", FillRandom);
        }

        void AddActionButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var button = CreateButton("Action_" + label, parent, new Color(.27f, .21f, .14f, .92f));
            button.onClick.AddListener(action);
            var text = CreateKoreanText("Label", button.transform, label, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
        }

        void ClearDeck()
        {
            _deck.Clear();
            Save();
            RefreshAll();
        }

        /// 남은 자리를 규칙에 맞는 카드로 채운다. 종류당 2장 상한 때문에
        /// 25장이면 최소 13종이 되어 GameRules.IsValidDeck 을 그대로 통과한다.
        void FillRandom()
        {
            var pool = new List<byte>(Cards.All.Length);
            for (var i = 0; i < Cards.All.Length; i++)
                if (i != (byte)CardId.Move) pool.Add((byte)i);

            while (_deck.Count < DeckLimit && pool.Count > 0)
            {
                var index = Random.Range(0, pool.Count);
                var id = pool[index];
                if (CountOf(id) >= GameRules.CopyLimitOf(id)) { pool.RemoveAt(index); continue; }
                _deck.Add(id);
            }

            Save();
            RefreshAll();
        }

        // ---------------- 덱 상태 ----------------

        void AddCard(byte cardId)
        {
            if (_deck.Count >= DeckLimit || CountOf(cardId) >= GameRules.CopyLimitOf(cardId)) return;
            _deck.Add(cardId);
            Save();
            RefreshAll();
        }

        void RemoveCard(byte cardId)
        {
            _deck.Remove(cardId);
            Save();
            RefreshAll();
        }

        void Save()
        {
            LocalPrefs.Deck = _deck.ToArray();
        }

        /// 지난번에 만든 덱을 그대로 불러온다. 카드 목록이 바뀌었거나 저장본이
        /// 규칙을 벗어나면 그 카드만 버린다.
        void LoadSavedDeck()
        {
            var saved = LocalPrefs.Deck;
            // A first-time browser has no PlayerPrefs deck. Keep the collection
            // empty so the player explicitly chooses the cards to submit.
            if (saved == null) return;

            for (var i = 0; i < saved.Length; i++)
            {
                var id = saved[i];
                if (id >= Cards.All.Length || id == (byte)CardId.Move) continue;
                if (_deck.Count >= DeckLimit || CountOf(id) >= GameRules.CopyLimitOf(id)) continue;
                _deck.Add(id);
            }
        }

        void RefreshAll()
        {
            RefreshCollection();
            RefreshDeckList();
            RefreshAttributeBar();
            if (_deckCountText != null)
                _deckCountText.text = "DECK " + _deck.Count + "/" + DeckLimit;
        }

        int CountOf(byte cardId)
        {
            var count = 0;
            for (var i = 0; i < _deck.Count; i++)
                if (_deck[i] == cardId) count++;
            return count;
        }

        static Sprite LoadIcon(byte cardId)
        {
            if (cardId >= CardText.ArtNames.Length) return null;
            return Resources.Load<Sprite>("CardArt/" + CardText.ArtNames[cardId]);
        }

        static string CardDescription(CardDef card)
        {
            return CardText.GetDescription(card.Id, card);
        }

        // ---------------- 만들기 ----------------

        /// 세로 스크롤 뼈대 하나. 수집 목록과 덱 목록이 같은 것을 쓴다.
        static RectTransform BuildScroll(RectTransform scrollRoot)
        {
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = .08f;
            scroll.elasticity = .08f;
            scroll.scrollSensitivity = 32f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-ScrollbarWidth, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 카드 사이 빈 틈에는 판정 대상이 없어 휠 이벤트가 아예 발생하지 않는다.
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.content = content;

            var scrollbarRoot = CreateRect("Scrollbar", scrollRoot);
            scrollbarRoot.anchorMin = new Vector2(1f, 0f);
            scrollbarRoot.anchorMax = Vector2.one;
            scrollbarRoot.pivot = Vector2.one;
            scrollbarRoot.offsetMin = new Vector2(-ScrollbarWidth, 0f);
            scrollbarRoot.offsetMax = Vector2.zero;
            var scrollbarBackground = scrollbarRoot.gameObject.AddComponent<Image>();
            scrollbarBackground.color = new Color(.08f, .05f, .03f, .7f);
            var scrollbar = scrollbarRoot.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = scrollbarBackground;
            scrollbar.numberOfSteps = 0;

            var handle = CreateRect("Handle", scrollbarRoot);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = new Vector2(1f, .25f);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            handle.gameObject.AddComponent<Image>().color = new Color(.83f, .65f, .32f, .95f);
            scrollbar.handleRect = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        Text CreateKoreanText(string name, Transform parent, string value, TextAnchor anchor)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = KoreanFont;
            text.fontSize = TextSize;
            text.color = Color.white;
            text.fontStyle = FontStyle.Normal;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            // 원본 16px 아래로 내려가면 획이 사라져 깨진다. 줄이지 않고 그 크기로 고정한다.
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = TextSize;
            text.resizeTextMaxSize = TextSize + 1;
            text.raycastTarget = false;
            return text;
        }

        /// 실행 중이 아니면 Destroy 를 쓸 수 없다. 씬에 미리 구울 때도 같은 코드를 탄다.
        static void ClearChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                // Destroy는 프레임 끝에 실행되므로, 먼저 목록에서 분리해야 연속 클릭도
                // 즉시 최신 x수량 행 하나로 다시 그릴 수 있다.
                child.SetParent(null);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Button CreateButton(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect.gameObject.AddComponent<Button>();
        }

        static TMP_Text CreateLabel(string name, Transform parent, string value, float fontSize)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        /// 배치는 부모 크기에 대한 비율로만 준다. Panel 이 커지면 내용도 같이 커진다.
        static void Anchor(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Transform FindDescendant(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// 커서 진입만 듣는 최소 부품. EventTrigger 와 달리 다른 이벤트는 건드리지 않아
    /// 휠·드래그가 그대로 부모 ScrollRect 로 올라간다.
    public sealed class HoverDetail : MonoBehaviour, IPointerEnterHandler
    {
        public System.Action Show;
        public void OnPointerEnter(PointerEventData eventData) { if (Show != null) Show(); }
    }
}
