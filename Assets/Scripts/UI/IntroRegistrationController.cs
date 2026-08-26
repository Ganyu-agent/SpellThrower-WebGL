using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellThrower
{
    /// 타이틀부터 결투사 등록 완료까지의 첫 진입 흐름만 담당한다.
    /// 로비, 매칭, 네트워크 및 씬 전환은 의도적으로 포함하지 않는다.
    public sealed class IntroRegistrationController : MonoBehaviour
    {
        public string ConfirmedNickname { get; private set; }

        GameObject _titleState;
        GameObject _registryState;
        GameObject _lobbyState;
        GameObject _titleUi;
        GameObject _registryUi;
        Animator _letterAnimator;
        TMP_InputField _nicknameInput;
        RectTransform _menuCanvas;
        RectTransform _nicknameInputRect;
        Transform _registryPaper;
        SpriteRenderer _registryPaperRenderer;
        Vector3 _registryPaperRestingPosition;

        // 편지지 이미지 내부의 정규화 영역. 화면 좌표가 아니라 bigletter.png의
        // 보라색 이름판에 대한 비율이므로 해상도와 화면 비율이 바뀌어도 함께 움직인다.
        [SerializeField] Rect _nicknamePaperArea = new Rect(0.20f, 0.18f, 0.60f, 0.11f);
        // TMP 글자도 편지지 이름판의 높이에 비례시킨다. CanvasScaler가 배경만
        // 키우거나 줄이는 경우에도 입력 글자가 이름판 안에서 같은 비율로 보인다.
        [SerializeField] float _nicknameFontHeightRatio = 0.62f;
        [SerializeField] float _nicknameFontMinSize = 14f;
        [SerializeField] float _nicknameFontMaxSize = 42f;

        const float PaperFallHeight = 5.5f;
        const float PaperFallDuration = 0.34f;
        const float PaperSettleDuration = 0.18f;

        void Awake()
        {
            var worldVisuals = transform.Find("WorldVisuals");
            _titleState = worldVisuals.Find("TitleState").gameObject;
            _registryState = worldVisuals.Find("RegistryState").gameObject;
            _lobbyState = worldVisuals.Find("LobbyState").gameObject;
            _letterAnimator = _titleState.transform.Find("Letter").GetComponent<Animator>();
            _registryPaper = _registryState.transform.Find("RegistryPaper");
            if (_registryPaper != null)
            {
                _registryPaperRestingPosition = _registryPaper.localPosition;
                _registryPaperRenderer = _registryPaper.GetComponent<SpriteRenderer>();
            }

            var menuCanvas = transform.Find("MenuCanvas");
            _menuCanvas = menuCanvas as RectTransform;
            _titleUi = menuCanvas.Find("TitleUI").gameObject;
            _registryUi = menuCanvas.Find("RegistryUI").gameObject;
            _nicknameInput = _registryUi.transform.Find("NicknameInput").GetComponent<TMP_InputField>();
            _nicknameInputRect = _nicknameInput.GetComponent<RectTransform>();
            EnsureNicknamePlaceholder();
            // 지난번에 등록한 이름을 그대로 물려받는다.
            ConfirmedNickname = LocalPrefs.Nickname;
            if (!string.IsNullOrEmpty(ConfirmedNickname)) _nicknameInput.text = ConfirmedNickname;

            _titleUi.transform.Find("StartButton").GetComponent<Button>().onClick.AddListener(BeginRegistration);
            _registryUi.transform.Find("RegisterButton").GetComponent<Button>().onClick.AddListener(Register);

            // WorldVisuals/Table은 공용 배경이므로 전혀 건드리지 않는다.
            _titleState.SetActive(true);
            _titleUi.SetActive(true);
            _registryState.SetActive(false);
            _registryUi.SetActive(false);
            _lobbyState.SetActive(false);
            menuCanvas.Find("DeckUI").gameObject.SetActive(false);
            menuCanvas.Find("SettingsUI").gameObject.SetActive(false);
            menuCanvas.Find("MatchingUI").gameObject.SetActive(false);

            // Default state가 씬 진입 시 자동 재생되지 않도록 멈춘다.
            _letterAnimator.enabled = false;
        }

        void LateUpdate()
        {
            if (_registryPaperRenderer == null || _menuCanvas == null || _nicknameInputRect == null ||
                !_registryState.activeInHierarchy || !_registryUi.activeInHierarchy)
                return;

            SyncNicknameInputLayout();
        }

        void BeginRegistration()
        {
            _titleUi.SetActive(false);
            _letterAnimator.enabled = true;
            _letterAnimator.Rebind();
            _letterAnimator.Play(0, 0, 0f);
            StartCoroutine(WaitForLetterAnimation());
        }

        System.Collections.IEnumerator WaitForLetterAnimation()
        {
            // AnimationClip 길이가 바뀌어도 Animator state의 실제 종료 시점까지 기다린다.
            yield return null;
            while (_letterAnimator.IsInTransition(0) ||
                   _letterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;

            _titleState.SetActive(false);
            // 저장된 이름이 있으면 등록을 건너뛰고 바로 로비로 간다.
            // 한 판 끝낸 뒤 돌아왔을 때 이름을 다시 쓰지 않게 하는 것이 목적이다.
            if (!string.IsNullOrWhiteSpace(ConfirmedNickname))
            {
                _lobbyState.SetActive(true);
                yield break;
            }

            _registryState.SetActive(true);
            _registryUi.SetActive(true);
            _nicknameInput.ActivateInputField();
        }

        void Register()
        {
            var nickname = _nicknameInput.text.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                _nicknameInput.ActivateInputField();
                return;
            }

            ConfirmedNickname = nickname;
            LocalPrefs.Nickname = nickname;
            _registryUi.SetActive(false);
            _registryState.SetActive(false);
            _lobbyState.SetActive(true);
        }

        public void OpenRegistryFromLobby()
        {
            _lobbyState.SetActive(false);
            _registryState.SetActive(true);
            _registryUi.SetActive(false);
            StartCoroutine(DropRegistryPaperThenShowUi());
        }

        System.Collections.IEnumerator DropRegistryPaperThenShowUi()
        {
            // 로비의 NameLetter에서 열었을 때만 종이가 먼저 떨어지고, 입력 UI는
            // 종이가 안착한 뒤 보인다. 타이틀에서의 기존 등록 전환은 유지한다.
            if (_registryPaper != null)
            {
                var start = _registryPaperRestingPosition + Vector3.up * PaperFallHeight;
                var overshoot = _registryPaperRestingPosition + Vector3.down * 0.16f;
                _registryPaper.localPosition = start;

                for (var elapsed = 0f; elapsed < PaperFallDuration; elapsed += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / PaperFallDuration);
                    _registryPaper.localPosition = Vector3.LerpUnclamped(start, overshoot, t * t);
                    yield return null;
                }

                for (var elapsed = 0f; elapsed < PaperSettleDuration; elapsed += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / PaperSettleDuration);
                    t = t * t * (3f - 2f * t);
                    _registryPaper.localPosition = Vector3.LerpUnclamped(overshoot, _registryPaperRestingPosition, t);
                    yield return null;
                }

                _registryPaper.localPosition = _registryPaperRestingPosition;
            }

            _registryUi.SetActive(true);
            _nicknameInput.text = ConfirmedNickname;
            SyncNicknameInputLayout();
            _nicknameInput.ActivateInputField();
        }

        void SyncNicknameInputLayout()
        {
            var camera = Camera.main;
            var sprite = _registryPaperRenderer != null ? _registryPaperRenderer.sprite : null;
            if (camera == null || sprite == null || _menuCanvas == null) return;

            var spriteBounds = sprite.bounds;
            var areaMin = _nicknamePaperArea.min;
            var areaMax = _nicknamePaperArea.max;
            var screenMin = new Vector2(float.MaxValue, float.MaxValue);
            var screenMax = new Vector2(float.MinValue, float.MinValue);

            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    var normalizedX = x == 0 ? areaMin.x : areaMax.x;
                    var normalizedY = y == 0 ? areaMin.y : areaMax.y;
                    var local = new Vector3(
                        Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, normalizedX),
                        Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, normalizedY),
                        0f);
                    var screen = camera.WorldToScreenPoint(_registryPaper.TransformPoint(local));
                    if (screen.z <= 0f) return;

                    screenMin = new Vector2(Mathf.Min(screenMin.x, screen.x), Mathf.Min(screenMin.y, screen.y));
                    screenMax = new Vector2(Mathf.Max(screenMax.x, screen.x), Mathf.Max(screenMax.y, screen.y));
                }
            }

            var canvas = _menuCanvas.GetComponent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_menuCanvas, screenMin, eventCamera, out var localMin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(_menuCanvas, screenMax, eventCamera, out var localMax))
                return;

            var lower = new Vector2(Mathf.Min(localMin.x, localMax.x), Mathf.Min(localMin.y, localMax.y));
            var upper = new Vector2(Mathf.Max(localMin.x, localMax.x), Mathf.Max(localMin.y, localMax.y));
            var size = upper - lower;
            if (size.x <= 0f || size.y <= 0f) return;

            _nicknameInputRect.anchorMin = _nicknameInputRect.anchorMax = new Vector2(0.5f, 0.5f);
            _nicknameInputRect.anchoredPosition = (lower + upper) * 0.5f;
            _nicknameInputRect.sizeDelta = size;
            SyncNicknameFontSize(size.y);
        }

        void SyncNicknameFontSize(float inputHeight)
        {
            var fontSize = Mathf.Clamp(inputHeight * _nicknameFontHeightRatio,
                                       _nicknameFontMinSize, _nicknameFontMaxSize);
            if (_nicknameInput.textComponent != null)
            {
                _nicknameInput.textComponent.enableAutoSizing = false;
                _nicknameInput.textComponent.fontSize = fontSize;
            }

            if (_nicknameInput.placeholder is TMP_Text placeholder)
            {
                placeholder.enableAutoSizing = false;
                placeholder.fontSize = fontSize;
            }
        }

        void EnsureNicknamePlaceholder()
        {
            if (_nicknameInput.textComponent != null)
            {
                ConfigureNicknameText(_nicknameInput.textComponent);
            }

            if (_nicknameInput.placeholder != null)
            {
                if (_nicknameInput.placeholder is TextMeshProUGUI existingPlaceholder)
                {
                    ConfigureNicknameText(existingPlaceholder);
                    existingPlaceholder.text = "Name";
                }
                return;
            }

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.SetParent(_nicknameInput.textComponent.rectTransform.parent, false);
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            placeholderRect.SetSiblingIndex(0);

            var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.font = _nicknameInput.textComponent.font;
            placeholder.fontSharedMaterial = _nicknameInput.textComponent.fontSharedMaterial;
            placeholder.fontSize = _nicknameInput.textComponent.fontSize;
            ConfigureNicknameText(placeholder);
            placeholder.color = new Color(1f, 1f, 1f, 0.55f);
            placeholder.text = "Name";
            _nicknameInput.placeholder = placeholder;
        }

        static void ConfigureNicknameText(TMP_Text text)
        {
            if (text == null) return;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
