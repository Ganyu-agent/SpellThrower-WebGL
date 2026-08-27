using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellThrower
{
    /// 새 메뉴의 로컬 닉네임/덱을 Matchmaking 및 NetGame 공개 API에 전달한다.
    /// 세션이 갈라지거나 Relay 연결이 멈추면 한 번 정리·재시도한다.
    public sealed class MenuMatchmakingController : MonoBehaviour
    {
        DeckBuildingController _deckBuilder;
        IntroRegistrationController _registration;
        GameObject _matchingUi;
        TMP_Text _connectionLog;
        bool _connecting;
        bool _matchOperationActive;
        bool _cancelRequested;
        // This is the early UI gate requested by the menu flow. The rules
        // layer still validates the complete legal deck before connecting.
        const int MinimumSelectedCardsForMatchHint = 13;

        void Awake()
        {
            _deckBuilder = GetComponent<DeckBuildingController>();
            _registration = GetComponent<IntroRegistrationController>();
            _matchingUi = transform.Find("MenuCanvas/MatchingUI")?.gameObject;
            // 매칭 문구는 MatchingUI/Panel 안에 있다. 어디에 있든 찾도록 자식 전체를 뒤진다.
            _connectionLog = _matchingUi?.GetComponentInChildren<TMP_Text>(true);
        }

        public void BeginMatch()
        {
            if (_connecting || _matchOperationActive) return;
            if (_deckBuilder == null || _registration == null) return;

            _matchingUi?.SetActive(true);
            var cards = _deckBuilder.CurrentDeck;
            if (cards.Length < MinimumSelectedCardsForMatchHint)
            {
                SetLog("SELECT AT LEAST 13 CARDS BEFORE MATCH (" + cards.Length + "/13)");
                return;
            }

            if (!GameRules.IsValidDeck(cards))
            {
                SetLog("BUILD A VALID DECK (" + cards.Length + "/" + GameRules.DeckSize + ")");
                return;
            }

            var nickname = _registration.ConfirmedNickname?.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetLog("REGISTER YOUR NAME FIRST");
                return;
            }

            _cancelRequested = false;
            _matchOperationActive = true;
            StartCoroutine(ConnectAndSubmit(nickname, cards));
        }

        /// Matching 화면의 Back이 눌려도 진행 중인 UGS 비동기 호출은 즉시
        /// 취소할 수 없으므로, 코루틴을 죽이지 않고 완료 시 세션을 정리한다.
        public void CancelMatch()
        {
            if (!_matchOperationActive) return;
            _cancelRequested = true;
            SetLog("CANCELLING MATCH...");
        }

        IEnumerator ConnectAndSubmit(string nickname, byte[] cards)
        {
            _connecting = true;
            const int maxAttempts = 2;
            const float networkReadyTimeout = 12f;
            const float opponentLoadoutTimeout = 12f;

            try
            {
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    SetLog(attempt == 0 ? "CONNECTING TO SERVER..." : "RETRYING MATCH...");

                    var matchTask = Matchmaking.FindMatchAsync(nickname);
                    while (!matchTask.IsCompleted) yield return null;

                    if (_cancelRequested)
                    {
                        yield return LeaveCurrentSession();
                        yield break;
                    }

                    if (matchTask.IsFaulted || matchTask.IsCanceled)
                    {
                        if (matchTask.IsFaulted) Debug.LogException(matchTask.Exception);
                        if (attempt + 1 < maxAttempts)
                        {
                            yield return LeaveCurrentSession();
                            continue;
                        }

                        SetLog("CONNECTION FAILED - PRESS MATCH TO RETRY");
                        yield break;
                    }

                    SetLog(Matchmaking.Current != null && Matchmaking.Current.IsHost
                        ? "CONNECTED - WAITING FOR OPPONENT..."
                        : "OPPONENT FOUND - SENDING DECK...");

                    var elapsed = 0f;
                    while ((NetGame.I == null || NetGame.I.MyPlayer < 0) && elapsed < networkReadyTimeout)
                    {
                        if (_cancelRequested)
                        {
                            yield return LeaveCurrentSession();
                            yield break;
                        }
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (NetGame.I != null && NetGame.I.MyPlayer >= 0)
                    {
                        if (_cancelRequested)
                        {
                            yield return LeaveCurrentSession();
                            yield break;
                        }

                        var deck = new FixedList32Bytes<byte>();
                        for (var i = 0; i < cards.Length; i++) deck.Add(cards[i]);
                        NetGame.I.SubmitLoadoutServerRpc(new FixedString32Bytes(nickname), deck);
                        SetLog("DECK SENT - WAITING FOR OPPONENT...");

                        elapsed = 0f;
                        while (NetGame.I != null && !NetGame.I.InGame && elapsed < opponentLoadoutTimeout)
                        {
                            if (_cancelRequested)
                            {
                                yield return LeaveCurrentSession();
                                yield break;
                            }
                            elapsed += Time.unscaledDeltaTime;
                            yield return null;
                        }

                        if (NetGame.I != null && NetGame.I.InGame)
                        {
                            if (_cancelRequested)
                            {
                                yield return LeaveCurrentSession();
                                yield break;
                            }
                            SetLog("DUEL STARTING...");
                            yield return null;
                            if (_cancelRequested)
                            {
                                yield return LeaveCurrentSession();
                                yield break;
                            }
                            SceneManager.LoadScene("GameScene");
                            yield break;
                        }
                    }

                    Debug.LogWarning("[스펠 스로워] 매칭 동기화 시간 초과 - 세션을 정리하고 재시도합니다.");
                    yield return LeaveCurrentSession();
                    if (_cancelRequested) yield break;
                }

                SetLog("MATCH TIMED OUT - PRESS MATCH TO RETRY");
            }
            finally
            {
                _connecting = false;
                _matchOperationActive = false;
                _cancelRequested = false;
            }
        }

        IEnumerator LeaveCurrentSession()
        {
            var leaveTask = Matchmaking.LeaveAsync();
            while (!leaveTask.IsCompleted) yield return null;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            var elapsed = 0f;
            while (NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.ShutdownInProgress && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void SetLog(string message)
        {
            if (_connectionLog != null) _connectionLog.text = message;
        }
    }
}
