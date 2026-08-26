namespace SpellThrower
{
    /// Shared card copy for rules, card UI, deck building, and future clients.
    /// The array index is the serialized CardId value; keep it stable when cards
    /// are added because card ids are part of the network state.
    public static class CardText
    {
        public static readonly string[] Names =
        {
            "파이어볼", "불사르기", "화염 기둥", "파이어 레인", "오의 ： 익스플로전",
            "아이스볼", "얼음 방벽", "냉기", "동상", "회귀 : 빙하기의 시작",
            "숨결", "바람", "흡인", "격돌", "발생 : 사이클론",
            "전류 방출", "썬더볼트", "낙뢰", "번개", "연부 ： 마스터 스파크",
            "회복", "온기", "재생", "정화", "부여 : 세례",
            "드로우", "점술", "묻고 더블로 가", "보급", "이 중에 하나는 쓸만하겠지",
            "질주", "단보", "돌진", "가속", "곡예 : 뛰어넘기",
            "토템 : 저격", "토템 : 경계", "토템 : 감지", "토템 : 축복", "토템 : 폭탄",
            "듀얼을 신청하지", "이동",
        };

        // {0}=range, {1}=card-specific power or amount. UI can format these through GetDescription.
        public static readonly string[] Descriptions =
        {
            "{0}칸 안의 적에게 즉시 피해 {1}.\n장애물에 막힌다.",
            "{0}칸 안의 대상 타일에 불길 장판.\n상대 턴이 끝날 때마다 그 칸의 대상에게 피해 {1}.\n2회 후 소멸하며, 빈 칸이어도 횟수는 줄어든다.",
            "{0}칸 안의 대상 타일에 즉시 피해 {1} + 불길 장판.\n상대 턴 종료 2회, 그때마다 피해 10.",
            "{0}칸 안의 대상 타일과 상하좌우에 즉시 피해 {1}.\n인접 장애물 칸은 제외하고, 중심에 불길 장판.\n상대 턴 종료 2회, 그때마다 피해 10.",
            "대상 타일 기준 3×3에 즉시 피해 {1}.\n3×3 전체에 불길 장판(상대 턴 종료 2회, 틱당 8).\n같은 턴 추가 피해 없음.",

            "{0}칸 안의 적에게 즉시 피해 {1}.\n상대가 다음 턴 스스로 1칸 이동 불가.\n서리 장판이 아님.",
            "{0}칸 안의 빈 칸 중심으로 가로 또는 세로 3칸 얼음 방벽.\n칸마다 HP {1}, 2턴. 양옆의 점유·장애물 칸은 건너뜀.",
            "{0}칸 안의 대상 타일에 즉시 피해 {1} + 서리 1턴.\n그 칸에서 시작하는 이동 -1, 최소 1.",
            "{0}칸 안의 대상 타일에 즉시 피해 {1} + 서리 2턴.\n서리 위에서 시작하는 이동 -1, 최소 1.",
            "{0}칸 안의 대상 타일에 즉시 피해 {1}.\n중심 기준 3×3에 서리 2턴. 빈 타일 조준 가능.",

            "{0}칸 안의 적을 {1}칸 밀고 피해 6.\n공통 바람 규칙: 벽 충돌 시 유닛·장애물에 피해 12.",
            "{0}칸 안의 적을 {1}칸 밀고 피해 6.\n시전자는 반동으로 반대 방향 1칸 이동.\n벽 충돌 시 유닛·장애물에 피해 12.",
            "{0}칸 안의 적을 시전자 쪽으로 {1}칸 당기고 피해 6.\n경로 장애물에서 멈추며 충돌 피해 12.",
            "{0}칸 안의 적을 {1}칸 밀고 피해 6.\n벽 충돌 시 유닛·장애물에 피해 12.",
            "{0}칸 안의 적을 {1}칸 밀고 피해 6.\n벽 충돌 시 유닛·장애물에 피해 12.",

            "직선 {0}칸 경로의 대상에게 피해 {1}.\n번개 스택 장당 +4. 첫 장애물에서 멈춘다.",
            "{0}칸 안의 대상에게 피해 {1}.\n번개 스택 장당 +4.\n장애물을 무시하고, 벽을 때려도 스택 증가.",
            "{0}칸 안의 대상에게 피해 {1}.\n번개 스택 장당 +4.\n장애물을 무시하고, 벽을 때려도 스택 증가.",
            "{0}칸 안의 대상에게 피해 {1}.\n번개 스택 장당 +4.\n장애물을 무시하고, 벽을 때려도 스택 증가.",
            "시전자 기준 상하좌우 한 방향의 맵 끝까지\n모든 유닛·구조물·벽에 피해 {1}.\n장애물 관통, 번개 스택 장당 +6.",

            "HP {1} 회복. 최대 HP {2}.",
            "HP {1} 회복. 최대 HP {2}.",
            "즉시 HP {1} 회복.\n이후 자기 턴 시작마다 HP 10을 2회 회복.",
            "자신 칸과 맨해튼 1의 불길·서리 장판 제거.\nHP 회복·구조물 제거 없음.",
            "HP {1} 회복하고 자신 주변 맨해튼 1의 불길·서리 제거.\n최대 HP {2}. 구조물은 제거하지 않음.",

            "자기 덱에서 일반 카드 {1}장 드로우.\n손패 7장 초과분은 폐기.",
            "덱 위 3장을 보고 1장만 손패에 넣고\n나머지 2장은 휴지통.",
            "손패 일반 카드 1장을 휴지통에 보내고\n자기 덱에서 2장 드로우.",
            "다음 자기 턴 시작 3회 동안\n일반 카드 드로우 +1. 비중첩.",
            "자기 덱에서 일반 카드 3장 드로우.\n손패 7장 초과분은 폐기. 코스트 변경 없음.",

            "{1}칸 독립 이동. 기본 이동과 별도.",
            "{1}칸 독립 이동. 기본 이동·질주와 별도.",
            "직선 최대 {1}칸 이동.\n경로에 상대가 있으면 그 칸 앞에서 멈추고 피해 12.",
            "이번 턴 자신의 모든 이동 거리 +1.\n기본 이동·단보·질주·돌진에 적용.",
            "이번 턴 기본 이동·단보·질주·돌진으로\n경로의 장애물 또는 적 1개를 넘어 빈 칸 착지.\n돌진으로 적을 넘으면 피해 2.",

            "{0}칸 안 빈 칸에 HP {1} 저격 토템 설치.\n2턴, 자기 턴 종료 시 맨해튼 2의 가장 가까운 적에게 피해 8.",
            "{0}칸 안 빈 칸에 HP {1} 경계 토템 설치.\n2턴, 자기 턴 종료 시 맨해튼 1의 적에게 피해 6.",
            "{0}칸 안 빈 칸에 HP {1} 감지 토템 설치.\n2턴, 인접 적의 자기 턴 종료 시 피해 12.",
            "{0}칸 안 빈 칸에 HP {1} 축복 토템 설치.\n2턴, 자기 턴 시작 시 HP 14 회복. 최대 HP {2}.",
            "{0}칸 안 빈 칸에 HP {1} 폭탄 토템 설치.\n자기 턴 종료 시 맨해튼 2의 가장 가까운 적에게 피해 8.\nHP 0이면 그 칸과 상하좌우 적에게 피해 16.",

            "이 카드를 낸 뒤 양쪽 손패의 즉시 피해 합을 비교.\n승자가 자신의 손패 즉시 피해 합 절반(내림)을 패자에게 피해.\n같으면 피해 없음. 손패를 버리거나 잠그지 않음.",
            "코스트 없이 1칸 움직인다.\n매 턴 한 장 들어오고\n안 쓰면 턴 끝에 사라진다.",
        };

        /// Resources/CardArt 아래의 카드별 일러스트 파일 이름. 인덱스는 CardId.
        /// 이동 카드만 노션 카드 DB에 행이 없어 기존 아트를 그대로 옮겨 왔다.
        public static readonly string[] ArtNames =
        {
            "Fireball", "Burn", "FlamePillar", "FireRain", "Explosion",
            "Iceball", "IceWall", "Chill", "Frostbite", "IceAge",
            "Breath", "Wind", "Pull", "Collision", "Gust",
            "Discharge", "Thunderbolt", "LightningStrike", "Lightning", "MasterSpark",
            "Heal", "Warmth", "Regeneration", "Purify", "Baptism",
            "Draw", "Divination", "Exchange", "Supply", "Harvest",
            "Sprint", "Step", "Charge", "Acceleration", "Leap",
            "Totem", "Guardian", "Thorn", "Blessing", "Detonation",
            "Duel", "Move",
        };

        /// CardAttribute 순서와 같다. 덱 빌딩의 속성 탭·분포 바가 이 이름을 쓴다.
        public static readonly string[] AttributeNames =
        {
            "화염", "얼음", "바람", "번개", "회복", "드로우", "질주", "토템", "특수",
        };

        /// CardValueTier 순서와 같다. 카드의 등급이 아니라 밸류(값어치)의 크기다.
        public static readonly string[] ValueNames =
        {
            "기본", "저", "중저", "중고", "고",
        };

        /// PlayerTagId 순서와 같다. 0번(None)은 표시하지 않는다.
        /// 자신·상대에게 걸려 있는 효과 줄이 이 이름을 쓴다.
        public static readonly string[] TagNames =
        {
            "", "이동 불가", "재생", "보급", "가속", "서리 둔화", "도약",
        };

        /// CardTargetKind 순서와 같다.
        public static readonly string[] TargetKindNames =
        {
            "자신", "타일 지정", "적 지정", "이동 타일", "방향",
        };

        public const string ArcText = "장애물 관통";
        public const string BlockedText = "장애물에 막힘";
        public const string EmptyTileText = "빈 칸 조준 가능";

        public static string GetName(CardId id)
        {
            byte index = (byte)id;
            return index < Names.Length ? Names[index] : string.Empty;
        }

        public static string GetTagName(PlayerTagId id)
        {
            byte index = (byte)id;
            return index < TagNames.Length ? TagNames[index] : string.Empty;
        }

        public static string GetDescription(CardId id, CardDef card)
        {
            byte index = (byte)id;
            return index < Descriptions.Length
                ? string.Format(Descriptions[index], card.Range, card.Power, GameRules.MaxHp)
                : string.Empty;
        }

        /// 설명 위에 붙는 카드 제원. CardDef 에 이미 있는 값만 읽어 만들며,
        /// 의미가 없는 항목(자신 대상의 사거리, 위력 0)은 줄에서 뺀다.
        public static string GetStats(CardDef card)
        {
            if (card == null) return string.Empty;

            var line = Pick(AttributeNames, (byte)card.Attribute)
                     + " · 밸류 " + Pick(ValueNames, (byte)card.Tier)
                     + " · 코스트 " + card.Cost + "\n";
            if (card.Targeted && card.Range > 0) line += "사거리 " + card.Range + " · ";
            if (card.Power > 0) line += "위력 " + card.Power + " · ";
            line += Pick(TargetKindNames, (byte)card.TargetKind);
            if (card.Targeted) line += " · " + (card.Arc ? ArcText : BlockedText);
            if (card.AllowEmptyTile) line += " · " + EmptyTileText;
            return line;
        }

        static string Pick(string[] table, byte index)
        {
            return index < table.Length ? table[index] : string.Empty;
        }
    }
}
