namespace SpellThrower
{
    public enum CardId : byte
    {
        Fireball = 0,
        Burn = 1,
        FlamePillar = 2,
        FireRain = 3,
        Explosion = 4,

        Iceball = 5,
        IceWall = 6,
        Chill = 7,
        Frostbite = 8,
        IceAge = 9,

        Breath = 10,
        Wind = 11,
        Pull = 12,
        Collision = 13,
        Gust = 14,

        Discharge = 15,
        Thunderbolt = 16,
        LightningStrike = 17,
        Lightning = 18,
        MasterSpark = 19,

        Heal = 20,
        Warmth = 21,
        Regeneration = 22,
        Purify = 23,
        Baptism = 24,

        Draw = 25,
        Divination = 26,
        Exchange = 27,
        Supply = 28,
        Harvest = 29,

        Sprint = 30,
        Step = 31,
        Charge = 32,
        Acceleration = 33,
        Leap = 34,

        Totem = 35,
        Guardian = 36,
        Thorn = 37,
        Blessing = 38,
        Detonation = 39,

        Duel = 40,

        /// 덱에 넣을 수 없는 기본 이동. 매 턴 자동 지급되고 남으면 사라진다.
        Move = 41,

        // Readable aliases for names used in the design notes.
        Regen = Regeneration,
        Frost = Chill,
        IceAgeStart = IceAge,
        LightningBolt = LightningStrike,
        MasterSparkLine = MasterSpark,
        Cyclone = Gust,
        TotemBlast = Detonation,
    }

    public enum CardAttribute : byte
    {
        Fire,
        Ice,
        Wind,
        Lightning,
        Heal,
        Draw,
        Sprint,
        Totem,
        Special,
    }

    public enum CardValueTier : byte
    {
        Basic,
        Low,
        MidLow,
        MidHigh,
        High,
    }

    public enum CardTargetKind : byte
    {
        Self,
        Tile,
        Enemy,
        MoveTile,
        Direction,
    }

    public enum CardEffectKind : byte
    {
        Damage,
        FireZone,
        FireDamageAndZone,
        FireRain,
        FireExplosion,
        LockMove,
        IceWall,
        FrostZone,
        Frostbite,
        IceAge,
        WindPush,
        WindPull,
        LightningStrike,
        LightningLine,
        MasterSpark,
        Heal,
        Regeneration,
        Purify,
        Baptism,
        Draw,
        Divination,
        Exchange,
        Supply,
        Harvest,
        Move,
        Charge,
        Acceleration,
        Leap,
        Structure,
        Duel,
    }

    public interface ICardPlayer
    {
        int PlayerIndex { get; }
        int X { get; }
        int Y { get; }
        int Hp { get; }
        int HandImmediateDamagePower { get; }
        void Damage(int amount);
        void Heal(int amount);
        void MoveTo(int x, int y);
        void PushFrom(int sourceX, int sourceY, int tiles);
        void LockMove();
        void DrawCards(int count);
        bool AddTag(PlayerTagId tagId, byte duration, byte value = 0);
        bool HasTag(PlayerTagId tagId);
        bool RemoveTag(PlayerTagId tagId);
    }

    public sealed class CardUseContext
    {
        public CardDef Card { get; }
        public ICardPlayer Instigator { get; }
        public ICardPlayer TargetPlayer { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public IWorldEffectSink WorldEffects { get; }
        public CardEffectRuntime Runtime { get; }
        public bool HasTargetPlayer => TargetPlayer != null;

        public CardUseContext(CardDef card, ICardPlayer instigator, ICardPlayer targetPlayer, int targetX, int targetY)
            : this(card, instigator, targetPlayer, targetX, targetY, null, null)
        {
        }

        public CardUseContext(
            CardDef card,
            ICardPlayer instigator,
            ICardPlayer targetPlayer,
            int targetX,
            int targetY,
            IWorldEffectSink worldEffects)
            : this(card, instigator, targetPlayer, targetX, targetY, worldEffects, null)
        {
        }

        public CardUseContext(
            CardDef card,
            ICardPlayer instigator,
            ICardPlayer targetPlayer,
            int targetX,
            int targetY,
            IWorldEffectSink worldEffects,
            CardEffectRuntime runtime)
        {
            Card = card;
            Instigator = instigator;
            TargetPlayer = targetPlayer;
            TargetX = targetX;
            TargetY = targetY;
            WorldEffects = worldEffects;
            Runtime = runtime;
        }
    }

    public abstract class CardDef
    {
        public string Name => CardText.GetName(Id);
        public byte Cost { get; }
        public byte Range { get; }
        public byte Power { get; }
        public byte ImmediateDamagePower { get; }
        public bool Targeted { get; }
        public bool Arc { get; }
        public CardId Id { get; }
        public CardAttribute Attribute { get; }
        public CardValueTier Tier { get; }
        public CardTargetKind TargetKind { get; }
        public CardEffectKind Effect { get; }
        public bool AllowEmptyTile { get; }
        public virtual SfxId SfxCue => SfxFor(Attribute);

        protected CardDef(
            CardId id,
            CardAttribute attribute,
            CardValueTier tier,
            byte cost,
            byte range,
            byte power,
            CardTargetKind targetKind,
            CardEffectKind effect,
            byte immediateDamagePower = 0,
            bool arc = false,
            bool allowEmptyTile = false)
        {
            Id = id;
            Attribute = attribute;
            Tier = tier;
            Cost = cost;
            Range = range;
            Power = power;
            ImmediateDamagePower = immediateDamagePower;
            Targeted = targetKind != CardTargetKind.Self;
            Arc = arc;
            TargetKind = targetKind;
            Effect = effect;
            AllowEmptyTile = allowEmptyTile;
        }

        public abstract void OnUse(CardUseContext context);

        public static SfxId SfxFor(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Fire: return SfxId.CardFire;
                case CardAttribute.Ice: return SfxId.CardIce;
                case CardAttribute.Wind: return SfxId.CardWind;
                case CardAttribute.Lightning: return SfxId.CardLightning;
                case CardAttribute.Heal: return SfxId.CardHeal;
                case CardAttribute.Draw: return SfxId.CardDraw;
                case CardAttribute.Sprint: return SfxId.CardSprint;
                case CardAttribute.Totem: return SfxId.CardTotem;
                default: return SfxId.CardSpecial;
            }
        }
    }

    public sealed class ConfiguredCardDef : CardDef
    {
        public ConfiguredCardDef(
            CardId id,
            CardAttribute attribute,
            CardValueTier tier,
            byte cost,
            byte range,
            byte power,
            CardTargetKind targetKind,
            CardEffectKind effect,
            byte immediateDamagePower = 0,
            bool arc = false,
            bool allowEmptyTile = false)
            : base(id, attribute, tier, cost, range, power, targetKind, effect,
                   immediateDamagePower, arc, allowEmptyTile)
        {
        }

        public override void OnUse(CardUseContext context)
        {
            if (context == null || context.Instigator == null) return;

            switch (Effect)
            {
                case CardEffectKind.Damage:
                    if (context.Runtime != null)
                        context.Runtime.DamageAt(context.TargetX, context.TargetY, Power, context.Instigator.PlayerIndex);
                    else
                        context.TargetPlayer?.Damage(Power);
                    break;
                case CardEffectKind.FireZone:
                    if (context.Runtime != null)
                        context.Runtime.TryAddFireZone(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power, 2);
                    else
                        context.WorldEffects?.TryAddFireZone(
                            context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power, 2);
                    break;
                case CardEffectKind.FireDamageAndZone:
                    if (context.Runtime != null)
                    {
                        context.Runtime.DamageAt(context.TargetX, context.TargetY, Power, context.Instigator.PlayerIndex);
                        context.Runtime.TryAddFireZone(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, GameRules.FireZoneTick, 2);
                    }
                    else
                    {
                        context.TargetPlayer?.Damage(Power);
                        context.WorldEffects?.TryAddFireZone(
                            context.Instigator.PlayerIndex, context.TargetX, context.TargetY, GameRules.FireZoneTick, 2);
                    }
                    break;
                case CardEffectKind.FireRain:
                    context.Runtime?.FireRain(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    break;
                case CardEffectKind.FireExplosion:
                    context.Runtime?.FireExplosion(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    break;
                case CardEffectKind.LockMove:
                    if (context.Runtime != null)
                        context.Runtime.DamageAt(context.TargetX, context.TargetY, Power, context.Instigator.PlayerIndex);
                    else
                        context.TargetPlayer?.Damage(Power);
                    context.TargetPlayer?.LockMove();
                    break;
                case CardEffectKind.IceWall:
                    context.Runtime?.TryAddIceWall(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    break;
                case CardEffectKind.FrostZone:
                    if (context.Runtime != null)
                    {
                        context.Runtime.DamageAt(context.TargetX, context.TargetY, Power, context.Instigator.PlayerIndex);
                        context.Runtime.TryAddFrostZone(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, 1);
                    }
                    else
                        context.TargetPlayer?.Damage(Power);
                    break;
                case CardEffectKind.Frostbite:
                    if (context.Runtime != null)
                    {
                        context.Runtime.DamageAt(context.TargetX, context.TargetY, Power, context.Instigator.PlayerIndex);
                        context.Runtime.TryAddFrostZone(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, 2);
                    }
                    else
                        context.TargetPlayer?.Damage(Power);
                    break;
                case CardEffectKind.IceAge:
                    context.Runtime?.IceAge(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    break;
                case CardEffectKind.WindPush:
                    if (context.TargetPlayer != null)
                    {
                        if (context.Runtime != null)
                            // '바람'만 시전자를 반동시킨다. 다른 바람 계열 카드는 반동이 없다.
                            context.Runtime.WindPush(context.Instigator.PlayerIndex, context.TargetPlayer.PlayerIndex, Power,
                                                     Id == CardId.Wind);
                        else
                            context.TargetPlayer.PushFrom(context.Instigator.X, context.Instigator.Y, Power);
                    }
                    break;
                case CardEffectKind.WindPull:
                    if (context.TargetPlayer != null)
                    {
                        if (context.Runtime != null)
                            context.Runtime.WindPull(context.Instigator.PlayerIndex, context.TargetPlayer.PlayerIndex, Power);
                        else
                            context.TargetPlayer.MoveTo(context.Instigator.X, context.Instigator.Y);
                    }
                    break;
                case CardEffectKind.LightningStrike:
                    if (context.Runtime != null)
                        context.Runtime.LightningStrike(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    else
                        context.TargetPlayer?.Damage(Power);
                    break;
                case CardEffectKind.LightningLine:
                    context.Runtime?.LightningLine(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power, true);
                    break;
                case CardEffectKind.MasterSpark:
                    context.Runtime?.MasterSpark(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power);
                    break;
                case CardEffectKind.Heal:
                    context.Instigator.Heal(Power);
                    break;
                case CardEffectKind.Regeneration:
                    context.Instigator.Heal(Power);
                    if (context.Runtime != null)
                        context.Runtime.AddTag(context.Instigator.PlayerIndex, PlayerTagId.Regeneration, 3, GameRules.RegenerationTick);
                    else
                        context.Instigator.AddTag(PlayerTagId.Regeneration, 3, GameRules.RegenerationTick);
                    break;
                case CardEffectKind.Purify:
                    context.Runtime?.Purify(context.Instigator.PlayerIndex);
                    break;
                case CardEffectKind.Baptism:
                    context.Instigator.Heal(Power);
                    context.Runtime?.Purify(context.Instigator.PlayerIndex);
                    break;
                case CardEffectKind.Draw:
                    context.Instigator.DrawCards(Power);
                    break;
                case CardEffectKind.Divination:
                    context.Runtime?.Divination(context.Instigator.PlayerIndex, context.TargetX);
                    break;
                case CardEffectKind.Exchange:
                    context.Runtime?.Exchange(context.Instigator.PlayerIndex, context.TargetX);
                    break;
                case CardEffectKind.Supply:
                    if (context.Runtime != null)
                        context.Runtime.AddTag(context.Instigator.PlayerIndex, PlayerTagId.Supply, 4, 1);
                    else
                        context.Instigator.AddTag(PlayerTagId.Supply, 4, 1);
                    break;
                case CardEffectKind.Harvest:
                    context.Instigator.DrawCards(3);
                    break;
                case CardEffectKind.Move:
                    if (context.Runtime == null)
                        context.Instigator.MoveTo(context.TargetX, context.TargetY);
                    else
                        context.Runtime.TryMoveCard(context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power, false);
                    break;
                case CardEffectKind.Charge:
                    context.Runtime?.Charge(
                        context.Instigator.PlayerIndex, context.TargetX, context.TargetY, ImmediateDamagePower);
                    break;
                case CardEffectKind.Acceleration:
                    if (context.Runtime != null)
                        context.Runtime.AddTag(context.Instigator.PlayerIndex, PlayerTagId.MoveBoost, 1, 1);
                    else
                        context.Instigator.AddTag(PlayerTagId.MoveBoost, 1, 1);
                    break;
                case CardEffectKind.Leap:
                    if (context.Runtime != null)
                        context.Runtime.AddTag(context.Instigator.PlayerIndex, PlayerTagId.Leap, 1);
                    else
                        context.Instigator.AddTag(PlayerTagId.Leap, 1);
                    break;
                case CardEffectKind.Structure:
                    context.Runtime?.TryAddStructure(
                        context.Instigator.PlayerIndex, Structure, context.TargetX, context.TargetY, Power);
                    break;
            }
        }

        public StructureKind Structure { get; set; }
    }

    public sealed class DuelCardDef : CardDef
    {
        public DuelCardDef()
            : base(CardId.Duel, CardAttribute.Special, CardValueTier.High,
                   7, 0, 0, CardTargetKind.Self, CardEffectKind.Duel, 0, false)
        {
        }

        public override void OnUse(CardUseContext context)
        {
            if (context?.Instigator == null) return;

            // Duel is self-targeted in the card metadata, but its resolution
            // still compares both players' hands on the authoritative state.
            if (context.Runtime != null)
            {
                context.Runtime.ResolveDuel(context.Instigator.PlayerIndex);
                return;
            }

            if (context.TargetPlayer == null) return;

            int instigatorPower = context.Instigator.HandImmediateDamagePower;
            int targetPower = context.TargetPlayer.HandImmediateDamagePower;
            if (instigatorPower > targetPower)
                context.TargetPlayer.Damage(instigatorPower / 2);
            else if (targetPower > instigatorPower)
                context.Instigator.Damage(targetPower / 2);
        }
    }

    /// 기본 이동을 손패 카드로 보여주기 위한 카드. 코스트 0, 1칸.
    /// 실제 판정·소모는 GameRules 의 기본 이동(BaseMove) 경로가 맡는다.
    public sealed class MoveCardDef : CardDef
    {
        public MoveCardDef()
            : base(CardId.Move, CardAttribute.Sprint, CardValueTier.Basic,
                   0, 1, 1, CardTargetKind.MoveTile, CardEffectKind.Move)
        {
        }

        public override void OnUse(CardUseContext context)
        {
            if (context?.Instigator == null) return;

            if (context.Runtime != null)
                context.Runtime.TryMoveCard(
                    context.Instigator.PlayerIndex, context.TargetX, context.TargetY, Power, false);
            else
                context.Instigator.MoveTo(context.TargetX, context.TargetY);
        }
    }

    public static class Cards
    {
        public static readonly CardDef[] All =
        {
            // 코스트·사거리·위력은 노션 '카드 정보2 DB'가 원본이다. 위력의 뜻은 효과마다 다르다.
            // 즉시 피해 / 장판 틱 / 밀기 칸수 / 드로우 장수 / 구조물 HP 순으로 읽으면 된다.
            new ConfiguredCardDef(CardId.Fireball, CardAttribute.Fire, CardValueTier.Basic, 1, 4, 5, CardTargetKind.Tile, CardEffectKind.Damage, 5),
            new ConfiguredCardDef(CardId.Burn, CardAttribute.Fire, CardValueTier.Low, 4, 4, 10, CardTargetKind.Tile, CardEffectKind.FireZone, 0, false, true),
            new ConfiguredCardDef(CardId.FlamePillar, CardAttribute.Fire, CardValueTier.MidLow, 4, 4, 6, CardTargetKind.Tile, CardEffectKind.FireDamageAndZone, 6, false, true),
            new ConfiguredCardDef(CardId.FireRain, CardAttribute.Fire, CardValueTier.MidHigh, 5, 4, 12, CardTargetKind.Tile, CardEffectKind.FireRain, 12, false, true),
            new ConfiguredCardDef(CardId.Explosion, CardAttribute.Fire, CardValueTier.High, 7, 4, 16, CardTargetKind.Tile, CardEffectKind.FireExplosion, 16, false, true),

            new ConfiguredCardDef(CardId.Iceball, CardAttribute.Ice, CardValueTier.Basic, 1, 3, 3, CardTargetKind.Tile, CardEffectKind.LockMove, 3),
            new ConfiguredCardDef(CardId.IceWall, CardAttribute.Ice, CardValueTier.Low, 4, 3, 20, CardTargetKind.Tile, CardEffectKind.IceWall, 0, false, true),
            new ConfiguredCardDef(CardId.Chill, CardAttribute.Ice, CardValueTier.MidLow, 2, 3, 8, CardTargetKind.Tile, CardEffectKind.FrostZone, 8, false, true),
            new ConfiguredCardDef(CardId.Frostbite, CardAttribute.Ice, CardValueTier.MidHigh, 4, 3, 15, CardTargetKind.Tile, CardEffectKind.Frostbite, 15, false, true),
            new ConfiguredCardDef(CardId.IceAge, CardAttribute.Ice, CardValueTier.High, 7, 3, 18, CardTargetKind.Tile, CardEffectKind.IceAge, 18, false, true),

            // 바람은 위력이 밀기 칸수다. 적중 피해 6·벽 박힘 12는 CardEffectRuntime 의 공통 규칙.
            new ConfiguredCardDef(CardId.Breath, CardAttribute.Wind, CardValueTier.Basic, 2, 4, 1, CardTargetKind.Tile, CardEffectKind.WindPush, 6),
            new ConfiguredCardDef(CardId.Wind, CardAttribute.Wind, CardValueTier.Low, 2, 4, 2, CardTargetKind.Tile, CardEffectKind.WindPush, 6),
            new ConfiguredCardDef(CardId.Pull, CardAttribute.Wind, CardValueTier.MidLow, 2, 4, 2, CardTargetKind.Tile, CardEffectKind.WindPull, 6),
            new ConfiguredCardDef(CardId.Collision, CardAttribute.Wind, CardValueTier.MidHigh, 3, 4, 2, CardTargetKind.Tile, CardEffectKind.WindPush, 6),
            new ConfiguredCardDef(CardId.Cyclone, CardAttribute.Wind, CardValueTier.High, 5, 4, 3, CardTargetKind.Tile, CardEffectKind.WindPush, 6),

            // 전류 방출은 2칸 직선으로만 뻗고 첫 장애물에서 멈춘다. 빈
            // 끝 칸을 조준할 수는 있지만, Arc는 켜지 않는다.
            new ConfiguredCardDef(CardId.Discharge, CardAttribute.Lightning, CardValueTier.Basic, 2, 2, 10, CardTargetKind.Tile, CardEffectKind.LightningLine, 10, false, true),
            new ConfiguredCardDef(CardId.Thunderbolt, CardAttribute.Lightning, CardValueTier.Low, 3, 6, 12, CardTargetKind.Tile, CardEffectKind.LightningStrike, 12, true, true),
            new ConfiguredCardDef(CardId.LightningStrike, CardAttribute.Lightning, CardValueTier.MidLow, 2, 4, 6, CardTargetKind.Tile, CardEffectKind.LightningStrike, 6, true, true),
            new ConfiguredCardDef(CardId.Lightning, CardAttribute.Lightning, CardValueTier.MidHigh, 4, 5, 12, CardTargetKind.Tile, CardEffectKind.LightningStrike, 12, true, true),
            new ConfiguredCardDef(CardId.MasterSpark, CardAttribute.Lightning, CardValueTier.High, 6, 100, 16, CardTargetKind.Direction, CardEffectKind.MasterSpark, 16, true),

            new ConfiguredCardDef(CardId.Heal, CardAttribute.Heal, CardValueTier.Basic, 3, 0, 16, CardTargetKind.Self, CardEffectKind.Heal),
            new ConfiguredCardDef(CardId.Warmth, CardAttribute.Heal, CardValueTier.Low, 2, 0, 10, CardTargetKind.Self, CardEffectKind.Heal),
            new ConfiguredCardDef(CardId.Regeneration, CardAttribute.Heal, CardValueTier.MidLow, 4, 0, 8, CardTargetKind.Self, CardEffectKind.Regeneration),
            new ConfiguredCardDef(CardId.Purify, CardAttribute.Heal, CardValueTier.MidHigh, 2, 0, 0, CardTargetKind.Self, CardEffectKind.Purify),
            new ConfiguredCardDef(CardId.Baptism, CardAttribute.Heal, CardValueTier.High, 5, 0, 16, CardTargetKind.Self, CardEffectKind.Baptism),

            new ConfiguredCardDef(CardId.Draw, CardAttribute.Draw, CardValueTier.Basic, 2, 0, 2, CardTargetKind.Self, CardEffectKind.Draw),
            new ConfiguredCardDef(CardId.Divination, CardAttribute.Draw, CardValueTier.Low, 2, 0, 1, CardTargetKind.Self, CardEffectKind.Divination),
            new ConfiguredCardDef(CardId.Exchange, CardAttribute.Draw, CardValueTier.MidLow, 2, 0, 2, CardTargetKind.Self, CardEffectKind.Exchange),
            new ConfiguredCardDef(CardId.Supply, CardAttribute.Draw, CardValueTier.MidHigh, 3, 0, 1, CardTargetKind.Self, CardEffectKind.Supply),
            new ConfiguredCardDef(CardId.Harvest, CardAttribute.Draw, CardValueTier.High, 5, 0, 3, CardTargetKind.Self, CardEffectKind.Harvest),

            // DB 기준 단보가 기본, 질주가 저다.
            new ConfiguredCardDef(CardId.Sprint, CardAttribute.Sprint, CardValueTier.Low, 3, 2, 2, CardTargetKind.MoveTile, CardEffectKind.Move),
            new ConfiguredCardDef(CardId.Step, CardAttribute.Sprint, CardValueTier.Basic, 2, 1, 1, CardTargetKind.MoveTile, CardEffectKind.Move),
            new ConfiguredCardDef(CardId.Charge, CardAttribute.Sprint, CardValueTier.MidLow, 4, 2, 2, CardTargetKind.MoveTile, CardEffectKind.Charge, 12),
            new ConfiguredCardDef(CardId.Acceleration, CardAttribute.Sprint, CardValueTier.MidHigh, 3, 0, 1, CardTargetKind.Self, CardEffectKind.Acceleration),
            new ConfiguredCardDef(CardId.Leap, CardAttribute.Sprint, CardValueTier.High, 5, 0, 0, CardTargetKind.Self, CardEffectKind.Leap),

            // 토템은 위력이 구조물 HP다. 턴 종료 피해·회복은 WorldEffectSystem 이 종류별로 갖는다.
            new ConfiguredCardDef(CardId.Totem, CardAttribute.Totem, CardValueTier.Basic, 3, 3, 20, CardTargetKind.Tile, CardEffectKind.Structure, 0, false, true),
            new ConfiguredCardDef(CardId.Guardian, CardAttribute.Totem, CardValueTier.Low, 2, 4, 12, CardTargetKind.Tile, CardEffectKind.Structure, 0, false, true),
            new ConfiguredCardDef(CardId.Thorn, CardAttribute.Totem, CardValueTier.MidLow, 4, 3, 20, CardTargetKind.Tile, CardEffectKind.Structure, 0, false, true),
            new ConfiguredCardDef(CardId.Blessing, CardAttribute.Totem, CardValueTier.MidHigh, 4, 3, 20, CardTargetKind.Tile, CardEffectKind.Structure, 0, false, true),
            new ConfiguredCardDef(CardId.Detonation, CardAttribute.Totem, CardValueTier.High, 6, 3, 20, CardTargetKind.Tile, CardEffectKind.Structure, 0, false, true),

            new DuelCardDef(),
            new MoveCardDef(),
        };

        static Cards()
        {
            ((ConfiguredCardDef)All[(byte)CardId.Totem]).Structure = StructureKind.Totem;
            ((ConfiguredCardDef)All[(byte)CardId.Guardian]).Structure = StructureKind.Guardian;
            ((ConfiguredCardDef)All[(byte)CardId.Thorn]).Structure = StructureKind.Thorn;
            ((ConfiguredCardDef)All[(byte)CardId.Blessing]).Structure = StructureKind.Blessing;
            ((ConfiguredCardDef)All[(byte)CardId.Detonation]).Structure = StructureKind.Detonation;
        }

        // A valid 25-card starter deck: thirteen types, at most two copies each.
        public static readonly byte[] DeckList =
        {
            (byte)CardId.Fireball, (byte)CardId.Fireball,
            (byte)CardId.FlamePillar, (byte)CardId.FlamePillar,
            (byte)CardId.Iceball, (byte)CardId.Iceball,
            (byte)CardId.Frostbite, (byte)CardId.Frostbite,
            (byte)CardId.Breath, (byte)CardId.Breath,
            (byte)CardId.Pull, (byte)CardId.Pull,
            (byte)CardId.LightningStrike, (byte)CardId.LightningStrike,
            (byte)CardId.Heal, (byte)CardId.Heal,
            (byte)CardId.Warmth, (byte)CardId.Warmth,
            (byte)CardId.Draw, (byte)CardId.Draw,
            (byte)CardId.Divination, (byte)CardId.Divination,
            (byte)CardId.Sprint, (byte)CardId.Sprint,
            (byte)CardId.Step,
        };

        public static CardDef Get(byte id) => id < All.Length ? All[id] : null;
    }
}
