using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SpellThrower
{
    /// Unity Relay + Lobby 자동 매칭.
    /// QuickJoin 으로 빈 방을 찾고, 없으면 방을 만들어 상대를 기다린다.
    /// Relay 가 패킷을 중계하므로 포트포워딩·공인 IP·전용 서버가 전부 필요 없다.
    public static class Matchmaking
    {
        public const string SessionType = "spellthrower1v1";
        const string QueueName = "spellthrower-1v1";
        const string WebSessionName = "SpellThrower-WebGL-v3";
        const string WebSessionIdPrefix = "spellthrower-webgl-v3-";
        const int WebSessionSlots = 8;

        public static ISession Current;
        static Task _prepareTask;
        static int _webSlotOffset;

        public static Task PrepareAsync()
        {
            if (_prepareTask != null && (_prepareTask.IsFaulted || _prepareTask.IsCanceled))
                _prepareTask = null;
            return _prepareTask ??= Prepare();
        }

        public static async Task FindMatchAsync(string nick)
        {
            await PrepareAsync();

            // 브라우저는 UDP 소켓을 사용할 수 없으므로 보안 WebSocket Relay를 사용한다.
            // 데스크톱은 기존 Burst/UnityTLS 문제를 피하기 위해 프로토타입의 UDP 경로를 유지한다.
#if UNITY_WEBGL && !UNITY_EDITOR
            var networkManager = NetworkManager.Singleton;
            var transport = networkManager == null ? null : networkManager.GetComponent<UnityTransport>();
            if (transport == null)
                throw new System.InvalidOperationException("WebGL matchmaking requires UnityTransport.");
            transport.UseWebSockets = true;
            var relayProtocol = RelayProtocol.WSS;
#else
            var relayProtocol = RelayProtocol.UDP;
#endif

            var sessionName = "SpellThrower";
#if UNITY_WEBGL && !UNITY_EDITOR
            sessionName = WebSessionName;
#endif

            var session = new SessionOptions
            {
                MaxPlayers = 2,
                Type = SessionType,
                Name = sessionName
            }
            .WithNetworkOptions(new NetworkOptions { RelayProtocol = relayProtocol })
            .WithRelayNetwork();

#if UNITY_WEBGL && !UNITY_EDITOR
            // CreateOrJoin은 같은 ID에 동시에 들어온 요청을 하나의 세션으로 수렴시킨다.
            // Quick Join timeout 뒤 양쪽이 각각 호스트가 되는 race와 의도적인 대기를 없앤다.
            // 이미 2명이 찬 슬롯은 다음 ID로 넘어가 여러 대전을 동시에 수용한다.
            Current = null;
            for (var attempt = 0; attempt < WebSessionSlots; attempt++)
            {
                var slot = (_webSlotOffset + attempt) % WebSessionSlots;
                try
                {
                    Current = await MultiplayerService.Instance.CreateOrJoinSessionAsync(
                        WebSessionIdPrefix + slot, session);
                    _webSlotOffset = slot;
                    break;
                }
                catch (SessionException e) when (attempt + 1 < WebSessionSlots)
                {
                    Debug.LogWarning("[스펠 스로워] WebGL 세션 슬롯 " + slot +
                                     " 참가 실패, 다음 슬롯 시도: " + e.Message);
                    // Create-or-join은 플레이어당 초당 1회 제한이므로 다음 요청을 간격 둔다.
                    await Awaitable.WaitForSecondsAsync(1.1f);
                }
            }

            if (Current == null)
                throw new System.InvalidOperationException("No WebGL matchmaking session slot is available.");
#else
            Current = await MultiplayerService.Instance.MatchmakeSessionAsync(
                new MatchmakerOptions { QueueName = QueueName }, session);
#endif
            Debug.Log("[스펠 스로워] 세션 " + Current.Id +
                      " 호스트=" + Current.IsHost + " 플레이어=" + Current.PlayerCount + "/" + Current.MaxPlayers);
        }

        static async Task Prepare()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // 데스크톱은 프로세스별, WebGL은 페이지 로드별 프로필을 사용한다.
                // 두 브라우저 탭이 같은 익명 플레이어로 인식되면 1:1 매칭이 성립하지 않는다.
                var options = new InitializationOptions()
                    .SetProfile(RuntimeProfile());
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

        }

        static string RuntimeProfile()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Authentication 프로필 이름은 최대 30자다.
            return "web-" + System.Guid.NewGuid().ToString("N").Substring(0, 24);
#else
            return "p" + System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
        }

        public static async Task LeaveAsync()
        {
            var s = Current;
            Current = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            // 재시도나 다음 판은 방금 실패했거나 끝난 슬롯을 건너뛴다.
            _webSlotOffset = (_webSlotOffset + 1) % WebSessionSlots;
#endif
            if (s == null) return;
            try { await s.LeaveAsync(); }
            catch (System.Exception e) { Debug.LogWarning("[스펠 스로워] 나가기 실패: " + e.Message); }
        }
    }
}
