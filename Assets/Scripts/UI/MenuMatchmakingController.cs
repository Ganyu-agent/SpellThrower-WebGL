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
            if (_connecting) return;
            if (_deckBuilder == null || _registration == null) return;

            _matchingUi?.SetActive(true);
            var nickname = _registration.ConfirmedNickname?.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetLog("REGISTER YOUR NAME FIRST");
                return;
            }

            var cards = _deckBuilder.CurrentDeck;
            if (!GameRules.IsValidDeck(cards))
            {
                SetLog("BUILD A VALID 25-CARD DECK (" + cards.Length + "/" + GameRules.DeckSize + ")");
                return;
            }

            StartCoroutine(ConnectAndSubmit(nickname, cards));
        }

        IEnumerator ConnectAndSubmit(string nickname, byte[] cards)
        {
            _connecting = true;
            const int maxAttempts = 2;
            const float networkReadyTimeout = 12f;
            const float opponentLoadoutTimeout = 12f;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                SetLog(attempt == 0 ? "CONNECTING TO SERVER..." : "RETRYING MATCH...");

                var matchTask = Matchmaking.FindMatchAsync(nickname);
                while (!matchTask.IsCompleted) yield return null;

                if (matchTask.IsFaulted || matchTask.IsCanceled)
                {
                    if (matchTask.IsFaulted) Debug.LogException(matchTask.Exception);
                    if (attempt + 1 < maxAttempts)
                    {
                        yield return LeaveCurrentSession();
                        continue;
                    }

                    SetLog("CONNECTION FAILED - PRESS MATCH TO RETRY");
                    _connecting = false;
                    yield break;
                }

                SetLog(Matchmaking.Current != null && Matchmaking.Current.IsHost
                    ? "CONNECTED - WAITING FOR OPPONENT..."
                    : "OPPONENT FOUND - SENDING DECK...");

                var elapsed = 0f;
                while ((NetGame.I == null || NetGame.I.MyPlayer < 0) && elapsed < networkReadyTimeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (NetGame.I != null && NetGame.I.MyPlayer >= 0)
                {
                    var deck = new FixedList32Bytes<byte>();
                    for (var i = 0; i < cards.Length; i++) deck.Add(cards[i]);
                    NetGame.I.SubmitLoadoutServerRpc(new FixedString32Bytes(nickname), deck);
                    SetLog("DECK SENT - WAITING FOR OPPONENT...");

                    elapsed = 0f;
                    while (NetGame.I != null && !NetGame.I.InGame && elapsed < opponentLoadoutTimeout)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (NetGame.I != null && NetGame.I.InGame)
                    {
                        SetLog("DUEL STARTING...");
                        yield return null;
                        SceneManager.LoadScene("GameScene");
                        yield break;
                    }
                }

                Debug.LogWarning("[스펠 스로워] 매칭 동기화 시간 초과 - 세션을 정리하고 재시도합니다.");
                yield return LeaveCurrentSession();
            }

            SetLog("MATCH TIMED OUT - PRESS MATCH TO RETRY");
            _connecting = false;
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
