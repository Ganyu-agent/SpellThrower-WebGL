# 작업 현황 — VFX + 노션 카드 개편 + 이동 카드화

작성 2026-08-19 / 갱신 2026-08-19 (2차) / 브랜치 `feat/vfx`

> ✅ **컴파일 통과, EditMode 테스트 53개 전부 통과.** 남은 건 2인 접속 실기 확인뿐입니다.

---

## 1. 확정된 결정 (질문·답변 결과)

| 항목 | 결정 |
|---|---|
| 화염 | **노션대로.** `Arc=false`(장애물 차단), 장판 없음, 즉시 피해 2, 사거리 4 |
| 카드 범위 | **기본 카드만** — 8속성 기본 1장씩 + 듀얼 + 이동 = 10종. 노션 확장 32종은 다음 작업 |
| 갈고리(Hook) | **삭제** |
| 토템 | **규칙까지 전부 구현** (HP 3, 칸 점유, 대상형 타격 가능, 자기 턴 종료 발사) |
| 얼음 장판 VFX | 대상이 **묶여 있는 동안 유지** (Start → Active 루프 → Ending) |
| 집중(Focus) | 노션 8속성에서 빠짐 → **삭제** |
| 이동 | 기본 이동 UI 삭제, **손패 카드로 전환** (기획서 v2 §6 그대로) |
| 상대 턴 UI | 내 행동력·이동 정보 숨김 |

### 참고한 노션 문서
- `카드 정보2` (2026-08-19 06:14) — 8속성 × 5밸류 = 40종 + 특수 1종 확정안
- `Spell Thrower — 카드 시스템 기획서 (v2/v3)` — 턴 구조, 이동 카드 규칙, 덱 순환
- `토템` (카드 정보2 DB) — 토템 기본 카드 속성

---

## 2. 완료된 작업

### 2.1 VFX 에셋 준비 ✅

`Assets/Resources/VFX/Spell/` 8장의 임포트 설정을 픽셀아트용으로 수정했습니다.
기본 설정이면 `nPOTScale: ToNearest`가 걸려 **832×64 → 1024×64로 강제 리사이즈**되어 런타임 슬라이싱이 깨집니다.

바꾼 값: `enableMipMap 0` / `filterMode 0(Point)` / `nPOTScale 0` / `alphaIsTransparency 1` / `textureCompression 0`
→ 에디터에서 8장 전부 원본 해상도 + Point 필터로 로드되는 것을 확인했습니다.

**시트 구조 (코드에 반영 완료)**

| 시트 | 셀 | 열 | 프레임 | 피벗(x,y) | 내용 |
|---|---|---|---|---|---|
| `Firebolt SpriteSheet` | 48×48 | 11 | 0~3 투사체 / 4 빈칸 / **5~10 폭발** | 0.5, 0.40 | 투사체 + 명중이 한 장에 |
| `Fire Breath SpriteSheet` | 48×48 | 8 | 행0=0~2 / 행1=8~11 / 행2=16~22 | 0.43, 0.06 | 장판용 — **아직 미사용** |
| `Ice VFX 2 Start` | 32×32 | 9 | 0~8 | 0.5, 0 | 서리 솟음 |
| `Ice VFX 2 Active` | 32×32 | 8 | 0~7 | 0.5, 0 | 얼음 결정 (루프) |
| `Ice VFX 2 Ending` | 32×32 | 18 | 0~17 | 0.5, 0 | 부서짐 |
| `Wind` | 48×32 | 12 | 0~11 | **0, 0.5** | 왼→오른쪽으로 뻗는 돌풍 |
| `Thunder` | 64×64 | 13 | 0~12 | 0.5, 0 | 바닥 기준 낙뢰 |
| `Heal` | 48×48 | 16 | 0~15 | 0.5, 0 | 바닥에서 솟는 기둥 |
| `TotemSheet` (신규) | 24×136 | 17 | 0~11 사용 | 0.5, 0 | 점점 밝아지는 토템 (12~16은 미사용) |

> 🗑️ **`Assets/Resources/VFX/Spell/totem.png` 는 지워도 됩니다.** Resources 안에 있으면 빌드에 그대로 포함됩니다. (사용자 파일이라 임의 삭제 안 함)

### 2.2 코어 규칙 — 노션 반영 ✅

**`CardDef.cs`**
- `CardId`: `Fire0 Lightning1 Ice2 Wind3 Heal4 Draw5 Sprint6 Totem7 Duel8 Move9`
- `ICardPlayer.PushFrom` → `bool PushFrom(int sx, int sy, int tiles, out int wallX, out int wallY)`
- `CardUseContext.DamageBonus` 추가 (번개 스택)
- 전격 피해 3 / 바람 적중 1 + 밀기 2 + 벽 박힘 2 + 시전자 반동 1 / 토템·이동 신규

**`GameState.cs`** — `p0BaseMove` 삭제, `lightningCount` 추가, `lastCard/Player/X/Y/Seq` 5바이트 추가(연출 라우팅 전제)

**`GameRules.cs`** — `IsBlocked`가 토템 포함, `TryMove/CanMove/BaseMove` 삭제, `GiveMoveCard/DropMoveCards/NormalHandCount`, `HandSlots = MaxHand + 1`, 번개 스택 `+2×(n−1)`, 이동 카드 즉시 소멸, `Push`가 충돌 칸 반환

**`WorldEffect.cs` / `WorldEffectSystem.cs`** — `Totem = 3` (Power = HP), `TryAddTotem/HasTotem/TryDamageTotem/ApplyTotem`, 소유자 턴 종료마다 맨해튼 2 안 적에게 1, 2턴 또는 HP 0에 소멸

### 2.3 UI ✅
- `GameUI.cs`: 기본 이동 버튼 제거, 손패 슬롯 8칸, `Card_Move` 아트, 카드 설명 갱신, 상대 턴에는 내 행동력 숨김
- `SpellThrowerSceneBuilder.cs`: `BasicMoveButton` 생성 제거

### 2.4 테스트 복구 ✅ (2차 작업)

`GameRules.TryMove` / `CardId.Focus` / `CardId.Hook` / 옛 `PushFrom` 참조를 전부 정리했습니다. **53/53 통과.**

- `RuleTestDriver.Move()` → 손패의 이동 카드를 찾아 `TryPlay` (없으면 false). `HasMoveCard()` 추가
- `GameRulesTests` 재작성 — 손패 장수는 `NormalCount()`(이동 카드 제외)로 센다
- 삭제: Hook 테스트 2개(`DuelHookIntegrationTests.cs` → **`DuelIntegrationTests.cs`** 로 이름 변경, meta guid 유지), Focus 사거리 테스트
- 태그 계약 테스트의 `PlayerTagId.Focus` → `MoveLocked` (범용 태그 자리표시자였음)
- 듀얼 테스트 기대값을 `Cards.Get(Lightning).ImmediateDamagePower` 로 바꿈 — 전격 즉발이 4→3 이 되면서 피해가 2→1 로 내려간 게 원인이었음 (규칙 버그 아님)

**새로 넣은 테스트**: 이동 카드 지급/소멸/코스트0, 덱의 이동 카드 거부, 번개 스택(3/5/7 + 턴 넘기면 리셋), 바람 벽 충돌(적중1+박힘2), 바람 밀기 2칸 + 시전자 반동, 토템 설치·칸 점유·턴 종료 발사, 토템 피격·파괴, 얼음이 이동 카드 지급을 막는지

### 2.5 VFX 구현 ✅ (2차 작업)

**`BattleFx.cs`**
- `Sheet` 에 `First / PivotX / PivotY / PingPong` 추가. 시트 경로·프레임·피벗을 표 그대로 상수화
- `FxKind` 확장: `FireFly FireHit IceStart IceLoop IceEnd Wind Thunder HealCast TotemRise TotemIdle None`
- `IEnumerator Projectile(from, to)` — 0~3 루프로 날아가고 **도착 순간 반환**(호출한 쪽이 바로 피해 연출을 붙일 수 있게), 폭발 5~10은 뒤에서 재생
- `IEnumerator Beam(from, to, tiles)` — 대상 방향으로 회전 + 거리와 무관하게 사거리(4칸)만큼 X 스케일. 피벗 x=0
- `void PlayGround(kind, world, scale)` — 낙뢰·회복 기둥을 바닥 기준 1회 재생
- `BeginZones() / KeepZone(key, world, start, loop, end, scale) / EndZones()` — 유지형 연출. 이번 프레임에 안 불린 존은 Ending 재생 후 스스로 정리
- `void Dash(Transform, seconds)` — 0.04초마다 파츠 스냅샷을 떠 파랗게 페이드아웃
- 토템은 `TotemRise`(0~11 정재생) → `TotemIdle`(0~11 핑퐁 22프레임) 존으로 처리. 별도 `SpawnTotem` 없음

**`BattleSequencer.cs`**
- `Differs`: `rangeBonus` → `lastCardSeq`
- 순서: **카드 시전(투사체/빔 도착까지 대기) → 이동 → 피해 → 그 밖**
- 라우팅: 화염=투사체+폭발 / 전격=Thunder / 바람=Beam / 회복=HealCast / 질주=Dash / 얼음·토템=존(GameUI) / 드로우·이동·듀얼=없음
- 카드 연출이 있으면 범용 `FxKind.Hit` 과 중복 공격 모션을 생략한다

**`GameUI.cs`**
- `DrawFieldFx()` — 매 프레임 `BeginZones → 얼음(양쪽 MoveLocked/태그 확인) · 토템(worldEffects) → EndZones`
- 얼음 존은 `_seq.WorldOf()` 로 말을 따라간다 (밀려도 붙어 있게)
- 토템 타일: `tileTotem` 색 + `▲남은HP` 마커

**검증**: 에디터에서 `BattleFx.Frames()` 를 리플렉션으로 직접 호출해 14종 전부 프레임 수·rect·피벗을 확인했습니다. (예: FireHit 이 x=240~480, TotemIdle 이 22프레임 핑퐁)

### 2.6 코스트 UI + 턴당 코스트 10 ✅ (3차 작업)

- **`GameRules.ActionsPerTurn` 3 → 10** (사용자 지시). 카드 코스트는 그대로 1 = **반 칸**.
  노션 기획서 v2 의 "턴당 코스트 3" 과는 이제 다릅니다 — 기획서를 고쳐야 하면 알려주세요.
- `GameUI` 에 코스트 줄 두 개(내 것 아래, 상대 것 위). 동그라미 5칸, `CostUI_Fill/Half/Empty` 를 이름으로 찾아 씁니다
- **점멸**: 드래그 중인 카드가 소모할 만큼만 `CostUI_Mask` 를 겹쳐 흰색으로 깜빡입니다. 마스크는 `CostUI.png` 와 같은 캔버스에 그려져 있어 **슬라이스 rect 를 그대로 재사용**합니다 (`Sprite.Create(mask, _costFill.rect, ...)`) → 나중에 칸을 다시 잘라도 자동으로 따라옵니다
- 상대 코스트도 같은 방식. 상대가 끄는 카드는 `NetGame.DragIndexOf` 로 알 수 있어 점멸까지 같이 됩니다
- 자기 턴이 아닌 쪽 줄은 빈 칸으로 그립니다 (`actionLeft` 는 현재 턴 플레이어 값 하나뿐이라 그대로 쓰면 헷갈립니다)
- `_selfBar` 의 "행동력 N" 글자는 제거
- `CostUI_Mask.png` 임포트 설정을 고쳤습니다 — 기본값이면 nPOT 리사이즈로 320×268 → 256×256 이 되어 rect 가 어긋납니다 (Sprite / Point / no mipmap / nPOT None / Uncompressed)

**같이 고친 것**: 씬의 `Game/Hand` 슬롯이 7칸인데 `HandSlots` 가 8이라 `Awake` 의 `GetChild(7)` 에서 예외가 나 **게임 UI 가 통째로 안 붙던 버그**. 2차 작업 때 들어간 문제입니다. 마지막 슬롯을 복제해 8칸을 채웁니다 (턴 배너와 같은 방식)

### 2.7 보드 표시 월드화 + 연출 손질 ✅ (4차 작업)

**원인 하나로 묶인 문제들** — 캔버스가 Screen Space Overlay 라 UI 타일이 말·이펙트를 무조건 덮고, 포스트프로세스(블룸)도 안 탔습니다. 칸 표시를 월드 스프라이트로 내렸습니다.

- `_tileMark`(테두리) / `_tileZone`(속이 빛나는 채움) 두 장을 칸마다 런타임 생성. **사거리·대상·드래그는 프레임, 장판은 채움**으로 형태를 구분
- 정렬: 바닥 타일맵(0~1) < **칸 표시(2, z=+0.05 라 같은 값인 소품보다 뒤)** < 장애물·소품(2) < 말(파츠 정렬을 통째로 +200) < 이펙트(450~520)
- 장판 색은 HDR(1 초과) → 씬의 URP Bloom 이 그대로 번지게 함. `BattleFx.Puff` 로 알갱이가 천천히 올라오다 사라짐 (**ParticleSystem 안 씀** — 빌드에서 안 보인다는 제보 때문)
- `GameRules.CanPlay`: 대상형 카드가 **자기 칸을 조준하지 못하게** 함 (사거리 표시에서도 자기 칸 제외)
- 드래그 중 커서가 올라간 칸은 노란 프레임으로 따로 표시
- 화염 투사체의 방향 회전 제거(시트가 이미 옆으로 나는 그림이라 기울어 보였음), 공중 이펙트 높이 0.9 → **0.35**(한 칸 위에 뜨던 문제), 투사체·폭발·바닥 이펙트 크기 상향
- 장애물 소품 6개를 칸 중앙에 스냅 (룬 기둥이 칸 경계에 걸쳐 있던 문제). 씬 저장함
- `Card_Totem` 아트 연결

### 2.8 시점 반전 · 대각선 이동 · 장판 연출 (5차 작업)

- **상대(후공) 화면 상하 반전** — 카메라를 Z축 180도 돌리고, 거꾸로 서면 안 되는 월드 스프라이트(두 말 + `PF Props - *` 소품)를 같이 180도 돌려 세운다. 칸 안에서의 "발밑 높이"도 거울로 뒤집어야 해서(`FootInTile` / 소품은 소수부 반전) 그 처리까지 포함. UI(오버레이 캔버스)는 영향 없음
- **이동·질주 대각선 허용** — `GameRules.StepDist`(체비셰프) 도입. 이동 = 8방향 한 걸음, 질주 = 두 걸음(대각선 포함). `CanMovePath` 는 "중간에 설 수 있는 칸이 하나라도 있으면 통과"로 단순화. 사거리 하이라이트도 이 카드들만 걸음 수로 표시
- **장판 연출 통일** — 월드 효과 레코드마다 같은 유지 연출을 건다(`ZoneFxOf`). 불길용 `Fire Breath` 시트 3구간을 등록
- **장판 파티클을 네모 픽셀로** — 1×1 Point 스프라이트, 맵 도트(1/16 유닛) 배수로만 확대
- **장판 칸 호버 시 이름 표시** — "불길 2턴" / "토템 HP 3" / "얼음 이동 불가"
- 화염 투사체는 시트 그림이 위를 향하고 있어 `projectileArtAngle = -90` 으로 진행 방향 보정
- 사거리 색을 HDR로 올리고 테두리 5px 로 강화

**빌드 검증** (셰이더·포스트프로세싱·블룸이 빌드에서 사라지는 문제)
- `Shader.Find("SpellThrower/SpriteFlash")` → 셰이더가 `Assets/Resources/Shaders/` 안이라 이미 포함되지만, **Always Included Shaders 에도 추가**해 이중으로 막음
- URP 에셋 2종(PC/Mobile) 모두 `supportsHDR = true`, 두 렌더러 모두 `PostProcessData` 연결됨(없으면 빌드에서 포스트프로세싱이 조용히 죽는다)
- Standalone 기본 품질 = PC 레벨 → 에디터와 같은 `PC_RPAsset` 사용
- `StripUnusedPostProcessingVariants = true` 지만 Bloom 이 든 `SampleSceneProfile` 이 빌드에 들어가는 씬(GameScene)에서 참조되므로 유지됨
- 카메라 `allowHDR = true`, `renderPostProcessing = true`, Bloom threshold 1 / intensity 0.25 → HDR(1 초과) 색만 번짐
- 파티클은 ParticleSystem 이 아니라 스프라이트라 셰이더 스트리핑 영향 없음

---

## 3. 남은 작업

### A. 2인 접속 실기 확인 🟡
컴파일·테스트·시트 슬라이싱까지는 확인했지만, **실제 대전 화면은 아직 못 봤습니다** (게임이 2인 접속을 요구해 에디터 한 대로는 진행이 안 됨). 빌드 2개 또는 에디터+빌드로 붙여서 볼 것:

- 화염 투사체가 대상에 닿는 순간 피해가 뜨는지 (타이밍)
- 바람 빔 길이·회전이 맞는지 (피벗 x=0 가정)
- 얼음 존이 얼어 있는 동안만 남고 풀릴 때 Ending 이 나오는지
- 토템 스프라이트 크기(ppu 68 → 약 2타일 높이)와 서는 위치
- 질주 잔상이 SPUM 파츠에 제대로 붙는지
- 눈으로 보고 `BattleFx` 인스펙터의 `fxScale / fxSeconds / projectileTilesPerSecond / zoneLoopFps` 를 조절
- **턴당 코스트 10 밸런스** — 카드 코스트가 1이라 한 턴에 손패를 거의 다 낼 수 있습니다

### B. 아트 빈자리 🟡
- **토템·듀얼 카드 일러스트 없음.** 노션 토템 페이지의 `토템_기본.png` 를 받아 `Resources/Cards/Card_Totem.png` 로 넣으면 `GameUI.CardArt` 7번 자리에 자동으로 붙습니다

---

## 4. 판단해서 정한 것 (다르면 알려주세요)

1. **이동 카드는 손패 7장 제한 밖** — `MaxHand`는 일반 카드만 셉니다. UI 슬롯은 8칸
2. **이동 카드는 손패 맨 앞**에 들어갑니다
3. **번개 스택은 사용할 때마다 오릅니다** — 노션의 "빗나가면 안 오른다"는 빈 칸 조준을 뜻하는 듯한데 "벽을 때려도 오른다"와 구분이 애매해 전부 카운트했습니다
4. **바람의 "장애물 2 피해"는 토템에만 들어갑니다** — 맵 벽 파괴까지 하려면 `obstacles`를 HP 배열로 바꿔야 합니다
5. **`FireZone` / `DelayedTeleport` 월드 효과는 이제 아무 카드도 생성하지 않습니다** — 확장 카드용으로 코드만 남겨뒀습니다
6. **토템 연출은 시트 자체의 밝아짐으로 갈음했습니다** — 별도의 "타일 아래에서 솟아오르며 페이드인 + 상하 부유"는 넣지 않았습니다. 필요하면 `ZoneRoutine` 에 몇 줄 추가하면 됩니다
7. **`Fire Breath` 시트는 아직 코드에 없습니다** — 장판(불길) 카드가 생길 때 `Sheet` 에 3줄 추가하면 됩니다 (다행 시트라 `Cols` 를 명시해야 함)

## 5. 이번에 안 한 것
- 노션 확장 카드 32종
- 맵 벽 HP·파괴
- 덱 구성 UI
