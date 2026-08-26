using System;
using UnityEditor;
using UnityEngine;

namespace SpellThrower.EditorTools
{
    /// 프로젝트에 VFX 스프라이트가 하나도 없어서 픽셀아트 프레임을 직접 그려 만든다.
    /// 구매한 아트로 바꾸려면 같은 경로에 같은 이름(0.png ~ 5.png)으로 덮어쓰면 된다.
    public static class VfxSpriteGenerator
    {
        const int Px = 32, Frames = 6, Ppu = 24;
        const string Root = "Assets/Resources/VFX";

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// 프레임 내 픽셀 하나의 색. x,y 는 중심 기준 좌표, t 는 0~1 진행도.
        delegate Color32 Painter(float x, float y, float t);

        [MenuItem("SpellThrower/Generate VFX Sprites")]
        public static void Generate()
        {
            Write("HitFx", Hit);
            Write("IceFx", Ice);
            Write("BuffFx", (x, y, t) => Chevron(x, y, t, 1f, new Color32(107, 232, 138, 255)));
            Write("DebuffFx", (x, y, t) => Chevron(x, y, t, -1f, new Color32(180, 107, 232, 255)));
            AssetDatabase.Refresh();
            Debug.Log("[스펠 스로워] VFX 스프라이트 생성 완료: " + Root);
        }

        static void Write(string name, Painter paint)
        {
            var dir = Root + "/" + name;
            System.IO.Directory.CreateDirectory(dir);
            for (int f = 0; f < Frames; f++)
            {
                float t = f / (float)(Frames - 1);
                var tex = new Texture2D(Px, Px, TextureFormat.RGBA32, false);
                var pixels = new Color32[Px * Px];
                for (int py = 0; py < Px; py++)
                    for (int px = 0; px < Px; px++)
                        pixels[py * Px + px] = paint(px - Px * 0.5f + 0.5f, py - Px * 0.5f + 0.5f, t);
                tex.SetPixels32(pixels);
                tex.Apply();

                var path = string.Format("{0}/{1}.png", dir, f);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Configure(path);
            }
        }

        static void Configure(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = Ppu;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        // ---------------- 이펙트별 그리기 ----------------

        /// 피격: 톱니 모양으로 퍼지는 충격 링 + 초반 코어 섬광.
        static Color32 Hit(float x, float y, float t)
        {
            float dist = Mathf.Sqrt(x * x + y * y);
            float fade = 1f - t * t;

            if (t < 0.45f && dist < Mathf.Lerp(5f, 0f, t / 0.45f))
                return Fade(new Color32(255, 255, 255, 255), 1f);

            float angle = Mathf.Atan2(y, x);
            float wobble = 1f + 0.18f * Mathf.Sin(angle * 5f) + 0.10f * Mathf.Sin(angle * 9f + 1.3f);
            float radius = Mathf.Lerp(2f, 14f, t) * wobble;
            float thick = Mathf.Lerp(6f, 2f, t) * 0.5f;

            float off = Mathf.Abs(dist - radius);
            if (off > thick) return Clear;

            float k = off / thick;
            var c = k < 0.35f ? new Color32(255, 255, 255, 255)
                  : k < 0.70f ? new Color32(255, 220, 120, 255)
                              : new Color32(255, 120, 40, 255);
            return Fade(c, fade);
        }

        /// 빙결: 6방향으로 자라는 결정 쐐기 + 서리 링.
        static Color32 Ice(float x, float y, float t)
        {
            float dist = Mathf.Sqrt(x * x + y * y);
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            if (fade <= 0f) return Clear;

            float length = Mathf.Lerp(3f, 13f, t);

            // 축에서 벗어난 거리로 쐐기를 만든다. 끝으로 갈수록 뾰족해진다.
            float angle = Mathf.Atan2(y, x);
            float step = Mathf.PI * 2f / 6f;
            float lateral = Mathf.Abs(Mathf.Repeat(angle + step * 0.5f, step) - step * 0.5f) * dist;
            float halfWidth = Mathf.Max(0.6f, 2.6f * (1f - dist / 14f));

            if (dist <= length && lateral <= halfWidth)
            {
                var c = lateral < halfWidth * 0.4f ? new Color32(255, 255, 255, 255)
                      : lateral < halfWidth * 0.75f ? new Color32(200, 245, 255, 255)
                                                    : new Color32(90, 180, 235, 255);
                return Fade(c, fade);
            }

            // 후반부에 퍼지는 서리 링
            if (t > 0.35f && Mathf.Abs(dist - length * 0.8f) < 1.1f)
                return Fade(new Color32(140, 215, 245, 255), fade * 0.8f);

            return Clear;
        }

        /// 버프/디버프: 진행 방향으로 흐르는 셰브론 3개 + 바닥 링. dir 1=위(버프), -1=아래(디버프).
        static Color32 Chevron(float x, float y, float t, float dir, Color32 tint)
        {
            float best = 0f;
            for (int k = 0; k < 3; k++)
            {
                float phase = Mathf.Repeat(t + k / 3f, 1f);
                float cy = Mathf.Lerp(-12f, 12f, phase) * dir;
                float edge = cy - Mathf.Abs(x) * 0.5f * dir;   // 진행 방향 쪽이 뾰족한 V
                if (Mathf.Abs(x) > 9f || Mathf.Abs(y - edge) > 1.6f) continue;
                best = Mathf.Max(best, Mathf.Sin(phase * Mathf.PI));   // 가운데서 가장 진하다
            }

            float ringY = -11f * dir;
            float dist = Mathf.Sqrt(x * x + (y - ringY) * (y - ringY) * 4f);
            if (Mathf.Abs(dist - Mathf.Lerp(3f, 10f, t)) < 1.2f)
                best = Mathf.Max(best, 1f - t);

            if (best <= 0.02f) return Clear;
            var c = best > 0.75f ? new Color32(255, 255, 255, 255) : tint;
            return Fade(c, best);
        }

        static Color32 Fade(Color32 c, float a) =>
            new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(255f * Mathf.Clamp01(a)), 0, 255));
    }
}
