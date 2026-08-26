using SpellThrower;

namespace SpellThrower.Tests
{
    /// <summary>
    /// Tests talk to the rules through this small adapter instead of depending on
    /// GameState fields directly. If the runtime state is reorganized, the test
    /// seam is intentionally limited to this file.
    /// </summary>
    internal sealed class RuleTestDriver
    {
        GameState _state;

        public RuleTestDriver(uint seed = 0x12345678u)
        {
            _state = GameRules.NewGame(seed);
        }

        public int CurrentPlayer => _state.turnPlayer;
        public byte ActionsRemaining => _state.actionLeft;
        public byte Winner => _state.winner;

        public int AddCard(int player, CardId card)
        {
            ref var hand = ref GameRules.Hand(ref _state, player);
            hand.Add((byte)card);
            return hand.Length - 1;
        }

        public void ClearHand(int player) => GameRules.Hand(ref _state, player).Clear();

        public int HandCount(int player) => GameRules.Hand(ref _state, player).Length;
        public int DeckCount(int player) => GameRules.Deck(ref _state, player).Length;
        public int DiscardCount(int player) => GameRules.Disc(ref _state, player).Length;

        public byte Health(int player) => GameRules.Hp(ref _state, player);

        public void SetHealth(int player, byte value) => GameRules.Hp(ref _state, player) = value;

        public void SetPosition(int player, int x, int y)
        {
            GameRules.X(ref _state, player) = (byte)x;
            GameRules.Y(ref _state, player) = (byte)y;
        }

        public void BlockTile(int x, int y)
        {
            if (!GameRules.InBounds(x, y))
                throw new System.ArgumentOutOfRangeException(nameof(x));

            _state.obstacles |= 1UL << (y * GameRules.Size + x);
        }

        public bool CanPlay(int player, int handIndex, int x = 0, int y = 0) =>
            GameRules.CanPlay(ref _state, player, handIndex, x, y);

        public bool Play(int player, int handIndex, int x = 0, int y = 0) =>
            GameRules.TryPlay(ref _state, player, handIndex, x, y);

        /// 기본 이동. 턴마다 한 번만 쓸 수 있다.
        public bool Move(int player, int x, int y) => GameRules.TryMove(ref _state, player, x, y);

        public bool HasMoveCard(int player) => GameRules.BaseMove(ref _state, player) != 0;

        public void EndTurn() => GameRules.EndTurn(ref _state);

        public int Range(CardId card) => GameRules.RangeOf(ref _state, (byte)card);
    }
}
