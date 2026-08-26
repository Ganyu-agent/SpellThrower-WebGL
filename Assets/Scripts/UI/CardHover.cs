using UnityEngine;
using UnityEngine.EventSystems;

namespace SpellThrower
{
    /// 손패 카드의 커서 상태와 우클릭만 GameUI 에 알린다. 연출은 GameUI 가 한다.
    /// 좌클릭(사용/선택)은 슬롯에 이미 붙어 있는 Button 이 맡는다.
    public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        GameUI _ui;
        int _index;

        public void Init(GameUI ui, int index)
        {
            _ui = ui;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (_ui != null) _ui.SetHoveredCard(_index);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_ui != null) _ui.ClearHoveredCard(_index);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_ui != null && e.button == PointerEventData.InputButton.Right)
                _ui.ToggleCardDetail(_index);
        }
    }
}
