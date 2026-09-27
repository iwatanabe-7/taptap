using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapTap
{
    /// <summary>
    /// UI 上に描く軽量パーティクル (火花・リング・紙吹雪)。Image をプールして使い回す。
    /// 速度は HTML 版の px/ms を 1000 倍して units/秒 に換算している。
    /// </summary>
    public class UiFx
    {
        enum Kind { Spark, Ring, Confetti, Twinkle }

        class P
        {
            public Image img;
            public Kind kind;
            public Vector2 pos, vel;
            public float life, max, size, g, rot, vr, radius;
            public Color color;
            public bool alive;
        }

        const int MaxParticles = 700;

        readonly RectTransform layer;
        readonly List<P> pool = new List<P>();
        public bool Strong = true;

        public UiFx(RectTransform layer) { this.layer = layer; }

        P Get()
        {
            foreach (var p in pool)
                if (!p.alive) return Activate(p);
            if (pool.Count >= MaxParticles) return null;
            var img = Gfx.Img("P", layer, null, Color.white);
            img.rectTransform.Place(Vector2.zero, Vector2.one);
            var np = new P { img = img };
            pool.Add(np);
            return Activate(np);
        }

        static P Activate(P p)
        {
            p.alive = true;
            p.life = 0;
            p.img.gameObject.SetActive(true);
            return p;
        }

        public void Burst(Vector2 at, Color[] colors, int count, float speed, float life = 0.6f, float size = 4f,
            float gravity = 600f)
        {
            if (!Strong) count = Mathf.CeilToInt(count * 0.35f);
            for (int i = 0; i < count; i++)
            {
                var p = Get();
                if (p == null) return;
                float a = Random.value * Mathf.PI * 2f, s = speed * 1000f * (0.4f + Random.value * 0.9f);
                p.kind = Kind.Spark;
                p.pos = at;
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
                p.max = life * (0.6f + Random.value * 0.6f);
                p.size = size * (0.6f + Random.value * 0.8f);
                p.color = colors[Random.Range(0, colors.Length)];
                p.g = gravity;
                p.img.sprite = Gfx.SoftDot;
                p.img.rectTransform.localRotation = Quaternion.identity;
            }
        }

        public void RingAt(Vector2 at, Color color, float radius)
        {
            var p = Get();
            if (p == null) return;
            p.kind = Kind.Ring;
            p.pos = at;
            p.max = 0.42f;
            p.radius = radius;
            p.color = color;
            p.img.sprite = Gfx.Ring;
            p.img.rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>その場でふわっと大きくなって消える星形のきらめき。</summary>
        public void Twinkle(Vector2 at, Color color, float size)
        {
            var p = Get();
            if (p == null) return;
            p.kind = Kind.Twinkle;
            p.pos = at;
            p.vel = new Vector2(0f, 18f);
            p.max = 0.45f + Random.value * 0.2f;
            p.size = size;
            p.color = color;
            p.rot = Random.value * 90f;
            p.vr = (Random.value < 0.5f ? -1f : 1f) * (90f + Random.value * 90f);
            p.img.sprite = Gfx.Star;
        }

        public void ConfettiRain()
        {
            var colors = new[]
            {
                Gfx.Hex("#ff4fd8"), Gfx.Hex("#4fc3ff"), Gfx.Hex("#ffd166"),
                Gfx.Hex("#7dff9a"), Gfx.Hex("#b36bff"), Color.white,
            };
            var r = layer.rect;
            int n = Strong ? 160 : 50;
            for (int i = 0; i < n; i++)
            {
                var p = Get();
                if (p == null) return;
                p.kind = Kind.Confetti;
                p.pos = new Vector2(Random.Range(r.xMin, r.xMax), r.yMax + 20f + Random.value * r.height * 0.6f);
                p.vel = new Vector2((Random.value - 0.5f) * 80f, -(120f + Random.value * 180f));
                p.max = 4.2f;
                p.size = 6f + Random.value * 6f;
                p.color = colors[Random.Range(0, colors.Length)];
                p.g = 50f;
                p.rot = Random.value * 6f;
                p.vr = (Random.value - 0.5f) * 20f;
                p.img.sprite = null;
            }
        }

        public void Tick(float dt)
        {
            foreach (var p in pool)
            {
                if (!p.alive) continue;
                p.life += dt;
                float t = p.life / p.max;
                if (t >= 1f)
                {
                    p.alive = false;
                    p.img.gameObject.SetActive(false);
                    continue;
                }
                var rt = p.img.rectTransform;
                var c = p.color;
                switch (p.kind)
                {
                    case Kind.Ring:
                        float d = 2f * p.radius * (0.2f + 0.8f * Mathf.Sqrt(t));
                        rt.sizeDelta = new Vector2(d, d);
                        c.a = (1f - t) * 0.9f;
                        break;
                    case Kind.Spark:
                        p.vel.y -= p.g * dt;
                        p.pos += p.vel * dt;
                        float sz = p.size * (1f - t * 0.6f) * 2.6f * 2f;
                        rt.sizeDelta = new Vector2(sz, sz);
                        c.a = 1f - t;
                        break;
                    case Kind.Twinkle:
                        p.pos += p.vel * dt;
                        p.rot += p.vr * dt;
                        float ts = p.size * Mathf.Sin(Mathf.PI * t);
                        rt.sizeDelta = new Vector2(ts, ts);
                        rt.localRotation = Quaternion.Euler(0, 0, p.rot);
                        c.a = Mathf.Sin(Mathf.PI * t);
                        break;
                    case Kind.Confetti:
                        p.vel.y -= p.g * dt;
                        p.pos += p.vel * dt;
                        p.rot += p.vr * dt;
                        rt.sizeDelta = new Vector2(p.size, p.size * 0.5f);
                        rt.localRotation = Quaternion.Euler(0, 0, p.rot * Mathf.Rad2Deg);
                        rt.localScale = new Vector3(1f, Mathf.Abs(Mathf.Cos(p.rot * 2f)), 1f);
                        c.a = t > 0.8f ? (1f - t) * 5f : 1f;
                        break;
                }
                if (p.kind != Kind.Confetti) rt.localScale = Vector3.one;
                rt.anchoredPosition = p.pos;
                p.img.color = c;
            }
        }
    }
}
