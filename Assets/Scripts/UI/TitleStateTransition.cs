using UnityEngine;
using UnityEngine.UI;

namespace SpellThrower
{
    /// Title 화면에서 다음 메뉴 상태로 넘어가는 최소 전환 지점이다.
    /// 카메라 줌 등의 연출은 BeginRegistryTransition 앞뒤에 추가할 수 있다.
    public sealed class TitleStateTransition : MonoBehaviour
    {
        GameObject _titleState;
        GameObject _registryState;
        GameObject _titleUi;
        GameObject _registryUi;

        void Awake()
        {
            if (GetComponent<IntroRegistrationController>() != null)
            {
                enabled = false;
                return;
            }

            _titleState = transform.Find("WorldVisuals/TitleState").gameObject;
            _registryState = transform.Find("WorldVisuals/RegistryState").gameObject;
            _titleUi = transform.Find("MenuCanvas/TitleUI").gameObject;
            _registryUi = transform.Find("MenuCanvas/RegistryUI").gameObject;

            _registryState.SetActive(false);
            _registryUi.SetActive(false);
            _titleUi.transform.Find("StartButton").GetComponent<Button>()
                .onClick.AddListener(BeginRegistryTransition);
        }

        public void BeginRegistryTransition()
        {
            // Future camera zoom / paper animation hook.
            _titleUi.SetActive(false);
            _titleState.SetActive(false);
            _registryState.SetActive(true);
            _registryUi.SetActive(true);
        }
    }
}
