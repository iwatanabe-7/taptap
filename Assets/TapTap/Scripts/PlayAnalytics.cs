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
    /// 統計は常に裏で送信する (スタート画面の切り替えは設けない)。
    /// プロジェクトが Unity Cloud にリンクされていない場合やオフラインでも、ゲーム自体は普通に動く。
    /// </summary>
    public class PlayAnalytics : MonoBehaviour
    {
        const string PlayCountKey = "taptap_play_count";

        public static class Result
        {
            public const string Clear = "clear";       // 時間切れまで生き残った
            public const string GameOver = "gameover"; // ライフがなくなった
            public const string Quit = "quit";         // 一時停止から「最初からやり直す」
        }

        bool collecting;

        async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                StartCollection();
                Debug.Log("[TapTap] Unity Analytics を初期化しました");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TapTap] Unity Analytics を初期化できませんでした (統計は送信されません): " + e.Message);
            }
        }

        void StartCollection()
        {
#if ENABLE_UNITY_CONSENT
            // Unity 6.3 以降の同意 API。Analytics SDK はこの状態を見て収集を開始する
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = ConsentStatus.Granted;
            EndUserConsent.SetConsentState(state);
#else
            AnalyticsService.Instance.StartDataCollection();
#endif
            collecting = true;
        }

        public void RoundStarted()
        {
            int count = PlayerPrefs.GetInt(PlayCountKey, 0) + 1;
            PlayerPrefs.SetInt(PlayCountKey, count);
            Record("roundStarted", new CustomEvent("roundStarted") { { "playCount", count } });
        }

        public void RoundEnded(string result, int score, int maxCombo, float playSeconds, int livesLeft)
        {
            Record("roundEnded", new CustomEvent("roundEnded")
            {
                { "result", result },
                { "score", score },
                { "maxCombo", maxCombo },
                { "playSeconds", Math.Round((double)playSeconds, 1) }, // float のままだと 5.80000019 のような誤差が出る
                { "livesLeft", livesLeft },
                { "playCount", PlayerPrefs.GetInt(PlayCountKey, 0) },
            });
            // ブラウザを閉じられても取りこぼしにくいよう、ラウンド終了ごとに送信する
            if (collecting) AnalyticsService.Instance.Flush();
        }

        void Record(string eventName, CustomEvent e)
        {
            if (!collecting) return;
            try
            {
                AnalyticsService.Instance.RecordEvent(e);
#if UNITY_EDITOR
                Debug.Log("[TapTap] Analytics イベントを記録: " + eventName);
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TapTap] Analytics イベントを記録できませんでした: " + ex.Message);
            }
        }
    }
}
