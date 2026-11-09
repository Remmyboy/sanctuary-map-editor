// The Props panel (IMGUI, built at runtime): show/hide props; paint a chosen
// prop type with a brush; erase props under the brush; or select one prop to
// move (drag), turn, scale, drop to the ground or delete. A circle on the
// ground shows the brush, a ring the selected prop. Every edit is one undo
// step (Ctrl+Z / Ctrl+Y): a whole brush stroke, a whole drag, each button.
using System;
using System.Collections.Generic;
using System.Linq;
using Sanctuary.MapFormat;
using UndoHistory;
using UnityEngine;

namespace Sanctuary
{
	public class SanctuaryPropsPanel : MonoBehaviour
	{
		enum Mode { Off, Paint, Erase, Select }

		static SanctuaryPropsPanel current;
		bool open;
		Mode mode = Mode.Off;
		float radius = 15f;
		int count = 5;
		bool eraseOnlyBrushType;
		string group, brushType;
		SortedDictionary<string, List<string>> catalog;
		Vector2 scroll;
		Rect window = new Rect(320, 70, 380, 640);
		readonly System.Random rng = new System.Random();
		float nextStroke;
		bool overUi;

		// The selected prop (Select mode) and the drag in progress.
		string selType;
		SanTransform selProp;
		bool dragging, dragUndoTaken;
		Vector2 dragOffset, dragStartMouse;

		SanctuaryOutline brushRing, selRing;

		// The opener sits in the middle of the menu bar, clear of the menus'
		// drop-downs (IMGUI draws over them and would take their clicks).
		static Rect Opener => new Rect(Screen.width / 2f - 75, 3, 150, 22);

		public static void Ensure()
		{
			if (current == null) current = new GameObject("SanctuaryPropsPanel").AddComponent<SanctuaryPropsPanel>();
		}

		public static void Close()
		{
			if (current != null) { current.open = false; current.mode = Mode.Off; current.Deselect(); }
		}

		void OnGUI()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			if (GUI.Button(Opener, $"Props ({SanctuaryProps.Count:n0})" + (SanctuaryProps.Dirty ? " *" : "")))
			{
				open = !open;
				if (open && catalog == null)
				{
					catalog = SanctuaryProps.Catalog();
					group = catalog.Keys.FirstOrDefault();
				}
				if (!open) { mode = Mode.Off; Deselect(); }
			}
			if (open) window = GUI.Window(0x5A9D, window, Draw, "Props");
		}

		void Draw(int id)
		{
			SanctuaryProps.Visible = GUILayout.Toggle(SanctuaryProps.Visible, "Show props");
			GUILayout.BeginHorizontal();
			foreach (Mode m in Enum.GetValues(typeof(Mode)))
				if (GUILayout.Toggle(mode == m, m.ToString(), "Button") && mode != m)
				{
					mode = m;
					if (m != Mode.Select) Deselect();
				}
			GUILayout.EndHorizontal();

			if (mode == Mode.Select) { DrawSelected(); GUI.DragWindow(); return; }

			GUILayout.Label($"Brush radius {radius:0} m   ([ and ] change it)");
			radius = GUILayout.HorizontalSlider(radius, 1f, 150f);
			if (mode != Mode.Erase)
			{
				GUILayout.Label($"Props per stroke {count}");
				count = Mathf.RoundToInt(GUILayout.HorizontalSlider(count, 1f, 40f));
			}
			else eraseOnlyBrushType = GUILayout.Toggle(eraseOnlyBrushType, "Erase only the type picked below");

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
					if (GUILayout.Toggle(bp == brushType, name, "Button")) brushType = bp;
				}
			GUILayout.EndScrollView();
			GUILayout.Label(brushType == null ? "Pick a prop to paint." : "Picked: " + System.IO.Path.GetFileNameWithoutExtension(brushType));
			GUILayout.Label("Click or drag on the map. Ctrl+Z undoes a stroke. Saving writes props only if they changed.");
			GUI.DragWindow();
		}

		void DrawSelected()
		{
			if (selProp != null && !SanctuaryProps.Contains(selType, selProp)) Deselect(); // undone or erased
			if (selProp == null)
			{
				GUILayout.Label("Click a prop on the map to select it.\nDrag to move it; Delete removes it.");
				return;
			}
			var p = selProp;
			GUILayout.Label(System.IO.Path.GetFileNameWithoutExtension(selType));
			GUILayout.Label($"Position  x {p.Position.X:0.0}  z {p.Position.Z:0.0}  height {p.Position.Y:0.0} m");
			GUILayout.Label($"Heading {Heading(p):0}°   scale {p.Scale.Y:0.00}");
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Turn -15°")) Edit(t => Turn(t, -15));
			if (GUILayout.Button("Turn +15°")) Edit(t => Turn(t, 15));
			if (GUILayout.Button("Turn 90°")) Edit(t => Turn(t, 90));
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Smaller")) Edit(t => Scaled(t, 1 / 1.1));
			if (GUILayout.Button("Bigger")) Edit(t => Scaled(t, 1.1));
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Drop to ground")) Edit(t => Moved(t, t.Position.X, t.Position.Z));
			if (GUILayout.Button("Delete")) DeleteSelected();
			GUILayout.EndHorizontal();
			GUILayout.Label("Drag the prop to move it (it stays on the ground). Ctrl+Z undoes.");
		}

		void Update()
		{
			var edit = MapLuaParser.Current?.EditMenu;
			if (edit == null || !SanctuaryMapLoader.IsActive) { HideRings(); return; }
			if (brushRing == null)
			{
				brushRing = new SanctuaryOutline("props brush", new Color(1f, 0.85f, 0.3f, 0.9f));
				selRing = new SanctuaryOutline("selected prop", new Color(0.3f, 0.9f, 1f, 1f));
			}

			// IMGUI windows are invisible to the editor's pointer events.
			var mouse = Input.mousePosition;
			var gui = new Vector2(mouse.x, Screen.height - mouse.y);
			bool inside = (open && window.Contains(gui)) || Opener.Contains(gui);
			if (inside) { edit.MauseOnGameplay = false; overUi = true; }
			else if (overUi)
			{
				overUi = false;
				var es = UnityEngine.EventSystems.EventSystem.current;
				edit.MauseOnGameplay = es == null || !es.IsPointerOverGameObject();
			}

			if (selProp != null && SanctuaryProps.Contains(selType, selProp)) selRing.Circle((float)selProp.Position.X, (float)selProp.Position.Z, Mathf.Max(2f, (float)selProp.Scale.Y * 2f));
			else selRing.Hide();

			if (!open || mode == Mode.Off) { brushRing.Hide(); dragging = false; return; }

			if (!CameraControler.IsInputFieldFocused())
			{
				if (Input.GetKeyDown(KeyCode.LeftBracket)) radius = Mathf.Max(1f, radius / 1.25f);
				if (Input.GetKeyDown(KeyCode.RightBracket)) radius = Mathf.Min(150f, radius * 1.25f);
				if (mode == Mode.Select && selProp != null && Input.GetKeyDown(KeyCode.Delete)) DeleteSelected();
			}

			bool onMap = edit.MauseOnGameplay && !inside;
			Vector2 at = default;
			bool hit = onMap && MouseOnMap(out at);
			if (hit && mode != Mode.Select) brushRing.Circle(at.x, at.y, radius);
			else brushRing.Hide();

			if (mode == Mode.Select) { UpdateSelect(hit, at); return; }

			if (!hit || !Input.GetMouseButton(0)) return;
			if (Input.GetMouseButtonDown(0))
			{
				HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
				nextStroke = 0;
			}
			if (Time.unscaledTime < nextStroke) return;
			nextStroke = Time.unscaledTime + 0.12f;
			if (mode == Mode.Paint && brushType != null) SanctuaryProps.Paint(brushType, at.x, at.y, radius, count, rng);
			else if (mode == Mode.Erase) SanctuaryProps.Erase(at.x, at.y, radius, eraseOnlyBrushType ? brushType : null);
		}

		void UpdateSelect(bool hit, Vector2 at)
		{
			if (hit && Input.GetMouseButtonDown(0))
			{
				// Pick within a few metres, more when zoomed out.
				float reach = Mathf.Clamp(Camera.main.transform.position.y * 10f * 0.02f, 3f, 25f);
				if (SanctuaryProps.Pick(at.x, at.y, reach, out var type, out var prop))
				{
					selType = type; selProp = prop;
					dragging = true; dragUndoTaken = false;
					dragOffset = new Vector2((float)prop.Position.X - at.x, (float)prop.Position.Z - at.y);
					dragStartMouse = Input.mousePosition;
				}
				else Deselect();
			}
			if (!Input.GetMouseButton(0)) { dragging = false; return; }
			if (!dragging || !hit || selProp == null) return;
			// A click without a drag only selects.
			if (!dragUndoTaken && Vector2.Distance(dragStartMouse, Input.mousePosition) < 4f) return;
			if (!dragUndoTaken) { HistorySanctuaryState.Register(SanctuaryProps.Snapshot); dragUndoTaken = true; }
			var moved = Moved(selProp, at.x + dragOffset.x, at.y + dragOffset.y);
			if (SanctuaryProps.Replace(selType, selProp, moved)) selProp = moved;
		}

		void Edit(Func<SanTransform, SanTransform> change)
		{
			if (selProp == null) return;
			HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			var next = change(selProp);
			if (SanctuaryProps.Replace(selType, selProp, next)) selProp = next;
		}

		void DeleteSelected()
		{
			if (selProp == null) return;
			HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			SanctuaryProps.Remove(selType, selProp);
			Deselect();
		}

		void Deselect() { selProp = null; selType = null; dragging = false; selRing?.Hide(); }

		void HideRings() { brushRing?.Hide(); selRing?.Hide(); }

		static bool MouseOnMap(out Vector2 at)
		{
			at = default;
			var cam = Camera.main;
			var col = ScmapEditor.Current.Teren.GetComponent<TerrainCollider>();
			if (cam == null || col == null) return false;
			if (!col.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 100000f)) return false;
			at = new Vector2(hit.point.x * 10f, hit.point.z * 10f + SanctuaryMapLoader.Size);
			return true;
		}

		// Props are replaced, never changed in place (undo keeps old lists).
		static SanTransform Copy(SanTransform t) => new SanTransform { Position = t.Position, Rotation = t.Rotation, Scale = t.Scale };

		internal static SanTransform Moved(SanTransform t, double x, double z)
		{
			double size = SanctuaryMapLoader.Size;
			x = Math.Max(0, Math.Min(size, x)); z = Math.Max(0, Math.Min(size, z));
			var c = Copy(t);
			c.Position = new SanVec3(Math.Round(x, 3), Math.Round(SanctuaryProps.GroundAt(x, z), 3), Math.Round(z, 3));
			return c;
		}

		internal static SanTransform Turn(SanTransform t, float degrees)
		{
			var q = Quaternion.AngleAxis(degrees, Vector3.up) * ToUnity(t.Rotation);
			var c = Copy(t);
			c.Rotation = new SanQuat(Math.Round(q.x, 7), Math.Round(q.y, 7), Math.Round(q.z, 7), Math.Round(q.w, 7));
			return c;
		}

		internal static SanTransform Scaled(SanTransform t, double f)
		{
			var c = Copy(t);
			c.Scale = new SanVec3(Math.Round(t.Scale.X * f, 4), Math.Round(t.Scale.Y * f, 4), Math.Round(t.Scale.Z * f, 4));
			return c;
		}

		static Quaternion ToUnity(SanQuat r)
		{
			var q = new Quaternion((float)r.X, (float)r.Y, (float)r.Z, (float)r.W);
			return q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0 ? Quaternion.identity : q.normalized;
		}

		internal static float Heading(SanTransform t)
		{
			float y = ToUnity(t.Rotation).eulerAngles.y;
			return y < 0 ? y + 360 : y;
		}
	}
}
