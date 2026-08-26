---
name: unity-build-render-diff
description: 에디터에서는 멀쩡한데 빌드에서만 화면이 다를 때(블룸이 안 걸림, HDR 색이 잘림, 포스트프로세싱·라이팅 미적용, 스프라이트 색이 흰색으로 나옴) 원인을 순서대로 좁힌다. "빌드에서만 블룸이 안 나온다", "에디터랑 빌드 화면이 다르다", "빌드에서 색이 이상하다", "빌드에서만 이펙트가 안 보인다" 에 사용.
---

# 빌드에서만 렌더가 다를 때

## 0. 제일 중요한 규칙 — 추측으로 고치지 마라

이 문제는 **정적 설정만 보고는 절대 못 잡는다.** 설정이 전부 정상인데도 증상이 남는 게 이 버그의 본질이다.

빌드 한 사이클은 몇 분에서 십 분이다. 추측 → 빌드 → 실패를 반복하면 사용자 시간만 날린다.
**1번(런타임 진단)을 먼저 넣어라.** 정적 점검(2번)은 진단을 기다리는 동안 곁들이는 것이지, 그것만으로 결론 내면 안 된다.

실제 사례에서 스트리핑 설정을 두 번 고쳤지만 둘 다 원인이 아니었고, 런타임 진단을 넣은 뒤에야 한 번에 잡혔다.

## 1. 런타임 진단을 화면에 띄운다

로그 파일을 찾게 하지 마라. **화면에 직접 띄워서 스크린샷 한 장으로 끝내라.**

```csharp
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 게임 화면 진입 후 1회만 호출
void LogRenderDiagnosticsOnce()
{
    if (_renderLogged) return;
    _renderLogged = true;

    var cam   = /* 실제로 그리는 카메라 */;
    var data  = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
    var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
    var bloom = stack != null ? stack.GetComponent<Bloom>() : null;
    var rp    = GraphicsSettings.currentRenderPipeline;

    string diag = string.Format(
        "gfx={0} quality={1} rp={2}\ncamHDR={3} postFx={4}\nbloom={5} intensity={6} threshold={7}"
        + "\nbloomShader={8} uberShader={9}",
        SystemInfo.graphicsDeviceType,
        QualitySettings.names[QualitySettings.GetQualityLevel()],
        rp != null ? rp.name : "(null)",
        cam != null ? cam.allowHDR.ToString() : "(no cam)",
        data != null ? data.renderPostProcessing.ToString() : "(no data)",
        bloom != null ? bloom.active.ToString() : "(no bloom)",
        bloom != null ? bloom.intensity.value.ToString() : "-",
        bloom != null ? bloom.threshold.value.ToString() : "-",
        Shader.Find("Hidden/Universal Render Pipeline/Bloom") != null,
        Shader.Find("Hidden/Universal Render Pipeline/UberPost") != null);

    // 전용 라벨에 띄운다. 기존 UI 자리에 얹으면 손패·상단바에 가려 정작 필요한 줄이 안 보인다.
    Debug.Log("[렌더 진단] " + diag.Replace("\n", " "));
}
```

라벨은 **좌하단에 검은 배경 판을 깔고 `SetAsLastSibling()`** 으로 다른 UI 위에 그린다.
값별 해석:

| 값 | 뜻 |
|---|---|
| `camHDR=False` | 카메라가 LDR 버퍼. HDR 색이 잘려 블룸 임계값을 못 넘는다 |
| `postFx=False` | 포스트프로세싱 자체가 안 돈다 |
| `bloom=False` / `(no bloom)` | 볼륨 스택에 Bloom 이 안 올라옴 |
| `threshold` 가 씬 프로필 값과 다름 | 볼륨이 씬 프로필이 아니라 기본 프로필로 해석되는 중 |
| `bloomShader=False` | 셰이더가 빌드에서 빠짐 → 항상 포함 목록에 강제로 넣는다 |
| **전부 정상인데 증상이 남음** | **파이프라인은 결백하다. 3번으로 간다** |

## 2. 정적 점검 (진단 기다리는 동안)

한 항목이라도 어긋나면 그게 원인일 수 있지만, **전부 정상이어도 안심하면 안 된다.**

- 컬러 스페이스: `ProjectSettings.asset` → `m_ActiveColorSpace: 1` (Linear)
- 빌드 퀄리티 레벨: `QualitySettings.asset` → `m_PerPlatformDefaultQuality: Standalone` 값과
  각 레벨의 `excludedTargetPlatforms`. 에디터(`m_CurrentQuality`)와 다르면 다른 RP 에셋을 쓴다
- 레벨별 `customRenderPipeline` 이 가리키는 RP 에셋의 `m_SupportsHDR`
- Renderer 에셋의 `postProcessData` 가 비어 있지 않은지
- 카메라: `m_HDR: 1`, `m_RenderPostProcessing: 1`, `m_VolumeLayerMask` 에 볼륨 오브젝트 레이어 포함
- Volume: 오브젝트·컴포넌트 `m_IsActive`/`m_Enabled`, `weight`, `sharedProfile`
- 씬이 `EditorBuildSettings` 빌드 목록에 있는지
- 그래픽 API: `m_BuildTargetGraphicsAPIs` 에 해당 플랫폼 항목이 있으면 에디터와 다를 수 있다

## 3. 진짜 원인 1순위 — 스프라이트 HDR 틴트가 정점색에서 잘린다

**파이프라인이 전부 정상인데 블룸이 안 걸리면 거의 이거다.**

`SpriteRenderer.color` 에 1을 넘는 HDR 값을 넣어 블룸을 유도하는 코드는 빌드에서 깨진다.
`Sprites/Default` 가 쓰는 `UnitySprites.cginc` 는 틴트를 **정점 단계에서 `fixed4` 보간기(COLOR 시맨틱)** 에 실어 보낸다:

```hlsl
struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
OUT.color = IN.color * _Color * _RendererColor;   // 여기서 HDR 이 잘린다
```

증상: 금색(2.4, 1.9, 0.55)이 (1, 1, 0.55)로 잘려 **칙칙한 올리브/흰색**으로 보이고,
임계값 1을 못 넘으니 **블룸이 켜져 있어도 기여분이 0**이다. 에디터는 셰이더 변형 컴파일 경로가 달라 멀쩡해 보인다.

### 고치는 법 — 틴트를 프래그먼트에서 `float4` 로 곱한다

셰이더를 `Assets/Resources/Shaders/` 에 둔다. **Resources 에 있으면 빌드 포함이 보장된다.**

```hlsl
Shader "YourGame/HdrSpriteTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _TintHDR ("HDR Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off  ZWrite Off
        Blend One OneMinusSrcAlpha   // 프리멀티플라이

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            float4 _TintHDR;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            float4 frag(Varyings IN) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _TintHDR;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
```

색은 `MaterialPropertyBlock` 으로 넘긴다:

```csharp
static readonly int TintHdrId = Shader.PropertyToID("_TintHDR");

void SetFxColor(SpriteRenderer sr, Color c)
{
    sr.color = c;                       // 기존 코드가 현재 색을 다시 읽는다면 같이 채워 둔다
    if (_hdrMat == null || _mpb == null) return;
    sr.GetPropertyBlock(_mpb);
    _mpb.SetColor(TintHdrId, c);
    sr.SetPropertyBlock(_mpb);
}
```

### 함정 두 가지

1. **`_TintHDR` 기본값이 흰색이다.** 스프라이트를 만들자마자 MPB 를 안 채우면
   **불투명한 흰 판**이 화면에 뜬다. 생성 직후 반드시 원하는 색(보통 `Color.clear`)으로 채워라.
2. **`sr.color` 를 쓰는 곳을 전부 찾아 바꿔라.** 이 셰이더는 `sr.color` 를 읽지 않는다.
   한 군데라도 빠뜨리면 그 스프라이트만 흰색으로 남는다.
   `grep -n "\.color = " <파일>` 로 전수 확인할 것.

## 4. 절대 하지 말 것 — 빌드 시간 폭탄

URP 글로벌 설정에서 **`m_StripUnusedVariants` 를 끄지 마라.**
이게 변형 스트리핑의 주 스위치라, 끄면 셰이더 변형을 전부 컴파일해서 **빌드가 수십 분**이 된다.

블룸 확인이 목적이면 좁은 쪽인 `m_StripUnusedPostProcessingVariants` 만 끈다.
빌드 시간에 영향을 주는 설정은 **건드리기 전에 사용자에게 먼저 말해라.**

## 5. 참고 — 원인이 아니었던 것들

같은 증상에서 의심했지만 아니었던 항목. 여기에 시간 쓰지 마라.

- 셰이더 변형 스트리핑 (`bloomShader=True` 로 확인됨)
- 퀄리티 레벨 / RP 에셋 불일치
- 컬러 스페이스, 그래픽 API
- 라이팅·2D 라이트 — 스프라이트가 언릿 셰이더면 애초에 무관하다.
  렌더러가 `UniversalRendererData`(3D)면 씬에 `Light2D` 가 있어도 동작하지 않으며,
  이건 에디터·빌드가 똑같으므로 둘의 차이를 설명하지 못한다
