using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// One-off scene surgery that turned the FAF editor into a Sanctuary-only
/// editor. Kept so the change is reviewable and repeatable:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod StandaloneCleanup.Run
/// Objects scripts hold references to are deactivated, not destroyed, so no
/// field goes null; objects whose scripts are deleted are destroyed first.
public static class StandaloneCleanup
{
	const string UI = "Interface/MapEditorUI/";
	const string Menu = UI + "AppMenu/AppMenu/Popup/";
	const string Left = UI + "UiCore/EditingScreen/MenuLeft/";
	const string Prefs = UI + "Windows/Preferences/field1/";

	static readonly string[] Deactivate =
	{
		// Tool tabs whose edits a Sanctuary map does not store.
		Left + "SwitchButtons/LightingBttn",
		Left + "SwitchButtons/PropsBttn",
		Left + "SwitchButtons/DecalsBttn",
		Left + "SwitchButtons/UnitsBttn",
		// File: FA versioned folders, launching Forged Alliance, FA preview render.
		Menu + "File/SaveAsNewVersion",
		Menu + "File/PlayMap",
		Menu + "File/Render",
		Menu + "File/line (1)",
		// Tools: browsers of FA's env.scd / units.scd, FAF vault uploader.
		Menu + "Tools/ResourceBrowser",
		Menu + "Tools/UnitBrowser",
		Menu + "Tools/Map Uploader",
		// View: FA's AI intel grid.
		Menu + "View/AIIntelGridToggle",
		// Help: FAF Discord, forum, wiki, Gaea tutorial.
		Menu + "Help/Discord",
		Menu + "Help/Forum",
		Menu + "Help/Wiki",
		Menu + "Help/Gaea",
		Menu + "Help/line (1)",
		// Map page: FAF vault versioning, FA slot advice, FA shader and scmap version.
		Left + "Map/ToolPageGroup/Page_Map/field/Viewport/Pivot/Version",
		Left + "Map/ToolPageGroup/Page_Map/field/Viewport/Pivot/Tools/BoldLabel (1)",
		Left + "Map/ToolPageGroup/Page_Map/field/Viewport/Pivot/Other",
		// Water: FA wave generators.
		Left + "Terrain/ToolPageGroup/Water/field/Pivot/Waves",
		// Textures: FA terrain-shader texture generators (ran the Neroxis toolsuite).
		Left + "Textures/field/Viewport/Pivot/ToolPageGroup/Tools/ShaderSettings",
		Left + "Textures/field/Viewport/Pivot/ToolPageGroup/Tools/ShaderTools",
		// Preferences: FAF data folder, FA maps folder, "play map in FA" options.
		Prefs + "FafFolder",
		Prefs + "MapsFolder",
		Prefs + "PlayMap",
		// Splash: the FA Forever logo.
		UI + "Background/Logo",
	};

	/// Components whose scripts are deleted with this change: their objects go.
	static readonly string[] DestroyWithComponent = { "UploadMapPopUp", "NeroxisProcessRunner" };

	static readonly Dictionary<string, string> Relabel = new Dictionary<string, string>
	{
		{ Prefs + "FoldarName", "Sanctuary: Shattered Sun install folder" },
	};

	public static void Run()
	{
		foreach (var path in new[] { "Assets/MapEditor.unity", "Assets/Preload.unity" })
		{
			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
			var byPath = new Dictionary<string, GameObject>();
			foreach (var t in all)
			{
				string p = PathOf(t);
				if (!byPath.ContainsKey(p)) byPath[p] = t.gameObject;
			}

			int off = 0, gone = 0, text = 0;
			// Every match, not just the first: the splash has two "Logo"s.
			foreach (var t in all)
			{
				if (t == null || !Deactivate.Contains(PathOf(t)) || !t.gameObject.activeSelf) continue;
				t.gameObject.SetActive(false);
				Record(t.gameObject);
				off++;
			}

			foreach (var t in all.ToList())
			{
				if (t == null) continue;
				foreach (var c in t.GetComponents<MonoBehaviour>())
					if (c != null && DestroyWithComponent.Contains(c.GetType().Name))
					{
						Object.DestroyImmediate(t.gameObject);
						gone++;
						break;
					}
			}

			foreach (var kv in Relabel)
				if (byPath.TryGetValue(kv.Key, out var go) && go != null)
				{
					var label = go.GetComponent<Text>();
					if (label != null) { label.text = kv.Value; Record(label); text++; }
				}

			// Splash credits: the editor this grew from, kept credited.
			foreach (var t in all)
			{
				if (t == null || PathOf(t).IndexOf("/Background/Credits", System.StringComparison.Ordinal) < 0) continue;
				var label = t.GetComponent<Text>();
				if (label == null) continue;
				string before = label.text;
				if (label.text.StartsWith("Made by")) label.text = "Sanctuary Map Editor";
				else if (label.text.StartsWith("Contributors")) label.text = "Based on the FA Forever Map Editor\nby ozonex and contributors (MIT)";
				else if (label.text.StartsWith("Thanks to")) label.text = "For Sanctuary: Shattered Sun";
				if (label.text != before) { Record(label); text++; }
			}

			// A title where the FA Forever logo was.
			foreach (var t in all.ToList())
			{
				if (t == null || !PathOf(t).EndsWith("MapEditorUI/Background") || t.Find("SanctuaryTitle") != null) continue;
				var credits = t.GetComponentsInChildren<Text>(true).FirstOrDefault();
				var title = new GameObject("SanctuaryTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
				title.transform.SetParent(t, false);
				var rt = (RectTransform)title.transform;
				rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
				rt.anchoredPosition = new Vector2(0, -120); rt.sizeDelta = new Vector2(1200, 260);
				var label = title.GetComponent<Text>();
				if (credits != null) label.font = credits.font;
				label.fontSize = 84; label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.UpperCenter;
				label.color = new Color(0.86f, 0.89f, 0.92f, 1f);
				label.text = "SANCTUARY\n<size=46>MAP EDITOR</size>";
				label.supportRichText = true; label.raycastTarget = false;
				title.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
				text++;
			}

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"StandaloneCleanup {path}: {off} deactivated, {gone} destroyed, {text} relabelled");
		}
	}

	/// Second step: the FA systems themselves. Run before their scripts are
	/// deleted (objects are matched by component type name):
	///   Unity.exe -batchmode -quit -projectPath . -executeMethod StandaloneCleanup.RemoveFaSystems
	/// Destroys every object that exists only to host an FA system, and
	/// strips the FA decal renderer off the two cameras.
	static readonly string[] FaSystemHosts =
	{
		"UnitsInfo", "UnitsControler", "UnitBrowser",
		"PropsInfo", "PropsRenderer",
		"DecalsInfo", "DecalsControler", "OzoneDecal",
		"ResourceBrowser", "LightingInfo", "AdaptiveInfo", "WavesRenderer",
	};
	static readonly string[] FaComponentsOnly = { "OzoneDecalRenderer" };

	public static void RemoveFaSystems()
	{
		foreach (var path in new[] { "Assets/MapEditor.unity", "Assets/Preload.unity" })
		{
			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			int objects = 0, comps = 0;
			var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
			foreach (var t in all)
			{
				if (t == null) continue;
				var behaviours = t.GetComponents<MonoBehaviour>().Where(c => c != null).ToList();
				if (behaviours.Any(c => FaSystemHosts.Contains(c.GetType().Name)))
				{
					Debug.Log("RemoveFaSystems: destroy " + PathOf(t));
					Object.DestroyImmediate(t.gameObject);
					objects++;
					continue;
				}
				foreach (var c in behaviours.Where(c => FaComponentsOnly.Contains(c.GetType().Name)))
				{
					Debug.Log("RemoveFaSystems: strip " + c.GetType().Name + " from " + PathOf(t));
					Object.DestroyImmediate(c);
					comps++;
				}
			}
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"RemoveFaSystems {path}: {objects} objects destroyed, {comps} components stripped");
		}
	}

	/// Changes to objects inside a prefab instance are dropped on save
	/// unless recorded as overrides (the splash Background is a prefab).
	static void Record(Object o)
	{
		if (PrefabUtility.IsPartOfPrefabInstance(o))
			PrefabUtility.RecordPrefabInstancePropertyModifications(o);
		EditorUtility.SetDirty(o);
	}

	static string PathOf(Transform t)
	{
		string p = t.name;
		for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
		return p;
	}
}
