using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SpellThrower
{
    /// 중앙 전용 서버가 권위를 갖는다 (JungleMon 의 socketHandler 모델).
    /// 두 클라이언트가 서버에 접속 → 슬롯 0/1 배정 → 2명 차면 자동 시작.
    /// 클라이언트는 의도만 보내고 판정은 전부 서버의 GameRules 가 한다.
    public class NetGame : NetworkBehaviour
    {
        public const int Port = 7777;
        const ulong Empty = ulong.MaxValue;

        public static NetGame I;

        public readonly NetworkVariable<GameState> State = new NetworkVariable<GameState>();
        public readonly NetworkVariable<bool> Started = new NetworkVariable<bool>();

        // 접속 순서대로 배정되는 플레이어 슬롯
        readonly NetworkVariable<ulong> _slot0 = new NetworkVariable<ulong>(Empty);
        readonly NetworkVariable<ulong> _slot1 = new NetworkVariable<ulong>(Empty);

        // 지금 끌고 있는 손패 자리. 상대 화면에서 그 카드만 떠오르게 하는 데만 쓴다. 255=없음.
        readonly NetworkVariable<byte> _drag0 = new NetworkVariable<byte>(byte.MaxValue);
        readonly NetworkVariable<byte> _drag1 = new NetworkVariable<byte>(byte.MaxValue);

        // 지금 행동 턴이 끝나는 서버 시각. 클라이언트는 이 값으로 남은 초를 그린다.
        readonly NetworkVariable<double> _turnEndsAt = new NetworkVariable<double>();

        // 서버 전용: 아직 게임이 시작되기 전에 받아 둔 닉네임
        readonly FixedString32Bytes[] _names = new FixedString32Bytes[2];
        readonly FixedList32Bytes<byte>[] _decks = new FixedList32Bytes<byte>[2];

        /// 이 클라이언트가 조작하는 플레이어 번호. 아직 자리를 못 받았으면 -1.
        public int MyPlayer
        {
            get
            {
                var id = NetworkManager.Singleton.LocalClientId;
                if (_slot0.Value == id) return 0;
                if (_slot1.Value == id) return 1;
                return -1;
            }
        }

        /// 셧다운 뒤에도 NetworkVariable·슬롯 값은 남는다. 실제로 연결이 살아 있을 때만 참이어야
        /// 로비로 나간 뒤 GameScene 으로 되튀지 않는다.
        public bool InGame => NetworkManager.Singleton != null &&
                              NetworkManager.Singleton.IsListening &&
                              !NetworkManager.Singleton.ShutdownInProgress &&
                              Started.Value && MyPlayer >= 0;
        public bool IsMyTurn => InGame && State.Value.turnPlayer == MyPlayer && State.Value.winner == 0;

        /// 이번 행동 턴에 남은 초. 판이 끝났으면 0.
        public float TurnSecondsLeft
        {
            get
            {
                if (!InGame || State.Value.winner != 0) return 0f;
                double left = _turnEndsAt.Value - NetworkManager.Singleton.ServerTime.Time;
                return left > 0d ? (float)left : 0f;
            }
        }

        int PlayerOf(ulong id) => _slot0.Value == id ? 0 : (_slot1.Value == id ? 1 : -1);

        /// 그 플레이어가 끌고 있는 손패 자리. 없으면 -1.
        public int DragIndexOf(int player)
        {
            if (player != 0 && player != 1) return -1;
            byte v = player == 0 ? _drag0.Value : _drag1.Value;
            return v == byte.MaxValue ? -1 : v;
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton != null) UnityEngine.Object.DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            I = this;
            if (!IsServer) return;

            State.Value = default;
            Started.Value = false;
            _slot0.Value = Empty;
            _slot1.Value = Empty;
            _drag0.Value = _drag1.Value = byte.MaxValue;
            _names[0] = _names[1] = default;
            _decks[0] = _decks[1] = default;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds) Assign(id);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (I == this) I = null;
        }

        void OnClientConnected(ulong id)
        {
            Assign(id);
        }

        void Assign(ulong id)
        {
            if (_slot0.Value == id || _slot1.Value == id) return;
            if (_slot0.Value == Empty) { _slot0.Value = id; Debug.Log("[스펠 스로워] 슬롯 0 배정: " + id); }
            else if (_slot1.Value == Empty) { _slot1.Value = id; Debug.Log("[스펠 스로워] 슬롯 1 배정: " + id); }
            else
            {
                Debug.Log("[스펠 스로워] 방이 가득 차서 접속 거부: " + id);
                NetworkManager.Singleton.DisconnectClient(id);
            }
        }

        void TryStart()
        {
            if (Started.Value) return;
            if (_slot0.Value == Empty || _slot1.Value == Empty) return;

            if (_names[0].IsEmpty || _names[1].IsEmpty) return;
            var s = GameRules.NewGame(
                (uint)(Time.realtimeSinceStartup * 1000f) ^ 0x9E3779B9u,
                ToArray(_decks[0]), ToArray(_decks[1]));
            s.p0Name = _names[0].IsEmpty ? "플레이어 1" : _names[0];
            s.p1Name = _names[1].IsEmpty ? "플레이어 2" : _names[1];
            State.Value = s;
            Started.Value = true;
            RestartTurnClock();
            Debug.Log("[스펠 스로워] 매칭 성공: 2인 게임 시작");
        }

        /// 턴 시작 처리(이동 카드 지급·드로우·코스트 충전)가 끝난 뒤부터 45초를 센다.
        void RestartTurnClock()
        {
            _turnEndsAt.Value = NetworkManager.Singleton.ServerTime.Time + GameRules.TurnSeconds;
        }

        /// 제한 시간이 0이 되면 그 플레이어가 턴 종료를 누른 것과 같이 처리한다.
        void Update()
        {
            if (!IsServer || !Started.Value) return;
            var s = State.Value;
            if (s.winner != 0) return;
            if (NetworkManager.Singleton.ServerTime.Time < _turnEndsAt.Value) return;

            GameRules.EndTurn(ref s);
            State.Value = s;
            RestartTurnClock();
        }

        /// 끊기면 그 판은 끝 (재접속 처리 없음). 두 명 다 나가면 다음 매치를 받을 수 있게 초기화한다.
        void OnClientDisconnected(ulong id)
        {
            int p = PlayerOf(id);
            if (p < 0) return;

            if (Started.Value && State.Value.winner == 0)
            {
                var s = State.Value;
                s.winner = (byte)(2 - p);   // 남은 쪽 승
                s.foeLeft = 1;
                State.Value = s;
            }

            if (p == 0) { _slot0.Value = Empty; _drag0.Value = byte.MaxValue; _names[0] = default; _decks[0] = default; }
            else { _slot1.Value = Empty; _drag1.Value = byte.MaxValue; _names[1] = default; _decks[1] = default; }

            if (_slot0.Value == Empty && _slot1.Value == Empty)
            {
                Started.Value = false;
                Debug.Log("[스펠 스로워] 방이 비었습니다 - 다음 매칭 대기");
            }
        }

        // ---------------- 클라이언트 → 서버 ----------------
        [ServerRpc(RequireOwnership = false)]
        public void SubmitLoadoutServerRpc(FixedString32Bytes name,
            ForceNetworkSerializeByMemcpy<FixedList32Bytes<byte>> deck,
            ServerRpcParams p = default)
        {
            int player = PlayerOf(p.Receive.SenderClientId);
            byte[] cards = ToArray(deck.Value);
            if (player < 0 || name.IsEmpty || !GameRules.IsValidDeck(cards)) return;
            _names[player] = name;
            _decks[player] = deck.Value;
            TryStart();
        }

        static byte[] ToArray(FixedList32Bytes<byte> list)
        {
            var cards = new byte[list.Length];
            for (int i = 0; i < list.Length; i++) cards[i] = list[i];
            return cards;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetNameServerRpc(FixedString32Bytes name, ServerRpcParams p = default)
        {
            int player = PlayerOf(p.Receive.SenderClientId);
            if (player < 0 || name.IsEmpty) return;
            _names[player] = name;

            if (!Started.Value) return;
            var s = State.Value;
            if (player == 0) s.p0Name = name; else s.p1Name = name;
            State.Value = s;
        }

        [ServerRpc(RequireOwnership = false)]
        public void MoveServerRpc(int x, int y, ServerRpcParams p = default)
        {
            if (!Started.Value) return;
            int player = PlayerOf(p.Receive.SenderClientId);
            if (player < 0) return;
            var s = State.Value;
            if (GameRules.TryMove(ref s, player, x, y)) State.Value = s;
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayCardServerRpc(int handIndex, int x, int y, ServerRpcParams p = default)
        {
            if (!Started.Value) return;
            int player = PlayerOf(p.Receive.SenderClientId);
            if (player < 0) return;
            var s = State.Value;
            if (GameRules.TryPlay(ref s, player, handIndex, x, y)) State.Value = s;
        }

        /// 연출 전용. 판정에 쓰지 않으므로 값 검사도 하지 않는다.
        [ServerRpc(RequireOwnership = false)]
        public void SetDragServerRpc(byte handIndex, ServerRpcParams p = default)
        {
            int player = PlayerOf(p.Receive.SenderClientId);
            if (player < 0) return;
            if (player == 0) _drag0.Value = handIndex; else _drag1.Value = handIndex;
        }

        /// 항복. 턴과 무관하게 언제든 받는다.
        [ServerRpc(RequireOwnership = false)]
        public void SurrenderServerRpc(ServerRpcParams p = default)
        {
            if (!Started.Value) return;
            int player = PlayerOf(p.Receive.SenderClientId);
            var s = State.Value;
            if (GameRules.Surrender(ref s, player)) State.Value = s;
        }

        [ServerRpc(RequireOwnership = false)]
        public void EndTurnServerRpc(ServerRpcParams p = default)
        {
            if (!Started.Value) return;
            int player = PlayerOf(p.Receive.SenderClientId);
            var s = State.Value;
            if (player < 0 || s.winner != 0 || s.turnPlayer != player) return;
            GameRules.EndTurn(ref s);
            State.Value = s;
            RestartTurnClock();
        }
    }
}
