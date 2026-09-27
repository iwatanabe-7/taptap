using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TapTap
{
    /// <summary>
    /// 画像アセットを使わずに、スプライト・フォント・UI 部品をコードで生成するヘルパー。
    /// </summary>
    public static class Gfx
    {
        public static Sprite ShadedCircle, SoftDot, Ring, RoundRect, Vignette, Radial, Star;
        public static Font Font;
        static bool bundledFont;

        static readonly string[] JapaneseFonts =
        {
            "Hiragino Maru Gothic ProN", "Hiragino Sans", "Hiragino Kaku Gothic ProN",
            "Yu Gothic UI", "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Noto Sans JP",
        };

        public static void Init()
        {
            if (Font != null) return;

            ShadedCircle = MakeSprite(128, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01((1f - d) * 64f);
                // 左上にハイライトを置いた球体風の陰影
                float h = Vector2.Distance(new Vector2(u, v), new Vector2(0.35f, 0.72f));
                float b = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(h / 0.75f));
                return new Color(b, b, b, a);
            });
            SoftDot = MakeSprite(64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1, 1, 1, a * a * (0.4f + 0.6f * a));
            });
            Ring = MakeSprite(256, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.04f);
                return new Color(1, 1, 1, a);
            });
            Radial = MakeSprite(128, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1, 1, 1, Mathf.SmoothStep(0, 1, a));
            });
            Vignette = MakeSprite(128, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.3f, d)));
            });
            // 4 方向に光が伸びるきらめき (アストロイド形 + 中心のにじみ)
            Star = MakeSprite(64, (u, v) =>
            {
                float x = Mathf.Abs(u * 2f - 1f), y = Mathf.Abs(v * 2f - 1f);
                float s = Mathf.Sqrt(x) + Mathf.Sqrt(y);
                float spike = Mathf.Clamp01((1f - s) * 3f);
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.5f);
                return new Color(1, 1, 1, Mathf.Clamp01(spike + core * core));
            });
            const int rr = 64, radius = 24;
            RoundRect = MakeSprite(rr, (u, v) =>
            {
                float x = u * rr, y = v * rr;
                float cx = Mathf.Clamp(x, radius, rr - radius), cy = Mathf.Clamp(y, radius, rr - radius);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                return new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f));
            }, new Vector4(radius, radius, radius, radius));

            // WebGL では OS フォントが使えないので、同梱した M PLUS Rounded 1c (Bold) を優先して使う
            Font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Bold");
            bundledFont = Font != null;
            if (!bundledFont)
            {
                var installed = Font.GetOSInstalledFontNames();
                var available = JapaneseFonts.Where(n => installed.Contains(n)).ToArray();
                Font = available.Length > 0
                    ? Font.CreateDynamicFontFromOSFont(available, 32)
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        delegate Color PixelFn(float u, float v);

        static Sprite MakeSprite(int size, PixelFn shader, Vector4 border = default)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = shader((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                SpriteMeshType.FullRect, border);
        }

        public static Texture2D Gradient(bool horizontal, params Color[] stops)
        {
            const int n = 256;
            var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f) * (stops.Length - 1);
                int k = Mathf.Min(Mathf.FloorToInt(t), stops.Length - 2);
                var c = Color.Lerp(stops[k], stops[k + 1], t - k);
                if (horizontal) tex.SetPixel(i, 0, c); else tex.SetPixel(0, i, c);
            }
            tex.Apply();
            return tex;
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        // ---------- UI builders ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite == RoundRect) img.type = Image.Type.Sliced;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = true)
        {
            var t = Node("Text", parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            // 同梱フォントはもともと太字なので、疑似ボールドを重ねない
            t.fontStyle = bold && !bundledFont ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>角丸パネル (枠線つき)。中身を入れる inner を返す。</summary>
        public static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color fill, Color border,
            float borderWidth = 1.5f)
        {
            var outer = Img(name, parent, RoundRect, border);
            outer.rectTransform.Place(pos, size);
            var inner = Img("Fill", outer.transform, RoundRect, fill);
            inner.rectTransform.Stretch(borderWidth);
            return inner;
        }

        public static Button Button(Transform parent, string text, Vector2 pos, Vector2 size, Color color,
            int fontSize, System.Action onClick, out Text label)
        {
            var img = Img("Button", parent, RoundRect, color, raycast: true);
            img.rectTransform.Place(pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            label = Label(img.transform, text, fontSize, Color.white);
            label.rectTransform.Stretch();
            return btn;
        }
    }
}
