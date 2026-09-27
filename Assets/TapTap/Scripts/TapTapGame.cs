using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTap
{
    public enum CellKind { Idle, Blue, Red, Gold }

    /// <summary>
    /// タプタプ本体。空のシーンにこのコンポーネントを 1 つ置くだけで、
    /// カメラ・UI・効果音をすべて実行時に組み立てて遊べる状態にする。
    /// </summary>
    public class TapTapGame : MonoBehaviour
    {
        // ---------- ルール (HTML 版と同じ値) ----------
        const int GridSize = 16;
        const int FeverCombo = 10;
        const float FeverDuration = 7f;
        const float PerfectWindow = 0.28f;
        const float TimeLimit = 60f;
        const float GoldTimeBonus = 2f;
        const float FlashMinInterval = 0.34f; // 強い光の点滅は約 3 回/秒以下に制限
        const float RefW = 540f, RefH = 960f;

        const string BestKey = "taptap_best";
        const string FxKey = "taptap_fx_strong";

        enum State { Ready, Playing, Paused, GameOver }

        struct Difficulty
        {
            public float spawnInterval, activeDuration, redRatio, goldRatio;
            public int maxSimultaneous;
        }

        class Cell
        {
            public RectTransform root;
            public Image glow, body, shine;
            public CellKind kind;
            public float remaining, age;
            public float anim;          // 出現してからの経過 (演出用)
            public float hitT = 9f;     // タップ成功演出
            public float idleT = 9f;    // 空マスを押したときの沈み込み
            public float sparkleT;      // 金ボタンのきらめき発生タイマー
        }

        // ---------- palette ----------
        static readonly Color BgDeep = Gfx.Hex("#05060f");
        static readonly Color BgMid = Gfx.Hex("#0c1130");
        static readonly Color PanelBg = Gfx.Hex("#0e132d", 0.58f);
        static readonly Color PanelBorder = Gfx.Hex("#6b8cff", 0.35f);
        static readonly Color Blue = Gfx.Hex("#3b82f6");
        static readonly Color BlueGlow = Gfx.Hex("#8ec1ff");
        static readonly Color Red = Gfx.Hex("#ef4444");
        static readonly Color RedGlow = Gfx.Hex("#ff9a9a");
        static readonly Color Gold = Gfx.Hex("#ffd166");
        static readonly Color GoldDeep = Gfx.Hex("#f5a524");
        static readonly Color TextPrimary = Gfx.Hex("#f4f6ff");
        static readonly Color TextDim = Gfx.Hex("#9aa3c7");
        static readonly Color CellOff = Gfx.Hex("#2c355f");
        static readonly Color Pink = Gfx.Hex("#ff4fd8");
        static readonly Color Cyan = Gfx.Hex("#4fc3ff");

        // ---------- game state ----------
        State state = State.Ready;
        readonly List<Cell> cells = new List<Cell>();
        int score, best, lives = 3, combo, maxCombo, feverCharge, lastLevel, lastSec = 60, beatCount;
        float spawnTimer, hitStop, feverTime, beatTimer, timeLeft = TimeLimit;
        float playTime; // 1 ラウンドの実プレイ時間 (一時停止中は数えない)
        bool strongFx = true;

        // ---------- UI refs ----------
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform column, gridRT, fxLayer, slamRT, toastRT, timerRT, meterFillRT;
        Image feverTint, gridBorder, gridGlow, flashImg, vignetteImg, beatGlowImg;
        RawImage meterFill;
        Texture2D meterTex, meterFeverTex;
        Text scoreText, bestText, startBestText, timerText, timeBonusText, meterLabel, comboLabel;
        Text toastMain, toastSub, slamText, fxBtnText, finalScore, finalCombo, finalBest, gameoverTitle;
        Text[] lifeTexts;
        Text consentText;
        CanvasGroup toastGroup, slamGroup;
        GameObject startOverlay, pauseOverlay, gameoverOverlay, newBadge;
        readonly List<RectTransform> drifters = new List<RectTransform>();
        readonly List<float> drifterSpeed = new List<float>();

        UiFx fx;
        Sfx sfx;
        Bgm bgm;
        PlayAnalytics analytics;

        // ---------- animation state ----------
        float scoreBumpT = 9f, slamT = 9f, toastT = 9f, flashT = 9f, beatGlowT = 9f;
        float shakeT = 9f, shakeDur = 1f, glitchT = 9f, gridPunchT = 9f, timeBonusT = 9f, lastFlash = -9f;
        bool shakeBig;
        readonly List<(Text text, float t, float baseY)> pops = new List<(Text, float, float)>();
        readonly List<(float at, Action act)> later = new List<(float, Action)>();

        // =====================================================================
        // setup
        // =====================================================================

        void Awake()
        {
            Application.targetFrameRate = 60;
            Gfx.Init();
            EnsureSceneBasics();
            sfx = gameObject.AddComponent<Sfx>();
            bgm = gameObject.AddComponent<Bgm>();
            analytics = gameObject.AddComponent<PlayAnalytics>();
            best = PlayerPrefs.GetInt(BestKey, 0);
            strongFx = PlayerPrefs.GetInt(FxKey, 1) == 1;
            BuildUi();
            fx = new UiFx(fxLayer) { Strong = strongFx };
            ApplyFxMode();
            RenderBest();
            RenderMeter();
            RenderLives();
            RenderTimer();
            RenderConsent();
        }

        static void EnsureSceneBasics()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgDeep;
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            BuildBackdrop(root);

            column = Gfx.Node("App", root).Place(Vector2.zero, new Vector2(RefW, RefH));
            BuildHud(column);
            BuildGrid(column);

            vignetteImg = Gfx.Img("Vignette", root, Gfx.Vignette, new Color(0.94f, 0.27f, 0.27f, 0f));
            vignetteImg.rectTransform.Stretch(-80f);
            flashImg = Gfx.Img("Flash", root, Gfx.Radial, Color.clear);
            flashImg.rectTransform.Stretch(-200f);
            fxLayer = Gfx.Node("Fx", root).Stretch();

            slamText = Gfx.Label(root, "", 68, Color.white);
            slamRT = slamText.rectTransform;
            slamRT.anchorMin = slamRT.anchorMax = new Vector2(0.5f, 0.58f);
            slamRT.sizeDelta = new Vector2(600, 100);
            slamText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.35f);
            var s1 = slamText.gameObject.AddComponent<Shadow>();
            s1.effectColor = new Color(1f, 0.16f, 0.47f, 0.8f);
            s1.effectDistance = new Vector2(3, 0);
            var s2 = slamText.gameObject.AddComponent<Shadow>();
            s2.effectColor = new Color(0.16f, 0.78f, 1f, 0.8f);
            s2.effectDistance = new Vector2(-3, 0);
            slamGroup = slamText.gameObject.AddComponent<CanvasGroup>();
            slamGroup.alpha = 0;
            slamGroup.blocksRaycasts = false;

            BuildOverlays(root);
        }

        void BuildBackdrop(Transform root)
        {
            var bg = Gfx.Node("Backdrop", root).Stretch().gameObject.AddComponent<RawImage>();
            bg.texture = Gfx.Gradient(false, Gfx.Hex("#020208"), BgDeep, BgDeep, BgMid);
            bg.raycastTarget = false;

            var topGlow = Gfx.Img("TopGlow", bg.transform, Gfx.Radial, new Color(0.23f, 0.35f, 0.86f, 0.25f));
            var tg = topGlow.rectTransform;
            tg.anchorMin = tg.anchorMax = new Vector2(0.5f, 1f);
            tg.sizeDelta = new Vector2(1200, 700);

            // 下の方にビル群のシルエット
            var skyline = Gfx.Node("Skyline", bg.transform);
            skyline.anchorMin = Vector2.zero;
            skyline.anchorMax = new Vector2(1f, 0.38f);
            skyline.offsetMin = skyline.offsetMax = Vector2.zero;
            float[,] blocks = { { .06f, .10f, .9f }, { .18f, .24f, .6f }, { .33f, .40f, .95f }, { .52f, .58f, .7f },
                                { .68f, .76f, .85f }, { .84f, .92f, .55f } };
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                var b = Gfx.Img("Building", skyline, null, new Color(0.08f, 0.1f, 0.23f, 0.65f)).rectTransform;
                b.anchorMin = new Vector2(blocks[i, 0], 0);
                b.anchorMax = new Vector2(blocks[i, 1], blocks[i, 2]);
                b.offsetMin = b.offsetMax = Vector2.zero;
            }

            beatGlowImg = Gfx.Img("BeatGlow", bg.transform, Gfx.Radial, new Color(1f, 0.31f, 0.78f, 0f));
            beatGlowImg.rectTransform.anchorMin = beatGlowImg.rectTransform.anchorMax = new Vector2(0.5f, 0.45f);
            beatGlowImg.rectTransform.sizeDelta = new Vector2(900, 900);

            feverTint = Gfx.Img("FeverTint", bg.transform, null, Color.clear);
            feverTint.rectTransform.Stretch();

            string[] icons = { "♥", "★", "♪", "✦", "♥", "★", "♪", "✦" };
            float[] xs = { .08f, .22f, .38f, .55f, .68f, .80f, .14f, .90f };
            for (int i = 0; i < icons.Length; i++)
            {
                var t = Gfx.Label(bg.transform, icons[i], 22, new Color(0.55f, 0.65f, 1f, 0.5f));
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(xs[i], 0f);
                t.rectTransform.sizeDelta = new Vector2(40, 40);
                t.rectTransform.anchoredPosition = new Vector2(0, UnityEngine.Random.Range(0f, 1200f));
                drifters.Add(t.rectTransform);
                drifterSpeed.Add(1f / UnityEngine.Random.Range(14f, 22f));
            }
        }

        static float Top(float y) => RefH / 2f - y; // 上端からの距離 → 列内の y 座標

        void BuildHud(RectTransform col)
        {
            // top bar
            Gfx.Button(col, "II", new Vector2(-236, Top(42)), new Vector2(48, 44), PanelBorder, 18, TogglePause, out _);
            Gfx.Button(col, "演出 強", new Vector2(-150, Top(42)), new Vector2(104, 44), PanelBorder, 16,
                () =>
                {
                    strongFx = !strongFx;
                    PlayerPrefs.SetInt(FxKey, strongFx ? 1 : 0);
                    ApplyFxMode();
                }, out fxBtnText);

            var bestPanel = Gfx.Panel("Best", col, new Vector2(200, Top(44)), new Vector2(120, 54), PanelBg, PanelBorder);
            var bl = Gfx.Label(bestPanel.transform, "ベストスコア", 11, TextDim);
            bl.rectTransform.Place(new Vector2(0, 11), new Vector2(120, 16));
            bestText = Gfx.Label(bestPanel.transform, "0", 20, Gold);
            bestText.rectTransform.Place(new Vector2(0, -9), new Vector2(120, 24));

            // score block
            Gfx.Label(col, "SCORE", 14, TextDim).rectTransform.Place(new Vector2(0, Top(96)), new Vector2(200, 20));
            scoreText = Gfx.Label(col, "0", 60, TextPrimary);
            scoreText.rectTransform.Place(new Vector2(0, Top(140)), new Vector2(300, 70));

            lifeTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                lifeTexts[i] = Gfx.Label(col, "♥", 22, Gfx.Hex("#ff4d6d"));
                lifeTexts[i].rectTransform.Place(new Vector2((i - 1) * 26, Top(188)), new Vector2(26, 26));
            }

            var timer = Gfx.Panel("Timer", col, new Vector2(0, Top(222)), new Vector2(140, 34), PanelBg, PanelBorder);
            timerRT = (RectTransform)timer.transform.parent;
            timerText = Gfx.Label(timer.transform, "TIME 60.0", 18, TextPrimary);
            timerText.rectTransform.Stretch();
            timeBonusText = Gfx.Label(col, "+2秒", 15, Gfx.Hex("#7dff9a"), TextAnchor.MiddleLeft);
            timeBonusText.rectTransform.Place(new Vector2(110, Top(222)), new Vector2(60, 24));
            timeBonusText.color = Color.clear;

            // combo meter
            meterLabel = Gfx.Label(col, "フィーバーまで", 12, TextDim, TextAnchor.MiddleLeft);
            meterLabel.rectTransform.Place(new Vector2(0, Top(262)), new Vector2(360, 18));
            comboLabel = Gfx.Label(col, "0 コンボ", 12, Gold, TextAnchor.MiddleRight);
            comboLabel.rectTransform.Place(new Vector2(0, Top(262)), new Vector2(360, 18));
            var meterBg = Gfx.Img("Meter", col, null, new Color(1, 1, 1, 0.07f));
            meterBg.rectTransform.Place(new Vector2(0, Top(280)), new Vector2(360, 10));
            meterFill = Gfx.Node("Fill", meterBg.transform).gameObject.AddComponent<RawImage>();
            meterFill.raycastTarget = false;
            meterTex = Gfx.Gradient(true, Cyan, Gfx.Hex("#b36bff"), Pink);
            meterFeverTex = Gfx.Gradient(true, Pink, Gold, Cyan, Pink);
            meterFill.texture = meterTex;
            meterFillRT = meterFill.rectTransform;
            meterFillRT.anchorMin = Vector2.zero;
            meterFillRT.anchorMax = new Vector2(0, 1);
            meterFillRT.offsetMin = meterFillRT.offsetMax = Vector2.zero;

            // rules
            RuleRow(col, Top(314), Blue, "のボタンを押そう！（金はボーナス）", TextPrimary);
            RuleRow(col, Top(342), Red, "のボタンは押さないで！", Gfx.Hex("#ffb3b3"));

            // toast
            var toastFill = Gfx.Panel("Toast", col, new Vector2(0, Top(812)), new Vector2(200, 58), Gfx.Hex("#0c1230"),
                PanelBorder);
            toastRT = (RectTransform)toastFill.transform.parent;
            toastGroup = toastRT.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.blocksRaycasts = false;
            toastMain = Gfx.Label(toastFill.transform, "ナイス！", 17, TextPrimary);
            toastMain.rectTransform.Place(new Vector2(0, 10), new Vector2(200, 24));
            toastSub = Gfx.Label(toastFill.transform, "+1", 15, Gold);
            toastSub.rectTransform.Place(new Vector2(0, -12), new Vector2(200, 20));
        }

        static void RuleRow(RectTransform col, float y, Color dot, string text, Color textColor)
        {
            var d = Gfx.Img("Dot", col, Gfx.ShadedCircle, dot);
            d.rectTransform.Place(new Vector2(-150, y), new Vector2(20, 20));
            var t = Gfx.Label(col, text, 15, textColor, TextAnchor.MiddleLeft);
            t.rectTransform.Place(new Vector2(-136 + 150, y), new Vector2(300, 24));
        }

        void BuildGrid(RectTransform col)
        {
            const float cellSize = 72f, gap = 16f, pad = 18f;
            float inner = cellSize * 4 + gap * 3;
            float side = inner + pad * 2;
            var pos = new Vector2(0, Top(560));

            gridGlow = Gfx.Img("GridGlow", col, Gfx.Radial, Color.clear);
            gridGlow.rectTransform.Place(pos, new Vector2(side * 1.7f, side * 1.7f));
            var fill = Gfx.Panel("Grid", col, pos, new Vector2(side, side), PanelBg, PanelBorder, 2f);
            gridRT = (RectTransform)fill.transform.parent;
            gridBorder = gridRT.GetComponent<Image>();

            for (int i = 0; i < GridSize; i++)
            {
                int cx = i % 4, cy = i / 4;
                var cellPos = new Vector2(-inner / 2 + cellSize / 2 + cx * (cellSize + gap),
                    inner / 2 - cellSize / 2 - cy * (cellSize + gap));
                var root = Gfx.Node("Cell" + i, gridRT).Place(cellPos, new Vector2(cellSize, cellSize));
                var cell = new Cell
                {
                    root = root,
                    glow = Gfx.Img("Glow", root, Gfx.SoftDot, Color.clear),
                    body = Gfx.Img("Body", root, Gfx.ShadedCircle, CellOff, raycast: true),
                };
                cell.glow.rectTransform.Place(Vector2.zero, new Vector2(cellSize * 2f, cellSize * 2f));
                cell.body.rectTransform.Stretch();
                cell.shine = Gfx.Img("Shine", cell.body.transform, Gfx.SoftDot, Color.clear);
                cell.shine.rectTransform.Place(new Vector2(-cellSize * 0.14f, cellSize * 0.18f),
                    new Vector2(cellSize * 0.55f, cellSize * 0.55f));
                int index = i;
                cell.body.gameObject.AddComponent<PointerDown>().OnDown = () => OnTap(index);
                cells.Add(cell);
            }
        }

        void BuildOverlays(Transform root)
        {
            // start
            startOverlay = Overlay(root, "Start", 540, out var p);
            Gfx.Label(p, "タプタプ", 30, TextPrimary).rectTransform.Place(new Vector2(0, 225), new Vector2(300, 40));
            var tag = Gfx.Label(p, "青と金を押し続けてコンボ、赤を押すとリセット。\n制限時間は60秒、金ボタンで+2秒。\n" +
                                   "10コンボでフィーバー突入、スコア2倍。\n出た瞬間に押せば PERFECT！", 14, TextDim, bold: false);
            tag.lineSpacing = 1.35f;
            tag.rectTransform.Place(new Vector2(0, 135), new Vector2(300, 110));
            startBestText = Stat(p, new Vector2(0, 40), "ベスト");
            Gfx.Button(p, "スタート", new Vector2(0, -50), new Vector2(290, 52), Blue, 18, StartGame, out _);
            Gfx.Button(p, "", new Vector2(0, -112), new Vector2(290, 36), PanelBorder, 13, () =>
            {
                analytics.Consent = !analytics.Consent;
                RenderConsent();
            }, out consentText);
            var note = Gfx.Label(p, "匿名のプレイ統計（プレイ回数・スコア・プレイ時間など）を\n" +
                                    "ゲーム改善のために送信します。上のボタンでオフにできます。\n\n" +
                                    "強い光の点滅があります。気分が悪くなったら\n「演出 弱」に切り替えるか休憩してください。", 11,
                TextDim, bold: false);
            note.lineSpacing = 1.3f;
            note.rectTransform.Place(new Vector2(0, -200), new Vector2(300, 90));

            // pause
            pauseOverlay = Overlay(root, "Pause", 270, out p);
            Gfx.Label(p, "一時停止中", 26, TextPrimary).rectTransform.Place(new Vector2(0, 80), new Vector2(300, 40));
            Gfx.Button(p, "再開する", new Vector2(0, 5), new Vector2(290, 52), Blue, 18, TogglePause, out _);
            Gfx.Button(p, "最初からやり直す", new Vector2(0, -67), new Vector2(290, 52), PanelBorder, 18, ShowStartScreen,
                out _);
            pauseOverlay.SetActive(false);

            // game over
            gameoverOverlay = Overlay(root, "GameOver", 340, out p);
            var badge = Gfx.Img("NewBest", p, Gfx.RoundRect, Gold);
            badge.rectTransform.Place(new Vector2(0, 130), new Vector2(120, 28));
            Gfx.Label(badge.transform, "新記録！", 14, Gfx.Hex("#1a1200")).rectTransform.Stretch();
            newBadge = badge.gameObject;
            gameoverTitle = Gfx.Label(p, "ゲームオーバー", 26, TextPrimary);
            gameoverTitle.rectTransform.Place(new Vector2(0, 88), new Vector2(300, 40));
            finalScore = Stat(p, new Vector2(-96, 10), "スコア");
            finalCombo = Stat(p, new Vector2(0, 10), "最大コンボ");
            finalBest = Stat(p, new Vector2(96, 10), "ベスト");
            Gfx.Button(p, "スタート画面へ", new Vector2(0, -100), new Vector2(290, 52), Blue, 18, ShowStartScreen, out _);
            gameoverOverlay.SetActive(false);
        }

        static GameObject Overlay(Transform root, string name, float height, out Transform panel)
        {
            var dim = Gfx.Img(name, root, null, new Color(0.01f, 0.01f, 0.04f, 0.78f), raycast: true);
            dim.rectTransform.Stretch();
            var fill = Gfx.Panel("Panel", dim.transform, Vector2.zero, new Vector2(340, height), Gfx.Hex("#10153a"),
                PanelBorder);
            panel = fill.transform;
            return dim.gameObject;
        }

        static Text Stat(Transform parent, Vector2 pos, string label)
        {
            var box = Gfx.Img("Stat", parent, Gfx.RoundRect, new Color(1, 1, 1, 0.05f));
            box.rectTransform.Place(pos, new Vector2(88, 64));
            var n = Gfx.Label(box.transform, "0", 24, TextPrimary);
            n.rectTransform.Place(new Vector2(0, 8), new Vector2(88, 30));
            Gfx.Label(box.transform, label, 11, TextDim).rectTransform.Place(new Vector2(0, -18), new Vector2(88, 16));
            return n;
        }

        // =====================================================================
        // main loop
        // =====================================================================

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            // 縦長画面では幅、横長画面では高さに合わせて 540x960 の列が必ず収まるようにする
            scaler.matchWidthOrHeight = (float)Screen.width / Screen.height < RefW / RefH ? 0f : 1f;

            RunLater();
            fx.Tick(dt);
            AnimateUi(dt);
            AnimateCells(dt);

            if (state != State.Playing) return;
            playTime += dt;
            if (hitStop > 0f) { hitStop -= dt; return; }

            timeLeft -= dt;
            RenderTimer();
            int sec = Mathf.CeilToInt(timeLeft);
            if (sec != lastSec)
            {
                lastSec = sec;
                if (sec <= 10 && sec > 0)
                {
                    sfx.Tone(sec <= 3 ? 1320 : 880, 0.07f, Sfx.Wave.Square, sec <= 3 ? 0.14f : 0.08f);
                    if (sec <= 3) { Slam(sec.ToString(), Color.white); Vibrate(); }
                }
            }
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                RenderTimer();
                EndGame(timeUp: true);
                return;
            }

            if (feverTime > 0f)
            {
                feverTime -= dt;
                beatTimer -= dt;
                if (beatTimer <= 0f)
                {
                    beatTimer = 60f / 128f / 2f; // BGM (128 BPM) の 8 分音符に合わせる
                    beatCount++;
                    if (strongFx && beatCount % 2 == 1) beatGlowT = 0f;
                }
                if (feverTime <= 0f)
                {
                    feverTime = 0f;
                    EndFever();
                    ShowToast("フィーバー終了", "また10コンボで再突入");
                }
                RenderMeter();
            }

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                DoSpawn();
                var d = GetDifficulty(score);
                spawnTimer = d.spawnInterval * (feverTime > 0f ? 0.7f : 1f) + UnityEngine.Random.Range(-0.07f, 0.07f);
            }

            foreach (var cell in cells)
            {
                if (cell.kind == CellKind.Idle) continue;
                cell.age += dt;
                cell.remaining -= dt;
                if (cell.remaining <= 0f) ClearCell(cell);
            }
        }

        static Difficulty GetDifficulty(int s) => new Difficulty
        {
            spawnInterval = Mathf.Max(0.34f, 0.86f - s * 0.006f),
            activeDuration = Mathf.Max(0.6f, 1.35f - s * 0.008f),
            maxSimultaneous = s < 8 ? 1 : s < 20 ? 2 : s < 45 ? 3 : 4,
            redRatio = Mathf.Min(0.42f, 0.22f + s * 0.0025f),
            goldRatio = 0.06f,
        };

        void DoSpawn()
        {
            var d = GetDifficulty(score);
            var idle = new List<Cell>();
            foreach (var c in cells) if (c.kind == CellKind.Idle) idle.Add(c);
            int count = Mathf.Min(d.maxSimultaneous + (feverTime > 0f ? 1 : 0), idle.Count);
            for (int k = 0; k < count; k++)
            {
                int pick = UnityEngine.Random.Range(0, idle.Count);
                var cell = idle[pick];
                idle.RemoveAt(pick);
                float r = UnityEngine.Random.value;
                float red = d.redRatio * (feverTime > 0f ? 0.6f : 1f);
                cell.kind = r < red ? CellKind.Red : r < d.redRatio + d.goldRatio ? CellKind.Gold : CellKind.Blue;
                cell.age = 0f;
                cell.anim = 0f;
                cell.hitT = 9f;
                cell.remaining = (cell.kind == CellKind.Gold ? d.activeDuration * 0.7f : d.activeDuration)
                                 + UnityEngine.Random.Range(-0.1f, 0.1f);
            }
        }

        static void ClearCell(Cell cell) => cell.kind = CellKind.Idle;

        // =====================================================================
        // input
        // =====================================================================

        void OnTap(int index)
        {
            if (state != State.Playing) return;
            var cell = cells[index];
            Vector2 p = fxLayer.InverseTransformPoint(cell.root.position);
            float w = cell.root.rect.width;

            if (cell.kind == CellKind.Idle)
            {
                cell.idleT = 0f;
                return;
            }

            if (cell.kind == CellKind.Blue || cell.kind == CellKind.Gold)
            {
                bool isGold = cell.kind == CellKind.Gold, perfect = cell.age <= PerfectWindow;
                bool fever = feverTime > 0f;
                ClearCell(cell);
                cell.hitT = 0f;
                combo++;
                if (combo > maxCombo) maxCombo = combo;
                if (!fever) feverCharge++;
                int gain = isGold ? 5 : 1;
                if (perfect) gain += 1;
                if (combo % 5 == 0) gain += 1;
                if (fever) gain *= 2;
                score += gain;
                RenderScore();

                Color[] cols = isGold
                    ? new[] { Gfx.Hex("#ffe08a"), Gold, Gfx.Hex("#fff6d6"), Gfx.Hex("#ffb347") }
                    : fever
                        ? new[] { Pink, Cyan, Gold, Color.white }
                        : new[] { BlueGlow, Blue, Gfx.Hex("#cfe4ff"), Color.white };
                float intensity = Mathf.Min(1f + combo * 0.06f, 2.4f);
                fx.Burst(p, cols, Mathf.RoundToInt((isGold ? 44 : 18) * intensity), 0.35f * intensity, 0.56f,
                    isGold ? 5f : 3.6f);
                fx.RingAt(p, isGold ? Gold : fever ? Pink : BlueGlow, w * (isGold ? 2.4f : 1.3f));
                gridPunchT = 0f;

                PopText(cell, perfect ? "PERFECT +" + gain : "+" + gain, perfect);
                sfx.Tone(440f * Mathf.Pow(2f, Mathf.Min(combo, 24) / 12f), 0.1f, Sfx.Wave.Triangle, 0.18f);
                if (perfect) sfx.Tone(1760, 0.08f, Sfx.Wave.Sine, 0.1f);

                if (isGold)
                {
                    Slam("JACKPOT!", Gfx.Hex("#ffe08a"));
                    Flash(Gold, 0.5f);
                    hitStop = 0.07f;
                    timeLeft = Mathf.Min(TimeLimit, timeLeft + GoldTimeBonus);
                    RenderTimer();
                    timeBonusT = 0f;
                    sfx.Chord(new[] { 784f, 988f, 1175f, 1568f }, 0.25f, Sfx.Wave.Square, 0.1f);
                    Shake(false);
                    Vibrate();
                }
                else if (combo % 5 == 0)
                {
                    Slam(combo + " COMBO", Color.white);
                    Flash(new Color(0.47f, 0.67f, 1f), 0.35f);
                    hitStop = 0.045f;
                    sfx.Noise(0.08f, 0.12f);
                }
                else
                {
                    ShowToast(perfect ? "パーフェクト！" : "ナイス！", "+" + gain);
                }

                int lvl = score / 30;
                if (lvl > lastLevel)
                {
                    lastLevel = lvl;
                    Later(0.25f, () =>
                    {
                        Slam("LEVEL " + (lvl + 1), Gfx.Hex("#ffe08a"));
                        sfx.Chord(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.3f, Sfx.Wave.Triangle, 0.1f);
                        fx.RingAt(Vector2.zero, Gold, fxLayer.rect.width * 0.8f);
                    });
                }
                if (feverTime <= 0f && feverCharge >= FeverCombo) StartFever();
                RenderMeter();
            }
            else // red
            {
                ClearCell(cell);
                int hadCombo = combo;
                if (feverTime > 0f) { feverTime = 0f; EndFever(); }
                combo = 0;
                feverCharge = 0;
                lives--;
                RenderLives();
                RenderMeter();
                fx.Burst(p, new[] { Gfx.Hex("#ff3b3b"), RedGlow, Gfx.Hex("#660000"), Color.white }, 40, 0.55f, 0.7f,
                    4.5f, 1200f);
                fx.RingAt(p, Gfx.Hex("#ff3b3b"), w * 2.2f);
                Shake(true);
                if (strongFx) glitchT = 0f;
                Flash(Red, 0.6f);
                hitStop = 0.11f;
                sfx.Tone(180, 0.35f, Sfx.Wave.Saw, 0.25f, 50);
                sfx.Noise(0.25f, 0.3f);
                Vibrate();
                Slam(hadCombo >= 5 ? "コンボ切れ…" : "OUT!", Color.white);
                if (lives <= 0) EndGame(timeUp: false);
            }
        }

        void StartFever()
        {
            feverTime = FeverDuration;
            bgm.Fever = true;
            beatTimer = 0f;
            beatCount = 0;
            feverCharge = 0;
            Slam("FEVER!!", Gfx.Hex("#ff9be6"));
            Flash(Pink, 0.55f);
            Shake(true);
            hitStop = 0.09f;
            var r = fxLayer.rect;
            fx.Burst(Vector2.zero, new[] { Pink, Cyan, Gold, Color.white }, 90, 0.9f, 0.9f, 5f);
            fx.RingAt(Vector2.zero, Pink, Mathf.Max(r.width, r.height) * 0.7f);
            sfx.Chord(new[] { 523f, 659f, 784f, 1047f }, 0.35f, Sfx.Wave.Square, 0.12f);
            Vibrate();
        }

        void EndFever()
        {
            bgm.Fever = false;
            RenderMeter();
        }

        // =====================================================================
        // state transitions
        // =====================================================================

        void ResetRound()
        {
            score = 0; lives = 3; combo = 0; maxCombo = 0; feverCharge = 0; feverTime = 0f;
            timeLeft = TimeLimit; lastSec = 60; spawnTimer = 0.6f; lastLevel = 0; hitStop = 0f; playTime = 0f;
            foreach (var c in cells) ClearCell(c);
            scoreText.text = "0";
            RenderTimer();
            RenderMeter();
            RenderLives();
            gameoverOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
        }

        /// <summary>ゲームオーバー画面から、ベストスコアつきのスタート画面へ戻る。</summary>
        void ShowStartScreen()
        {
            if (state == State.Paused)
                analytics.RoundEnded(PlayAnalytics.Result.Quit, score, maxCombo, playTime, lives);
            state = State.Ready;
            bgm.Stop();
            ResetRound();
            RenderBest();
            startOverlay.SetActive(true);
        }

        void StartGame()
        {
            ResetRound();
            startOverlay.SetActive(false);
            state = State.Playing;
            bgm.Play();
            analytics.RoundStarted();
            RenderLives();
            Slam("START!", Color.white);
            sfx.Chord(new[] { 523f, 784f }, 0.2f, Sfx.Wave.Triangle, 0.12f);
        }

        void EndGame(bool timeUp)
        {
            analytics.RoundEnded(timeUp ? PlayAnalytics.Result.Clear : PlayAnalytics.Result.GameOver, score, maxCombo,
                playTime, lives);
            state = State.GameOver;
            bgm.Stop();
            feverTime = 0f;
            foreach (var c in cells) ClearCell(c);
            RenderMeter();
            gameoverTitle.text = timeUp ? "タイムアップ！" : "ゲームオーバー";
            if (timeUp) { Slam("TIME UP!", Gfx.Hex("#ffe08a")); sfx.Noise(0.2f, 0.2f); }
            bool isNew = score > best;
            if (isNew)
            {
                best = score;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
            RenderBest();
            RenderLives();
            finalScore.text = score.ToString();
            finalBest.text = best.ToString();
            finalCombo.text = maxCombo.ToString();
            newBadge.SetActive(isNew);
            sfx.Tone(330, 0.5f, Sfx.Wave.Square, 0.15f, 110);
            Later(0.5f, () =>
            {
                gameoverOverlay.SetActive(true);
                if (isNew)
                {
                    fx.ConfettiRain();
                    sfx.Chord(new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.4f, Sfx.Wave.Triangle, 0.12f);
                }
            });
        }

        void TogglePause()
        {
            if (state == State.Playing) { state = State.Paused; bgm.Pause(); }
            else if (state == State.Paused) { state = State.Playing; bgm.Resume(); }
            pauseOverlay.SetActive(state == State.Paused);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && state == State.Playing) TogglePause();
        }

        // ブラウザ (WebGL) でタブやウィンドウを離れたときも一時停止する
        void OnApplicationFocus(bool focused)
        {
            if (!focused && state == State.Playing) TogglePause();
        }

        // =====================================================================
        // rendering
        // =====================================================================

        void RenderConsent() => consentText.text = analytics.Consent ? "プレイ統計の送信：オン" : "プレイ統計の送信：オフ";

        void ApplyFxMode()
        {
            fxBtnText.text = strongFx ? "演出 強" : "演出 弱";
            if (fx != null) fx.Strong = strongFx;
        }

        void RenderScore()
        {
            scoreText.text = score.ToString();
            scoreBumpT = 0f;
        }

        void RenderBest()
        {
            bestText.text = best.ToString();
            startBestText.text = best.ToString();
        }

        void RenderLives()
        {
            for (int i = 0; i < lifeTexts.Length; i++)
                lifeTexts[i].color = i < lives ? Gfx.Hex("#ff4d6d") : new Color(0.5f, 0.5f, 0.5f, 0.25f);
        }

        void RenderTimer() => timerText.text = "TIME " + Mathf.Max(0f, timeLeft).ToString("0.0");

        void RenderMeter()
        {
            float ratio = feverTime > 0f ? feverTime / FeverDuration : Mathf.Min(feverCharge, FeverCombo) / (float)FeverCombo;
            meterFillRT.anchorMax = new Vector2(ratio, 1f);
            meterFill.texture = feverTime > 0f ? meterFeverTex : meterTex;
            meterLabel.text = feverTime > 0f ? "フィーバー中 スコア2倍" : "フィーバーまで";
            comboLabel.text = combo + " コンボ";
        }

        void AnimateUi(float dt)
        {
            float now = Time.unscaledTime;
            bool fever = feverTime > 0f && state != State.GameOver;

            // score bump
            scoreBumpT += dt;
            float bt = scoreBumpT / 0.22f;
            float bump = bt < 1f ? (bt < 0.35f ? Mathf.Lerp(1f, 1.35f, bt / 0.35f) : Mathf.Lerp(1.35f, 1f, (bt - 0.35f) / 0.65f)) : 1f;
            scoreText.rectTransform.localScale = Vector3.one * bump;
            scoreText.color = bt < 1f ? Color.Lerp(Gold, TextPrimary, bt) : fever ? Color.white : TextPrimary;

            // timer warning
            bool warn = state == State.Playing && timeLeft <= 10f;
            timerText.color = warn ? Gfx.Hex("#ff6b6b") : TextPrimary;
            timerRT.localScale = Vector3.one * (warn ? 1f + 0.12f * Mathf.PingPong(now * 2f, 1f) : 1f);

            timeBonusT += dt;
            timeBonusText.color = new Color(0.49f, 1f, 0.6f, timeBonusT < 0.7f ? 1f : Mathf.Max(0f, 1f - (timeBonusT - 0.7f) / 0.2f));

            // meter scroll during fever
            meterFill.uvRect = fever ? new Rect(-now * 1.66f, 0, 0.5f, 1) : new Rect(0, 0, 1, 1);

            // fever backdrop / grid
            feverTint.color = fever ? Color.HSVToRGB(now / 2.4f % 1f, 0.8f, 1f) * new Color(1, 1, 1, strongFx ? 0.16f : 0.08f) : Color.clear;
            float pulse = Mathf.PingPong(now * 2f, 1f);
            gridBorder.color = fever ? Color.Lerp(Pink, Cyan, pulse) : PanelBorder;
            gridGlow.color = fever ? Color.Lerp(Pink, Cyan, pulse) * new Color(1, 1, 1, 0.35f) : Color.clear;
            gridPunchT += dt;
            gridRT.localScale = Vector3.one * (gridPunchT < 0.08f ? 1.035f : 1f);

            // drifting icons
            float driftMul = fever && strongFx ? 4f : 1f;
            float h = ((RectTransform)canvas.transform).rect.height + 80f;
            for (int i = 0; i < drifters.Count; i++)
            {
                var d = drifters[i];
                float y = d.anchoredPosition.y + drifterSpeed[i] * driftMul * h * dt;
                if (y > h) y = -40f;
                d.anchoredPosition = new Vector2(0, y);
                float k = y / h;
                d.GetComponent<Text>().color = new Color(0.55f, 0.65f, 1f, 0.45f * Mathf.Clamp01(k * 10f) * Mathf.Clamp01((1f - k) * 10f));
            }

            // beat glow
            beatGlowT += dt;
            var bgc = beatGlowImg.color;
            bgc.a = beatGlowT < 0.06f ? 0.35f : Mathf.Max(0f, 0.35f * (1f - (beatGlowT - 0.06f) / 0.18f));
            beatGlowImg.color = bgc;

            // flash
            flashT += dt;
            var fc = flashImg.color;
            fc.a = flashT < 0.09f ? fc.a : Mathf.MoveTowards(fc.a, 0f, dt / 0.35f);
            flashImg.color = fc;

            // danger vignette (残りライフ 1)
            var vc = vignetteImg.color;
            vc.a = lives == 1 && state == State.Playing ? Mathf.Lerp(0.45f, 1f, Mathf.PingPong(now / 0.9f, 1f)) : Mathf.MoveTowards(vc.a, 0f, dt / 0.3f);
            vignetteImg.color = vc;

            // shake + glitch
            shakeT += dt;
            glitchT += dt;
            Vector2 offset = Vector2.zero;
            float rot = 0f;
            if (shakeT < shakeDur)
            {
                float k = 1f - shakeT / shakeDur;
                float amp = shakeBig ? 11f : 7f;
                offset = new Vector2(Mathf.Sin(shakeT * 70f) * amp * k, shakeBig ? Mathf.Cos(shakeT * 53f) * amp * 0.5f * k : 0f);
                if (shakeBig) rot = Mathf.Sin(shakeT * 47f) * 1.5f * k;
            }
            if (glitchT < 0.35f) offset.x += (Mathf.Floor(glitchT / 0.0875f) % 2 == 0 ? 6f : -6f);
            column.anchoredPosition = offset;
            column.localRotation = Quaternion.Euler(0, 0, rot);

            AnimateSlam(dt);
            AnimateToast(dt);
            AnimatePops(dt);
        }

        void AnimateCells(float dt)
        {
            float now = Time.unscaledTime;
            foreach (var c in cells)
            {
                c.anim += dt;
                c.hitT += dt;
                c.idleT += dt;
                float scale = 1f, rot = 0f, glowScale = -1f;
                Color body, glow;
                float shine;
                switch (c.kind)
                {
                    case CellKind.Blue:
                        body = BlueGlow; glow = Blue * new Color(1, 1, 1, 0.65f); shine = 0.7f;
                        scale = PopIn(c.anim);
                        break;
                    case CellKind.Red:
                        body = RedGlow; glow = Red * new Color(1, 1, 1, 0.65f); shine = 0.7f;
                        scale = c.anim < 0.18f ? PopIn(c.anim) : 1f + 0.07f * Mathf.PingPong((c.anim - 0.18f) / 0.45f, 1f);
                        break;
                    case CellKind.Gold:
                    {
                        // 1 秒 3 回ほど脈打つように明るさ・光のにじみを変え、周りに星をまたたかせる
                        float glint = 0.5f + 0.5f * Mathf.Sin(c.anim * Mathf.PI * 6f);
                        body = Color.Lerp(Gfx.Hex("#ffd75e"), Gfx.Hex("#fff0b0"), glint);
                        glow = Color.Lerp(GoldDeep, Gfx.Hex("#ffe27a"), glint);
                        shine = Mathf.Lerp(0.8f, 1f, glint);
                        float spin = c.anim % 1f;
                        scale = c.anim < 0.18f ? PopIn(c.anim) : 1f + 0.1f * Mathf.Sin(spin * Mathf.PI);
                        rot = -spin * 360f;
                        glowScale = scale * Mathf.Lerp(1.4f, 1.85f, glint);
                        EmitGoldSparkles(c, dt);
                        break;
                    }
                    default:
                        body = CellOff; glow = Color.clear; shine = 0.06f;
                        if (c.hitT < 0.18f)
                        {
                            float k = c.hitT / 0.18f;
                            scale = Mathf.Lerp(1.3f, 1f, k);
                            body = Color.Lerp(Color.white, CellOff, k);
                        }
                        if (c.idleT < 0.12f) scale = 0.9f;
                        break;
                }
                c.body.rectTransform.localScale = Vector3.one * scale;
                c.body.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                c.body.color = body;
                c.glow.color = glow;
                c.glow.rectTransform.localScale = Vector3.one * (glowScale > 0f ? glowScale : scale);
                c.shine.color = new Color(1, 1, 1, shine);
            }
        }

        static readonly Color[] SparkleColors = { Color.white, Gfx.Hex("#fff6d6"), Gfx.Hex("#ffe08a") };

        void EmitGoldSparkles(Cell c, float dt)
        {
            c.sparkleT -= dt;
            if (c.sparkleT > 0f) return;
            c.sparkleT = (strongFx ? 0.07f : 0.16f) + UnityEngine.Random.value * 0.05f;
            Vector2 center = fxLayer.InverseTransformPoint(c.root.position);
            float w = c.root.rect.width;
            var offset = UnityEngine.Random.insideUnitCircle.normalized * (w * UnityEngine.Random.Range(0.35f, 0.75f));
            fx.Twinkle(center + offset, SparkleColors[UnityEngine.Random.Range(0, SparkleColors.Length)],
                w * UnityEngine.Random.Range(0.4f, 0.65f));
        }

        static float PopIn(float t) => t < 0.18f ? Mathf.Lerp(0.55f, 1f, 1f - (1f - t / 0.18f) * (1f - t / 0.18f)) : 1f;

        // ---------- slam text ----------

        void Slam(string text, Color color)
        {
            slamText.text = text;
            slamText.color = color;
            slamT = 0f;
        }

        // keyframes: (time, scale, rotation, alpha, yOffset)
        static readonly float[,] SlamKeys =
        {
            { 0f, 3f, -8f, 0f, 0f }, { .18f, .92f, 2f, 1f, 0f }, { .28f, 1.06f, -1f, 1f, 0f },
            { .75f, 1f, 0f, 1f, 0f }, { 1f, 1.1f, 0f, 0f, 14f },
        };

        void AnimateSlam(float dt)
        {
            slamT += dt;
            float t = slamT / 0.9f;
            if (t >= 1f) { slamGroup.alpha = 0f; return; }
            var k = SlamKeys;
            int i = 0;
            while (i < k.GetLength(0) - 2 && t > k[i + 1, 0]) i++;
            float u = Mathf.InverseLerp(k[i, 0], k[i + 1, 0], t);
            if (i == 0) u = 1f - (1f - u) * (1f - u) * (1f - u);
            slamRT.localScale = Vector3.one * Mathf.Lerp(k[i, 1], k[i + 1, 1], u);
            slamRT.localRotation = Quaternion.Euler(0, 0, -Mathf.Lerp(k[i, 2], k[i + 1, 2], u));
            slamGroup.alpha = Mathf.Lerp(k[i, 3], k[i + 1, 3], u);
            slamRT.anchoredPosition = new Vector2(0, Mathf.Lerp(k[i, 4], k[i + 1, 4], u));
        }

        // ---------- toast ----------

        void ShowToast(string main, string sub)
        {
            toastMain.text = main;
            toastSub.text = sub;
            toastT = 0f;
        }

        void AnimateToast(float dt)
        {
            toastT += dt;
            float target = toastT < 0.65f ? 1f : 0f;
            toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, dt / 0.18f);
            float a = toastGroup.alpha;
            toastRT.anchoredPosition = new Vector2(0, Top(812) - 8f * (1f - a));
            toastRT.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, a);
        }

        // ---------- score pop ----------

        void PopText(Cell cell, string text, bool perfect)
        {
            var t = Gfx.Label(gridRT, text, perfect ? 15 : 17, perfect ? Color.white : Gold);
            t.rectTransform.Place(cell.root.anchoredPosition, new Vector2(160, 24));
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = perfect ? Pink : Gfx.Hex("#7a4a00");
            sh.effectDistance = perfect ? new Vector2(0, 0) : new Vector2(0, -2);
            if (perfect) t.gameObject.AddComponent<Outline>().effectColor = Pink * new Color(1, 1, 1, 0.8f);
            pops.Add((t, 0f, cell.root.anchoredPosition.y));
        }

        void AnimatePops(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (text, t, baseY) = pops[i];
                t += dt;
                if (t >= 0.7f)
                {
                    Destroy(text.gameObject);
                    pops.RemoveAt(i);
                    continue;
                }
                pops[i] = (text, t, baseY);
                float u = t / 0.7f;
                float alpha, rise, scale;
                if (u < 0.15f)
                {
                    float k = u / 0.15f;
                    alpha = k; rise = 0.25f * k; scale = Mathf.Lerp(0.6f, 1.25f, k);
                }
                else
                {
                    float k = (u - 0.15f) / 0.85f;
                    alpha = 1f - k; rise = Mathf.Lerp(0.25f, 1.1f, k); scale = Mathf.Lerp(1.25f, 1f, k);
                }
                var rt = text.rectTransform;
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + rise * 24f);
                var c = text.color;
                c.a = alpha;
                text.color = c;
            }
        }

        // ---------- misc effects ----------

        void Flash(Color color, float strength)
        {
            if (Time.unscaledTime - lastFlash < FlashMinInterval) return;
            lastFlash = Time.unscaledTime;
            color.a = strongFx ? strength : strength * 0.35f;
            flashImg.color = color;
            flashT = 0f;
        }

        void Shake(bool big)
        {
            if (big && !strongFx) return;
            shakeBig = big;
            shakeDur = big ? 0.45f : 0.32f;
            shakeT = 0f;
        }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        void Later(float delay, Action act) => later.Add((Time.unscaledTime + delay, act));

        void RunLater()
        {
            float now = Time.unscaledTime;
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (later[i].at > now) continue;
                var act = later[i].act;
                later.RemoveAt(i);
                act();
            }
        }
    }

    /// <summary>押した瞬間に反応させるため、Button の onClick (離したとき) ではなく PointerDown を使う。</summary>
    public class PointerDown : MonoBehaviour, IPointerDownHandler
    {
        public Action OnDown;
        public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
    }
}
