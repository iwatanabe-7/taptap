using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapTap
{
    /// <summary>
    /// UI 上に描く軽量パーティクル (火花・リング・紙吹雪・破片・得点の光・泡)。Image をプールして使い回す。
    /// 速度は HTML 版の px/ms を 1000 倍して units/秒 に換算している。
    /// </summary>
    public class UiFx
    {
        enum Kind { Spark, Ring, Confetti, Twinkle, Shard, Orb, Bubble }

        class P
        {
            public Image img;
            public Kind kind;
            public Vector2 pos, vel;
            public float life, max, size, g, rot, vr, radius;
            public Color color;
            public bool alive;
            public Vector2 from, to, ctrl;      // Orb: 2 次ベジェで from → to へ飛ぶ
            public System.Action onArrive;     // Orb: 到着時に呼ぶ
        }

        const int MaxParticles = 900;

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

        /// <summary>ボタンが割れたように、色つきの破片を回転させながら飛び散らせる。</summary>
        public void Shatter(Vector2 at, Color color, float radius, int count)
        {
            if (!Strong) count = Mathf.CeilToInt(count * 0.5f);
            for (int i = 0; i < count; i++)
            {
                var p = Get();
                if (p == null) return;
                float a = (i + Random.value * 0.8f) / count * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.kind = Kind.Shard;
                p.pos = at + dir * radius * Random.Range(0.1f, 0.6f);
                p.vel = dir * Random.Range(260f, 620f) + new Vector2(0f, Random.Range(80f, 260f));
                p.max = Random.Range(0.55f, 0.85f);
                p.size = radius * Random.Range(0.45f, 0.85f);
                p.color = Color.Lerp(color, Color.white, Random.value * 0.35f);
                p.g = 1500f;
                p.rot = Random.value * 360f;
                p.vr = (Random.value < 0.5f ? -1f : 1f) * Random.Range(360f, 900f);
                p.img.sprite = Gfx.Shard;
            }
        }

        /// <summary>光の粒を from から to (スコア表示) へ弧を描いて飛ばし、着いたら onArrive を呼ぶ。</summary>
        public void Orb(Vector2 from, Vector2 to, Color color, float delay, System.Action onArrive)
        {
            var p = Get();
            if (p == null) { onArrive?.Invoke(); return; }
            p.kind = Kind.Orb;
            p.from = p.pos = from;
            p.to = to;
            var mid = (from + to) * 0.5f;
            var side = new Vector2(-(to.y - from.y), to.x - from.x).normalized;
            p.ctrl = mid + side * Random.Range(-140f, 140f) + new Vector2(0f, 60f);
            p.life = -delay;
            p.max = 0.48f;
            p.size = Random.Range(20f, 28f);
            p.color = color;
            p.onArrive = onArrive;
            p.img.sprite = Gfx.SoftDot;
            p.img.rectTransform.localRotation = Quaternion.identity;
            p.img.color = Color.clear;
        }

        /// <summary>下からゆらゆら浮かび上がる光の泡 (フィーバー中の演出)。</summary>
        public void Bubble(Vector2 at, Color color, float size)
        {
            var p = Get();
            if (p == null) return;
            p.kind = Kind.Bubble;
            p.pos = p.from = at;
            p.vel = new Vector2(0f, Random.Range(140f, 300f));
            p.max = Random.Range(2.2f, 3.4f);
            p.size = size;
            p.color = color;
            p.rot = Random.value * 6f; // ゆれの位相
            p.img.sprite = Gfx.Ring;
            p.img.rectTransform.localRotation = Quaternion.identity;
        }

        public void ConfettiRain(int count = 160)
        {
            var colors = new[]
            {
                Gfx.Hex("#ff4fd8"), Gfx.Hex("#4fc3ff"), Gfx.Hex("#ffd166"),
                Gfx.Hex("#7dff9a"), Gfx.Hex("#b36bff"), Color.white,
            };
            var r = layer.rect;
            int n = Strong ? count : Mathf.CeilToInt(count * 0.3f);
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
                if (p.life < 0f) continue; // Orb の発射待ち
                float t = p.life / p.max;
                if (t >= 1f)
                {
                    if (p.kind == Kind.Orb)
                    {
                        var arrive = p.onArrive;
                        p.onArrive = null;
                        arrive?.Invoke();
                    }
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
                    case Kind.Shard:
                        p.vel.y -= p.g * dt;
                        p.pos += p.vel * dt;
                        p.rot += p.vr * dt;
                        float ss = p.size * (1f - t * 0.5f);
                        rt.sizeDelta = new Vector2(ss * 0.7f, ss);
                        rt.localRotation = Quaternion.Euler(0, 0, p.rot);
                        c.a = t < 0.7f ? 1f : (1f - t) / 0.3f;
                        break;
                    case Kind.Orb:
                        float e = t * t * (3f - 2f * t); // 出だしと到着をなめらかに
                        float u1 = 1f - e;
                        p.pos = u1 * u1 * p.from + 2f * u1 * e * p.ctrl + e * e * p.to;
                        float os = p.size * (1f - 0.4f * t);
                        rt.sizeDelta = new Vector2(os, os);
                        c.a = Mathf.Min(1f, t * 6f);
                        break;
                    case Kind.Bubble:
                        p.pos.y += p.vel.y * dt;
                        p.pos.x = p.from.x + Mathf.Sin(p.life * 3f + p.rot) * 18f;
                        rt.sizeDelta = new Vector2(p.size, p.size);
                        c.a *= Mathf.Sin(Mathf.PI * t) * 0.8f;
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
                if (p.kind == Kind.Ring || p.kind == Kind.Orb || p.kind == Kind.Bubble) rt.localRotation = Quaternion.identity;
                rt.anchoredPosition = p.pos;
                p.img.color = c;
            }
        }
    }
}
