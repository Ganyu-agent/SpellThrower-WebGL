using System;
using System.Collections.Generic;
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
        const string WebSessionName = "SpellThrower-WebGL-v1";

        public static ISession Current;
        static Task _prepareTask;

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
            // WebGL 심사 빌드는 Matchmaker queue가 아니라 Lobby quick-join을 사용한다.
            // 같은 SpellThrower 로비가 있으면 입장하고, 없으면 짧게 재시도한 뒤 하나를 만든다.
            // Relay/WSS와 Netcode 세션은 동일하게 유지하므로 브라우저의 UDP 제한만 우회한다.
            // 두 페이지가 동시에 빈 로비를 확인하면 둘 다 새 로비를 만들 수 있으므로,
            // 요청 시작 전에 페이지별 무작위 지연을 둬 먼저 생성된 로비에 참가할 시간을 준다.
            var rendezvousDelayMs = 1000 + Math.Abs(Guid.NewGuid().GetHashCode() % 9000);
            await Awaitable.WaitForSecondsAsync(rendezvousDelayMs / 1000f);
            var quickJoin = new QuickJoinOptions
            {
                Filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.Name, WebSessionName, FilterOperation.Equal)
                },
                // 두 브라우저가 동시에 빈 로비를 조회하면 둘 다 생성 단계로
                // 넘어갈 수 있다. 페이지별 작은 지연으로 한쪽이 먼저 만든 로비를
                // 다른 쪽이 발견할 시간을 남긴다.
                Timeout = TimeSpan.FromSeconds(16d),
                CreateSession = true
            };
            Current = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, session);
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
            if (s == null) return;
            try { await s.LeaveAsync(); }
            catch (System.Exception e) { Debug.LogWarning("[스펠 스로워] 나가기 실패: " + e.Message); }
        }
    }
}
