using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TapTap.EditorTools
{
    /// <summary>メニュー「TapTap > ○○をビルド」、またはコマンドラインの -executeMethod から使う。</summary>
    public static class TapTapBuild
    {
        const string MacOutput = "Builds/Mac/TapTap.app";
        const string WebGLOutput = "Builds/WebGL";
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("TapTap/Mac アプリをビルド")]
        public static void BuildMac() => Build(MacOutput, BuildTarget.StandaloneOSX);

        /// <summary>unityroom 向け。縦長 540x960、Gzip 圧縮 (Decompression Fallback なし)。</summary>
        [MenuItem("TapTap/WebGL をビルド (unityroom 用)")]
        public static void BuildWebGL()
        {
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = false;
            Build(WebGLOutput, BuildTarget.WebGL);
        }

        static void Build(string output, BuildTarget target)
        {
            var report = BuildPipeline.BuildPlayer(Scenes, output, target, BuildOptions.None);
            Debug.Log($"[TapTap] ビルド結果: {report.summary.result} ({output})");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
