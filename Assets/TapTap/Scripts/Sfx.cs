using System.Collections.Generic;
using UnityEngine;

namespace TapTap
{
    /// <summary>
    /// 効果音をすべてコードで合成する (音声ファイル不要)。
    /// Web Audio 版の tone / chord / kick / noiseHit を AudioClip 生成で再現している。
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public enum Wave { Sine, Square, Triangle, Saw }

        const float Attack = 0.008f;
        const float Floor = 0.0001f;

        AudioSource source;
        int rate;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly List<(float at, System.Action play)> scheduled = new List<(float, System.Action)>();

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = scheduled.Count - 1; i >= 0; i--)
            {
                if (scheduled[i].at > now) continue;
                var play = scheduled[i].play;
                scheduled.RemoveAt(i);
                play();
            }
        }

        public void Tone(float freq, float dur, Wave wave = Wave.Sine, float vol = 0.2f, float slideTo = 0f)
        {
            string key = $"t{freq:F1}_{dur:F3}_{wave}_{vol:F3}_{slideTo:F1}";
            if (!cache.TryGetValue(key, out var clip))
            {
                clip = BuildTone(key, freq, dur, wave, vol, slideTo);
                cache[key] = clip;
            }
            source.PlayOneShot(clip);
        }

        public void Chord(float[] freqs, float dur, Wave wave, float vol)
        {
            for (int i = 0; i < freqs.Length; i++)
            {
                float f = freqs[i];
                scheduled.Add((Time.unscaledTime + i * 0.055f, () => Tone(f, dur, wave, vol)));
            }
        }

        public void Kick() => Tone(160, 0.16f, Wave.Sine, 0.35f, 42);

        public void Noise(float dur, float vol)
        {
            string key = $"n{dur:F3}_{vol:F3}";
            if (!cache.TryGetValue(key, out var clip))
            {
                int len = Mathf.CeilToInt(rate * dur);
                var data = new float[len];
                var rng = new System.Random(len);
                for (int i = 0; i < len; i++)
                    data[i] = ((float)rng.NextDouble() * 2f - 1f) * (1f - (float)i / len) * vol;
                clip = AudioClip.Create(key, len, 1, rate, false);
                clip.SetData(data, 0);
                cache[key] = clip;
            }
            source.PlayOneShot(clip);
        }

        AudioClip BuildTone(string key, float freq, float dur, Wave wave, float vol, float slideTo)
        {
            int len = Mathf.CeilToInt(rate * (dur + 0.02f));
            var data = new float[len];
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                if (t > dur) break;
                float f = slideTo > 0f ? freq * Mathf.Pow(slideTo / freq, t / dur) : freq;
                phase += f / rate;
                phase -= System.Math.Floor(phase);
                float p = (float)phase;

                float v;
                switch (wave)
                {
                    case Wave.Square: v = p < 0.5f ? 1f : -1f; break;
                    case Wave.Triangle: v = 1f - 4f * Mathf.Abs(p - 0.5f); break;
                    case Wave.Saw: v = 2f * p - 1f; break;
                    default: v = Mathf.Sin(p * Mathf.PI * 2f); break;
                }

                // 8ms で立ち上がり、dur で 0.0001 まで指数減衰 (exponentialRampToValueAtTime 相当)
                float env = t < Attack
                    ? Floor * Mathf.Pow(vol / Floor, t / Attack)
                    : vol * Mathf.Pow(Floor / vol, (t - Attack) / Mathf.Max(0.001f, dur - Attack));
                data[i] = v * env;
            }
            var clip = AudioClip.Create(key, len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
