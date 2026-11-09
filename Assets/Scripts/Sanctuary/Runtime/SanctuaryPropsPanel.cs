// The Props panel (IMGUI, built at runtime): show/hide props, paint a chosen
// prop type with a brush, or erase props under the brush.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sanctuary
{
	public class SanctuaryPropsPanel : MonoBehaviour
	{
		enum Mode { Off, Paint, Erase }

		static SanctuaryPropsPanel current;
		bool open;
		Mode mode = Mode.Off;
		float radius = 15f;
		int count = 5;
		bool eraseOnlySelected;
		string group, selected;
		SortedDictionary<string, List<string>> catalog;
		Vector2 scroll;
		Rect window = new Rect(320, 70, 380, 600);
		readonly System.Random rng = new System.Random();
		float nextStroke;
		bool overUi;

		public static void Ensure()
		{
			if (current == null) current = new GameObject("SanctuaryPropsPanel").AddComponent<SanctuaryPropsPanel>();
		}

		public static void Close()
		{
			if (current != null) { current.open = false; current.mode = Mode.Off; }
		}

		void OnGUI()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			// The opener sits in the tool-tab row's free second line.
			if (GUI.Button(new Rect(8, 62, 150, 22), $"Props ({SanctuaryProps.Count:n0})" + (SanctuaryProps.Dirty ? " *" : "")))
			{
				open = !open;
				if (open && catalog == null)
				{
					catalog = SanctuaryProps.Catalog();
					group = catalog.Keys.FirstOrDefault();
				}
				if (!open) mode = Mode.Off;
			}
			if (open) window = GUI.Window(0x5A9D, window, Draw, "Props");
		}

		void Draw(int id)
		{
			SanctuaryProps.Visible = GUILayout.Toggle(SanctuaryProps.Visible, "Show props");
			GUILayout.BeginHorizontal();
			foreach (Mode m in Enum.GetValues(typeof(Mode)))
				if (GUILayout.Toggle(mode == m, m.ToString(), "Button")) mode = m;
			GUILayout.EndHorizontal();
			GUILayout.Label($"Brush radius {radius:0} m");
			radius = GUILayout.HorizontalSlider(radius, 1f, 150f);
			if (mode != Mode.Erase)
			{
				GUILayout.Label($"Props per stroke {count}");
				count = Mathf.RoundToInt(GUILayout.HorizontalSlider(count, 1f, 40f));
			}
			else eraseOnlySelected = GUILayout.Toggle(eraseOnlySelected, "Erase only the selected type");

			GUILayout.Space(6);
			GUILayout.BeginHorizontal();
			foreach (var g in catalog.Keys)
				if (GUILayout.Toggle(g == group, g.Replace('_', ' '), "Button")) group = g;
			GUILayout.EndHorizontal();
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(330));
			if (group != null && catalog.TryGetValue(group, out var list))
				foreach (var bp in list)
				{
					string name = System.IO.Path.GetFileNameWithoutExtension(bp);
					if (GUILayout.Toggle(bp == selected, name, "Button")) selected = bp;
				}
			GUILayout.EndScrollView();
			GUILayout.Label(selected == null ? "Pick a prop to paint." : "Selected: " + System.IO.Path.GetFileNameWithoutExtension(selected));
			GUILayout.Label("Click or drag on the map. Saving writes props only if you changed them.");
			GUI.DragWindow();
		}

		void Update()
		{
			var edit = MapLuaParser.Current?.EditMenu;
			if (edit == null || !SanctuaryMapLoader.IsActive) return;

			// IMGUI windows are invisible to the editor's pointer events.
			var mouse = Input.mousePosition;
			var gui = new Vector2(mouse.x, Screen.height - mouse.y);
			bool inside = (open && window.Contains(gui)) || new Rect(8, 62, 150, 22).Contains(gui);
			if (inside) { edit.MauseOnGameplay = false; overUi = true; return; }
			if (overUi)
			{
				overUi = false;
				var es = UnityEngine.EventSystems.EventSystem.current;
				edit.MauseOnGameplay = es == null || !es.IsPointerOverGameObject();
			}

			if (!open || mode == Mode.Off || !edit.MauseOnGameplay || !Input.GetMouseButton(0)) return;
			if (Time.unscaledTime < nextStroke) return;
			nextStroke = Time.unscaledTime + 0.12f;

			var cam = Camera.main;
			var col = ScmapEditor.Current.Teren.GetComponent<TerrainCollider>();
			if (cam == null || col == null) return;
			if (!col.Raycast(cam.ScreenPointToRay(mouse), out var hit, 100000f)) return;
			float x = hit.point.x * 10f, z = hit.point.z * 10f + SanctuaryMapLoader.Size;
			if (mode == Mode.Paint && selected != null) SanctuaryProps.Paint(selected, x, z, radius, count, rng);
			else if (mode == Mode.Erase) SanctuaryProps.Erase(x, z, radius, eraseOnlySelected ? selected : null);
		}
	}
}
