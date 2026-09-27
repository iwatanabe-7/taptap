using UnityEngine;

namespace TapTap
{
    /// <summary>
    /// プレイ中の BGM をコードで合成してループ再生する (音声ファイル不要)。
    /// 通常レイヤーとフィーバーレイヤーを同じ長さで作り、同時に鳴らして音量だけ切り替える。
    /// </summary>
    public class Bgm : MonoBehaviour
    {
        const float Bpm = 128f;
        const int Bars = 8;
        const float BaseVolume = 0.32f;
        const float FadeSpeed = 3f;

        // Am - F - C - G を 2 周 (コードトーンの MIDI 番号)
        static readonly int[][] Chords =
        {
            new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 },
        };
        static readonly int[] BassRoots = { 45, 41, 48, 43 };

        AudioSource baseSrc, feverSrc;
        float baseTarget, feverTarget;
        int rate;
        System.Random rng;

        public bool Fever { set => feverTarget = value ? BaseVolume : 0f; }

        void Awake()
        {
            rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            baseSrc = MakeSource();
            feverSrc = MakeSource();
        }

        AudioSource MakeSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            return src;
        }

        public void Play()
        {
            if (baseSrc.clip == null) Build();
            baseSrc.Stop();
            feverSrc.Stop();
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL は PlayScheduled に対応していないので同じフレームで鳴らす
            baseSrc.Play();
            feverSrc.Play();
#else
            // 2 つのレイヤーを同じ DSP 時刻から鳴らしてずれないようにする
            double at = AudioSettings.dspTime + 0.05;
            baseSrc.PlayScheduled(at);
            feverSrc.PlayScheduled(at);
#endif
            baseSrc.volume = baseTarget = BaseVolume;
            feverSrc.volume = feverTarget = 0f;
        }

        public void Stop()
        {
            baseTarget = 0f;
            feverTarget = 0f;
        }

        public void Pause()
        {
            baseSrc.Pause();
            feverSrc.Pause();
        }

        public void Resume()
        {
            baseSrc.UnPause();
            feverSrc.UnPause();
        }

        void Update()
        {
            float step = FadeSpeed * Time.unscaledDeltaTime * BaseVolume;
            baseSrc.volume = Mathf.MoveTowards(baseSrc.volume, baseTarget, step);
            feverSrc.volume = Mathf.MoveTowards(feverSrc.volume, feverTarget, step);
            if (baseTarget <= 0f && baseSrc.volume <= 0f && baseSrc.isPlaying)
            {
                baseSrc.Stop();
                feverSrc.Stop();
            }
        }

        // =====================================================================
        // synthesis
        // =====================================================================

        enum Wave { Sine, Square, Triangle, Saw }

        void Build()
        {
            rng = new System.Random(7);
            float beat = 60f / Bpm, eighth = beat / 2f, sixteenth = beat / 4f;
            int len = Mathf.RoundToInt(Bars * 4 * beat * rate);
            var main = new float[len];
            var fever = new float[len];

            for (int bar = 0; bar < Bars; bar++)
            {
                int[] chord = Chords[bar % 4];
                int root = BassRoots[bar % 4];
                float barStart = bar * 4 * beat;

                for (int b = 0; b < 4; b++)
                {
                    float t = barStart + b * beat;
                    Kick(main, t, 0.9f);
                    if (b % 2 == 1) Clap(main, t, 0.35f);
                    Hat(main, t + eighth, 0.035f, 0.14f);

                    // フィーバー: 裏拍のオープンハイハットと追加キック
                    Hat(fever, t + eighth, 0.12f, 0.16f);
                    if (b == 3) Kick(fever, t + eighth, 0.6f);
                }

                // ベース: 8 分音符でルート、4 拍目の裏だけオクターブ上
                for (int i = 0; i < 8; i++)
                {
                    int note = root + (i == 7 ? 12 : 0);
                    Tone(main, barStart + i * eighth, eighth * 0.9f, Freq(note), Wave.Triangle, 0.45f, 0.18f);
                    Tone(main, barStart + i * eighth, eighth * 0.9f, Freq(note), Wave.Square, 0.08f, 0.08f);
                }

                // アルペジオ: 16 分音符でコードトーンを上下
                int[] pattern = { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0, 1, 2, 3 };
                for (int i = 0; i < 16; i++)
                {
                    int k = pattern[i];
                    int note = k == 3 ? chord[0] + 12 : chord[k];
                    float t = barStart + i * sixteenth;
                    Tone(main, t, sixteenth * 0.8f, Freq(note + 12), Wave.Square, 0.07f, 0.07f);

                    // フィーバー: 1 オクターブ上のきらきらアルペジオと 16 分ハイハット
                    Tone(fever, t, sixteenth * 0.8f, Freq(note + 24), Wave.Triangle, 0.12f, 0.06f);
                    Hat(fever, t, 0.02f, 0.06f);
                }

                // パッド: 小節頭にコードを長めに鳴らす
                foreach (int n in chord)
                    Tone(main, barStart, 4 * beat * 0.95f, Freq(n), Wave.Saw, 0.025f, 2.5f);
            }

            baseSrc.clip = ToClip("bgm-main", main);
            feverSrc.clip = ToClip("bgm-fever", fever);
        }

        AudioClip ToClip(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i]); // 軽いソフトクリップ
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>ループの継ぎ目で音が切れないよう、はみ出した余韻は先頭に回り込ませる。</summary>
        void Tone(float[] buf, float start, float dur, float freq, Wave wave, float vol, float decay,
            float slideTo = 0f)
        {
            int s0 = Mathf.RoundToInt(start * rate);
            int n = Mathf.RoundToInt((dur + 0.01f) * rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = slideTo > 0f ? freq * Mathf.Pow(slideTo / freq, Mathf.Min(1f, t / dur)) : freq;
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
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / decay);
                if (t > dur) env *= Mathf.Max(0f, 1f - (t - dur) / 0.01f);
                buf[(s0 + i) % buf.Length] += v * env * vol;
            }
        }

        void Kick(float[] buf, float t, float vol) =>
            Tone(buf, t, 0.22f, 150f, Wave.Sine, vol, 0.12f, 42f);

        void Clap(float[] buf, float t, float vol)
        {
            Noise(buf, t, 0.16f, vol, 0.05f, 0.5f);
            Tone(buf, t, 0.08f, 220f, Wave.Triangle, vol * 0.3f, 0.04f);
        }

        void Hat(float[] buf, float t, float decay, float vol) => Noise(buf, t, decay * 3f, vol, decay, 1f);

        /// <summary>brightness=1 で高域だけ (差分ノイズ)、0.5 で中域寄りのノイズ。</summary>
        void Noise(float[] buf, float start, float dur, float vol, float decay, float brightness)
        {
            int s0 = Mathf.RoundToInt(start * rate);
            int n = Mathf.RoundToInt(dur * rate);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float w = (float)rng.NextDouble() * 2f - 1f;
                float v = brightness >= 1f ? w - prev : Mathf.Lerp(prev, w, brightness);
                prev = w;
                buf[(s0 + i) % buf.Length] += v * Mathf.Exp(-t / decay) * vol;
            }
        }
    }
}
