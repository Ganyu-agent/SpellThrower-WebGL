using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellThrower
{
    /// 결투사 등록 화면의 로컬 닉네임 확정만 담당한다.
    /// 매칭 또는 NetGame 전송은 다음 단계에서 연결한다.
    public sealed class MenuRegistryController : MonoBehaviour
    {
        public string ConfirmedNickname { get; private set; }

        TMP_InputField _nicknameInput;

        void Awake()
        {
            if (transform.parent.parent.GetComponent<IntroRegistrationController>() != null)
            {
                enabled = false;
                return;
            }

            _nicknameInput = transform.Find("NicknameInput").GetComponent<TMP_InputField>();
            _nicknameInput.characterLimit = 12;
            transform.Find("SignButton").GetComponent<Button>().onClick.AddListener(Sign);
        }

        void Sign()
        {
            var nickname = _nicknameInput.text.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                _nicknameInput.ActivateInputField();
                return;
            }

            ConfirmedNickname = nickname;
            gameObject.SetActive(false);
            var menuRoot = transform.parent.parent;
            menuRoot.Find("WorldVisuals/RegistryState").gameObject.SetActive(false);
            menuRoot.Find("WorldVisuals/LobbyState").gameObject.SetActive(true);
        }

        public void ShowConfirmedNickname()
        {
            if (_nicknameInput == null) return;

            _nicknameInput.text = ConfirmedNickname;
            _nicknameInput.ActivateInputField();
        }
    }
}
