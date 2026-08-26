using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellThrower
{
    /// 새 메뉴의 로컬 닉네임/덱을 기존 매칭 및 NetGame 공개 API에 전달한다.
    /// Matchmaking, NetGame, NetworkManager의 구현은 변경하지 않는다.
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
            SetLog("CONNECTING TO SERVER...");

            var matchTask = Matchmaking.FindMatchAsync(nickname);
            while (!matchTask.IsCompleted) yield return null;

            if (matchTask.IsFaulted || matchTask.IsCanceled)
            {
                SetLog("CONNECTION FAILED");
                if (matchTask.IsFaulted) Debug.LogException(matchTask.Exception);
                _connecting = false;
                yield break;
            }

            SetLog(Matchmaking.Current != null && Matchmaking.Current.IsHost
                ? "CONNECTED - WAITING FOR OPPONENT..."
                : "OPPONENT FOUND - SENDING DECK...");

            const float loadoutTimeout = 20f;
            var elapsed = 0f;
            while ((NetGame.I == null || NetGame.I.MyPlayer < 0) && elapsed < loadoutTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (NetGame.I == null || NetGame.I.MyPlayer < 0)
            {
                SetLog("CONNECTED - WAITING FOR GAME SERVER...");
                _connecting = false;
                yield break;
            }

            var deck = new FixedList32Bytes<byte>();
            for (var i = 0; i < cards.Length; i++) deck.Add(cards[i]);
            NetGame.I.SubmitLoadoutServerRpc(new FixedString32Bytes(nickname), deck);
            SetLog("DECK SENT - WAITING FOR OPPONENT...");
            StartCoroutine(WaitForMatchStart());
        }

        IEnumerator WaitForMatchStart()
        {
            // 서버가 두 플레이어의 유효한 loadout을 모두 받은 뒤 Started를 동기화한다.
            // NetGame의 구현은 건드리지 않고 그 공개 상태만 관찰한다.
            while (NetGame.I != null && !NetGame.I.InGame)
                yield return null;

            if (NetGame.I == null)
            {
                SetLog("CONNECTION LOST");
                _connecting = false;
                yield break;
            }

            SetLog("DUEL STARTING...");
            yield return null;
            SceneManager.LoadScene("GameScene");
        }

        void SetLog(string message)
        {
            if (_connectionLog != null) _connectionLog.text = message;
        }
    }
}
