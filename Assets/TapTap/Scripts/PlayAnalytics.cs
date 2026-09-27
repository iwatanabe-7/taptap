using System;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
#if ENABLE_UNITY_CONSENT
using UnityEngine.UnityConsent;
#endif

namespace TapTap
{
    /// <summary>
    /// Unity Analytics (Unity Gaming Services) への送信をまとめる。
    ///
    /// ユニークユーザー・新規プレイヤー・セッション数・セッション時間は SDK が自動で集計する。
    /// ここではゲーム固有のカスタムイベントを送る (ダッシュボードの Event Manager に同名・同型で登録が必要):
    ///   roundStarted : playCount (int)
    ///   roundEnded   : result (string: clear / gameover / quit), score (int), maxCombo (int),
    ///                  playSeconds (float), livesLeft (int), playCount (int)
    ///
    /// プロジェクトが Unity Cloud にリンクされていない場合やオフラインでも、ゲーム自体は普通に動く。
    /// </summary>
    public class PlayAnalytics : MonoBehaviour
    {
        const string ConsentKey = "taptap_analytics_consent";
        const string PlayCountKey = "taptap_play_count";

        public static class Result
        {
            public const string Clear = "clear";       // 時間切れまで生き残った
            public const string GameOver = "gameover"; // ライフがなくなった
            public const string Quit = "quit";         // 一時停止から「最初からやり直す」
        }

        bool ready;
        bool collecting;

        /// <summary>プレイ統計を送るかどうか (既定はオン、スタート画面で切り替え可能)。</summary>
        public bool Consent
        {
            get => PlayerPrefs.GetInt(ConsentKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyConsent();
            }
        }

        async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                ready = true;
                ApplyConsent();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TapTap] Unity Analytics を初期化できませんでした (統計は送信されません): " + e.Message);
            }
        }

        void ApplyConsent()
        {
            if (!ready) return;
            bool consent = Consent;
#if ENABLE_UNITY_CONSENT
            // Unity 6.3 以降の同意 API。Analytics SDK はこの状態に合わせて収集を開始/停止する
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = consent ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);
#else
            if (consent && !collecting) AnalyticsService.Instance.StartDataCollection();
            else if (!consent && collecting) AnalyticsService.Instance.StopDataCollection();
#endif
            collecting = consent;
        }

        public void RoundStarted()
        {
            int count = PlayerPrefs.GetInt(PlayCountKey, 0) + 1;
            PlayerPrefs.SetInt(PlayCountKey, count);
            Record(new CustomEvent("roundStarted") { { "playCount", count } });
        }

        public void RoundEnded(string result, int score, int maxCombo, float playSeconds, int livesLeft)
        {
            Record(new CustomEvent("roundEnded")
            {
                { "result", result },
                { "score", score },
                { "maxCombo", maxCombo },
                { "playSeconds", (float)Math.Round(playSeconds, 1) },
                { "livesLeft", livesLeft },
                { "playCount", PlayerPrefs.GetInt(PlayCountKey, 0) },
            });
            // ブラウザを閉じられても取りこぼしにくいよう、ラウンド終了ごとに送信する
            if (collecting) AnalyticsService.Instance.Flush();
        }

        void Record(CustomEvent e)
        {
            if (!collecting) return;
            try
            {
                AnalyticsService.Instance.RecordEvent(e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TapTap] Analytics イベントを記録できませんでした: " + ex.Message);
            }
        }
    }
}
