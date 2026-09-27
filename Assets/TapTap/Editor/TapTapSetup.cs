using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TapTap.EditorTools
{
    /// <summary>
    /// 初回にプロジェクトを開いたとき、Main シーンを自動生成してビルド設定に登録する。
    /// メニュー「TapTap > メインシーンを作り直す」から再生成もできる。
    /// </summary>
    [InitializeOnLoad]
    public static class TapTapSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static TapTapSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) CreateScene();
            };
        }

        [MenuItem("TapTap/メインシーンを作り直す")]
        static void RecreateFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateScene();
        }

        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(5, 6, 15, 255);
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            new GameObject("TapTapGame").AddComponent<TapTapGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "タプタプ";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            Debug.Log("[TapTap] Main シーンを作成しました: " + ScenePath);
        }
    }
}
