using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpellThrower
{
    /// 새 메뉴 로비의 월드 오브젝트 클릭과 화면 상태만 처리한다.
    /// 매칭 및 네트워크 호출은 의도적으로 포함하지 않는다.
    public sealed class MenuLobbyController : MonoBehaviour
    {
        [SerializeField] Sprite _matchPressedSprite;

        Transform _lobbyState;
        Transform _registryState;
        GameObject _registryUi;
        GameObject _deckUi;
        GameObject _settingsUi;
        GameObject _matchingUi;
        RectTransform _deckPanel;
        RectTransform _settingsPanel;
        IntroRegistrationController _introRegistration;
        MenuMatchmakingController _menuMatchmaking;
        readonly Transform[] _lobbyObjects = new Transform[4];
        readonly Vector3[] _restingPositions = new Vector3[4];
        readonly SpriteRenderer[] _lobbyRenderers = new SpriteRenderer[4];
        readonly TextMesh[] _lobbyLabels = new TextMesh[4];
        bool _wasLobbyVisible;
        bool _isLeavingLobby;

        static readonly string[] LobbyLabelTexts = { "REGISTER", "DECK", "SETTINGS", "MATCH" };

        const float EntryStagger = 0.1f;
        const float EntryFallHeight = 5.25f;
        const float FallDuration = 0.34f;
        const float SettleDuration = 0.18f;
        const float ExitDuration = 0.42f;
        const float PanelFallDistance = 900f;
        const float PanelFallDuration = 0.32f;
        const float PanelSettleDuration = 0.16f;

        void Awake()
        {
            _introRegistration = GetComponent<IntroRegistrationController>();
            _menuMatchmaking = GetComponent<MenuMatchmakingController>();
            _lobbyState = transform.Find("WorldVisuals/LobbyState");
            _registryState = transform.Find("WorldVisuals/RegistryState");
            var menuCanvas = transform.Find("MenuCanvas");
            _registryUi = menuCanvas.Find("RegistryUI").gameObject;
            _deckUi = menuCanvas.Find("DeckUI").gameObject;
            _settingsUi = menuCanvas.Find("SettingsUI").gameObject;
            _matchingUi = menuCanvas.Find("MatchingUI").gameObject;
            _deckPanel = FindPanel(_deckUi.transform);
            _settingsPanel = FindPanel(_settingsUi.transform);

            _lobbyObjects[0] = _lobbyState.Find("NameLetter");
            _lobbyObjects[1] = _lobbyState.Find("DeckObject");
            _lobbyObjects[2] = _lobbyState.Find("SettingsObject");
            _lobbyObjects[3] = _lobbyState.Find("MatchObject");
            for (var i = 0; i < _lobbyObjects.Length; i++)
            {
                _restingPositions[i] = _lobbyObjects[i].localPosition;
                _lobbyRenderers[i] = _lobbyObjects[i].GetComponent<SpriteRenderer>();
                _lobbyLabels[i] = _lobbyObjects[i].GetComponentInChildren<TextMesh>(true);

                // Label은 각 로비 오브젝트의 자식으로 유지한다. TextMesh를 수동으로
                // 만든 경우에도 폰트 머티리얼과 렌더 순서를 명시해 테이블 위에 보이게 한다.
                if (_lobbyLabels[i] != null)
                {
                    _lobbyLabels[i].text = LobbyLabelTexts[i];
                    _lobbyLabels[i].anchor = TextAnchor.MiddleCenter;
                    _lobbyLabels[i].alignment = TextAlignment.Center;
                    var labelRenderer = _lobbyLabels[i].GetComponent<MeshRenderer>();
                    labelRenderer.sharedMaterial = _lobbyLabels[i].font.material;
                    labelRenderer.sortingOrder = 3;
                }
            }

            _lobbyState.gameObject.SetActive(false);
            _deckUi.SetActive(false);
            _settingsUi.SetActive(false);
            _matchingUi.SetActive(false);
            SetLabelsVisible(false);

            FindDescendant(_deckUi.transform, "BackButton").GetComponent<Button>().onClick.AddListener(ReturnToLobby);
            FindDescendant(_settingsUi.transform, "BackButton").GetComponent<Button>().onClick.AddListener(ReturnToLobby);
            FindDescendant(_matchingUi.transform, "BackButton").GetComponent<Button>().onClick.AddListener(ReturnToLobby);
        }

        void Update()
        {
            var lobbyVisible = _lobbyState.gameObject.activeInHierarchy;
            if (lobbyVisible && !_wasLobbyVisible)
                PlayLobbyEntryAnimation();
            _wasLobbyVisible = lobbyVisible;

            if (!_lobbyState.gameObject.activeInHierarchy || Mouse.current == null ||
                !Mouse.current.leftButton.wasPressedThisFrame || _isLeavingLobby)
                return;

            var camera = Camera.main;
            if (camera == null) return;

            var screen = Mouse.current.position.ReadValue();
            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            var hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
            if (hit == null || hit.transform.parent != _lobbyState) return;

            switch (hit.name)
            {
                case "NameLetter":
                    StartCoroutine(ExitLobbyAndOpen(0));
                    break;
                case "DeckObject":
                    StartCoroutine(ExitLobbyAndOpen(1));
                    break;
                case "SettingsObject":
                    StartCoroutine(ExitLobbyAndOpen(2));
                    break;
                case "MatchObject":
                    ShowMatchPressed();
                    _menuMatchmaking?.BeginMatch();
                    break;
            }
        }

        // 매칭 연결은 아직 하지 않는다. 로비를 유지한 채 버튼의 눌린 외형만 보여 준다.
        void ShowMatchPressed()
        {
            if (_matchPressedSprite != null && _lobbyRenderers[3] != null)
                _lobbyRenderers[3].sprite = _matchPressedSprite;
        }

        void OpenTemporaryUi(GameObject ui)
        {
            _lobbyState.gameObject.SetActive(false);
            ui.SetActive(true);

            // Deck/Settings의 실제 내용은 아직 건드리지 않는다. Panel만 먼저
            // 테이블 위로 떨어뜨려, 이후 내부 요소 등장 연출을 별도로 얹을 수 있게 한다.
            var panel = ui == _deckUi ? _deckPanel : ui == _settingsUi ? _settingsPanel : null;
            if (panel != null)
            {
                // 덱은 Panel의 자식인 카드 UI까지 함께 낙하한다. 설정 화면은 기존처럼
                // Panel이 안착한 뒤에만 내부 내용을 보여 준다.
                var deckPanel = ui == _deckUi;
                StartCoroutine(DropPanel(panel,
                    deckPanel ? null : HideContentsUntilPanelLands(ui.transform, panel), deckPanel));
            }
        }

        static System.Collections.Generic.List<GameObject> HideContentsUntilPanelLands(Transform uiRoot, RectTransform panel)
        {
            var hiddenContents = new System.Collections.Generic.List<GameObject>();
            for (var i = 0; i < uiRoot.childCount; i++)
            {
                var child = uiRoot.GetChild(i);
                if (child == panel) continue;
                hiddenContents.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
            return hiddenContents;
        }

        // BackButton 같은 임시 UI를 패널로 오인하지 않도록 이름이 Panel인 UI만 찾는다.
        // 씬에 Panel을 추가/저장하면 별도 연결 작업 없이 이 전환을 사용한다.
        static RectTransform FindPanel(Transform uiRoot)
        {
            foreach (var rect in uiRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect != uiRoot && rect.name.ToLowerInvariant().Contains("panel"))
                    return rect;
            }
            return null;
        }

        System.Collections.IEnumerator DropPanel(RectTransform panel,
            System.Collections.Generic.List<GameObject> hiddenContents, bool scaleDuringDrop)
        {
            var restingPosition = panel.anchoredPosition;
            var restingScale = panel.localScale;
            var startPosition = restingPosition + Vector2.up * PanelFallDistance;
            var overshootPosition = restingPosition + Vector2.down * 22f;
            panel.anchoredPosition = startPosition;
            if (scaleDuringDrop) panel.localScale = restingScale * 1.18f;

            for (var elapsed = 0f; elapsed < PanelFallDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / PanelFallDuration);
                panel.anchoredPosition = Vector2.LerpUnclamped(startPosition, overshootPosition, t * t);
                if (scaleDuringDrop)
                    panel.localScale = Vector3.LerpUnclamped(restingScale * 1.18f, restingScale * 0.97f, t * t);
                yield return null;
            }

            for (var elapsed = 0f; elapsed < PanelSettleDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / PanelSettleDuration);
                t = t * t * (3f - 2f * t);
                panel.anchoredPosition = Vector2.LerpUnclamped(overshootPosition, restingPosition, t);
                if (scaleDuringDrop)
                    panel.localScale = Vector3.LerpUnclamped(restingScale * 0.97f, restingScale, t);
                yield return null;
            }

            panel.anchoredPosition = restingPosition;
            panel.localScale = restingScale;

            // Panel이 안착한 다음에만 BackButton과 덱/설정 내용을 드러낸다.
            if (hiddenContents != null)
                for (var i = 0; i < hiddenContents.Count; i++)
                    hiddenContents[i].SetActive(true);
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

        void ReturnToLobby()
        {
            _menuMatchmaking?.CancelMatch();
            _deckUi.SetActive(false);
            _settingsUi.SetActive(false);
            _matchingUi.SetActive(false);
            _lobbyState.gameObject.SetActive(true);
        }

        void PlayLobbyEntryAnimation()
        {
            _isLeavingLobby = false;
            RestoreLobbyVisuals();
            SetLabelsVisible(false);
            for (var i = 0; i < _lobbyObjects.Length; i++)
            {
                var item = _lobbyObjects[i];
                item.localPosition = _restingPositions[i] + Vector3.up * EntryFallHeight;
                StartCoroutine(DropOntoTable(item, _restingPositions[i], i * EntryStagger));
            }

            StartCoroutine(ShowLabelsAfterEntry());
        }

        // 매칭을 제외한 메뉴 전환에서만 책상 위 오브젝트 전체를 화면 밖으로 밀어낸다.
        System.Collections.IEnumerator ExitLobbyAndOpen(int destination)
        {
            _isLeavingLobby = true;
            SetLabelsVisible(true);

            var exitOffsets = new[]
            {
                Vector3.left * 5.5f, // NameLetter
                Vector3.up * 5.5f,   // DeckObject
                Vector3.right * 5.5f,// SettingsObject
                Vector3.down * 5.5f  // MatchObject
            };
            var startPositions = new Vector3[_lobbyObjects.Length];
            for (var i = 0; i < _lobbyObjects.Length; i++)
                startPositions[i] = _lobbyObjects[i].localPosition;

            for (var elapsed = 0f; elapsed < ExitDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / ExitDuration);
                t = 1f - Mathf.Pow(1f - t, 3f); // ease out
                var alpha = 1f - t;
                for (var i = 0; i < _lobbyObjects.Length; i++)
                {
                    _lobbyObjects[i].localPosition = Vector3.LerpUnclamped(startPositions[i], startPositions[i] + exitOffsets[i], t);
                    SetVisualAlpha(i, alpha);
                }
                yield return null;
            }

            for (var i = 0; i < _lobbyObjects.Length; i++)
                SetVisualAlpha(i, 0f);

            if (destination == 0)
            {
                if (_introRegistration != null)
                    _introRegistration.OpenRegistryFromLobby();
                else
                {
                    _lobbyState.gameObject.SetActive(false);
                    _registryState.gameObject.SetActive(true);
                    _registryUi.SetActive(true);
                    _registryUi.GetComponent<MenuRegistryController>().ShowConfirmedNickname();
                }
            }
            else
            {
                OpenTemporaryUi(destination == 1 ? _deckUi : _settingsUi);
            }
        }

        void RestoreLobbyVisuals()
        {
            for (var i = 0; i < _lobbyObjects.Length; i++)
            {
                _lobbyObjects[i].localPosition = _restingPositions[i];
                SetVisualAlpha(i, 1f);
            }
        }

        void SetVisualAlpha(int index, float alpha)
        {
            var spriteColor = _lobbyRenderers[index].color;
            spriteColor.a = alpha;
            _lobbyRenderers[index].color = spriteColor;

            if (_lobbyLabels[index] != null)
            {
                var labelColor = _lobbyLabels[index].color;
                labelColor.a = alpha;
                _lobbyLabels[index].color = labelColor;
            }
        }

        System.Collections.IEnumerator DropOntoTable(Transform item, Vector3 restingPosition, float delay)
        {
            yield return new WaitForSeconds(delay);

            var start = item.localPosition;
            var overshoot = restingPosition + Vector3.down * 0.16f;
            for (var elapsed = 0f; elapsed < FallDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / FallDuration);
                item.localPosition = Vector3.LerpUnclamped(start, overshoot, t * t);
                yield return null;
            }

            for (var elapsed = 0f; elapsed < SettleDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / SettleDuration);
                t = t * t * (3f - 2f * t);
                item.localPosition = Vector3.LerpUnclamped(overshoot, restingPosition, t);
                yield return null;
            }

            item.localPosition = restingPosition;
        }

        System.Collections.IEnumerator ShowLabelsAfterEntry()
        {
            yield return new WaitForSeconds((_lobbyObjects.Length - 1) * EntryStagger + FallDuration + SettleDuration);
            SetLabelsVisible(true);

            const float fadeDuration = 0.35f;
            for (var elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
            {
                var alpha = Mathf.Clamp01(elapsed / fadeDuration);
                for (var i = 0; i < _lobbyLabels.Length; i++)
                {
                    if (_lobbyLabels[i] == null) continue;
                    var color = _lobbyLabels[i].color;
                    color.a = alpha;
                    _lobbyLabels[i].color = color;
                }
                yield return null;
            }

            for (var i = 0; i < _lobbyLabels.Length; i++)
            {
                if (_lobbyLabels[i] == null) continue;
                var color = _lobbyLabels[i].color;
                color.a = 1f;
                _lobbyLabels[i].color = color;
            }
        }

        void SetLabelsVisible(bool visible)
        {
            for (var i = 0; i < _lobbyLabels.Length; i++)
            {
                if (_lobbyLabels[i] == null) continue;
                _lobbyLabels[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    var color = _lobbyLabels[i].color;
                    color.a = 0f;
                    _lobbyLabels[i].color = color;
                }
            }
        }
    }
}
