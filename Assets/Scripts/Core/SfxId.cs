namespace SpellThrower
{
    /// 코드와 SfxLibrary.asset 사이의 안정적인 식별자. 실제 AudioClip은 UI 계층에서만 보유한다.
    /// 카드별 식별자를 만들지 않고 속성별 공용 슬롯을 사용한다.
    public enum SfxId : byte
    {
        None = 0,

        CardFire = 1,
        CardLightning = 2,
        CardIce = 3,
        CardWind = 4,
        CardHeal = 5,
        CardDraw = 6,
        CardSprint = 7,
        CardTotem = 8,
        CardSpecial = 9,

        PlayerMove = 10,
        PlayerHurt = 11,
        PlayerDeath = 12,
        TurnStart = 13,
        Victory = 14,
        Defeat = 15,
        Draw = 16,
        Burn = 17,
        CardSelect = 18,
        UiClick = 19,

        // 배열 기반 SfxLibrary의 마지막 경계. 사운드 슬롯이 아님.
        Count = 20,
    }

    public enum GameplayActionKind : byte
    {
        None = 0,
        CardUsed = 1,
    }
}
