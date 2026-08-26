using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpellThrower
{
    public enum FxKind
    {
        Hit, Ice, Heal, Buff,                       // 옛 시트 (32~82px, 보드 전체 공용)
        FireFly, FireHit,                           // 화염: 투사체 → 폭발 (한 장에 같이 있다)
        IceStart, IceLoop, IceEnd,                  // 얼음 장판: 묶여 있는 동안 유지
        Wind, Thunder, HealCast,                    // 바람 빔 / 낙뢰 / 회복 기둥
        TotemRise, TotemIdle,                       // 토템: 솟아오름 → 밝기 왕복
        FireZoneStart, FireZoneLoop, FireZoneEnd,   // 불길 장판 (8열 3행 한 장)
        None                                        // 마무리 연출이 없는 경우
    }

    /// 전투 연출 담당. 스프라이트 시트 한 장을 런타임에 잘라 재생하고 버린다.
    /// 시트를 에디터에서 슬라이스하지 않으므로 임포터 설정에 의존하지 않는다.
    public class BattleFx : MonoBehaviour
    {
        /// 시트마다 칸 크기·프레임 수·피벗이 다르다. 뒤쪽 빈 칸은 세지 않는다.
        struct Sheet
        {
            public string Path;
            public int CellW, CellH, First, Frames, Ppu;
            public float PivotX, PivotY;
            public bool PingPong;   // 루프용: 정재생 뒤 역재생을 이어 붙인다

            public Sheet(string path, int w, int h, int frames, int ppu,
                         int first = 0, float pivotX = 0.5f, float pivotY = 0.5f, bool pingPong = false)
            {
                Path = path; CellW = w; CellH = h; Frames = frames; Ppu = ppu;
                First = first; PivotX = pivotX; PivotY = pivotY; PingPong = pingPong;
            }
        }

        // 순서는 FxKind 와 같아야 한다. None 은 시트가 없다.
        static readonly Sheet[] Sheets =
        {
            new Sheet("VFX/Hit",  82, 65,  8, 64),
            new Sheet("VFX/Ice",  32, 32, 24, 32),
            new Sheet("VFX/Heal", 32, 32, 13, 32),
            new Sheet("VFX/Buff", 32, 32, 13, 32),

            new Sheet("VFX/Spell/Firebolt SpriteSheet", 48, 48,  4, 48, 0, 0.5f, 0.40f),
            new Sheet("VFX/Spell/Firebolt SpriteSheet", 48, 48,  6, 48, 5, 0.5f, 0.40f),

            new Sheet("VFX/Spell/Ice VFX 2 Start",  32, 32,  9, 32, 0, 0.5f, 0f),
            new Sheet("VFX/Spell/Ice VFX 2 Active", 32, 32,  6, 32, 2, 0.5f, 0f),   // 2~7번만 반복
            new Sheet("VFX/Spell/Ice VFX 2 Ending", 32, 32, 18, 32, 0, 0.5f, 0f),

            new Sheet("VFX/Spell/Wind",    48, 32, 12, 48, 0, 0f,    0.5f),  // 피벗 왼쪽 = 시전자에서 뻗는다
            new Sheet("VFX/Spell/Thunder", 64, 64, 13, 48, 0, 0.5f, 0f),
            new Sheet("VFX/Spell/Heal",    48, 48, 16, 48, 0, 0.5f, 0f),

            new Sheet("VFX/Spell/TotemSheet", 24, 136, 12, 68, 0, 0.5f, 0f),
            new Sheet("VFX/Spell/TotemSheet", 24, 136, 12, 68, 0, 0.5f, 0f, true),

            // 8열 3행: 행0 시작(0~2), 행1 지속(8~11), 행2 소멸(16~22)
            new Sheet("VFX/Spell/Fire Breath SpriteSheet", 48, 48, 3, 48,  0, 0.43f, 0.06f),
            new Sheet("VFX/Spell/Fire Breath SpriteSheet", 48, 48, 4, 48,  8, 0.43f, 0.06f),
            new Sheet("VFX/Spell/Fire Breath SpriteSheet", 48, 48, 7, 48, 16, 0.43f, 0.06f),
        };

        [Header("연출 세기 — 실기 화면 보고 맞춘다")]
        public float fxScale = 1.6f;
        public float fxSeconds = 0.45f;
        public float flashSeconds = 0.22f;
        public float shakeSeconds = 0.28f;
        public float shakeStrength = 0.18f;
        public float screenFlashSeconds = 0.30f;
        public Color screenFlashColor = new Color(0.85f, 0.1f, 0.1f, 0.42f);

        [Header("주문 연출")]
        public float projectileScale = 1.6f;
        public float projectileArtAngle = 0f;   // 시트 그림이 향한 방향 보정 (오른쪽 향함 = 0)
        public float impactScale = 2.2f;
        public float groundScale = 1.9f;
        public float projectileTilesPerSecond = 14f;
        public float projectileFps = 14f;
        public float zoneLoopFps = 10f;
        public float ghostInterval = 0.04f;
        public float ghostFadeSeconds = 0.22f;
        public Color ghostTint = new Color(0.65f, 0.85f, 1f, 0.55f);

        /// 공중에서 도는 연출은 말의 몸통 높이에 둔다. 더 올리면 한 칸 위에 뜬 것처럼 보인다.
        const float AirLiftY = 0.35f;
        static readonly Vector3 GroundLift = new Vector3(0f, 0f, -1f);

        /// 화면 기준으로 띄운다. 뒤집힌 화면에서도 같은 높이에 뜬다.
        Vector3 AirLift => ScreenUp * AirLiftY + new Vector3(0f, 0f, -1f);
        /// 블룸이 걸리도록 HDR 로 밝게. 카메라 HDR + URP Bloom 이 이미 켜져 있다.
        /// 장판·알갱이는 발밑에 깔리는 것이라 말보다 반드시 아래에 와야 한다.
        /// 맵·소품·칸 표시는 0~2, 말의 가장 뒤 파츠(망토)가 100 이므로 그 사이에 둔다.
        /// 반대로 폭발·투사체(500~) 는 말 위로 그린다.
        public const int ZoneOrder = 20;
        public const int PuffOrder = 30;

        readonly Dictionary<FxKind, Sprite[]> _frames = new Dictionary<FxKind, Sprite[]>();
        readonly Dictionary<Transform, SpriteRenderer[]> _parts = new Dictionary<Transform, SpriteRenderer[]>();
        readonly Dictionary<int, Zone> _zones = new Dictionary<int, Zone>();
        readonly List<int> _zoneDrop = new List<int>();
        MaterialPropertyBlock _mpb;
        Material _flashMat;
        // 스프라이트 기본 셰이더는 틴트를 fixed4 정점색으로 넘겨 HDR 값이 빌드에서 잘린다.
        // 칸 표시와 같은 전용 셰이더로 프래그먼트에서 float4 로 곱한다.
        Material _hdrMat;
        MaterialPropertyBlock _hdrMpb;
        static readonly int TintHdrId = Shader.PropertyToID("_TintHDR");
        Image _screenFlash;
        Camera _cam;
        Vector3 _camHome;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _hdrMpb = new MaterialPropertyBlock();
            var shader = Shader.Find("SpellThrower/SpriteFlash");
            if (shader != null) _flashMat = new Material(shader);
            var hdr = Resources.Load<Shader>("Shaders/TileGlow");
            if (hdr != null) _hdrMat = new Material(hdr);
            else Debug.LogWarning("[스펠 스로워] TileGlow 셰이더를 찾지 못했습니다 - 이펙트 블룸이 약해집니다.");
        }

        /// 시트를 프레임 단위로 잘라 캐시한다. 텍스처 원점이 좌하단이라 행을 뒤집어 읽는다.
        Sprite[] Frames(FxKind kind)
        {
            if (_frames.TryGetValue(kind, out var cached)) return cached;
            if ((int)kind < 0 || (int)kind >= Sheets.Length) return _frames[kind] = new Sprite[0];

            var def = Sheets[(int)kind];
            var tex = Resources.Load<Texture2D>(def.Path);
            if (tex == null)
            {
                Debug.LogWarning("[스펠 스로워] VFX 시트를 찾지 못했습니다: Resources/" + def.Path);
                return _frames[kind] = new Sprite[0];
            }

            int cols = Mathf.Max(1, tex.width / def.CellW);
            var pivot = new Vector2(def.PivotX, def.PivotY);
            var forward = new Sprite[def.Frames];
            for (int i = 0; i < def.Frames; i++)
            {
                int f = def.First + i;
                int cx = f % cols, cy = f / cols;
                var rect = new Rect(cx * def.CellW,
                                    tex.height - (cy + 1) * def.CellH,
                                    def.CellW, def.CellH);
                forward[i] = Sprite.Create(tex, rect, pivot, def.Ppu);
            }
            if (!def.PingPong || forward.Length < 3) return _frames[kind] = forward;

            // 0..n-1 뒤에 n-2..1 을 붙여 끊김 없이 왕복시킨다
            var loop = new Sprite[forward.Length * 2 - 2];
            for (int i = 0; i < forward.Length; i++) loop[i] = forward[i];
            for (int i = 1; i < forward.Length - 1; i++) loop[forward.Length + i - 1] = forward[forward.Length - 1 - i];
            return _frames[kind] = loop;
        }

        /// 상대 화면은 카메라를 180도 돌려 보여준다. 위아래가 있는 연출은 같이 돌려 세운다.
        /// 방향이 있는 연출(투사체·빔)은 월드 방향 그대로가 맞으므로 적용하지 않는다.
        public Quaternion Upright = Quaternion.identity;

        /// 화면 기준 위쪽 / 오른쪽. 뒤집힌 화면에서도 알갱이가 위로 올라가게 한다.
        Vector3 ScreenUp => Upright * Vector3.up;
        Vector3 ScreenRight => Upright * Vector3.right;

        SpriteRenderer NewFx(string name, Vector3 world, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.position = world;
            go.transform.rotation = Upright;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (_hdrMat != null) sr.sharedMaterial = _hdrMat;
            SetFxColor(sr, Color.white);   // 셰이더가 sr.color 를 안 읽으므로 기본값을 채워 둔다
            return sr;
        }

        /// 이펙트 색. HDR 값이 정점색에서 잘리지 않게 머티리얼 프로퍼티로 넘긴다.
        public void SetFxColor(SpriteRenderer sr, Color c)
        {
            sr.color = c;
            if (_hdrMat == null || _hdrMpb == null) return;
            sr.GetPropertyBlock(_hdrMpb);
            _hdrMpb.SetColor(TintHdrId, c);
            sr.SetPropertyBlock(_hdrMpb);
        }

        // ---------------- 한 번 재생 ----------------

        /// 월드 위치에 옛 이펙트를 한 번 재생한다 (피벗 가운데 → 살짝 띄운다).
        public void Play(FxKind kind, Vector3 world)
        {
            var frames = Frames(kind);
            if (frames.Length == 0) return;
            StartCoroutine(PlayFrames(frames, world + AirLift, fxScale, 500, Upright));
        }

        /// 바닥 기준 시트(낙뢰·회복 기둥)를 그 칸에 한 번 재생한다. scale 0 이면 기본값.
        public void PlayGround(FxKind kind, Vector3 world, float scale = 0f)
        {
            var frames = Frames(kind);
            if (frames.Length == 0) return;
            StartCoroutine(PlayFrames(frames, world + GroundLift, scale <= 0f ? groundScale : scale, 500, Upright));
        }

        IEnumerator PlayFrames(Sprite[] frames, Vector3 world, float scale, int order, Quaternion rot)
        {
            var sr = NewFx("Fx", world, scale, order);
            sr.transform.rotation = rot;
            float per = fxSeconds / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return new WaitForSeconds(per);
            }
            Destroy(sr.gameObject);
        }

        /// 화염: 투사체가 날아가고 도착하면 폭발한다. 도착까지만 기다리게 해서
        /// 호출한 쪽이 명중 순간에 피해 연출을 이어 붙일 수 있다.
        public IEnumerator Projectile(Vector3 from, Vector3 to)
        {
            var fly = Frames(FxKind.FireFly);
            if (fly.Length == 0) yield break;

            Vector3 start = from + AirLift, end = to + AirLift;
            var sr = NewFx("FxBolt", start, projectileScale, 520);
            var dir = end - start;
            // 시트 그림이 오른쪽을 보고 있으므로 방향각을 그대로 쓴다 (보정값은 인스펙터에서)
            var facing = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + projectileArtAngle);
            sr.transform.rotation = facing;

            float seconds = Mathf.Max(0.10f, dir.magnitude / projectileTilesPerSecond);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                sr.transform.position = Vector3.Lerp(start, end, t / seconds);
                sr.sprite = fly[(int)(t * projectileFps) % fly.Length];
                yield return null;
            }
            Destroy(sr.gameObject);

            var hit = Frames(FxKind.FireHit);
            // 폭발 그림도 오른쪽을 보고 있으니 투사체와 같은 방향으로 돌린다
            if (hit.Length > 0) StartCoroutine(PlayFrames(hit, end, impactScale, 520, facing));
        }

        /// 바람: 대상 방향으로 회전하고 사거리만큼 길게 늘린다. 피벗이 왼쪽이라 시전자에서 뻗는다.
        public IEnumerator Beam(Vector3 from, Vector3 to, float tiles)
        {
            var frames = Frames(FxKind.Wind);
            if (frames.Length == 0) yield break;

            var sr = NewFx("FxBeam", from + AirLift, 1f, 520);
            var dir = to - from;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(tiles, 1.2f, 1f);

            float per = fxSeconds / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return new WaitForSeconds(per);
            }
            Destroy(sr.gameObject);
        }

        // ---------------- 유지형 연출 (얼음 장판 · 토템) ----------------

        sealed class Zone
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public bool Seen, Alive;
        }

        /// 프레임 시작. 이번 프레임에 KeepZone 으로 다시 불리지 않은 존은 마무리 연출 후 사라진다.
        public void BeginZones()
        {
            foreach (var zone in _zones.Values) zone.Seen = false;
        }

        public void KeepZone(int key, Vector3 world, FxKind start, FxKind loop, FxKind end, float scale = 1f)
        {
            if (!_zones.TryGetValue(key, out var zone))
            {
                zone = new Zone { Alive = true };
                zone.Sr = NewFx("FxZone", world + GroundLift, scale, ZoneOrder);
                zone.Go = zone.Sr.gameObject;
                _zones[key] = zone;
                StartCoroutine(ZoneRoutine(zone, start, loop, end));
            }
            zone.Seen = true;
            if (zone.Go != null) zone.Go.transform.position = world + GroundLift;
        }

        public void EndZones()
        {
            _zoneDrop.Clear();
            foreach (var pair in _zones)
            {
                if (pair.Value.Seen) continue;
                pair.Value.Alive = false;   // 코루틴이 마무리 연출을 재생하고 스스로 정리한다
                _zoneDrop.Add(pair.Key);
            }
            for (int i = 0; i < _zoneDrop.Count; i++) _zones.Remove(_zoneDrop[i]);
        }

        IEnumerator ZoneRoutine(Zone zone, FxKind start, FxKind loop, FxKind end)
        {
            yield return ZoneFrames(zone, Frames(start), fxSeconds, false);

            // 루프 시트가 없으면 시작 연출의 마지막 프레임을 그대로 붙잡고 기다린다
            var loopFrames = Frames(loop);
            if (loopFrames.Length == 0)
                while (zone.Alive) yield return null;

            while (zone.Alive && loopFrames.Length > 0)
                yield return ZoneFrames(zone, loopFrames, loopFrames.Length / zoneLoopFps, true);

            yield return ZoneFrames(zone, Frames(end), fxSeconds, false);
            if (zone.Go != null) Destroy(zone.Go);
        }

        IEnumerator ZoneFrames(Zone zone, Sprite[] frames, float seconds, bool breakWhenDead)
        {
            if (frames.Length == 0) yield break;
            float per = seconds / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                if (zone.Sr == null) yield break;
                if (breakWhenDead && !zone.Alive) yield break;
                zone.Sr.sprite = frames[i];
                yield return new WaitForSeconds(per);
            }
        }

        // ---------------- 장판 파티클 ----------------

        [Header("장판 파티클")]
        public float puffSeconds = 0.9f;
        public float puffRise = 0.85f;
        public float puffPixels = 1f;      // 알갱이 한 변 = 이 값 × 맵 도트 (1 = 도트 한 칸)
        public float puffGlow = 2.5f;      // 장판 색보다 이만큼 더 밝게 → 블룸이 확실히 걸린다

        Sprite _puffSprite;

        /// 도트 느낌을 살리려고 네모 픽셀 한 칸으로 만든다. 파티클 시스템은 빌드에서 안 보이는
        /// 일이 있어 스프라이트로 직접 띄운다. HDR 색을 주면 URP Bloom 이 알아서 번지게 한다.
        Sprite PuffSprite()
        {
            if (_puffSprite != null) return _puffSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return _puffSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        /// 장판 한 칸에서 알갱이 하나가 천천히 떠오르다 사라진다.
        public void Puff(Vector3 world, Color hdrColor)
        {
            var at = world + ScreenRight * Random.Range(-0.4f, 0.4f)
                           + ScreenUp * Random.Range(0f, 0.2f) + new Vector3(0f, 0f, -1.2f);
            // 맵 도트 한 칸이 대략 1/16 유닛이다. 그 배수로만 키워 픽셀처럼 보이게 한다.
            var sr = NewFx("FxPuff", at, puffPixels * Random.Range(1, 3) / 16f, PuffOrder);
            sr.sprite = PuffSprite();
            // 네모 한 픽셀에 블룸이 확실히 걸리도록 장판 색보다 밝게 태운다
            var glow = new Color(hdrColor.r * puffGlow, hdrColor.g * puffGlow, hdrColor.b * puffGlow, 1f);
            SetFxColor(sr, glow);
            StartCoroutine(PuffRoutine(sr, glow, ScreenUp));
        }

        IEnumerator PuffRoutine(SpriteRenderer sr, Color color, Vector3 up)
        {
            var from = sr.transform.position;
            float t = 0f;
            while (t < puffSeconds)
            {
                t += Time.deltaTime;
                float k = t / puffSeconds;
                sr.transform.position = from + up * (puffRise * k);
                SetFxColor(sr, new Color(color.r, color.g, color.b, color.a * (1f - k)));
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        // ---------------- 질주 잔상 ----------------

        /// 달리는 동안 일정 간격으로 스냅샷을 남기고 지운다.
        public void Dash(Transform character, float seconds)
        {
            if (character != null) StartCoroutine(DashRoutine(character, seconds));
        }

        IEnumerator DashRoutine(Transform character, float seconds)
        {
            for (float t = 0f; t < seconds; t += ghostInterval)
            {
                SpawnGhost(character);
                yield return new WaitForSeconds(ghostInterval);
            }
        }

        void SpawnGhost(Transform character)
        {
            var root = new GameObject("FxGhost");
            var copies = new List<SpriteRenderer>();
            foreach (var src in Parts(character))
            {
                if (src == null || src.sprite == null || !src.enabled) continue;
                var go = new GameObject("part");
                go.transform.SetParent(root.transform, true);
                go.transform.position = src.transform.position;
                go.transform.rotation = src.transform.rotation;
                go.transform.localScale = src.transform.lossyScale;

                var dst = go.AddComponent<SpriteRenderer>();
                dst.sprite = src.sprite;
                dst.flipX = src.flipX;
                dst.flipY = src.flipY;
                dst.sortingLayerID = src.sortingLayerID;
                dst.sortingOrder = src.sortingOrder - 1;
                SetFxColor(dst, ghostTint);
                copies.Add(dst);
            }
            StartCoroutine(FadeGhost(root, copies));
        }

        IEnumerator FadeGhost(GameObject root, List<SpriteRenderer> parts)
        {
            float t = 0f;
            while (t < ghostFadeSeconds)
            {
                t += Time.deltaTime;
                float a = ghostTint.a * (1f - t / ghostFadeSeconds);
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i] != null) SetFxColor(parts[i], new Color(ghostTint.r, ghostTint.g, ghostTint.b, a));
                yield return null;
            }
            Destroy(root);
        }

        // ---------------- 피격 연출 ----------------

        /// SPUM 유닛은 부위별 SpriteRenderer 가 많다. 한 번 훑어 두고 계속 쓴다.
        SpriteRenderer[] Parts(Transform character)
        {
            if (_parts.TryGetValue(character, out var cached)) return cached;
            var list = character.GetComponentsInChildren<SpriteRenderer>(true);
            if (_flashMat != null)
                foreach (var sr in list) sr.sharedMaterial = _flashMat;
            return _parts[character] = list;
        }

        void SetFlash(Transform character, float amount)
        {
            foreach (var sr in Parts(character))
            {
                if (sr == null) continue;
                sr.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Flash", amount);
                sr.SetPropertyBlock(_mpb);
            }
        }

        /// 캐릭터의 모든 스프라이트를 절대적인 흰색으로 덮었다가 되돌린다.
        public void FlashWhite(Transform character)
        {
            if (character == null || _flashMat == null) return;
            StartCoroutine(FlashRoutine(character));
        }

        IEnumerator FlashRoutine(Transform character)
        {
            float t = 0f;
            while (t < flashSeconds)
            {
                t += Time.deltaTime;
                // 앞쪽 30% 는 완전한 흰색으로 붙잡고 나머지에서 되돌린다
                float k = t / flashSeconds;
                float amount = k < 0.3f ? 1f : 1f - (k - 0.3f) / 0.7f;
                SetFlash(character, Mathf.Clamp01(amount));
                yield return null;
            }
            SetFlash(character, 0f);
        }

        /// 내가 맞았을 때만: 카메라를 흔들고 화면을 붉게 번쩍인다.
        public void HurtFeedback(Camera cam, Transform canvas)
        {
            if (cam != null) StartCoroutine(ShakeRoutine(cam));
            var flash = ScreenFlash(canvas);
            if (flash != null) StartCoroutine(ScreenFlashRoutine(flash));
        }

        IEnumerator ShakeRoutine(Camera cam)
        {
            if (_cam != cam) { _cam = cam; _camHome = cam.transform.localPosition; }
            float t = 0f;
            while (t < shakeSeconds)
            {
                t += Time.deltaTime;
                float decay = 1f - t / shakeSeconds;
                cam.transform.localPosition = _camHome + (Vector3)(Random.insideUnitCircle * shakeStrength * decay);
                yield return null;
            }
            cam.transform.localPosition = _camHome;
        }

        /// 캔버스를 덮는 붉은 판. 처음 필요할 때 만들고 계속 재사용한다.
        Image ScreenFlash(Transform canvas)
        {
            if (_screenFlash != null || canvas == null) return _screenFlash;

            var go = new GameObject("HurtFlash", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            _screenFlash = go.AddComponent<Image>();
            _screenFlash.raycastTarget = false;
            _screenFlash.color = Color.clear;
            return _screenFlash;
        }

        IEnumerator ScreenFlashRoutine(Image img)
        {
            float t = 0f;
            while (t < screenFlashSeconds)
            {
                t += Time.deltaTime;
                var c = screenFlashColor;
                c.a *= 1f - t / screenFlashSeconds;
                img.color = c;
                yield return null;
            }
            img.color = Color.clear;
        }
    }
}
