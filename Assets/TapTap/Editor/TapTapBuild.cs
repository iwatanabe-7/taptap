using UnityEditor;
using UnityEngine;

namespace TapTap.EditorTools
{
    /// <summary>メニュー「TapTap > Mac アプリをビルド」、またはコマンドラインの -executeMethod から使う。</summary>
    public static class TapTapBuild
    {
        const string OutputPath = "Builds/Mac/TapTap.app";

        [MenuItem("TapTap/Mac アプリをビルド")]
        public static void BuildMac()
        {
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, OutputPath,
                BuildTarget.StandaloneOSX, BuildOptions.None);
            Debug.Log($"[TapTap] ビルド結果: {report.summary.result} ({OutputPath})");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
