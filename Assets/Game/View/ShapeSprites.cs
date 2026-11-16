using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>물질과 적을 그리는 모양 그림을 코드로 만들어 준다. 그림 파일 없이 색과 모양으로 구분하기 위한 것</summary>
    public static class ShapeSprites
    {
        const int Size = 64;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>모양 이름으로 그림을 얻는다. 모르는 이름이면 원을 돌려준다. 그림 한 변은 1 유닛이다</summary>
        public static Sprite Get(string shape)
        {
            if (shape == null) shape = "circle";
            if (cache.TryGetValue(shape, out Sprite cached) && cached != null) return cached;

            Func<float, float, bool> inside = InsideFunction(shape);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "shape_" + shape
            };
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    // 그림 좌표를 -1 에서 1 사이로 바꾼다
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    byte a = inside(x, y) ? (byte)255 : (byte)0;
                    pixels[py * Size + px] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = "shape_" + shape;
            cache[shape] = sprite;
            return sprite;
        }

        static Func<float, float, bool> InsideFunction(string shape)
        {
            switch (shape)
            {
                case "square": return (x, y) => Mathf.Abs(x) <= 0.9f && Mathf.Abs(y) <= 0.9f;
                case "diamond": return (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 0.97f;
                case "ring": return (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r <= 0.97f && r >= 0.8f; };
                case "thick_ring": return (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r <= 0.97f && r >= 0.55f; };
                case "triangle": return (x, y) => y >= -0.8f && y <= 0.9f && Mathf.Abs(x) <= 0.9f * (0.9f - y) / 1.7f;
                case "drop":
                    return (x, y) =>
                    {
                        float dy = y + 0.3f;
                        if (x * x + dy * dy <= 0.6f * 0.6f) return true;
                        return y > -0.3f && y <= 0.95f && Mathf.Abs(x) <= 0.6f * (0.95f - y) / 1.25f;
                    };
                case "cloud":
                    return (x, y) =>
                    {
                        if (Circle(x, y, -0.45f, -0.1f, 0.45f) || Circle(x, y, 0.45f, -0.1f, 0.45f) || Circle(x, y, 0f, 0.25f, 0.5f)) return true;
                        return y >= -0.55f && y <= -0.1f && Mathf.Abs(x) <= 0.45f;
                    };
                case "bolt":
                    return (x, y) =>
                    {
                        if (Mathf.Abs(y) > 0.95f) return false;
                        // 위쪽 토막과 아래쪽 토막이 어긋나 번개 모양이 된다
                        float cx = y >= 0f ? 0.35f - 0.5f * (0.95f - y) / 0.95f : 0.15f + 0.5f * y / 0.95f;
                        return Mathf.Abs(x - cx) <= 0.24f;
                    };
                default: return (x, y) => x * x + y * y <= 0.97f * 0.97f;
            }
        }

        static bool Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>#RRGGBB 글자를 색으로 바꾼다. 잘못된 글자면 흰색</summary>
        public static Color ParseColor(string hex)
        {
            return hex != null && ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
        }
    }
}
