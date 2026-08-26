using Unity.Netcode;

namespace SpellThrower
{
    public enum WorldEffectKind : byte
    {
        FireZone = 1,
        DelayedTeleport = 2,
        FrostZone = 3,
        Structure = 4,
    }

    public enum StructureKind : byte
    {
        None = 0,
        IceWall = 1,
        Totem = 2,
        Guardian = 3,
        Thorn = 4,
        Blessing = 5,
        Detonation = 6,
    }

    public enum WorldEffectPhase : byte
    {
        TurnStart = 1,
        TurnEnd = 2,
    }

    /// GameState 안에 저장되는 장기 효과의 unmanaged 표현.
    public struct WorldEffectRecord : INetworkSerializeByMemcpy
    {
        public WorldEffectKind Kind;
        public WorldEffectPhase Phase;
        public byte SourcePlayer;
        public byte TriggerPlayer;
        public byte TargetPlayer;
        public byte X;
        public byte Y;
        public byte RemainingTurns;
        public byte Power;
        public ushort Sequence;
        public StructureKind Structure;
        public byte Aux;

        public WorldEffectRecord(
            WorldEffectKind kind,
            WorldEffectPhase phase,
            byte sourcePlayer,
            byte triggerPlayer,
            byte targetPlayer,
            byte x,
            byte y,
            byte remainingTurns,
            byte power)
        {
            Kind = kind;
            Phase = phase;
            SourcePlayer = sourcePlayer;
            TriggerPlayer = triggerPlayer;
            TargetPlayer = targetPlayer;
            X = x;
            Y = y;
            RemainingTurns = remainingTurns;
            Power = power;
            Sequence = 0;
            Structure = StructureKind.None;
            Aux = 0;
        }
    }

    /// CardDef가 맵/턴 효과를 등록하기 위한 최소 표면.
    public interface IWorldEffectSink
    {
        bool TryAddFireZone(int sourcePlayer, int x, int y, byte power = 2, byte ticks = 2);
        bool TryScheduleTeleport(int targetPlayer, int x, int y, byte delayTurns = 2);
    }
}
