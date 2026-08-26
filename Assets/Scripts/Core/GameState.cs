using Unity.Collections;
using Unity.Netcode;

namespace SpellThrower
{
    public enum PlayerTagId : byte
    {
        None = 0,
        MoveLocked = 1,
        Regeneration = 2,
        Supply = 3,
        MoveBoost = 4,
        WindFrostSlow = 5,
        Leap = 6,
    }

    public struct PlayerTag : INetworkSerializeByMemcpy
    {
        public PlayerTagId Id;
        public byte DurationTurns;
        public byte Value;

        public PlayerTag(PlayerTagId id, byte durationTurns, byte value = 0)
        {
            Id = id;
            DurationTurns = durationTurns;
            Value = value;
        }
    }

    /// 전체 게임 상태. unmanaged 이므로 NetworkVariable<GameState> 가 memcpy 로 그대로 동기화된다.
    public struct GameState : INetworkSerializeByMemcpy
    {
        public byte p0X, p0Y, p1X, p1Y;
        public byte p0Hp, p1Hp;
        public byte turnPlayer;              // 0 / 1
        public byte turnCount;               // 행동 턴 번호. 1부터 시작, EndTurn 마다 +1. 라운드는 GameRules.Round
        public byte actionLeft;              // 남은 코스트. 턴 시작마다 첫 턴 4 → 최대 10 까지 충전
        public byte p0BaseMove, p1BaseMove;  // 기본 이동 카드 사용 가능 여부
        public byte p0Burned, p1Burned;      // 손패 초과로 방금 버린 카드 (255 = 없음)
        public byte p0BurnSeq, p1BurnSeq;    // UI 알림용 변경 번호
        // Kept in the old wire position so already-connected clients do not
        // receive a differently-sized prefix after the legacy range migration.
        public sbyte reservedLegacyRangeBonus;
        public byte p0MoveLocked, p1MoveLocked; // 얼음: 다음 턴 기본 이동 봉쇄
        public byte p0LightningStack, p1LightningStack; // 이번 턴 번개 카드 적중 횟수
        public byte winner;                  // 0=진행중 1=P1 2=P2
        public byte foeLeft;                 // 1 = 접속이 끊겨서 끝난 판
        public uint rng;                     // 셔플용 xorshift 시드
        public ulong obstacles;              // 8x8 보드 장애물 비트 마스크
        public FixedList128Bytes<byte> obstacleHp; // 맵 벽별 HP. 비트가 켜진 칸은 기본 HP 3.
        public FixedString32Bytes p0Name, p1Name;
        public FixedList64Bytes<byte> p0Hand, p1Hand;
        public FixedList64Bytes<byte> p0Deck, p1Deck;
        public FixedList64Bytes<byte> p0Disc, p1Disc;
        public FixedList32Bytes<PlayerTag> p0Tags, p1Tags;
        public FixedList512Bytes<WorldEffectRecord> worldEffects;
        public ushort nextWorldEffectSequence;
        public GameplayActionKind lastActionKind;
        public byte lastActionPlayer;
        public byte lastActionCardId;
        public byte lastActionTargetX, lastActionTargetY;
        public ushort lastActionSequence;
    }
}
