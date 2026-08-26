using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpellThrower.EditorTools
{
    /// 씬의 네트워크 오브젝트 + UI 계층을 통째로 만드는 빌더.
    /// 주의: 다시 실행하면 인스펙터에서 손본 값이 전부 초기화된다.
    public static class SpellThrowerSceneBuilder
    {
        const int N = GameRules.Size;
        const float Cell = 50f, Gap = 3f;
        static Font _font;

        [MenuItem("SpellThrower/Rebuild Scene UI")]
        public static void Build()
        {
            var scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Silver.ttf");

            foreach (var n in new[] { "UI", "GameUI", "EventSystem", "NetworkManager", "NetGame" })
            {
                var g = GameObject.Find(n);
                if (g != null) Object.DestroyImmediate(g);
            }

            BuildNetwork();
            var ui = BuildCanvas();
            BuildLobby(ui);
            BuildGame(ui);
            ui.gameObject.AddComponent<GameUI>();

            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();

            var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SpellThrower] Scene rebuilt.");
        }

        static void BuildNetwork()
        {
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<Unity.Netcode.NetworkManager>();
            var utp = nmGo.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            utp.SetConnectionData("127.0.0.1", NetGame.Port);
            if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.EnableSceneManagement = true;

            var ng = new GameObject("NetGame");
            ng.AddComponent<Unity.Netcode.NetworkObject>();
            ng.AddComponent<NetGame>();
        }

        // ---------------- 헬퍼 ----------------
        static RectTransform Go(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Text Label(Transform parent, string name, string txt, int size)
        {
            var t = Go(parent, name).gameObject.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.text = txt; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// Image + Button + 자식 Text. GameUI 가 GetChild(0) 로 Text 를 찾으므로 구조 고정.
        static RectTransform Btn(Transform parent, string name, string label, int size, Color bg)
        {
            var rt = Go(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            rt.gameObject.AddComponent<Button>().targetGraphic = img;
            Stretch(Label(rt, "Label", label, size).rectTransform);
            return rt;
        }

        static void Height(RectTransform rt, float h) =>
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = h;

        // ---------------- 계층 ----------------
        static RectTransform BuildCanvas()
        {
            var ui = new GameObject("UI", typeof(RectTransform));
            var c = ui.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = ui.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280, 720);
            s.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();
            return ui.GetComponent<RectTransform>();
        }

        static void BuildLobby(RectTransform ui)
        {
            var field = new Color(0.18f, 0.18f, 0.22f);
            var button = new Color(0.30f, 0.30f, 0.36f);

            var lobby = Go(ui, "Lobby");
            Place(lobby, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 520));
            lobby.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            var v = lobby.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.padding = new RectOffset(24, 24, 18, 18);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            Height(Label(lobby, "Title", "SPELL THROWER", 35).rectTransform, 48);
            Height(Label(lobby, "NickLabel", "이름  (클릭 후 입력)", 15).rectTransform, 20);
            Height(Btn(lobby, "NickValue", "", 23, field), 40);
            Height(Label(lobby, "ServerLabel", "서버 주소  (클릭 후 입력)", 15).rectTransform, 20);
            Height(Btn(lobby, "ServerValue", "", 23, field), 40);
            lobby.Find("ServerLabel").gameObject.SetActive(false);
            lobby.Find("ServerValue").gameObject.SetActive(false);
            Height(Label(lobby, "DeckLabel", "덱 15/15", 15).rectTransform, 22);
            var choices = Go(lobby, "DeckChoices");
            Height(choices, 58);
            var row = choices.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 4;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            for (int i = 0; i < Cards.All.Length; i++)
            {
                var card = Btn(choices, "Deck_" + i, "", 13, button);
                card.gameObject.AddComponent<LayoutElement>().preferredWidth = 58;
            }
            Height(Btn(lobby, "MatchButton", "매칭 시작", 23, button), 54);
            Height(Label(lobby, "Status", "", 17).rectTransform, 40);
        }

        static void BuildGame(RectTransform ui)
        {
            var game = Go(ui, "Game");
            Stretch(game);   // 화면 전체를 채운다. 고정 크기면 16:10 등에서 가장자리 UI 가 잘린다.

            // 위쪽 = 상대, 아래쪽 = 나
            Place(Label(game, "OpponentBar", "", 23).rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1100, 32));
            Place(Label(game, "TurnBar", "", 18).rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(1100, 26));
            Place(Label(game, "Burned", "", 18).rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(1100, 24));

            float boardSize = N * Cell + (N - 1) * Gap;
            var board = Go(game, "Board");
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(boardSize, boardSize));
            var grid = board.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Cell, Cell);
            grid.spacing = new Vector2(Gap, Gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = N;

            // 자식 순서 = 뒤집지 않았을 때의 (N-1-y)*N + x
            for (int y = N - 1; y >= 0; y--)
                for (int x = 0; x < N; x++)
                    Btn(board, string.Format("Tile_{0}_{1}", x, y), "", 14, new Color(0.22f, 0.22f, 0.26f));

            Place(Label(game, "SelfBar", "", 23).rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0, 148), new Vector2(1100, 32));

            var hand = Go(game, "Hand");
            Place(hand, new Vector2(0.5f, 0f), new Vector2(70, 66), new Vector2(840, 88));
            var h = hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            for (int i = 0; i < GameRules.MaxHand; i++)
            {
                var card = Btn(hand, "Card_" + i, "", 15, new Color(0.30f, 0.30f, 0.36f));
                var le = card.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 112; le.preferredHeight = 80;
                card.gameObject.AddComponent<CanvasGroup>();
                card.gameObject.AddComponent<CardDrag>();
            }

            Place(Btn(game, "EndTurnButton", "턴 종료", 21, new Color(0.30f, 0.30f, 0.36f)),
                new Vector2(1f, 0f), new Vector2(-120, 66), new Vector2(180, 52));

            Place(Label(game, "Result", "", 51).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(700, 70));

            // 승패 후 로비 복귀 — 없으면 결과 화면에서 아무것도 못 한다
            var back = Btn(game, "BackButton", "로비로 돌아가기", 23, new Color(0.35f, 0.30f, 0.45f));
            Place(back, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(280, 56));
            back.gameObject.SetActive(false);
        }
    }
}
