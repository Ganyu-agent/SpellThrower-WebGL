using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpellThrower
{
    /// 씬에 만들어 둔 UI 계층을 찾아 붙이고 GameState 를 그린다. UI 를 생성하지는 않는다.
    /// UI/Lobby/{Title,NickLabel,NickValue,ServerLabel,ServerValue,MatchButton,Status}
    /// UI/Game/{OpponentBar,TurnBar,Board/Tile_*,SelfBar,Hand/Card_*,EndTurnButton,Result}
    public class GameUI : MonoBehaviour
    {
        const int N = GameRules.Size;

        // 타일 표시는 UI 가 아니라 월드 스프라이트다. 오버레이 캔버스는 말·이펙트를 무조건 덮고
        // 포스트프로세스(블룸)도 안 타기 때문이다. 테두리는 진하고 속은 옅은 한 장을 색만 바꿔 쓴다.
        [Header("타일 색")]
        public Color tileMove = new Color(0.30f, 1.10f, 0.55f, 1f);
        // 대상 표시는 밝기 1을 넘겨야 블룸 임계값(1)에 걸린다. 가장 밝은 채널만
        // 크게 올리고 나머지는 1 아래로 두면 하얗게 날아가지 않고 색이 남는다.
        public Color tileTarget = new Color(2.00f, 0.38f, 0.26f, 0.85f);  // 쓸 수 있는 칸: 적색
        public Color tileDragPick = new Color(2.60f, 2.00f, 0.45f, 0.95f); // 끌고 있는 카드가 노리는 칸
        public Color tileBlocked = new Color(0.10f, 0.10f, 0.14f, 0.80f); // 장애물 칸: 지나갈 수 없다

        // 장판은 HDR(1 초과) 로 칠해 블룸이 걸리게 한다.
        [Header("월드 효과 표시")]
        public Color tileFireZone = new Color(4.60f, 0.90f, 0.20f, 0.95f);
        public Color tileDelayedTeleport = new Color(2.00f, 1.30f, 4.80f, 0.95f);
        public Color tileTotem = new Color(4.20f, 3.20f, 0.85f, 0.95f);      // 내 토템: 노랑
        public Color tileTotemFoe = new Color(4.60f, 0.70f, 1.80f, 0.95f);   // 상대 토템: 자홍
        public Color tileIceZone = new Color(1.20f, 3.60f, 5.40f, 0.95f);

        [Header("카드 색")]
        public Color cardSelected = new Color(0.75f, 0.65f, 0.20f);

        [Header("입력 필드 색")]
        public Color fieldIdle = new Color(0.18f, 0.18f, 0.22f);
        public Color fieldFocused = new Color(0.28f, 0.30f, 0.40f);

        GameObject _lobby, _game;
        Text _status, _oppBar, _turnBar, _selfBar, _result, _nickText, _serverText, _burned, _turnBanner;
        Image _nickBg, _serverBg;
        readonly Image[] _tileBg = new Image[N * N];
        readonly Text[] _tileTxt = new Text[N * N];   // 월드 효과 마커(F2/T2) 자리
        readonly GameObject[] _cardGo = new GameObject[HandSlots];
        readonly Image[] _cardBg = new Image[HandSlots];
        readonly Text[] _cardTxt = new Text[HandSlots];
        readonly CardView[] _cardView = new CardView[HandSlots];

        // 세로형 카드 크기 (원본 아트 비율 2:3). 아래 값들이 손패 연출 조절점 전부다.
        const float CardSlotW = 76f, CardSlotH = 114f;
        const float FanStepX = 46f;       // 카드 사이 가로 간격. 카드 폭보다 좁아 서로 겹친다
        const float FanStepDeg = 5.5f;    // 카드 한 장당 벌어지는 각도
        const float FanArcDrop = 4.5f;    // 가장자리 카드가 가라앉는 정도 (호 곡률)
        const float HandY = -30f;         // 음수 = 카드 아래쪽이 화면 밖으로 살짝 잘린다
        // 상대 패는 위쪽 가운데. 정보바(~90px) 아래에서 시작해 보드(~163px) 위에서 끝나야
        // 하므로 폭 66px 안에 들어가도록 작게 줄인다.
        const float OppHandY = -115f;
        const float OppHandScale = 0.58f;
        // 정보 UI 는 화면 맨 위·맨 아래에 붙인다. 손패는 그 바깥으로 넘지 않는다.
        const float OppBarY = -4f, TurnBarY = -38f, BurnedY = -66f, SelfBarY = 4f;
        // 두 플레이어 정보 줄은 왼쪽 구석에 위아래로 나란히 선다.
        const float PlayerBarX = 24f, PlayerBarW = 350f;
        // 코스트는 동그라미 다섯 칸. 한 칸 = 2, 반 칸 = 1 → 턴당 10.
        // 이름줄과 같은 줄에 두면 긴 이름·[지금 턴] 과 겹친다. 화면 바깥쪽 끝에 한 줄로 깔고
        // 이름줄을 안쪽으로 한 칸 밀어 위아래로 쌓는다. 가로 위치는 이름줄과 같다.
        const int CostCircles = GameRules.MaxCost / 2;
        const float CostIcon = 30f, CostGap = 3f, CostRowX = PlayerBarX;
        const float CostRowH = CostIcon + 4f;
        const string MyTurnMark = "    [내 턴]";
        const string FoeTurnMark = "    [상대 턴]";
        const string SurrenderText = "항복";
        const string SurrenderConfirmText = "정말 항복? 한 번 더";
        static readonly Color SurrenderIdle = new Color(0.34f, 0.16f, 0.18f, 1f);
        static readonly Color SurrenderArmed = new Color(0.72f, 0.20f, 0.20f, 1f);
        // 체력은 이름줄과 코스트 줄 사이에 바 한 줄로 선다. 내 쪽은 코스트 위·이름 아래,
        // 상대 쪽은 코스트 아래·이름 위 — 바깥에서 안쪽으로 코스트 → 체력 → 이름 순.
        const float HpBarH = 22f, HpRowH = HpBarH + 6f;
        // 화면 아래 왼쪽이 버린 패, 오른쪽이 덱. 손패는 그 사이 가운데.
        // 더미는 이름줄 위에 선다. 이름줄이 코스트 한 줄만큼 올라갔으므로 같이 올린다.
        const float PileScale = 0.72f, PileY = 100f + CostRowH + HpRowH, PileEdgeX = 70f;
        const float FlySeconds = 0.28f;   // 덱->손패, 손패->버린 패 이동 시간
        // 포커스는 "이 카드를 고르는 중"이라는 신호만 준다. 읽기는 우클릭 팝업이 맡는다.
        // 커진 카드가 화면 아래로 안 잘리려면 배율만큼 더 올려야 한다.
        // 쉬는 카드 중심이 y=27 이므로 lift = 57*배율 - 17 이면 아래 여백 10px 이 남는다.
        const float FocusScale = 2.7f;
        // 글자는 만들 때 정해진 크기로 그려져서, 키우면 그 그림이 늘어나 뭉개진다.
        // 그림은 가장 커질 크기로 만들어 두고 평소에는 줄여서 보여 준다.
        const float CardArtScale = FocusScale;
        // 제목은 CardView의 슬롯 오버레이에 그린다. 아트 확대 배율과 무관하게
        // 픽셀 폰트의 원래 크기를 유지해 FHD에서도 획이 사라지지 않게 한다.
        const float HandTitlePixels = PixelFontCrisp.NativeSize;
        const float FocusLift = 57f * FocusScale - 17f;
        const float FocusSpeed = 14f;

        // 우클릭한 카드를 크게 보여주는 팝업. 화면 가운데 위쪽에 기울기 없이 놓는다.
        const float DetailW = 300f, DetailH = 450f, DetailY = 90f;
        CardView _detailView;
        GameObject _detailGo;
        GameObject _settingsGo;   // ESC 로 여닫는 게임 중 설정 판
        bool _surrenderArmed;     // 항복 버튼을 한 번 눌러 확인을 기다리는 중
        System.Action _surrenderReset;
        int _detailIndex = -1;
        int _detailOpenedFrame = -1;
        const float DetailAnimSeconds = 0.22f;
        const float DetailDimAlpha = 0.62f;
        RectTransform _detailRootRt, _detailCardRt;
        Image _detailDim;

        // 상대가 방금 낸 카드를 오른쪽 위에 잠깐 띄운다. 상세 팝업과 달리 화면을 덮지 않는다.
        const float FoeCardScale = 0.6f, FoeCardSeconds = 2.5f;
        CardView _foeCardView;
        GameObject _foeCardGo;
        float _foeCardUntil;
        ushort _seenActionSeq;
        object _foeCardSource;
        float _detailT;                 // 0=손패 자리 1=완전히 펼쳐짐
        Vector2 _detailFrom;            // 떠오르기 시작하는 자리
        Quaternion _detailFromRot = Quaternion.identity;
        int _detailLast = -1;           // 마지막으로 띄운 자리. 되감는 동안에도 그 자리는 비워 둔다

        /// 팝업이 지금 들고 있는 손패 자리. 없으면 -1.
        int DetailSlot => _detailIndex >= 0 ? _detailIndex : (_detailT > 0f ? _detailLast : -1);

        RectTransform _deckRt, _discRt, _flyRt;
        Image _deckImg, _discImg, _flyImg;
        Text _deckTxt, _discTxt;
        float _flyT = 1f;               // 1 = 도착해서 숨김
        Vector2 _flyFrom, _flyTo;

        readonly float[] _cardFocus = new float[HandSlots];   // 0=제자리 1=완전히 떠오름
        int _hoverIndex = -1;
        RectTransform _handRt, _oppHandRt;
        readonly Image[] _oppBack = new Image[HandSlots];
        readonly float[] _oppFocus = new float[HandSlots];   // 상대가 만지는 카드
        readonly CanvasGroup[] _cardGroup = new CanvasGroup[HandSlots];
        // 카드가 말을 가리면 그 카드를 흐리게 한다.
        const float CoveredCardAlpha = 0.35f;
        Renderer[] _selfParts, _foeParts;
        static readonly Vector3[] _corners = new Vector3[4];

        /// 카드별 일러스트. 인덱스는 CardId.
        readonly Sprite[] _cardIcons = new Sprite[CardText.ArtNames.Length];

        /// 손패 슬롯 수. 이동 카드가 한 장 더 들어온다.
        const int HandSlots = GameRules.HandSlots;

        enum Field { Nick, Server }
        Field _focus = Field.Nick;
        string _nick = "";
        string _server = "127.0.0.1";
        bool _serverTouched, _loadoutSent, _searching;
        Transform _board;
        GameObject _backBtn, _endTurnBtn;
        Transform _selfCharacter, _foeCharacter;
        BattleFx _fx;
        SfxPlayer _sfx;
        byte _seenWinner;
        BattleSequencer _seq;
        bool _boardAligned;
        bool _wasLive;   // GameScene 에서 한 번이라도 접속돼 있었는지
        Material _tileMat;
        MaterialPropertyBlock _tileMpb;
        static readonly int TintHdrId = Shader.PropertyToID("_TintHDR");
        Camera _gameCamera;
        bool _cameraPoseSaved;
        Vector3 _cameraHomePosition;
        Quaternion _cameraHomeRotation;


        byte _seenBurnSeq;
        NetGame _burnSource;
        int _burnPlayer = -1;
        float _burnUntil;
        byte _seenTurnCount, _seenTurnPlayer;
        float _turnBannerUntil;
        readonly float[] _cardPopAt = new float[HandSlots];
        int _prevHandLen = -1;
        byte _prevMyHp, _prevOppHp;
        bool _prevHpValid;
        float _myHurtUntil, _oppHurtUntil;
        Color _selfBarHome, _oppBarHome;
        // 체력 바: 0 = 나, 1 = 상대.
        readonly RectTransform[] _hpFill = new RectTransform[2];
        readonly Image[] _hpFillImg = new Image[2];
        readonly Text[] _hpText = new Text[2];
        static readonly Color HpBack = new Color(0.08f, 0.06f, 0.10f, 0.85f);
        static readonly Color HpHigh = new Color(0.35f, 0.85f, 0.40f);
        static readonly Color HpMid = new Color(0.95f, 0.78f, 0.25f);
        static readonly Color HpLow = new Color(0.90f, 0.25f, 0.25f);
        readonly SpriteRenderer[] _tileMark = new SpriteRenderer[N * N];   // 사거리·대상: 테두리
        readonly SpriteRenderer[] _tileZone = new SpriteRenderer[N * N];   // 장판: 가운데가 빛나는 채움
        readonly float[] _puffAt = new float[N * N];      // 장판 알갱이를 다음에 띄울 시각
        bool _viewFlipped;
        int _hoverTile = -1;
        Transform[] _uprightTargets;
        readonly Dictionary<Transform, Vector3> _obstacleHomePositions = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, List<int>> _obstacleCells = new Dictionary<Transform, List<int>>();
        bool _obstacleCellsMapped;
        readonly Dictionary<Transform, Vector3> _frameHomePositions = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Quaternion> _frameHomeRotations = new Dictionary<Transform, Quaternion>();
        Image[] _selfCost, _oppCost, _selfCostBlink, _oppCostBlink;
        Sprite _costFill, _costHalf, _costEmpty, _costMaskFull;
        [Header("보드 월드 기준")]
        public Tilemap groundTilemap;
        public Vector2 boardOffset;
        [Tooltip("정상 화면에서 씬 원본 위치에 더할 장애물 로컬 보정값")]
        public Vector2 normalObstacleOffset = Vector2.zero;
        [Tooltip("후공 화면에서 장애물의 씬 기준 위치에 더할 로컬 보정값")]
        public Vector2 flippedObstacleOffset = new Vector2(0f, 0.7f);

        Tilemap _groundMap;
        Vector3 _tileOrigin;

        void FindGroundTilemap()
        {
            if (groundTilemap != null)
            {
                _groundMap = groundTilemap;
                return;
            }
            if (_groundMap != null) return;

            // Grass/Wall 타일맵도 8칸보다 크기 때문에 크기만 보고 고르면
            // 보드 기준이 아닌 장식 레이어가 먼저 걸린다. 실제 보드 바닥을
            // 이름으로 우선 선택하고, 이름이 바뀐 씬에서는 채워진 큰 맵을 대체로 쓴다.
            Tilemap fallback = null;
            foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (map.name.Contains("Stone Ground"))
                {
                    _groundMap = map;
                    return;
                }
                if (fallback == null && map.size.x >= N && map.size.y >= N && map.HasTile(map.origin))
                    fallback = map;
            }
            _groundMap = fallback != null ? fallback : FindFirstObjectByType<Tilemap>();
            if (_groundMap == null && _selfCharacter != null)
            {
                // 타일맵이 없으면 에디터에 배치된 플레이어 위치(기본 타일 (3, 0))를 기준으로 원점 설정
                _tileOrigin = _selfCharacter.position - new Vector3(3.5f, FootInTile, 0f);
            }
        }

        int _dragIndex = -1;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        int _selected = -1;

        /// 칸 안에서 말이 서는 높이. 화면을 뒤집으면 칸 안 위아래도 뒤집힌다.
        const float FootInTile = 0.38f;

        /// 타일 좌표 → 말이 서는 월드 위치.
        public Vector3 TileWorld(int x, int y)
        {
            if (_groundMap != null)
            {
                var center = _groundMap.GetCellCenterWorld(new Vector3Int(_groundMap.origin.x + x, _groundMap.origin.y + y, 0));
                float footY = _viewFlipped ? center.y + (0.5f - FootInTile) : center.y - (0.5f - FootInTile);
                return new Vector3(center.x + boardOffset.x, footY + boardOffset.y, -0.1f);
            }
            return _tileOrigin + new Vector3(x + 0.5f + boardOffset.x,
                                             (_viewFlipped ? 1f - FootInTile : FootInTile) + y + boardOffset.y,
                                             -0.1f);
        }

        /// 타일 칸 한가운데. 말은 칸 중심보다 살짝 위에 서므로 표시는 그만큼 내린다.
        Vector3 TileCenterWorld(int x, int y)
        {
            if (_groundMap != null)
            {
                var center = _groundMap.GetCellCenterWorld(new Vector3Int(_groundMap.origin.x + x, _groundMap.origin.y + y, 0));
                return new Vector3(center.x + boardOffset.x, center.y + boardOffset.y, -0.1f);
            }
            return _tileOrigin + new Vector3(x + 0.5f + boardOffset.x, y + 0.5f + boardOffset.y, -0.1f);
        }

        int TileUnderWorld(Vector3 world)
        {
            int x, y;
            if (_groundMap != null)
            {
                var cell = _groundMap.WorldToCell(world - (Vector3)boardOffset);
                x = cell.x - _groundMap.origin.x;
                y = cell.y - _groundMap.origin.y;
            }
            else
            {
                x = Mathf.FloorToInt(world.x - boardOffset.x - _tileOrigin.x);
                y = Mathf.FloorToInt(world.y - boardOffset.y - _tileOrigin.y);
            }
            return x >= 0 && x < N && y >= 0 && y < N ? TileIndex(x, y) : -1;
        }

        // 칸 표시는 바닥 타일맵(0~1) 보다는 위, 장애물·소품(2) 보다는 뒤여야 한다.
        // 같은 정렬값이면 카메라에서 먼 쪽이 뒤로 가므로 z 를 소품보다 뒤(+)에 둔다.
        const int TileMarkOrder = 2;
        const float TileZoneZ = 0.06f, TileFrameZ = 0.05f;

        /// 타일맵과 하이라이트는 원본 월드 맵 방향을 그대로 사용한다.
        int TileIndex(int x, int y) => (N - 1 - y) * N + x;
        void TileCoord(int i, out int x, out int y)
        {
            x = i % N;
            y = N - 1 - i / N;
        }

        void Awake()
        {
            Application.runInBackground = true;   // 한 PC에서 여러 인스턴스를 띄우면 비활성 창이 멈춘다
            Bind();
        }

        void Start()
        {
            if (SceneManager.GetActiveScene().name == "GameScene") return;
            LoadServerConfig();
            _ = Matchmaking.PrepareAsync();
            AutoStart();
        }

        /// exe 옆의 server.txt 에 주소가 있으면 그걸 기본 서버로 쓴다.
        /// 배포할 때 이 파일만 같이 주면 상대는 IP 를 타이핑할 필요가 없다.
        void LoadServerConfig()
        {
            var path = System.IO.Path.Combine(Application.dataPath, "../server.txt");
            try
            {
                if (!System.IO.File.Exists(path)) return;
                var t = System.IO.File.ReadAllText(path).Trim();
                if (t.Length == 0) return;
                _server = t;
                _serverTouched = true;
                Debug.Log("[스펠 스로워] server.txt 적용: " + t);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[스펠 스로워] server.txt 읽기 실패: " + e.Message);
            }
        }

        void Bind()
        {
            _lobby = transform.Find("Lobby").gameObject;
            _game = transform.Find("Game").gameObject;
            _status = transform.Find("Lobby/Status").GetComponent<Text>();

            var nick = transform.Find("Lobby/NickValue");
            _nickText = nick.GetChild(0).GetComponent<Text>();
            _nickBg = nick.GetComponent<Image>();
            nick.GetComponent<Button>().onClick.AddListener(() => _focus = Field.Nick);

            var srv = transform.Find("Lobby/ServerValue");
            _serverText = srv.GetChild(0).GetComponent<Text>();
            _serverBg = srv.GetComponent<Image>();
            srv.GetComponent<Button>().onClick.AddListener(() => _focus = Field.Server);

            transform.Find("Lobby/MatchButton").GetComponent<Button>().onClick.AddListener(Match);

            _oppBar = transform.Find("Game/OpponentBar").GetComponent<Text>();
            _turnBar = transform.Find("Game/TurnBar").GetComponent<Text>();
            _selfBar = transform.Find("Game/SelfBar").GetComponent<Text>();
            _result = transform.Find("Game/Result").GetComponent<Text>();
            // 턴 배너는 씬에 없다. Result 를 복제해 글꼴·정렬을 그대로 물려받는다.
            _turnBanner = Instantiate(_result.gameObject, _result.transform.parent).GetComponent<Text>();
            _turnBanner.name = "TurnBanner";
            _turnBanner.text = "";
            _selfBarHome = _selfBar.color;
            _oppBarHome = _oppBar.color;
            _burned = transform.Find("Game/Burned").GetComponent<Text>();
            // 씬에 저장된 HUD 글씨가 작아 시간·이름줄이 잘 안 읽힌다. 한 번만 키운다.
            Enlarge(_oppBar); Enlarge(_turnBar); Enlarge(_selfBar); Enlarge(_burned);
            _selfCharacter = GameObject.Find("PlayerSelf")?.transform;
            _foeCharacter = GameObject.Find("PlayerFoe")?.transform;
            _selfParts = _selfCharacter != null ? _selfCharacter.GetComponentsInChildren<Renderer>(true) : null;
            _foeParts = _foeCharacter != null ? _foeCharacter.GetComponentsInChildren<Renderer>(true) : null;
            FindGroundTilemap();

            BuildTileMarks();
            BindCostRows();
            BindHpBars();
            var endTurn = transform.Find("Game/EndTurnButton");
            _endTurnBtn = endTurn.gameObject;
            endTurn.GetComponent<Button>().onClick.AddListener(EndTurn);
            // 이동은 손패 카드다. 씬에 남아 있는 옛 버튼은 감춘다.
            var oldMoveBtn = transform.Find("Game/BasicMoveButton");
            if (oldMoveBtn != null) oldMoveBtn.gameObject.SetActive(false);

            _backBtn = transform.Find("Game/BackButton").gameObject;
            _backBtn.GetComponent<Button>().onClick.AddListener(Leave);

            _board = transform.Find("Game/Board");

            for (int i = 0; i < N * N; i++)
            {
                int ci = i;
                var t = _board.GetChild(i);
                _tileBg[i] = t.GetComponent<Image>();
                _tileBg[i].raycastTarget = false;
                t.GetComponent<Button>().onClick.AddListener(() => OnTile(ci));
            }

            // 새 카드 아트는 세로형(500x750)이라 가로형이던 손패 슬롯을 세로로 바꾼다.
            // 부채꼴로 직접 배치하므로 가로 정렬 레이아웃은 걷어내고, 손패 루트 위치는 씬 설정을 유지한다.
            var hand = transform.Find("Game/Hand");
            _handRt = (RectTransform)hand;
            var layout = hand.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) Destroy(layout);

            var frame = Resources.Load<Sprite>("Cards/CardFrame");
            var bodyFont = Resources.Load<Font>("neodgm");
            // GameScene's serialized title used Silver.ttf, which has no
            // glyphs for most Korean card names. Card titles must use the same
            // Korean font as the deck builder instead of inheriting that asset.
            var cardTitleFont = bodyFont;
            BuildTileLabels(bodyFont);
            for (int i = 0; i < _cardIcons.Length; i++)
                _cardIcons[i] = Resources.Load<Sprite>("CardArt/" + CardText.ArtNames[i]);

            // 씬에는 카드 슬롯이 7칸뿐이다. 이동 카드가 한 장 더 들어오므로 마지막 칸을
            // 복제해 채운다. 턴 배너와 같은 방식이라 글꼴·구성이 그대로 따라온다.
            while (hand.childCount < HandSlots)
            {
                var extra = Instantiate(hand.GetChild(hand.childCount - 1).gameObject, hand);
                extra.name = "Card_" + (hand.childCount - 1);
            }

            for (int i = 0; i < HandSlots; i++)
            {
                int ci = i;
                var c = hand.GetChild(i);
                _cardGo[i] = c.gameObject;
                _cardBg[i] = c.GetComponent<Image>();
                // 카드 슬롯은 구 구조에서는 Text가 첫 자식이었지만, 현재 씬에서는
                // Visual/Icon/Frame/Title/... 계층이 첫 자식이다. 첫 자식 타입을
                // 가정하면 여기서 예외가 나서 이후 카드 아트 초기화가 전부 중단된다.
                _cardTxt[i] = FindCardTitle(c);
                c.GetComponent<Button>().onClick.AddListener(() => OnCard(ci));

                // 슬롯은 커서 판정만 맡는 고정된 세로 띠다. 서로 겹치지 않게 간격만큼만 넓힌다.
                // 판정 영역이 움직이면 떠오른 카드가 커서에서 벗어나 다시 내려앉기를 반복한다.
                var rt = (RectTransform)c;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(FanStepX, CardSlotH);
                var le = c.GetComponent<LayoutElement>();
                if (le != null) Destroy(le);

                _cardView[i] = CardView.Build(rt, _cardBg[i], _cardTxt[i],
                                              new Vector2(CardSlotW, CardSlotH) * CardArtScale,
                                              frame, cardTitleFont, bodyFont, 2f * CardArtScale,
                                              CardArtScale);
                c.gameObject.AddComponent<CardHover>().Init(this, ci);
                c.GetComponent<CardDrag>().Init(ci);
                _cardGroup[i] = c.GetComponent<CanvasGroup>();
            }

            BuildOpponentHand();
            BuildPiles(bodyFont);
            BuildDetailPanel(frame, cardTitleFont, bodyFont);
            BuildFoePlayedPanel(frame, cardTitleFont, bodyFont);
            BuildSettingsPanel(bodyFont);

            _fx = gameObject.AddComponent<BattleFx>();
            _seq = gameObject.AddComponent<BattleSequencer>();
            _sfx = GetComponentInChildren<SfxPlayer>(true);
            _seq.Init(this, _fx, _selfCharacter, _foeCharacter, _sfx);

            // 레거시 InputField 는 Input System 전용 모드에서 글자를 못 받는다 → 키보드에서 직접 읽는다
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
            if (SceneManager.GetActiveScene().name != "GameScene") _game.SetActive(false);
        }

        static Text FindCardTitle(Transform slot)
        {
            var title = slot.Find("Visual/Title") ?? slot.Find("Title");
            var text = title != null ? title.GetComponent<Text>() : null;
            if (text != null) return text;

            // 이전 씬/런타임 생성 슬롯과의 호환. 이름이 바뀌어도 슬롯 내부의
            // Text를 찾을 수 있으면 CardView가 나머지 계층을 재사용한다.
            return slot.GetComponentInChildren<Text>(true);
        }

        void OnDestroy()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        /// -server : 전용 서버로 뜬다.  -match <ip> <nick> : 클릭 없이 매칭까지 간다.
        void AutoStart()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "-server")
                {
                    Transport().SetConnectionData("0.0.0.0", (ushort)NetGame.Port);
                    NetworkManager.Singleton.StartServer();
                    _status.text = "전용 서버 - 2명 대기 중";
                    Debug.Log("[SpellThrower] dedicated server on " + NetGame.Port);
                    return;
                }
                if (a[i] == "-match")
                {
                    if (i + 1 < a.Length) _server = a[i + 1];
                    _nick = i + 2 < a.Length ? a[i + 2] : "플레이어";
                    Match();
                    return;
                }
            }
        }

        void OnTextInput(char ch)
        {
            if (_lobby == null || !_lobby.activeSelf || _searching) return;
            if (_focus == Field.Nick)
            {
                // 기본 폰트에 한글 글리프가 없어 출력 가능한 ASCII 만 받는다 (아니면 두부가 보인다)
                if (ch >= ' ' && ch <= '~' && _nick.Length < 12) _nick += ch;
            }
            else
            {
                if (!char.IsDigit(ch) && ch != '.') return;
                if (!_serverTouched) { _server = ""; _serverTouched = true; }
                if (_server.Length < 15) _server += ch;
            }
        }

        void Backspace()
        {
            if (_focus == Field.Nick) { if (_nick.Length > 0) _nick = _nick.Substring(0, _nick.Length - 1); }
            else if (_server.Length > 0) { _server = _server.Substring(0, _server.Length - 1); _serverTouched = true; }
        }

        async void Match()
        {
            if (_searching) return;
            if (string.IsNullOrWhiteSpace(_nick))
            {
                _focus = Field.Nick;
                _status.text = "이름을 먼저 입력하세요";
                return;
            }
            _searching = true;
            _status.text = "서비스에 연결 중...";
            try
            {
                await Matchmaking.FindMatchAsync(_nick);
                _status.text = Matchmaking.Current.IsHost
                    ? "상대를 기다리는 중..."
                    : "상대를 찾았습니다 - 게임 시작";
            }
            catch (System.Exception e)
            {
                _searching = false;
                _status.text = "매칭에 실패했습니다";
                Debug.LogError("[스펠 스로워] 매칭 실패: " + e);
            }
        }

        void EndTurn()
        {
            _sfx?.Play(SfxId.UiClick);
            if (NetGame.I != null) NetGame.I.EndTurnServerRpc();
        }

        /// 승패가 난 뒤 로비로 돌아간다. 이게 없으면 결과 화면에서 아무것도 못 한다.
        void Leave()
        {
            ResetBurnSfxTracking();
            _selected = -1;
            _dragIndex = -1;
            _loadoutSent = false;
            _searching = false;
            _ = Matchmaking.LeaveAsync();
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            // NetGame 은 DontDestroyOnLoad 라 그냥 두면 로비 씬의 새 NetGame 과 겹쳐 다음 매칭이 깨진다.
            if (NetGame.I != null) { Destroy(NetGame.I.gameObject); NetGame.I = null; }
            SceneManager.LoadScene("MatchmakingScene 1");

            _status.text = "매칭 종료 - 다시 시작하려면 매칭 시작을 누르세요";
        }

        void ResetBurnSfxTracking()
        {
            _burnSource = null;
            _burnPlayer = -1;
            _seenBurnSeq = 0;
            _burnUntil = 0f;
            if (_burned != null) _burned.text = "";

            _foeCardSource = null;
            _seenActionSeq = 0;
            _foeCardUntil = 0f;
            if (_foeCardGo != null) _foeCardGo.SetActive(false);
        }

        // ---------------- 드래그 앤 드롭 ----------------
        public bool BeginCardDrag(int i)
        {
            if (NetGame.I == null || !NetGame.I.IsMyTurn) return false;
            var s = NetGame.I.State.Value;
            ref var hand = ref GameRules.Hand(ref s, NetGame.I.MyPlayer);
            if (i < 0 || i >= hand.Length) return false;
            _selected = i;      // 사거리 하이라이트는 클릭 방식과 같은 경로를 쓴다
            _dragIndex = i;
            NetGame.I.SetDragServerRpc((byte)i);
            return true;
        }

        public void EndCardDrag(int i, PointerEventData e)
        {
            _dragIndex = -1;
            _selected = -1;
            if (NetGame.I != null) NetGame.I.SetDragServerRpc(byte.MaxValue);
            if (i < 0 || NetGame.I == null || !NetGame.I.IsMyTurn) return;

            var s = NetGame.I.State.Value;
            ref var hand = ref GameRules.Hand(ref s, NetGame.I.MyPlayer);
            if (i >= hand.Length) return;

            // 보드 안에 떨궈야 쓴다. 밖이면 대상 없는 카드라도 취소.
            int tile = TileUnderPointer(e);
            if (tile < 0) return;
            var card = Cards.Get(hand[i]);
            if (card == null) return;
            if (!card.Targeted) { NetGame.I.PlayCardServerRpc(i, 0, 0); return; }

            int x, y; TileCoord(tile, out x, out y);
            NetGame.I.PlayCardServerRpc(i, x, y);
        }

        /// 후공(1번)은 보드 중심을 기준으로 자기 진영이 아래로 오도록 화면을 뒤집어 본다.
        /// 카메라를 자기 위치에서만 돌리면 보드가 화면 밖으로 이동하므로 카메라 위치도
        /// 보드 중심 반대편으로 옮긴다. 월드 좌표는 그대로 두고 위아래가 있는 그림만
        /// 카메라 회전을 상쇄해 똑바로 세운다.
        void SyncViewFlip(int me)
        {
            bool flip = me == 1;
            if (_viewFlipped == flip)
            {
                SyncObstaclePositions(flip);
                return;
            }
            _viewFlipped = flip;

            var cam = WorldCamera();
            var flipRotation = Quaternion.Euler(0f, 0f, 180f);
            if (cam != null)
            {
                if (!_cameraPoseSaved)
                {
                    _cameraHomePosition = cam.transform.position;
                    _cameraHomeRotation = cam.transform.rotation;
                    _cameraPoseSaved = true;
                }

                if (flip)
                {
                    var center = BoardCenterWorld();
                    cam.transform.position = center + flipRotation * (_cameraHomePosition - center);
                    cam.transform.rotation = flipRotation * _cameraHomeRotation;
                }
                else
                {
                    cam.transform.position = _cameraHomePosition;
                    cam.transform.rotation = _cameraHomeRotation;
                }
            }

            var upright = flip ? flipRotation : Quaternion.identity;

            foreach (var t in UprightTargets())
            {
                if (t == null) continue;
                t.rotation = upright;
            }
            SyncObstaclePositions(flip);
            SyncOuterFrame(flip);
            SyncTilemapUpright(flip);
            if (_fx != null) _fx.Upright = upright;
            _boardAligned = false;   // 클릭용 UI 보드 겹침을 다시 맞춘다
        }

        /// 카메라와 보드의 회전 중심. 바닥 맵의 양 끝 셀을 평균 내므로
        /// TileMarks·UI 보드도 같은 월드 기준을 공유한다.
        Vector3 BoardCenterWorld()
        {
            if (_groundMap != null)
            {
                var first = _groundMap.GetCellCenterWorld(_groundMap.origin);
                var last = _groundMap.GetCellCenterWorld(new Vector3Int(
                    _groundMap.origin.x + N - 1, _groundMap.origin.y + N - 1, 0));
                return (first + last) * 0.5f + new Vector3(boardOffset.x, boardOffset.y, 0f);
            }

            return _tileOrigin + new Vector3(N * 0.5f + boardOffset.x,
                                              N * 0.5f + boardOffset.y, 0f);
        }

        /// 소품이 덮는 보드 칸을 스프라이트 범위로 잡는다. 피벗만 보면 벤치처럼
        /// 두 칸을 덮는 소품과 보드 밖 장식을 구분할 수 없다. 처음 장애물 배치에
        /// 걸리는 칸이 하나도 없으면 순수 장식이므로 아예 관리하지 않는다.
        void MapObstacleCells()
        {
            if (_obstacleCellsMapped) return;
            // UprightTargets 의 캐시에 기대면 그쪽이 먼저 비어 있는 채로 굳었을 때
            // 영영 안 채워진다. 여기서 직접 훑는다.
            var obstacles = GameObject.Find("Obstacles");
            if (obstacles == null) return;      // 아직 씬이 안 올라왔으면 다음 프레임에 다시
            _obstacleCellsMapped = true;
            foreach (Transform target in obstacles.transform)
            {
                if (target == null) continue;
                Bounds bounds = default;
                bool any = false;
                foreach (var r in target.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (!any) continue;

                var cells = new List<int>();
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        if ((GameRules.DemoObstacles & (1UL << (y * N + x))) == 0) continue;
                        var c = TileCenterWorld(x, y);
                        if (c.x >= bounds.min.x && c.x <= bounds.max.x &&
                            c.y >= bounds.min.y && c.y <= bounds.max.y)
                            cells.Add(y * N + x);
                    }
                if (cells.Count > 0) _obstacleCells[target] = cells;
            }
        }

        /// 부서진 장애물은 화면에서도 치운다. 안 그러면 소품은 그대로 보이는데
        /// 넉백은 그 칸을 그냥 지나가서, 장애물 위로 밀려난 것처럼 보인다.
        void SyncObstacleVisibility(ref GameState s)
        {
            MapObstacleCells();
            foreach (var pair in _obstacleCells)
            {
                if (pair.Key == null) continue;
                bool alive = false;
                var cells = pair.Value;
                for (int i = 0; i < cells.Count; i++)
                    if (GameRules.IsMapObstacle(ref s, cells[i] % N, cells[i] / N)) { alive = true; break; }
                if (pair.Key.gameObject.activeSelf != alive) pair.Key.gameObject.SetActive(alive);
            }
        }

        /// 장애물의 씬 원본 위치를 기준으로 정상·후공 보정을 분리한다.
        /// 정상 방향에서도 먼저 호출해 두면 에디터에서 후공 오프셋을 조정해도
        /// 정상 화면의 위치가 함께 움직이지 않는다.
        void SyncObstaclePositions(bool flip)
        {
            UprightTargets();
            var offset = flip ? flippedObstacleOffset : normalObstacleOffset;
            var delta = new Vector3(offset.x, offset.y, 0f);
            foreach (var pair in _obstacleHomePositions)
                if (pair.Key != null) pair.Key.localPosition = pair.Value + delta;
        }

        static bool IsOuterFrameMap(Tilemap map)
        {
            return map != null && (map.name == "Layer 1 - Grass" ||
                                   map.name == "Layer 1 - Wall" ||
                                   map.name == "Layer 1 - Wall Shadow");
        }

        /// 보드 바깥 프레임은 아래쪽 발판이 한 줄 더 있는 비대칭 장식이다.
        /// 보드와 함께 뒤집으면 그 발판이 위로 가므로, 후공 화면에서는 프레임을
        /// 카메라와 반대로 돌려 일반 화면의 외곽 모양을 유지한다.
        void SyncOuterFrame(bool flip)
        {
            var frameRotation = Quaternion.Euler(0f, 0f, 180f);
            var center = BoardCenterWorld();
            foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (!IsOuterFrameMap(map)) continue;

                var t = map.transform;
                if (!_frameHomePositions.ContainsKey(t))
                {
                    _frameHomePositions[t] = t.position;
                    _frameHomeRotations[t] = t.rotation;
                }

                if (flip)
                {
                    t.position = center + frameRotation * (_frameHomePositions[t] - center);
                    t.rotation = frameRotation * _frameHomeRotations[t];
                }
                else
                {
                    t.position = _frameHomePositions[t];
                    t.rotation = _frameHomeRotations[t];
                }
            }
        }

        /// 바닥·벽 타일. 타일맵을 통째로 돌리면 칸 자리까지 어긋나므로 칸마다 스프라이트만
        /// 제자리에서 되돌린다. 화면이 뒤집혀도 타일 그림은 그대로 서 있어야 한다.
        static void SyncTilemapUpright(bool flip)
        {
            var boardMatrix = flip ? Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 180f)) : Matrix4x4.identity;
            foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                var m = IsOuterFrameMap(map) ? Matrix4x4.identity : boardMatrix;
                foreach (var cell in map.cellBounds.allPositionsWithin)
                    if (map.HasTile(cell)) map.SetTransformMatrix(cell, m);
            }
        }

        /// 뒤집힌 화면에서 거꾸로 서면 안 되는 것들 — 두 말과 맵 소품.
        Transform[] UprightTargets()
        {
            if (_uprightTargets != null) return _uprightTargets;
            var list = new List<Transform>();
            AddUprightTarget(list, _selfCharacter);
            AddUprightTarget(list, _foeCharacter);

            // 장애물과 말이 SCENE 아래의 공통 부모로 정리되면 renderer.transform.root 는
            // 더 이상 각 PF Props 오브젝트가 아니라 SCENE을 가리킨다. SCENE 자체를
            // 돌리면 모든 월드 좌표가 부모 피벗 기준으로 이동하므로, Obstacles의
            // 직접 자식(각 장애물 프리팹)까지만 올려서 개별적으로 방향을 보정한다.
            var obstacles = GameObject.Find("Obstacles")?.transform;
            if (obstacles != null)
            {
                foreach (var renderer in obstacles.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var target = renderer.transform;
                    while (target.parent != null && target.parent != obstacles)
                        target = target.parent;
                    if (target.parent == obstacles)
                    {
                        AddUprightTarget(list, target);
                        if (!_obstacleHomePositions.ContainsKey(target))
                            _obstacleHomePositions[target] = target.localPosition;
                    }
                }
            }

            // 공통 부모 아래에 들어간 장식 프리팹은 renderer.transform.root 가
            // SCENE을 가리킨다. PF Props 조상까지 올라가서 개별 피벗만 보정한다.
            foreach (var go in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                var target = go.transform;
                while (target != null && !target.name.StartsWith("PF Props"))
                    target = target.parent;
                AddUprightTarget(list, target);
            }
            return _uprightTargets = list.ToArray();
        }

        static void AddUprightTarget(List<Transform> targets, Transform target)
        {
            if (target != null && !targets.Contains(target)) targets.Add(target);
        }

        /// 씬 값 기준으로 글씨를 한 번만 키운다. BestFit 이 켜져 있으면 상한도 같이 올린다.
        static void Enlarge(Text t, float scale = 1.4f)
        {
            if (t == null) return;
            t.fontSize = Mathf.RoundToInt(t.fontSize * scale);
            if (t.resizeTextForBestFit)
                t.resizeTextMaxSize = Mathf.RoundToInt(t.resizeTextMaxSize * scale);
        }

        /// 지금 걸려 있는 효과와 남은 턴. 별도 패널 없이 이름줄 뒤에 한 줄로 붙인다.
        static string EffectsOf(ref GameState s, int player)
        {
            string text = "";
            ref var tags = ref GameRules.Tags(ref s, player);
            for (int i = 0; i < tags.Length; i++)
            {
                string name = CardText.GetTagName(tags[i].Id);
                if (name.Length == 0) continue;
                text += (text.Length == 0 ? "   [" : " · ") + name + " " + tags[i].DurationTurns;
            }
            // 아이스볼의 한 턴 봉쇄는 태그가 아니라 별도 값이라 따로 본다.
            if (GameRules.MoveLocked(ref s, player) != 0 &&
                !GameRules.HasTag(ref s, player, PlayerTagId.MoveLocked))
                text += (text.Length == 0 ? "   [" : " · ") + CardText.GetTagName(PlayerTagId.MoveLocked) + " 1";
            return text.Length == 0 ? "" : text + "]";
        }

        /// 카드 아이콘. 카드마다 전용 아트를 쓴다.
        Sprite IconOf(byte id) => id < _cardIcons.Length ? _cardIcons[id] : null;

        /// 지금 커서가 올라가 있는 칸. 보드 밖이면 -1.
        int TileUnderMouse()
        {
            var cam = WorldCamera();
            if (cam == null || Mouse.current == null) return -1;
            var screen = Mouse.current.position.ReadValue();
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return TileUnderWorld(world);
        }

        /// 드롭 위치를 월드 타일 좌표로 변환한다.
        int TileUnderPointer(PointerEventData e)
        {
            var cam = WorldCamera();
            if (cam == null) return -1;
            var world = cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, -cam.transform.position.z));
            return TileUnderWorld(world);
        }

        static UnityTransport Transport() => NetworkManager.Singleton.GetComponent<UnityTransport>();

        // ---------------- 입력 ----------------
        void OnTile(int i)
        {
            if (NetGame.I == null || !NetGame.I.IsMyTurn) return;
            int x, y; TileCoord(i, out x, out y);
            if (_selected >= 0) { NetGame.I.PlayCardServerRpc(_selected, x, y); _selected = -1; }
        }

        void OnCard(int i)
        {
            if (NetGame.I == null || !NetGame.I.IsMyTurn) return;
            var s = NetGame.I.State.Value;
            ref var hand = ref GameRules.Hand(ref s, NetGame.I.MyPlayer);
            if (i >= hand.Length) return;

            var card = Cards.Get(hand[i]);
            if (card == null) return;
            if (!card.Targeted) { NetGame.I.PlayCardServerRpc(i, 0, 0); _selected = -1; }
            else
            {
                bool selecting = _selected != i;
                _selected = selecting ? i : -1;
                if (selecting) _sfx?.Play(SfxId.CardSelect);
            }
        }

        // ---------------- 렌더 ----------------
        void Update()
        {
            bool live = NetGame.I != null && NetGame.I.InGame;
            bool gameScene = SceneManager.GetActiveScene().name == "GameScene";
            if (live && !gameScene)
            {
                SceneManager.LoadScene("GameScene");
                return;
            }
            if (gameScene)
            {
                _lobby.SetActive(false);
                _game.SetActive(true);
                // 상대가 나가 연결이 끊기면 여기서 아무것도 못 하고 갇힌다. 바로 로비로 보낸다.
                if (live) _wasLive = true;
                else if (_wasLive) { _wasLive = false; Leave(); }
                if (!live) return;
            }
            else if (_lobby.activeSelf == live)
            {
                _lobby.SetActive(!live);
                _game.SetActive(live);
            }

            if (!live) { DrawLobby(); return; }
            UpdateSettingsPanel();
            UpdateCardDetailClose();
            UpdateWorldTileClick();
            DrawGame();
        }

        void DrawBurnNotification(ref GameState live, int me)
        {
            byte burnSeq = GameRules.BurnSeq(ref live, me);

            // BurnSeq 는 GameState 에 보존되므로, UI 가 늦게 붙거나 재생성된 경우의 기존
            // 버림을 새 이벤트로 재생하지 않는다. NetGame/로컬 슬롯 변경도 새 게임으로
            // 취급해 이전 매치의 baseline 과 비교하지 않는다.
            if (_burnSource != NetGame.I || _burnPlayer != me)
            {
                _burnSource = NetGame.I;
                _burnPlayer = me;
                _seenBurnSeq = burnSeq;
                _burnUntil = 0f;
                _burned.text = "";
                return;
            }

            if (burnSeq == _seenBurnSeq) return;
            _seenBurnSeq = burnSeq;

            byte card = GameRules.Burned(ref live, me);
            if (card == byte.MaxValue) return;

            var burned = Cards.Get(card);
            _burned.text = burned != null ? "손패 가득 - " + burned.Name + " 버림" : "";
            _burnUntil = Time.unscaledTime + 2f;
            _sfx?.Play(SfxId.Burn);
        }

        void DrawLobby()
        {
            if (!_loadoutSent && NetGame.I != null && NetGame.I.MyPlayer >= 0 && !string.IsNullOrEmpty(_nick))
            {
                NetGame.I.SubmitLoadoutServerRpc(new FixedString32Bytes(_nick), BuildDeck());
                _loadoutSent = true;
                _status.text = "준비 완료 - 상대를 기다리는 중";
            }
            var kb = Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame && !_searching) Backspace();
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                _focus = _focus == Field.Nick ? Field.Server : Field.Nick;

            bool caret = !_searching && ((int)(Time.unscaledTime * 2f) & 1) == 0;
            DrawField(_nickText, _nick, "클릭 후 입력", _focus == Field.Nick, caret);
            DrawField(_serverText, _server, "서버 주소", _focus == Field.Server, caret);
            _nickBg.color = _focus == Field.Nick ? fieldFocused : fieldIdle;
            _serverBg.color = _focus == Field.Server ? fieldFocused : fieldIdle;

        }

        FixedList32Bytes<byte> BuildDeck()
        {
            var deck = new FixedList32Bytes<byte>();
            foreach (byte card in Cards.DeckList) deck.Add(card);
            return deck;
        }

        /// 값이 비었으면 흐린 안내문(플레이스홀더), 있으면 흰 글씨. 포커스 중이면 커서를 깜빡인다.
        static void DrawField(Text t, string value, string placeholder, bool focused, bool caret)
        {
            if (value.Length == 0 && !focused)
            {
                t.text = placeholder;
                t.color = new Color(1f, 1f, 1f, 0.35f);
                return;
            }
            t.text = value + (focused && caret ? "|" : "");
            t.color = Color.white;
        }

        void DrawGame()
        {
            // 서버가 보낸 최신 상태를 바로 그리지 않는다. 시퀀서가 이동→공격→피해를
            // 나눠 재생하는 동안에는 아직 보여줄 단계의 상태를 돌려준다.
            var live = NetGame.I.State.Value;
            var s = _seq.Present(live);
            int me = NetGame.I.MyPlayer, opp = 1 - me;
            bool myTurn = NetGame.I.IsMyTurn;

            int mx = GameRules.X(ref s, me), my = GameRules.Y(ref s, me);
            int ox = GameRules.X(ref s, opp), oy = GameRules.Y(ref s, opp);
            ref var hand = ref GameRules.Hand(ref s, me);
            if (_selected >= hand.Length) _selected = -1;
            // 두 클라이언트가 같은 보드 배치를 보도록 월드 기준은 플레이어 슬롯과 무관하게 고정한다.
            SyncViewFlip(0);
            SyncBoardOverlay();

            // 커서가 올라간 칸. 끌고 있으면 노랗게 집어 주고, 장판이면 이름을 보여준다.
            _hoverTile = TileUnderMouse();
            int dragTile = _dragIndex >= 0 ? _hoverTile : -1;

            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = TileIndex(x, y);
                    bool blocked = GameRules.IsBlocked(ref s, x, y);
                    // 막힌 칸이라도 규칙이 허용하면(토템 타격) 대상 표시가 우선한다
                    bool canPlay = myTurn && _selected >= 0 && GameRules.CanPlay(ref s, me, _selected, x, y);
                    Color c = Color.clear;
                    // 사거리 제한이 없으므로 실제로 쓸 수 있는 칸만 표시한다
                    if (canPlay) c = tileTarget;
                    else if (blocked) c = tileBlocked;   // 막힌 칸은 항상 표시한다
                    if (i == dragTile && canPlay) c = tileDragPick;
                    SetTileColor(_tileMark[i], c);
                    SetTileColor(_tileZone[i], Color.clear);   // 장판은 아래 DrawWorldEffects 가 다시 칠한다
                    _tileBg[i].color = Color.clear;     // 클릭 판정만 맡고 그림은 월드가 그린다
                    _tileTxt[i].text = "";
                    _tileTxt[i].color = Color.white;
                }

            DrawWorldEffects(ref s, me);
            DrawFieldFx(ref s, me);

            if (_selfCharacter != null) _selfCharacter.position = _seq.WorldOf(true, mx, my);
            if (_foeCharacter != null) _foeCharacter.position = _seq.WorldOf(false, ox, oy);

            // 새로 들어온 카드 슬롯에 등장 시각을 찍어 살짝 커지며 나타나게 한다
            if (_prevHandLen >= 0 && hand.Length != _prevHandLen)
            {
                if (hand.Length > _prevHandLen)
                {
                    for (int i = _prevHandLen; i < hand.Length && i < _cardPopAt.Length; i++)
                        _cardPopAt[i] = Time.unscaledTime;
                    Fly(LocalOf(_deckRt), HandCardPoint());   // 덱에서 뽑아 손으로
                }
                else Fly(HandCardPoint(), LocalOf(_discRt));  // 쓴 카드는 버린 패로
            }
            _prevHandLen = hand.Length;

            ref var deck = ref GameRules.Deck(ref s, me);
            ref var disc = ref GameRules.Disc(ref s, me);
            _deckTxt.text = deck.Length.ToString();
            _discTxt.text = disc.Length.ToString();
            if (_deckImg.enabled != (deck.Length > 0)) _deckImg.enabled = deck.Length > 0;
            if (_discImg.enabled != (disc.Length > 0)) _discImg.enabled = disc.Length > 0;
            UpdateFlyCard();

            for (int i = 0; i < _cardGo.Length; i++)
            {
                bool has = i < hand.Length;
                if (_cardGo[i].activeSelf != has) _cardGo[i].SetActive(has);
                if (!has) continue;
                byte id = hand[i];
                var d = Cards.Get(id);
                _cardView[i].Set(d, IconOf(id), _selected == i);
                _cardView[i].SetDescription(d != null ? CardText.GetDescription((CardId)id, d) : "");
            }

            var oppName = me == 0 ? s.p1Name : s.p0Name;
            var myName = me == 0 ? s.p0Name : s.p1Name;
            ref var oppHand = ref GameRules.Hand(ref s, opp);

            LayoutHand(hand.Length);
            LayoutOpponentHand(oppHand.Length);

            if (_detailIndex >= hand.Length) _detailIndex = -1;   // 그 카드를 이미 썼다
            DrawCardDetail(_detailIndex >= 0 ? Cards.Get(hand[_detailIndex]) : null,
                           _detailIndex >= 0 ? hand[_detailIndex] : (byte)0);

            // 누구 차례인지: 그 사람 이름줄에 표시를 붙이고, 턴 줄에도 이름을 쓴다.
            bool mine = s.winner == 0 && s.turnPlayer == me;
            bool theirs = s.winner == 0 && !mine;
            _oppBar.text = string.Format("{0}    카드 {1}{2}{3}",
                oppName, oppHand.Length, theirs ? FoeTurnMark : "", EffectsOf(ref s, opp));
            _turnBar.text = string.Format("Turn {0}/{1}   -   {2}의 턴   -   {3}초",
                GameRules.Round(s.turnCount), GameRules.MaxTurns, mine ? myName : oppName,
                Mathf.CeilToInt(NetGame.I.TurnSecondsLeft));
            _selfBar.text = myName + (mine ? MyTurnMark : "") + EffectsOf(ref s, me);
            // 체력 숫자는 이름줄에서 빼고 바 안에 "현재 / 최대" 로 찍는다.
            DrawHpBar(0, GameRules.Hp(ref s, me));
            DrawHpBar(1, GameRules.Hp(ref s, opp));
            SyncObstacleVisibility(ref s);

            // 코스트는 자기 턴인 쪽만 채워 그린다. 상대 턴에 남은 내 코스트는 쓸 수 없는 값이라 헷갈린다.
            DrawCostRow(_selfCost, _selfCostBlink, mine ? s.actionLeft : 0, PendingCost(ref s, me, _dragIndex));
            DrawCostRow(_oppCost, _oppCostBlink, theirs ? s.actionLeft : 0,
                        PendingCost(ref s, opp, NetGame.I.DragIndexOf(opp)));

            DrawBurnNotification(ref live, me);
            DrawFoePlayedCard(ref live, me);
            if (Time.unscaledTime >= _burnUntil) _burned.text = "";

            DrawHurtBars(ref s, me, opp);
            DrawTurnBanner(s, myTurn);

            if (s.winner != _seenWinner)
            {
                _seenWinner = s.winner;
                if (s.winner != 0)
                    _sfx?.Play(s.winner - 1 == me ? SfxId.Victory : SfxId.Defeat);
            }

            _result.text = s.winner == 0 ? ""
                : (s.winner - 1 == me ? "승리!" : "패배")
                  + (s.foeLeft != 0 ? "  (상대 연결 끊김)" : "");

            bool over = s.winner != 0;
            if (_backBtn.activeSelf != over) _backBtn.SetActive(over);
            // 턴 종료는 내 차례에만 누를 수 있으니 그때만 보여준다
            bool canEnd = myTurn && !over;
            if (_endTurnBtn.activeSelf != canEnd) _endTurnBtn.SetActive(canEnd);
        }

        /// 상대 손패는 씬에 없다. 화면 위쪽에 뒷면 카드를 매수만큼 만든다.
        /// 내 손패와 같은 부채꼴 공식을 쓰므로 모양이 똑같고 그림만 뒷면이다.
        void BuildOpponentHand()
        {
            var back = Resources.Load<Sprite>("Cards/cardBack");

            var root = new GameObject("OppHand", typeof(RectTransform));
            root.transform.SetParent(_game.transform, false);
            _oppHandRt = (RectTransform)root.transform;
            _oppHandRt.anchorMin = _oppHandRt.anchorMax = new Vector2(0.5f, 1f);
            _oppHandRt.pivot = new Vector2(0.5f, 1f);
            _oppHandRt.sizeDelta = new Vector2(FanStepX * HandSlots, CardSlotH);
            _oppHandRt.anchoredPosition = new Vector2(_handRt.anchoredPosition.x, OppHandY);
            _oppHandRt.localScale = Vector3.one * OppHandScale;

            for (int i = 0; i < _oppBack.Length; i++)
            {
                var go = new GameObject("Back_" + i, typeof(RectTransform));
                go.transform.SetParent(_oppHandRt, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = FanPivot;
                rt.sizeDelta = new Vector2(CardSlotW, CardSlotH);

                var img = go.AddComponent<Image>();
                img.sprite = back;
                img.raycastTarget = false;
                img.preserveAspect = true;
                CardView.AddPixelOutline(img);
                _oppBack[i] = img;
            }
        }

        /// 월드 효과 마커를 찍을 글자칸. 씬에는 타일 배경만 있으므로 타일마다 하나씩 만든다.
        void BuildTileLabels(Font font)
        {
            for (int i = 0; i < _tileTxt.Length; i++)
            {
                var go = new GameObject("Mark", typeof(RectTransform));
                go.transform.SetParent(_board.GetChild(i), false);
                Stretch((RectTransform)go.transform);

                var t = go.AddComponent<Text>();
                if (font != null) t.font = font;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                t.raycastTarget = false;      // 타일 클릭을 가로채지 않는다
                t.resizeTextForBestFit = true;
                // 원본 16px 위로는 키우지 않는다. 두 줄짜리 구조물 마커가 칸을 다 먹는다.
                // 서리 장판까지 겹쳐 세 줄이 되는 칸에서는 잘리는 대신 더 줄어들게 둔다.
                t.resizeTextMinSize = 10;
                t.resizeTextMaxSize = PixelFontCrisp.NativeSize;
                // 밝은 장판 색 위에서도 읽힐 수 있게 검은 테두리를 두른다.
                CardView.AddPixelOutline(t, 1f);
                _tileTxt[i] = t;
            }
        }

        /// 화면 아래 양 끝 뒷면 더미. 왼쪽이 버린 패, 오른쪽이 덱.
        /// 둘 사이를 오가는 카드 한 장도 여기서 같이 만든다.
        void BuildPiles(Font font)
        {
            var back = Resources.Load<Sprite>("Cards/cardBack");
            _discRt = BuildPile("Discard", back, font, 0f, PileEdgeX, out _discImg, out _discTxt);
            _deckRt = BuildPile("Deck", back, font, 1f, -PileEdgeX, out _deckImg, out _deckTxt);

            var fly = new GameObject("FlyCard", typeof(RectTransform));
            fly.transform.SetParent(_game.transform, false);
            _flyRt = (RectTransform)fly.transform;
            _flyRt.anchorMin = _flyRt.anchorMax = _flyRt.pivot = new Vector2(0.5f, 0.5f);
            _flyRt.sizeDelta = new Vector2(CardSlotW, CardSlotH) * PileScale;
            _flyImg = fly.AddComponent<Image>();
            _flyImg.sprite = back;
            _flyImg.preserveAspect = true;
            _flyImg.raycastTarget = false;
            _flyImg.enabled = false;
        }

        RectTransform BuildPile(string name, Sprite back, Font font, float edge, float x,
                                out Image img, out Text count)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_game.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(edge, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardSlotW, CardSlotH) * PileScale;
            rt.anchoredPosition = new Vector2(x, PileY);

            img = go.AddComponent<Image>();
            img.sprite = back;
            img.preserveAspect = true;
            img.raycastTarget = false;
            CardView.AddPixelOutline(img);

            // 장수는 더미 위에 겹쳐 찍는다. 뒷면 그림 위에서도 읽히도록 테두리를 준다.
            var label = new GameObject("Count", typeof(RectTransform));
            label.transform.SetParent(rt, false);
            Stretch((RectTransform)label.transform);

            count = label.AddComponent<Text>();
            if (font != null) count.font = font;
            count.color = Color.white;
            count.alignment = TextAnchor.MiddleCenter;
            count.raycastTarget = false;
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = PixelFontCrisp.NativeSize;
            count.resizeTextMaxSize = 34;
            CardView.AddPixelOutline(count, 1.5f);
            return rt;
        }

        /// 두 자리 사이로 뒷면 카드 한 장을 날린다. 뽑기와 버리기가 같은 연출을 쓴다.
        void Fly(Vector2 from, Vector2 to)
        {
            _flyFrom = from;
            _flyTo = to;
            _flyT = 0f;
        }

        /// 손패 루트의 피벗은 화면 밖 아래쪽이다. 실제 카드가 있는 높이로 올려 잡는다.
        Vector2 HandCardPoint() => LocalOf(_handRt) + new Vector2(0f, CardSlotH * 0.5f);

        /// Game 루트 기준 좌표. 오버레이 캔버스라 화면 픽셀과 1:1 이다.
        Vector2 LocalOf(RectTransform target)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_game.transform, screen, null, out var p);
            return p;
        }

        void UpdateFlyCard()
        {
            bool alive = _flyT < 1f;
            if (_flyImg.enabled != alive) _flyImg.enabled = alive;
            if (!alive) return;

            _flyT = Mathf.Min(1f, _flyT + Time.unscaledDeltaTime / FlySeconds);
            float k = Mathf.SmoothStep(0f, 1f, _flyT);
            float arc = Mathf.Sin(k * Mathf.PI);
            _flyRt.anchoredPosition = Vector2.Lerp(_flyFrom, _flyTo, k) + new Vector2(0f, 60f * arc);
            _flyRt.localScale = Vector3.one * (1f + 0.35f * arc);
        }

        /// 상대가 방금 낸 카드를 오른쪽 위 구석에 작게 띄우는 패널. 보드를 가리지 않도록
        /// 상세 팝업의 어둡게 덮개와 닫기 버튼 없이 카드만 놓는다.
        void BuildFoePlayedPanel(Sprite frame, Font titleFont, Font bodyFont)
        {
            var root = new GameObject("FoePlayedCard", typeof(RectTransform));
            root.transform.SetParent(_game.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(DetailW, DetailH) * FoeCardScale;
            rt.anchoredPosition = new Vector2(-24f, -24f);

            var img = root.AddComponent<Image>();
            var txtGo = new GameObject("Title", typeof(RectTransform));
            txtGo.transform.SetParent(root.transform, false);
            var txt = txtGo.AddComponent<Text>();

            _foeCardView = CardView.Build(rt, img, txt,
                                          new Vector2(DetailW, DetailH) * FoeCardScale,
                                          frame, titleFont, bodyFont);
            img.raycastTarget = false;   // 판을 가리지 않도록 클릭도 먹지 않는다
            _foeCardGo = root;
            _foeCardGo.SetActive(false);
        }

        /// 상대가 카드를 낼 때마다 그 카드를 몇 초 띄운다. lastActionSequence 는 GameState 에
        /// 남으므로, UI 가 늦게 붙은 경우의 지난 사용을 새 이벤트로 재생하지 않는다.
        void DrawFoePlayedCard(ref GameState live, int me)
        {
            if (_foeCardGo == null) return;

            if (_foeCardSource != (object)NetGame.I)
            {
                _foeCardSource = NetGame.I;
                _seenActionSeq = live.lastActionSequence;
                _foeCardUntil = 0f;
            }
            else if (live.lastActionSequence != _seenActionSeq)
            {
                _seenActionSeq = live.lastActionSequence;
                if (live.lastActionKind == GameplayActionKind.CardUsed && live.lastActionPlayer != me)
                {
                    byte id = live.lastActionCardId;
                    var card = Cards.Get(id);
                    if (card != null)
                    {
                        _foeCardView.Set(card, IconOf(id), false);
                        _foeCardView.SetDescription(CardText.GetDescription((CardId)id, card));
                        _foeCardUntil = Time.unscaledTime + FoeCardSeconds;
                    }
                }
            }

            bool show = Time.unscaledTime < _foeCardUntil;
            if (_foeCardGo.activeSelf != show) _foeCardGo.SetActive(show);
        }

        /// 커서가 닿은 카드를 크게 다시 그리는 패널. 손패 카드를 키워 읽으려 하면
        /// 화면 크기에 따라 또 안 읽히므로, 크기가 고정된 자리를 따로 둔다.
        void BuildDetailPanel(Sprite frame, Font titleFont, Font bodyFont)
        {
            var root = new GameObject("CardDetail", typeof(RectTransform));
            root.transform.SetParent(_game.transform, false);
            Stretch((RectTransform)root.transform);

            // 뒤쪽을 어둡게 덮어 카드에 시선을 모은다. 덮개가 레이캐스트를 먹으므로
            // 팝업이 떠 있는 동안 보드와 손패는 눌리지 않는다.
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dim.transform);
            _detailDim = dim.AddComponent<Image>();
            _detailDim.color = new Color(0f, 0f, 0f, 0f);
            dim.AddComponent<Button>().onClick.AddListener(CloseCardDetail);

            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(root.transform, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(DetailW, DetailH);
            rt.anchoredPosition = new Vector2(0f, DetailY);
            rt.localRotation = Quaternion.identity;

            var img = card.AddComponent<Image>();
            var txtGo = new GameObject("Title", typeof(RectTransform));
            txtGo.transform.SetParent(card.transform, false);
            var txt = txtGo.AddComponent<Text>();

            _detailView = CardView.Build(rt, img, txt, new Vector2(DetailW, DetailH),
                                         frame, titleFont, bodyFont);
            img.raycastTarget = false;   // 카드 자체는 눌러도 아무 일 없다

            BuildCloseButton(rt, titleFont);
            _detailRootRt = (RectTransform)root.transform;
            _detailCardRt = rt;
            _detailGo = root;
            _detailGo.SetActive(false);
        }

        /// 카드 오른쪽 위 모서리에 걸치는 닫기 버튼.
        void BuildCloseButton(RectTransform card, Font font)
        {
            var go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(44f, 44f);
            rt.anchoredPosition = new Vector2(4f, 4f);
            go.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.15f, 0.95f);
            go.AddComponent<Button>().onClick.AddListener(CloseCardDetail);

            var label = new GameObject("X", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            Stretch((RectTransform)label.transform);
            var t = label.AddComponent<Text>();
            t.text = "X";
            if (font != null) t.font = font;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = PixelFontCrisp.NativeSize;
            t.resizeTextMaxSize = 32;
        }

        /// 플레이어 정보 줄. 상대는 좌측 상단(edge=1), 나는 좌측 하단(edge=0).
        /// 손패가 화면 가운데를 차지하므로 전체폭 대신 왼쪽 구석에 짧게 붙인다.
        /// 칸 표시용 월드 스프라이트를 칸마다 두 장 만들어 둔다.
        /// 사거리·대상은 테두리, 장판은 속이 빛나는 채움 — 형태로 구분한다.
        void BuildTileMarks()
        {
            var frame = TileFrameSprite();
            var glow = TileGlowSprite();
            // 스프라이트 기본 셰이더는 틴트를 fixed4 정점색으로 실어 보내 HDR 값이 빌드에서
            // 잘린다. 프래그먼트에서 float4 로 곱하는 전용 셰이더를 쓴다.
            var tileShader = Resources.Load<Shader>("Shaders/TileGlow");
            if (tileShader != null) _tileMat = new Material(tileShader);
            else Debug.LogWarning("[스펠 스로워] TileGlow 셰이더를 찾지 못했습니다 - 칸 블룸이 약해집니다.");
            _tileMpb = new MaterialPropertyBlock();
            var root = new GameObject("TileMarks").transform;
            // 표시 루트도 보드와 같은 SCENE 아래에 둔다. 현재는 월드 좌표를
            // 직접 써도 보이지만, 공통 부모가 회전·이동될 때 독립 루트면
            // 검은 장애물 표시만 중앙에 남는다.
            if (_groundMap != null && _groundMap.transform.root != null)
                root.SetParent(_groundMap.transform.root, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = TileIndex(x, y);
                    var at = TileCenterWorld(x, y);
                    _tileZone[i] = NewTileMark(root, "Zone_" + x + "_" + y,
                                               new Vector3(at.x, at.y, TileZoneZ), glow, TileMarkOrder, _tileMat);
                    _tileMark[i] = NewTileMark(root, "Mark_" + x + "_" + y,
                                               new Vector3(at.x, at.y, TileFrameZ), frame, TileMarkOrder, _tileMat);
                    SetTileColor(_tileZone[i], Color.clear);
                    SetTileColor(_tileMark[i], Color.clear);
                }
        }

        static SpriteRenderer NewTileMark(Transform root, string name, Vector3 at, Sprite sprite, int order, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
            sr.color = Color.clear;
            return sr;
        }

        /// 칸 표시 색. HDR 값이 정점색에서 잘리지 않게 머티리얼 프로퍼티로 넘긴다.
        /// sr.color 도 같이 채워 둔다 - BlendTileColor 가 현재 색을 다시 읽는다.
        void SetTileColor(SpriteRenderer sr, Color c)
        {
            sr.color = c;
            if (_tileMat == null || _tileMpb == null) return;
            sr.GetPropertyBlock(_tileMpb);
            _tileMpb.SetColor(TintHdrId, c);
            sr.SetPropertyBlock(_tileMpb);
        }

        /// 사거리·대상 표시: 테두리만. 속이 비어 있어야 말과 이펙트를 가리지 않는다.
        static Sprite TileFrameSprite()
        {
            const int S = 32, Border = 9;
            return BuildTileSprite(S, (x, y) =>
                x < Border || y < Border || x >= S - Border || y >= S - Border ? 1f : 0.55f);
        }

        /// 장판 표시: 가운데가 밝고 가장자리로 옅어지는 채움. 도트 느낌을 살리려고
        /// 낮은 해상도 + Point 필터로 만들어 계단이 그대로 보이게 한다. HDR 색이면 블룸이 붙는다.
        static Sprite TileGlowSprite()
        {
            const int S = 16;
            return BuildTileSprite(S, (x, y) =>
            {
                float dx = Mathf.Abs(x - (S - 1) * 0.5f) / (S * 0.5f);
                float dy = Mathf.Abs(y - (S - 1) * 0.5f) / (S * 0.5f);
                return Mathf.Clamp01(1f - Mathf.Max(dx, dy) * 0.55f);
            });
        }

        /// 바닥 타일(TX Tileset Stone Ground_27)은 정사각형이 아니라 네 모서리가
        /// 32px 기준 3px 씩 대각으로 깎여 있다. 네모난 판을 그대로 올리면 모서리 블룸이
        /// 타일 밖으로 삐져나온다. 표시용 스프라이트도 같은 실루엣으로 잘라 낸다.
        const float TileCornerCut = 3.5f / 32f;

        static Sprite BuildTileSprite(int size, System.Func<int, int, float> alphaAt)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float corner = Mathf.Min(u, 1f - u) + Mathf.Min(v, 1f - v);
                    float a = corner < TileCornerCut ? 0f : Mathf.Clamp01(alphaAt(x, y));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// 코스트 줄 바인딩. 씬의 SelfCost, OppCost 오브젝트를 직접 연결한다.
        void BindCostRows()
        {
            foreach (var sp in Resources.LoadAll<Sprite>("CostUI"))
            {
                if (sp.name.EndsWith("_Fill")) _costFill = sp;
                else if (sp.name.EndsWith("_Half")) _costHalf = sp;
                else if (sp.name.EndsWith("_Empty")) _costEmpty = sp;
            }

            // 마스크는 CostUI 와 같은 캔버스라 칸 좌표를 그대로 쓴다.
            var mask = Resources.Load<Texture2D>("CostUI_Mask");
            if (mask != null && _costFill != null)
                _costMaskFull = Sprite.Create(mask, _costFill.rect, new Vector2(0.5f, 0.5f), _costFill.pixelsPerUnit);

            _selfCost = BindCostRow("SelfCost", out _selfCostBlink);
            _oppCost = BindCostRow("OppCost", out _oppCostBlink);
        }

        /// 이름으로 하위 오브젝트를 찾는다. Transform.Find 는 직속 자식만 보기 때문에,
        /// 하이어라키를 묶는 빈 오브젝트가 하나만 끼어도 바인딩이 조용히 끊긴다.
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        Image[] BindCostRow(string name, out Image[] blink)
        {
            var row = new Image[CostCircles];
            blink = new Image[CostCircles];
            var parent = _selfBar.transform.parent;

            var container = FindDeep(parent, name);
            if (container != null)
            {
                for (int i = 0; i < CostCircles; i++)
                {
                    Transform child = i < container.childCount ? container.GetChild(i) : container.Find("Cost_" + i);
                    if (child != null)
                    {
                        row[i] = child.GetComponent<Image>();
                        var blinkT = child.Find("Blink");
                        if (blinkT != null)
                        {
                            var b = blinkT.GetComponent<Image>();
                            b.enabled = false;
                            if (_costMaskFull != null) b.sprite = _costMaskFull;
                            blink[i] = b;
                        }
                    }
                }
            }
            return row;
        }

        /// 지금 끌고 있는 카드의 코스트. 끌고 있지 않으면 0.
        static int PendingCost(ref GameState s, int player, int dragIndex)
        {
            ref var hand = ref GameRules.Hand(ref s, player);
            if (dragIndex < 0 || dragIndex >= hand.Length) return 0;
            var card = Cards.Get(hand[dragIndex]);
            return card != null ? card.Cost : 0;
        }

        /// 남은 코스트를 칸으로 그리고, 지금 끌고 있는 카드가 쓸 만큼을 흰색으로 점멸시킨다.
        void DrawCostRow(Image[] row, Image[] blink, int left, int pending)
        {
            if (row == null) return;
            float pulse = 0.25f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                int units = Mathf.Clamp(left - i * 2, 0, 2);
                row[i].sprite = units == 2 ? _costFill : units == 1 ? _costHalf : _costEmpty;

                if (blink != null && i < blink.Length && blink[i] != null)
                {
                    int used = units - Mathf.Clamp(left - pending - i * 2, 0, 2);
                    blink[i].enabled = used > 0 && _costMaskFull != null;
                    if (!blink[i].enabled) continue;

                    // 꽉 찬 칸에서 반만 닳으면 없어지는 건 오른쪽 반. 반 칸이면 남아 있던 왼쪽 반.
                    blink[i].fillAmount = used == 2 ? 1f : 0.5f;
                    blink[i].fillOrigin = (int)(units == 2
                        ? Image.OriginHorizontal.Right
                        : Image.OriginHorizontal.Left);
                    blink[i].color = new Color(1f, 1f, 1f, pulse);
                }
            }
        }

        /// 체력 바 바인딩. 씬의 SelfHp, OppHp 오브젝트를 직접 연결한다.
        void BindHpBars()
        {
            BindHpBar("SelfHp", 0);
            BindHpBar("OppHp", 1);
        }

        void BindHpBar(string name, int slot)
        {
            var parent = _selfBar.transform.parent;
            var root = FindDeep(parent, name);
            if (root != null)
            {
                var fillT = root.Find("Inner/Fill");
                var valT = root.Find("Value");
                if (fillT != null)
                {
                    _hpFill[slot] = (RectTransform)fillT;
                    _hpFillImg[slot] = fillT.GetComponent<Image>();
                }
                if (valT != null)
                {
                    _hpText[slot] = valT.GetComponent<Text>();
                }
            }
        }

        /// slot 0 = 나, 1 = 상대.
        void DrawHpBar(int slot, int hp)
        {
            if (_hpFill[slot] == null || _hpFillImg[slot] == null || _hpText[slot] == null) return;
            float ratio = Mathf.Clamp01(hp / (float)GameRules.MaxHp);
            _hpFill[slot].anchorMax = new Vector2(ratio, 1f);
            _hpFillImg[slot].color = ratio > 0.5f ? HpHigh : ratio > 0.25f ? HpMid : HpLow;
            _hpText[slot].text = hp + " / " + GameRules.MaxHp;
        }

        static void PinPlayerBar(Text t, float edge, float y)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, edge);
            rt.sizeDelta = new Vector2(PlayerBarW, 32f);
            rt.anchoredPosition = new Vector2(PlayerBarX, y);
            t.alignment = TextAnchor.MiddleLeft;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = PixelFontCrisp.NativeSize;
            t.resizeTextMaxSize = 20;
        }

        /// 화면 오른쪽 아래 모서리 기준으로 옮긴다. 크기는 씬 값을 그대로 쓴다.
        static void PinCorner(RectTransform rt, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
        }

        /// 화면 위(edge=1) 또는 아래(edge=0) 끝에 붙인다. 크기는 씬 값을 그대로 쓴다.
        static void PinY(RectTransform rt, float edge, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, edge);
            rt.pivot = new Vector2(0.5f, edge);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// 우클릭 한 번 더, 또는 Esc 로도 닫는다. 덮개가 카드를 가려 CardHover 가
        /// 두 번째 우클릭을 받지 못하므로 여기서 직접 처리한다.
        /// 연 프레임에는 건너뛴다. 안 그러면 여는 클릭이 그대로 닫는 클릭이 된다.
        void UpdateCardDetailClose()
        {
            if (_detailIndex < 0 || Time.frameCount == _detailOpenedFrame) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { CloseCardDetail(); return; }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) CloseCardDetail();
        }

        /// 게임 중 ESC. 카드 확대 팝업이 떠 있으면 그걸 닫는 게 먼저다(UpdateCardDetailClose).
        /// 그 밖에는 설정 패널을 여닫는다.
        void UpdateSettingsPanel()
        {
            if (_settingsGo == null || _detailIndex >= 0) return;

            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            _settingsGo.SetActive(!_settingsGo.activeSelf);
            _surrenderReset?.Invoke();   // 판을 여닫으면 항복 확인 대기는 풀린다
        }

        /// 게임 화면에서 ESC 로 여는 설정 판. 로비 설정과 같은 슬라이더를 그대로 쓴다.
        void BuildSettingsPanel(Font font)
        {
            var root = new GameObject("Settings", typeof(RectTransform));
            root.transform.SetParent(_game.transform, false);
            Stretch((RectTransform)root.transform);

            // 덮개가 레이캐스트를 먹어야 패널이 떠 있는 동안 보드와 손패가 안 눌린다.
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dim.transform);
            dim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            dim.AddComponent<Button>().onClick.AddListener(() =>
            {
                _settingsGo.SetActive(false);
                _surrenderReset?.Invoke();
            });

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 340f);
            panel.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.15f, 0.98f);

            AddPanelLabel(rt, font, "설정", 136f, 26);
            SettingsController.BuildVolumeRows(rt, font);
            BuildSurrenderButton(rt, font);
            AddPanelLabel(rt, font, "ESC 로 닫기", -144f, 16);

            _settingsGo = root;
            _settingsGo.SetActive(false);
        }

        /// 항복 버튼. 되돌릴 수 없는 조작이라 한 번 더 눌러야 실제로 나간다.
        /// 확인 창을 따로 띄우지 않고 버튼 글씨만 바꿔 두 단계로 만든다.
        void BuildSurrenderButton(RectTransform parent, Font font)
        {
            var go = new GameObject("Surrender", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -100f);
            rt.sizeDelta = new Vector2(300f, 48f);

            var back = go.AddComponent<Image>();
            back.color = SurrenderIdle;

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(rt, false);
            Stretch((RectTransform)label.transform);
            var text = label.AddComponent<Text>();
            if (font != null) text.font = font;
            text.text = SurrenderText;
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            CardView.AddPixelOutline(text, 1f);

            go.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (!_surrenderArmed)
                {
                    _surrenderArmed = true;
                    text.text = SurrenderConfirmText;
                    back.color = SurrenderArmed;
                    return;
                }

                _surrenderArmed = false;
                text.text = SurrenderText;
                back.color = SurrenderIdle;
                _settingsGo.SetActive(false);
                NetGame.I?.SurrenderServerRpc();
            });

            _surrenderReset = () =>
            {
                _surrenderArmed = false;
                text.text = SurrenderText;
                back.color = SurrenderIdle;
            };
        }

        static void AddPanelLabel(RectTransform parent, Font font, string value, float y, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(520f, 34f);

            var t = go.AddComponent<Text>();
            if (font != null) t.font = font;
            t.text = value;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            CardView.AddPixelOutline(t, 1f);
        }


        void CloseCardDetail() => _detailIndex = -1;

        /// UI 요소가 화면에서 차지하는 사각형. 오버레이 캔버스라 월드 모서리가 곧 화면 픽셀이다.
        static Rect ScreenRectOf(RectTransform rt)
        {
            rt.GetWorldCorners(_corners);
            return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
        }

        /// 캐릭터가 화면에서 차지하는 사각형. 부위별 렌더러의 경계를 합친다.
        static bool TryScreenRectOf(Renderer[] parts, Camera cam, out Rect rect)
        {
            rect = default;
            if (parts == null || parts.Length == 0 || cam == null) return false;

            bool any = false;
            Bounds b = default;
            foreach (var r in parts)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return false;

            var lo = cam.WorldToScreenPoint(b.min);
            var hi = cam.WorldToScreenPoint(b.max);
            rect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y),
                                   Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
            return true;
        }

        /// 카드를 보드 한 칸 크기로 맞추는 배율.
        float TileScale()
        {
            var grid = _board != null ? _board.GetComponent<GridLayoutGroup>() : null;
            if (grid == null || grid.cellSize.y <= 0f) return 0.5f;
            return Mathf.Min(grid.cellSize.x / CardSlotW, grid.cellSize.y / CardSlotH);
        }

        /// 부채꼴 회전축. 카드 아래쪽이라 손아귀에서 펴지는 것처럼 보인다.
        static readonly Vector2 FanPivot = new Vector2(0.5f, 0.15f);

        /// 가운데를 0으로 둔 좌우 대칭 자리값.
        static float FanSlot(int i, int count) => i - (count - 1) * 0.5f;

        /// 부채꼴 한 장의 자리. 슬롯은 가만히 두고 그림만 움직인다.
        /// dir 은 부채가 열리는 방향. 내 손패는 아래에서 위로(+1), 상대는 위에서 아래로(-1)
        /// 매달리므로 호와 기울기를 위아래로 뒤집는다. 카드 자체는 똑바로 선 채다.
        static void FanPlace(RectTransform visual, float slot, float focus, float scale, float dir)
        {
            visual.anchoredPosition = new Vector2(slot * FanStepX,
                                                  dir * (-slot * slot * FanArcDrop + FocusLift * focus));
            // 떠오를 때는 기울기를 펴서 똑바로 세운다 → 고른 카드가 눈에 띈다
            visual.localRotation = Quaternion.Euler(0f, 0f, dir * -slot * FanStepDeg * (1f - focus));
            visual.localScale = Vector3.one * scale;
        }

        /// 손에 쥔 것처럼 부채꼴로 편다. 커서가 닿은 카드는 위로 떠오르고 앞으로 나온다.
        void LayoutHand(int count)
        {
            var cam = WorldCamera();
            bool hasSelf = TryScreenRectOf(_selfParts, cam, out var selfRect);
            bool hasFoe = TryScreenRectOf(_foeParts, cam, out var foeRect);

            for (int i = 0; i < _cardGo.Length; i++)
            {
                bool active = i < count;
                float target = active && i == _hoverIndex ? 1f : 0f;
                _cardFocus[i] = Mathf.MoveTowards(_cardFocus[i], target, Time.unscaledDeltaTime * FocusSpeed);
                if (!active) continue;

                // 우클릭 팝업이 들고 있는 카드는 손패에서 지운다. 같은 카드가 두 장 보이면 안 된다.
                if (i == DetailSlot)
                {
                    if (_cardGroup[i] != null) _cardGroup[i].alpha = 0f;
                    continue;
                }

                // 끌고 있는 카드는 자리를 CardDrag 가 잡는다. 보드를 가리지 않도록
                // 한 타일 크기로 줄이고 반투명하게 만든다. 사거리 표시는 그 아래로 비친다.
                if (i == _dragIndex)
                {
                    var dragged = _cardView[i].Visual;
                    dragged.anchoredPosition = Vector2.zero;
                    dragged.localRotation = Quaternion.identity;
                    dragged.localScale = Vector3.one * (TileScale() / CardArtScale);
                    _cardView[i].SetTitleFontSize(HandTitlePixels);
                    if (_cardGroup[i] != null) _cardGroup[i].alpha = 0.55f;
                    continue;
                }

                float slot = FanSlot(i, count);
                // 판정용 슬롯은 고정. 가로 자리만 잡고 그 뒤로 절대 움직이지 않는다.
                ((RectTransform)_cardGo[i].transform).anchoredPosition = new Vector2(slot * FanStepX, 0f);

                const float Pop = 0.25f;
                float pop = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01((Time.unscaledTime - _cardPopAt[i]) / Pop));
                // 그림은 슬롯 안에서 움직인다. 가로 자리는 슬롯이 이미 잡았으므로 0.
                var visual = _cardView[i].Visual;
                visual.anchoredPosition = new Vector2(0f,
                    -slot * slot * FanArcDrop + FocusLift * _cardFocus[i]);
                visual.localRotation = Quaternion.Euler(0f, 0f, -slot * FanStepDeg * (1f - _cardFocus[i]));
                visual.localScale =
                    Vector3.one * (pop * Mathf.Lerp(1f, FocusScale, _cardFocus[i]) / CardArtScale);
                _cardView[i].SetTitleFontSize(HandTitlePixels);

                // 말을 가리면 카드를 흐리게 한다. 손패가 화면 아래라 보통은 안 겹치지만
                // 말이 아래쪽 줄에 서면 겹친다.
                var cardRect = ScreenRectOf(visual);
                bool overSelf = hasSelf && cardRect.Overlaps(selfRect);
                bool overFoe = hasFoe && cardRect.Overlaps(foeRect);
                if (_cardGroup[i] != null)
                    _cardGroup[i].alpha =
                        (overSelf || overFoe) && _cardFocus[i] <= 0f ? CoveredCardAlpha : 1f;
            }

            // 커서가 닿은 카드는 슬롯째로 맨 뒤 형제가 되어 옆 카드 위에 그려진다.
            // 자리는 배열 번호로 잡으므로 형제 순서가 바뀌어도 카드가 움직이지 않는다.
            if (_hoverIndex >= 0 && _hoverIndex < count)
                _cardGo[_hoverIndex].transform.SetAsLastSibling();
        }

        /// 상대 손패 뒷면을 매수만큼 보여준다. 배치 공식은 내 손패와 같다.
        void LayoutOpponentHand(int count)
        {
            var cam = WorldCamera();
            bool hasSelf = TryScreenRectOf(_selfParts, cam, out var selfRect);
            bool hasFoe = TryScreenRectOf(_foeParts, cam, out var foeRect);

            // 상대가 지금 끌고 있는 카드는 내 손패 포커스와 똑같이 떠오른다.
            int dragged = NetGame.I != null && NetGame.I.MyPlayer >= 0
                ? NetGame.I.DragIndexOf(1 - NetGame.I.MyPlayer) : -1;

            for (int i = 0; i < _oppBack.Length; i++)
            {
                bool active = i < count;
                float target = active && i == dragged ? 1f : 0f;
                _oppFocus[i] = Mathf.MoveTowards(_oppFocus[i], target, Time.unscaledDeltaTime * FocusSpeed);
                if (_oppBack[i].enabled != active) _oppBack[i].enabled = active;
                if (!active) continue;

                var rt = (RectTransform)_oppBack[i].transform;
                FanPlace(rt, FanSlot(i, count), _oppFocus[i],
                         Mathf.Lerp(1f, FocusScale, _oppFocus[i]), -1f);
                if (_oppFocus[i] > 0f) rt.SetAsLastSibling();

                var backRect = ScreenRectOf(rt);
                bool overSelf = hasSelf && backRect.Overlaps(selfRect);
                bool overFoe = hasFoe && backRect.Overlaps(foeRect);

                var c = _oppBack[i].color;
                c.a = overSelf || overFoe ? CoveredCardAlpha : 1f;
                _oppBack[i].color = c;
            }
        }

        public void SetHoveredCard(int index) => _hoverIndex = index;

        public void ClearHoveredCard(int index)
        {
            if (_hoverIndex == index) _hoverIndex = -1;
        }

        /// 우클릭한 카드를 크게 띄운다. 같은 카드를 다시 누르면 닫는다.
        public void ToggleCardDetail(int index)
        {
            bool opening = _detailIndex != index;
            _detailIndex = opening ? index : -1;
            _detailOpenedFrame = Time.frameCount;
            if (opening) { _detailLast = index; RememberDetailStart(index); }
        }

        /// 팝업이 손패의 그 카드 자리에서 떠오르도록 출발 자세를 받아 둔다.
        void RememberDetailStart(int index)
        {
            var visual = _cardView[index] != null ? _cardView[index].Visual : null;
            if (visual == null || _detailRootRt == null) return;

            var screen = RectTransformUtility.WorldToScreenPoint(null, visual.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _detailRootRt, screen, null, out var local);
            _detailFrom = local;
            _detailFromRot = visual.localRotation;
            _detailT = 0f;
        }

        /// 손패가 바뀌면 가리키던 카드가 달라지므로 팝업을 닫는다.
        void DrawCardDetail(CardDef card, byte id)
        {
            if (_detailView == null) return;

            bool open = card != null;
            if (open)
            {
                _detailView.Set(card, IconOf(id), false);
                _detailView.SetDescription(CardText.GetDescription((CardId)id, card));
            }

            // 닫힐 때도 되감기가 보여야 하므로 T 가 0 이 될 때까지 살려 둔다.
            _detailT = Mathf.MoveTowards(_detailT, open ? 1f : 0f,
                                         Time.unscaledDeltaTime / DetailAnimSeconds);
            bool alive = open || _detailT > 0f;
            if (_detailGo.activeSelf != alive) _detailGo.SetActive(alive);
            if (!alive) return;

            float k = Mathf.SmoothStep(0f, 1f, _detailT);
            // 카드가 떠오르는 만큼 주위가 함께 어두워진다
            _detailDim.color = new Color(0f, 0f, 0f, DetailDimAlpha * k);

            const float FromScale = CardSlotW / DetailW;   // 손패 카드 크기에서 출발한다
            _detailCardRt.anchoredPosition = Vector2.Lerp(_detailFrom, new Vector2(0f, DetailY), k);
            _detailCardRt.localScale = Vector3.one * Mathf.Lerp(FromScale, 1f, k);
            _detailCardRt.localRotation = Quaternion.Slerp(_detailFromRot, Quaternion.identity, k);
        }

        /// 월드 효과는 별도 프리팹 없이 보드 타일 색과 짧은 문자 마커로 표시한다.
        /// F2 = 화염 장판 2회 남음, T2 = 지연 텔레포트 2턴 남음.
        void DrawWorldEffects(ref GameState s, int me)
        {
            ref var effects = ref GameRules.WorldEffects(ref s);
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!GameRules.InBounds(effect.X, effect.Y)) continue;

                int tile = TileIndex(effect.X, effect.Y);
                // 커서가 올라간 칸은 무슨 장판인지 풀어서 보여준다
                bool ownedByMe = effect.SourcePlayer == me;
                string marker = tile == _hoverTile
                    ? EffectLabel(effect, ownedByMe)
                    : EffectMarker(effect, ownedByMe);
                _tileTxt[tile].text = _tileTxt[tile].text.Length == 0
                    ? marker
                    : _tileTxt[tile].text + "\n" + marker;
                _tileTxt[tile].color = Color.white;

                Color effectColor = effect.Kind == WorldEffectKind.FireZone ? tileFireZone
                    : effect.Kind == WorldEffectKind.FrostZone ? tileIceZone
                    : effect.Kind != WorldEffectKind.Structure ? tileDelayedTeleport
                    : effect.Structure == StructureKind.IceWall ? tileIceZone
                    : ownedByMe ? tileTotem : tileTotemFoe;
                PaintZoneTile(tile, effect.X, effect.Y, effectColor);
            }
        }

        /// 장판 칸: 블룸이 걸리는 밝은 색으로 칠하고 알갱이를 천천히 띄운다.
        void PaintZoneTile(int tile, int x, int y, Color color)
        {
            SetTileColor(_tileZone[tile], BlendTileColor(_tileZone[tile].color, color));
            // 작은 알갱이를 촘촘하게 — 한 칸에서 초당 20~40개쯤 올라온다
            if (Time.time < _puffAt[tile]) return;
            _puffAt[tile] = Time.time + Random.Range(0.03f, 0.08f);
            var at = TileWorld(x, y);
            _fx.Puff(at, color);
            _fx.Puff(at, color);
        }

        /// 유지형 연출. 매 프레임 "아직 있다"고 알려주고, 사라진 것만 마무리 연출로 넘어간다.
        /// 얼음은 대상이 묶여 있는 동안, 토템은 구조물이 남아 있는 동안.
        void DrawFieldFx(ref GameState s, int me)
        {
            _fx.BeginZones();

            for (int p = 0; p < 2; p++)
            {
                if (GameRules.MoveLocked(ref s, p) == 0 && !GameRules.HasTag(ref s, p, PlayerTagId.MoveLocked))
                    continue;
                int fx = GameRules.X(ref s, p), fy = GameRules.Y(ref s, p);
                // 얼음: 솟아오른 뒤 결정 부분(2~7번 프레임)만 계속 돈다
                _fx.KeepZone(p, _seq.WorldOf(p == me, fx, fy), FxKind.IceStart, FxKind.IceLoop, FxKind.IceEnd, 1.6f);
                int iceTile = TileIndex(fx, fy);
                PaintZoneTile(iceTile, fx, fy, tileIceZone);
                if (iceTile == _hoverTile) _tileTxt[iceTile].text = "얼음\n이동 불가";
            }

            // 장판은 종류에 상관없이 깔린 칸마다 같은 연출을 건다
            ref var effects = ref GameRules.WorldEffects(ref s);
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!GameRules.InBounds(effect.X, effect.Y)) continue;
                FxKind start, loop, end;
                if (!ZoneFxOf(effect, out start, out loop, out end)) continue;
                _fx.KeepZone(100 + effect.Sequence, TileWorld(effect.X, effect.Y), start, loop, end,
                             effect.Kind == WorldEffectKind.Structure ? 1f : 1.6f);
            }

            _fx.EndZones();
        }

        /// 월드 효과 종류별 유지 연출. 시트가 없는 종류는 칸 색만 칠한다.
        static bool ZoneFxOf(WorldEffectRecord effect, out FxKind start, out FxKind loop, out FxKind end)
        {
            // 얼음 방벽은 세워 두는 구조물이지만 토템 연출을 쓰면 안 된다.
            if (effect.Kind == WorldEffectKind.Structure && effect.Structure == StructureKind.IceWall)
            {
                start = loop = end = FxKind.None;
                return false;
            }
            switch (effect.Kind)
            {
                case WorldEffectKind.Structure:
                    start = FxKind.TotemRise; loop = FxKind.TotemIdle; end = FxKind.None;
                    return true;
                case WorldEffectKind.FireZone:
                    start = FxKind.FireZoneStart; loop = FxKind.FireZoneLoop; end = FxKind.FireZoneEnd;
                    return true;
                default:
                    start = loop = end = FxKind.None;
                    return false;
            }
        }

        /// 커서가 올라갔을 때 보여줄 이름. 짧은 마커만으로는 무슨 장판인지 알 수 없다.
        static string EffectLabel(WorldEffectRecord effect, bool ownedByMe)
        {
            switch (effect.Kind)
            {
                case WorldEffectKind.FireZone: return "불길\n" + effect.RemainingTurns + "턴";
                case WorldEffectKind.DelayedTeleport: return "이동\n" + effect.RemainingTurns + "턴";
                case WorldEffectKind.FrostZone: return "서리\n" + effect.RemainingTurns + "턴";
                case WorldEffectKind.Structure:
                    return (effect.Structure == StructureKind.IceWall
                               ? "얼음벽"
                               : (ownedByMe ? "내 " : "상대 ") + StructureName(effect.Structure) + " 토템")
                           + "\nHP " + effect.Power + " · " + effect.RemainingTurns + "턴";
                default: return "?";
            }
        }

        static string EffectMarker(WorldEffectRecord effect, bool ownedByMe)
        {
            switch (effect.Kind)
            {
                case WorldEffectKind.FireZone: return "F" + effect.RemainingTurns;
                case WorldEffectKind.DelayedTeleport: return "T" + effect.RemainingTurns;
                case WorldEffectKind.FrostZone: return "C" + effect.RemainingTurns;
                case WorldEffectKind.Structure:
                    // 토템은 소유자를 삼각형 방향으로 나눈다. ▲ = 내 것, ▼ = 상대 것.
                    // 윗줄은 소유자·종류·남은 HP, 아랫줄은 사라지기까지 남은 턴.
                    return (effect.Structure == StructureKind.IceWall ? "■" : ownedByMe ? "▲" : "▼")
                           + StructureInitial(effect.Structure) + effect.Power
                           + "\n" + effect.RemainingTurns + "턴";
                default: return "?";
            }
        }

        /// 토템 종류. 카드 이름("토템 : 저격")의 뒷부분과 같은 말을 쓴다.
        static string StructureName(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Totem: return "저격";
                case StructureKind.Guardian: return "경계";
                case StructureKind.Thorn: return "감지";
                case StructureKind.Blessing: return "축복";
                case StructureKind.Detonation: return "폭탄";
                default: return "";
            }
        }

        /// 한 칸에 다 들어가야 하므로 마커에는 종류를 첫 글자로만 적는다.
        static string StructureInitial(StructureKind kind)
        {
            var name = StructureName(kind);
            return name.Length == 0 ? "" : name.Substring(0, 1);
        }

        static Color BlendTileColor(Color baseColor, Color effectColor)
        {
            return baseColor.a <= 0.01f
                ? effectColor
                : Color.Lerp(baseColor, effectColor, 0.70f);
        }

        /// 체력이 깎인 쪽 체력 바를 잠깐 붉게 물들인다.
        void DrawHurtBars(ref GameState s, int me, int opp)
        {
            const float Hurt = 0.45f;
            byte myHp = GameRules.Hp(ref s, me), oppHp = GameRules.Hp(ref s, opp);
            if (_prevHpValid)
            {
                if (myHp < _prevMyHp) _myHurtUntil = Time.unscaledTime + Hurt;
                if (oppHp < _prevOppHp) _oppHurtUntil = Time.unscaledTime + Hurt;
            }
            _prevMyHp = myHp; _prevOppHp = oppHp; _prevHpValid = true;

            bool mine = s.winner == 0 && s.turnPlayer == me;
            _selfBar.color = HurtTint(TurnTint(_selfBarHome, mine), _myHurtUntil, Hurt);
            _oppBar.color = HurtTint(TurnTint(_oppBarHome, s.winner == 0 && !mine), _oppHurtUntil, Hurt);
        }

        /// 차례가 아닌 쪽 이름줄은 흐리게 죽인다.
        static Color TurnTint(Color home, bool active)
            => active ? home : new Color(home.r, home.g, home.b, home.a * 0.45f);

        static Color HurtTint(Color home, float until, float span)
        {
            float left = until - Time.unscaledTime;
            if (left <= 0f) return home;
            return Color.Lerp(home, new Color(1f, 0.25f, 0.25f), left / span);
        }

        /// 턴이 넘어가는 순간 화면 가운데에 누구 턴인지 띄우고 서서히 지운다.
        void DrawTurnBanner(GameState s, bool myTurn)
        {
            const float Hold = 1.4f;
            if (s.turnCount != _seenTurnCount || s.turnPlayer != _seenTurnPlayer)
            {
                _seenTurnCount = s.turnCount;
                _seenTurnPlayer = s.turnPlayer;
                _turnBannerUntil = Time.unscaledTime + Hold;
            }

            float left = _turnBannerUntil - Time.unscaledTime;
            if (left <= 0f || s.winner != 0) { _turnBanner.text = ""; return; }

            _turnBanner.text = string.Format("{0}턴 - {1}",
                GameRules.Round(s.turnCount), myTurn ? "내 턴" : "상대 턴");
            var c = _turnBanner.color;
            c.a = Mathf.Clamp01(left / (Hold * 0.5f));   // 뒤쪽 절반 동안 사라진다
            _turnBanner.color = c;
        }

        void SyncBoardOverlay()
        {
            if (_boardAligned || _board == null || WorldCamera() == null) return;

            // 격자 레이아웃에 맡기면 씬에 남은 옛 셀 간격이 그대로 쓰여 칸이 어긋난다.
            // 칸마다 월드 좌표를 화면으로 옮겨 직접 얹는다. 화면을 뒤집어도 그대로 맞는다.
            var boardRect = (RectTransform)_board;
            var cam = WorldCamera();
            var grid = _board.GetComponent<GridLayoutGroup>();
            if (grid != null && grid.enabled) grid.enabled = false;

            boardRect.localRotation = Quaternion.identity;
            float cell = Mathf.Abs(cam.WorldToScreenPoint(TileCenterWorld(1, 0)).x
                                 - cam.WorldToScreenPoint(TileCenterWorld(0, 0)).x);

            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var rt = (RectTransform)_board.GetChild(TileIndex(x, y));
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localRotation = Quaternion.identity;
                    rt.sizeDelta = new Vector2(cell, cell) / boardRect.lossyScale.x;
                    rt.position = cam.WorldToScreenPoint(TileCenterWorld(x, y));
                }

            Canvas.ForceUpdateCanvases();
            _boardAligned = true;
        }


        void UpdateWorldTileClick()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            var screen = Mouse.current.position.ReadValue();
            if (EventSystem.current != null)
            {
                _hits.Clear();
                var pointer = new PointerEventData(EventSystem.current) { position = screen };
                EventSystem.current.RaycastAll(pointer, _hits);
                foreach (var hit in _hits)
                {
                    if (hit.gameObject.GetComponent<CardDrag>() != null ||
                        hit.gameObject.GetComponent<Button>() != null)
                        return;
                }
            }

            var cam = WorldCamera();
            if (cam == null) return;
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            int tile = TileUnderWorld(world);
            if (tile >= 0) OnTile(tile);
        }


public Camera WorldCamera()
        {
            if (_gameCamera != null) return _gameCamera;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var camera = root.GetComponent<Camera>();
                if (camera != null) return _gameCamera = camera;
            }
            return null;
        }
}
}
