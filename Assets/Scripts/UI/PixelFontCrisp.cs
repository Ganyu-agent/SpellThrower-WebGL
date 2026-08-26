using UnityEngine;

namespace SpellThrower
{
    /// 픽셀 폰트가 뿌옇게 번지는 가장 큰 원인은 동적 폰트 아틀라스의 이중선형 보간이다.
    /// 아틀라스는 새 글자가 들어올 때마다 다시 구워지고 그때 필터가 기본값으로 돌아가므로,
    /// 다시 구워질 때마다 Point 로 되돌린다.
    public static class PixelFontCrisp
    {
        /// neodgm·Silver 둘 다 16px 로 그려진 비트맵형 폰트다. 이보다 작게 굽으면
        /// 획이 통째로 사라져 글자가 깨진다. 자동 축소(bestFit)의 하한값으로 쓴다.
        public const int NativeSize = 16;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            Font.textureRebuilt -= Sharpen;
            Font.textureRebuilt += Sharpen;
            foreach (var font in Resources.FindObjectsOfTypeAll<Font>()) Sharpen(font);
        }

        static void Sharpen(Font font)
        {
            if (font == null || font.material == null) return;
            var texture = font.material.mainTexture;
            if (texture != null) texture.filterMode = FilterMode.Point;
        }
    }
}
