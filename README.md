# タプタプ (Unity 版)

4×4 のボタンが光る反射神経ゲーム。HTML 版「タプタプ」を Unity 6.3 LTS (6000.3.25f1) で再実装したもの。

## 遊び方

- **青** を押す → +1、**金** を押す → +5 と残り時間 +2 秒
- **赤** を押すとライフ −1・コンボリセット (ライフ 3)
- 制限時間 60 秒。出た瞬間 (0.28 秒以内) に押すと PERFECT で +1
- 5 コンボごとにボーナス +1、10 コンボでフィーバー (7 秒間スコア 2 倍)
- スコアが上がるほど出現が速く・同時に多く・赤が多くなる
- ベストスコアと「演出 強/弱」設定は PlayerPrefs に保存

## 開き方

1. Unity Hub で `TapTap` フォルダを「Add project from disk」で追加し、6000.3.25f1 で開く
2. `Assets/Scenes/Main.unity` を開いて Play を押す
   (シーンが無い場合は初回起動時に自動生成される。メニュー **TapTap > メインシーンを作り直す** でも可)
3. Game ビューを縦長 (例: 1080×1920) にすると本番に近い見た目になる

## Mac アプリのビルド

メニュー **TapTap > Mac アプリをビルド** で `Builds/Mac/TapTap.app` が作られる。コマンドラインからは:

```bash
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath . -executeMethod TapTap.EditorTools.TapTapBuild.BuildMac -quit
```

## 構成

画像・音声アセットは使っていない。スプライト・フォント・効果音はすべて実行時にコードで生成している。

| ファイル | 役割 |
|---|---|
| `Assets/TapTap/Scripts/TapTapGame.cs` | ゲームロジック、UI 構築、演出 (シェイク・スラム文字・フラッシュ等) |
| `Assets/TapTap/Scripts/UiFx.cs` | UI 上の火花・リング・紙吹雪パーティクル |
| `Assets/TapTap/Scripts/Sfx.cs` | 効果音のリアルタイム合成 (サイン/矩形/三角/ノコギリ波・ノイズ) |
| `Assets/TapTap/Scripts/Gfx.cs` | 生成スプライト、日本語 OS フォント読み込み、UI 部品ヘルパー |
| `Assets/TapTap/Editor/TapTapSetup.cs` | Main シーンの自動生成とビルド設定登録 |
| `Assets/TapTap/Editor/TapTapBuild.cs` | Mac アプリのビルド (メニュー / コマンドライン) |

## 補足

- 日本語はレガシー `Text` と OS フォント (ヒラギノ / 游ゴシック / Noto Sans CJK など) で表示する
- 強い光の点滅は 0.34 秒に 1 回までに制限している。「演出 弱」にするとフラッシュ・揺れ・パーティクルが控えめになる
