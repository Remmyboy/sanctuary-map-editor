// Hides the tool tabs whose edits a Sanctuary save does not write, so nobody
// spends an hour on FA lighting or props that vanish on save:
//   Lighting  FA's lighting model (Sanctuary's ~40 lighting fields pass
//             through untouched; the view uses them read-only)
//   Props     FA prop blueprints (Sanctuary props pass through, not shown yet)
//   Decals    FA decals (Sanctuary map decals draw invisibly in game anyway)
//   Units     FA units
// Shown again when an FA map is opened.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sanctuary
{
	public static class SanctuaryUi
	{
		static readonly HashSet<string> FaOnlyTabs = new HashSet<string> { "Lighting", "Props", "Decals", "Units" };
		static List<GameObject> hidden;

		public static void ApplyMode(bool sanctuary)
		{
			if (hidden == null) hidden = FindTabs();
			foreach (var go in hidden)
				if (go != null) go.SetActive(!sanctuary);
		}

		/// The toolbar is the parent of the button labelled "Terrain"; its
		/// FA-only siblings are found by label.
		static List<GameObject> FindTabs()
		{
			var result = new List<GameObject>();
			var editing = MapLuaParser.Current.EditMenu;
			Transform bar = null;
			foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				if (Label(b) == "Terrain" && (bar == null || IsUnder(b.transform, editing.transform))) bar = b.transform.parent;
			if (bar == null) { Debug.LogWarning("Sanctuary UI: tool tab bar not found"); return result; }
			foreach (var b in bar.GetComponentsInChildren<Button>(true))
				if (FaOnlyTabs.Contains(Label(b))) result.Add(b.gameObject);
			// Two-row toolbars: the second row is a sibling of the first.
			if (result.Count < FaOnlyTabs.Count && bar.parent != null)
				foreach (var b in bar.parent.GetComponentsInChildren<Button>(true))
					if (FaOnlyTabs.Contains(Label(b)) && !result.Contains(b.gameObject)) result.Add(b.gameObject);
			Debug.Log($"Sanctuary UI: {result.Count} FA-only tool tabs found under {bar.parent?.name}/{bar.name}");
			return result;
		}

		static string Label(Button b)
		{
			var t = b.GetComponentInChildren<Text>(true);
			return t != null ? t.text.Trim() : null;
		}

		static bool IsUnder(Transform t, Transform root)
		{
			for (; t != null; t = t.parent) if (t == root) return true;
			return false;
		}
	}
}
