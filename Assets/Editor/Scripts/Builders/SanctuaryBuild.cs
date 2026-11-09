using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Headless Windows build:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod SanctuaryBuild.Build -buildPath <dir>
/// Writes <dir>/SanctuaryMapEditor.exe plus the Structure folder the editor
/// reads at run time. Exits non-zero when the build fails.
public static class SanctuaryBuild
{
	public const string ExeName = "SanctuaryMapEditor.exe";
	static readonly string[] Scenes = { "Assets/Preload.unity", "Assets/MapEditor.unity" };

	[MenuItem("Build/Sanctuary Map Editor (64bit)")]
	public static void BuildFromMenu()
	{
		string path = EditorUtility.SaveFolderPanel("Build Sanctuary Map Editor into", "Build", "");
		if (!string.IsNullOrEmpty(path)) BuildTo(path);
	}

	public static void Build()
	{
		string path = "Build";
		var args = Environment.GetCommandLineArgs();
		for (int i = 0; i < args.Length - 1; i++)
			if (args[i] == "-buildPath") path = args[i + 1];

		bool ok = BuildTo(path);
		if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
	}

	static bool BuildTo(string path)
	{
		PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
		var options = new BuildPlayerOptions
		{
			scenes = Scenes,
			locationPathName = System.IO.Path.Combine(path, ExeName),
			target = BuildTarget.StandaloneWindows64,
			options = BuildOptions.CompressWithLz4HC,
		};
		BuildReport report = BuildPipeline.BuildPlayer(options);
		Debug.Log($"SanctuaryBuild: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB -> {path}");
		if (report.summary.result != BuildResult.Succeeded) return false;

		string structure = System.IO.Path.Combine(path, "SanctuaryMapEditor_Data", "Structure");
		FileUtil.DeleteFileOrDirectory(structure);
		FileUtil.CopyFileOrDirectory("Structure", structure);
		return true;
	}
}
