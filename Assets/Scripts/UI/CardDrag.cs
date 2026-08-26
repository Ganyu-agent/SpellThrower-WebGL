using UnityEngine;
using UnityEngine.EventSystems;

namespace SpellThrower
{
    /// 손패 카드를 끌어다 타일에 떨구면 사용된다. 기존 클릭 방식도 그대로 동작한다.
    [RequireComponent(typeof(RectTransform))]
    public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        RectTransform _rt;
        CanvasGroup _cg;
        GameUI _ui;
        Transform _home;
        int _sibling, _index = -1;
        bool _dragging;

        /// 손패 자리 번호. 포커스한 카드를 앞으로 끌어올리면 형제 순서가 바뀌므로
        /// GetSiblingIndex 로는 어느 카드인지 알 수 없다.
        public void Init(int index) => _index = index;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            _ui = GetComponentInParent<GameUI>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_ui == null || _index < 0) return;
            if (!_ui.BeginCardDrag(_index)) return;

            _dragging = true;
            _home = _rt.parent;
            _sibling = _rt.GetSiblingIndex();
            _rt.SetParent(_ui.transform, true);   // 레이아웃 밖으로 빼내 자유롭게 움직인다
            _rt.SetAsLastSibling();
            _cg.blocksRaycasts = false;           // 밑에 있는 타일이 레이캐스트에 잡히도록
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) _rt.position = e.position;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            _cg.blocksRaycasts = true;
            _rt.SetParent(_home, false);
            _rt.SetSiblingIndex(_sibling);
            _ui.EndCardDrag(_index, e);
        }
    }
}
