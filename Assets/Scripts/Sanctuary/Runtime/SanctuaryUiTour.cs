// -uitour <out dir>: clicks through the editor the way a person would, with
// nobody at the keyboard. Opens each tab and menu, screenshots it, presses
// every button and toggle in it that doesn't open a file dialog, a browser or
// quit, and logs each press with any exception or error it caused. Then
// writes every UI text in the scene (shown or not) to texts.txt, to find
// leftover FA wording. Nothing is saved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Sanctuary
{
	public static class SanctuaryUiTour
	{
		static readonly Regex Risky = new Regex(
			"save|open|load|export|import|quit|exit|browse|path|url|help|shortcut|log|upload|discord|github|newmap|new map|recent|close|apply|play",
			RegexOptions.IgnoreCase);

		static readonly List<string> Problems = new List<string>();
		static StreamWriter Out;

		public static IEnumerator Run(string dir)
		{
			Directory.CreateDirectory(dir);
			Out = new StreamWriter(Path.Combine(dir, "tour.txt"));
			Application.logMessageReceived += OnLog;
			try
			{
				var editing = UnityEngine.Object.FindAnyObjectByType<EditMap.Editing>(FindObjectsInactive.Include);
				var menu = AppMenu.instance;

				foreach (var tab in new[] { "Map", "Terrain", "Textures", "Markers", "Lighting", "Decals", "Props", "Ai", "Map" })
				{
					Step($"tab {tab}", () => editing.ButtonFunction(tab));
					yield return Frames(5);
					yield return Shot(dir, "tab-" + tab);
					int id = editing.GetCategoryId();
					var panel = id < editing.Categorys.Length ? editing.Categorys[id] : null;
					if (panel == null) { Write($"  tab {tab}: no panel"); continue; }
					WriteTexts(panel.transform, "  shown: ");
					yield return PressAll(panel.transform, dir, tab);
				}

				if (menu != null)
					for (int i = 0; i < menu.Popups.Length; i++)
					{
						string name = new[] { "File", "Edit", "View", "Tools", "Symmetry", "Help" }[Math.Min(i, 5)];
						Step($"menu {name}", () => menu.OpenMenu(name));
						yield return Frames(3);
						yield return Shot(dir, "menu-" + name);
						WriteTexts(menu.Popups[i].transform, "  shown: ");
						// Menu entries that open a window: open it, shoot it, close it.
						foreach (var b in menu.Popups[i].GetComponentsInChildren<Button>(false))
						{
							string label = b.GetComponentInChildren<Text>(true)?.text ?? "";
							if (!Regex.IsMatch(label, "^(New map|Preferences\\.\\.\\.|Symmetry settings\\.\\.\\.)$")) continue;
							var before = ActiveWindows();
							menu.OpenMenu(name);
							Step($"menu {name} > {label}", () => b.onClick.Invoke());
							yield return Frames(5);
							yield return Shot(dir, $"window-{label.Trim('.').Replace(' ', '_')}");
							foreach (var w in ActiveWindows())
								if (!before.Contains(w)) { WriteTexts(w.transform, $"  shown in {label}: "); w.SetActive(false); }
						}
						foreach (var p in menu.Popups) p.SetActive(false);
					}

				using (var all = new StreamWriter(Path.Combine(dir, "texts.txt")))
				{
					foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
					{
						if (t.gameObject.scene.name == null) continue; // prefab assets, not the scene
						if (string.IsNullOrWhiteSpace(t.text)) continue;
						all.WriteLine($"{(t.gameObject.activeInHierarchy ? "shown " : "hidden")} | {PathOf(t.transform)} | {t.text.Replace("\n", "\\n")}");
					}
				}
			}
			finally
			{
				Application.logMessageReceived -= OnLog;
				Write($"problems: {Problems.Count}");
				foreach (var p in Problems) Write("  " + p);
				Out.Dispose();
			}
			Debug.Log($"Sanctuary UI tour: {Problems.Count} problems, written to {dir}");
		}

		static IEnumerator PressAll(Transform panel, string dir, string tab)
		{
			// Pressing a sub-tab shows new buttons: keep going until nothing
			// shown is left unpressed.
			var done = new HashSet<Selectable>();
			var seen = new HashSet<string>();
			int n = 0;
			while (n < 400)
			{
				Selectable s = null;
				foreach (var c in panel.GetComponentsInChildren<Selectable>(false))
					if ((c is Button || c is Toggle) && !done.Contains(c) && c.interactable) { s = c; break; }
				if (s == null) break;
				done.Add(s);
				string label = Describe(s);
				// A list's rows share a path: press each kind once, or "+"
				// and "-" on new rows would go round for ever.
				if (!seen.Add(label)) continue;
				if (Risky.IsMatch(label)) { Write($"  skip {label}"); continue; }
				bool isTab = s.name.StartsWith("Tab-Prefab");
				string page = s.GetComponentInChildren<Text>(true)?.text ?? "page";
				if (s is Button b) Step($"press {label}", () => b.onClick.Invoke());
				else if (s is Toggle t) Step($"toggle {label}", () => { t.isOn = !t.isOn; t.isOn = !t.isOn; });
				yield return Frames(2);
				if (isTab)
				{
					yield return Frames(3);
					yield return Shot(dir, $"tab-{tab}-{page}");
					WriteTexts(panel, $"  shown on {tab}/{page}: ");
				}
				// Close what a press opened (a window, a popup) so the tour goes on.
				if (GenericPopup.Current != null && GenericPopup.Current.gameObject.activeSelf) GenericPopup.Current.gameObject.SetActive(false);
				if (++n % 15 == 0) yield return Shot(dir, $"tab-{tab}-{n}");
			}
		}

		// Shown objects two levels under each canvas: windows and popups.
		static HashSet<GameObject> ActiveWindows()
		{
			var set = new HashSet<GameObject>();
			foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
				foreach (Transform a in c.transform)
				{
					if (a.gameObject.activeInHierarchy) set.Add(a.gameObject);
					foreach (Transform b in a) if (b.gameObject.activeInHierarchy) set.Add(b.gameObject);
				}
			return set;
		}

		static string Describe(Selectable s)
		{
			var sb = new StringBuilder(PathOf(s.transform));
			var txt = s.GetComponentInChildren<Text>(true);
			if (txt != null && !string.IsNullOrWhiteSpace(txt.text)) sb.Append(" \"").Append(txt.text.Replace("\n", " ")).Append('"');
			if (s is Button b)
				for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
					sb.Append(" ->").Append(b.onClick.GetPersistentMethodName(i)).Append('(').Append(ArgOf(b.onClick, i)).Append(')');
			return sb.ToString();
		}

		// The string argument a persistent call passes (Editing.ButtonFunction("Save")).
		static string ArgOf(UnityEngine.Events.UnityEventBase e, int i)
		{
			try
			{
				var calls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_PersistentCalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(e);
				var list = (System.Collections.IList)calls.GetType().GetProperty("m_Calls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(calls)
					?? (System.Collections.IList)calls.GetType().GetField("m_Calls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(calls);
				var args = list[i].GetType().GetField("m_Arguments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(list[i]);
				return (string)args.GetType().GetProperty("stringArgument").GetValue(args);
			}
			catch { return ""; }
		}

		static void Step(string what, Action a)
		{
			int before = Problems.Count;
			Current = what;
			try { a(); }
			catch (Exception e) { Problems.Add($"{what}: {e.GetType().Name}: {e.Message} @ {FirstFrame(e.StackTrace)}"); }
			Write((Problems.Count > before ? "FAIL " : "ok   ") + what);
		}

		static string Current = "start";

		static void OnLog(string msg, string stack, LogType type)
		{
			if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
				Problems.Add($"{Current}: {type}: {msg} @ {FirstFrame(stack)}");
		}

		static string FirstFrame(string stack)
		{
			if (string.IsNullOrEmpty(stack)) return "";
			var line = stack.Split('\n')[0].Trim();
			return line.Length > 200 ? line.Substring(0, 200) : line;
		}

		static void WriteTexts(Transform root, string prefix)
		{
			foreach (var t in root.GetComponentsInChildren<Text>(false))
				if (!string.IsNullOrWhiteSpace(t.text)) Write(prefix + t.text.Replace("\n", "\\n"));
		}

		static void Write(string line) { Out.WriteLine(line); Out.Flush(); }

		static string PathOf(Transform t)
		{
			var parts = new List<string>();
			for (; t != null; t = t.parent) parts.Add(t.name);
			parts.Reverse();
			return string.Join("/", parts);
		}

		static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

		static IEnumerator Shot(string dir, string name)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
			yield return null;
		}
	}
}
