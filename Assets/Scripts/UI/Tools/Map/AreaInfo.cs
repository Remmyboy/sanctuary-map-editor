using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class AreaInfo : MonoBehaviour {

	public static AreaInfo Current;

	public Toggle BorderRelative;
	public Toggle Rounding;
	public GameObject AreaPrefab;
	public Transform Pivot;
	public ToggleGroup ToggleGrp;

	List<AreaListObject> Created = new List<AreaListObject>();

	
	public static void CleanSelection()
	{
		HideArea = false;
		SelectedArea = null;

		if (Current != null)
		{
			Current.DeselectAll();
		}
	}
	

	private void Awake()
	{
		Current = this;
	}

	private void OnEnable()
	{
		MapLuaParser.OnMapLoaded.AddListener(UpdateList);
		if (Sanctuary) SanctuaryLayout();
		UpdateList();
	}

	private void OnDisable()
	{
		MapLuaParser.OnMapLoaded.RemoveListener(UpdateList);
		drawing = false;
		outline?.Hide();
	}

	#region Sanctuary: one playable area
	// A Sanctuary map has one area, the playable area (FA maps had a list of
	// script areas). The page shows that one, without add/remove, and the
	// area can be dragged out on the map.
	static bool Sanctuary => global::Sanctuary.SanctuaryMapLoader.IsActive;
	Text hint;
	bool drawing, drawUndoTaken;
	Vector2 drawFrom;
	global::Sanctuary.SanctuaryOutline outline;

	void SanctuaryLayout()
	{
		foreach (var t in GetComponentsInChildren<Transform>(true))
			if (t.name == "AddNext") t.gameObject.SetActive(false);
		foreach (var label in GetComponentsInChildren<Text>(true))
		{
			if (label.text == "Areas") label.text = "Playable area";
			else if (hint == null && label.text == "List of all areas") hint = label;
		}
	}

	void SanctuaryHint()
	{
		if (hint == null) return;
		var a = MapLuaParser.Current.SaveLuaFile.Data.areas;
		string size = a.Length > 0 ? $"{a[0].rectangle.width - a[0].rectangle.x:0} x {a[0].rectangle.height - a[0].rectangle.y:0} m. " : "None set. ";
		hint.text = size + "Edges W, N, E, S, or drag on the map";
	}

	private void Update()
	{
		if (!Sanctuary) return;
		var edit = MapLuaParser.Current?.EditMenu;
		if (edit == null) return;
		if (!drawing && Input.GetMouseButtonDown(0) && edit.MauseOnGameplay && MouseOnMap(out var start))
		{
			drawing = true; drawUndoTaken = false; drawFrom = Snap(start);
		}
		if (!drawing) return;
		if (!Input.GetMouseButton(0)) { drawing = false; outline?.Hide(); UpdateList(); return; }
		if (!MouseOnMap(out var at)) return;
		at = Snap(at);
		if (!drawUndoTaken)
		{
			if (Mathf.Abs(at.x - drawFrom.x) < 8 && Mathf.Abs(at.y - drawFrom.y) < 8) return; // a click, not a drag
			UndoHistory.HistorySanctuaryState.Register(CaptureAreas);
			drawUndoTaken = true;
		}
		SetPlayableArea(drawFrom, at);
		if (outline == null) outline = new global::Sanctuary.SanctuaryOutline("playable area", new Color(1f, 0.85f, 0.3f, 1f));
		outline.Rect(Mathf.Min(drawFrom.x, at.x), Mathf.Min(drawFrom.y, at.y), Mathf.Max(drawFrom.x, at.x), Mathf.Max(drawFrom.y, at.y));
	}

	/// The playable area between two corners in Sanctuary metres (x east,
	/// z north), as a drag on the map sets it.
	public static void SetPlayableArea(Vector2 a, Vector2 b)
	{
		var data = MapLuaParser.Current.SaveLuaFile.Data;
		if (data.areas.Length == 0)
			data.areas = new[] { new MapLua.SaveLua.Areas { Name = global::Sanctuary.MapFormat.SanMapDocument.PlayableAreaKey } };
		float size = global::Sanctuary.SanctuaryMapLoader.Size;
		// FA corner pairs (x0, y0, x1, y1), y measured from the north edge.
		float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
		float z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y);
		data.areas[0].rectangle = new Rect(x0, size - z1, x1, size - z0);
		MapLuaParser.Current.UpdateArea(Current == null || Current.Rounding.isOn);
		if (Current != null) Current.SanctuaryHint();
	}

	Vector2 Snap(Vector2 p)
	{
		float step = Rounding.isOn ? 4f : 1f, size = global::Sanctuary.SanctuaryMapLoader.Size;
		return new Vector2(Mathf.Clamp(Mathf.Round(p.x / step) * step, 0, size), Mathf.Clamp(Mathf.Round(p.y / step) * step, 0, size));
	}

	/// For undo: the area list and the playable area's rectangle as now.
	public static Action CaptureAreas()
	{
		var data = MapLuaParser.Current.SaveLuaFile.Data;
		var list = data.areas.ToArray();
		var rects = list.Select(a => a.rectangle).ToArray();
		return () =>
		{
			for (int i = 0; i < list.Length; i++) list[i].rectangle = rects[i];
			data.areas = list.ToArray();
			if (Current != null) Current.UpdateList();
			else MapLuaParser.Current.UpdateArea();
		};
	}

	static bool MouseOnMap(out Vector2 at)
	{
		at = default;
		var cam = Camera.main;
		var col = ScmapEditor.Current.Teren.GetComponent<TerrainCollider>();
		if (cam == null || col == null) return false;
		if (!col.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 100000f)) return false;
		at = new Vector2(hit.point.x * 10f, hit.point.z * 10f + global::Sanctuary.SanctuaryMapLoader.Size);
		return true;
	}
	#endregion

	public void SwitchBorderRelative()
	{
		UpdateList();
	}

	public void SwitchPlayableAreaRounding()
	{
		//Rounding.isOn = !Rounding.isOn;

		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	public void UpdateList()
	{
		Clean();
		Generate();

		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	void Clean()
	{
		for(int i = 0; i < Created.Count; i++)
		{
			Destroy(Created[i].gameObject);
		}
		Created = new List<AreaListObject>();
	}

	void Generate()
	{
		MapLua.SaveLua.Areas[] Areas = MapLuaParser.Current.SaveLuaFile.Data.areas;

		bool ToogleFound = false;

		for (int i = 0; i < (Sanctuary ? Math.Min(1, Areas.Length) : Areas.Length); i++)
		{
			GameObject NewArea = Instantiate(AreaPrefab, Pivot);
			AreaListObject AreaObject = NewArea.GetComponent<AreaListObject>();

			AreaObject.Controler = this;
			AreaObject.InstanceId = i;
			AreaObject.Name.SetTextWithoutNotify(Areas[i].Name);
			if (Sanctuary)
			{
				// Always shown, never removed; the name is the game's key.
				AreaObject.Name.SetTextWithoutNotify("Playable area");
				AreaObject.Name.interactable = false;
				AreaObject.Selected.gameObject.SetActive(false);
				var remove = NewArea.transform.Find("Remove");
				if (remove != null) remove.gameObject.SetActive(false);
			}
			AreaObject.SizeX.SetTextWithoutNotify(Areas[i].rectangle.x.ToString());
			AreaObject.SizeY.SetTextWithoutNotify(Areas[i].rectangle.y.ToString());

			if (BorderRelative.isOn)
			{
				AreaObject.SizeWidth.SetTextWithoutNotify((ScmapEditor.Current.map.Width - Areas[i].rectangle.width).ToString());
				AreaObject.SizeHeight.SetTextWithoutNotify((ScmapEditor.Current.map.Height - Areas[i].rectangle.height).ToString());
			}
			else
			{
				AreaObject.SizeWidth.SetTextWithoutNotify(Areas[i].rectangle.width.ToString());
				AreaObject.SizeHeight.SetTextWithoutNotify(Areas[i].rectangle.height.ToString());
			}

			AreaObject.Selected.group = ToggleGrp;


			if(Areas[i] == SelectedArea)
			{
				AreaObject.Selected.SetIsOnWithoutNotify(true);
				ToogleFound = true;
			}

			Created.Add(AreaObject);
		}

		if (SelectedArea != null && !ToogleFound)
		{
			HideArea = true;
		}
		if (Sanctuary) { HideArea = false; SanctuaryHint(); }
	}


#region Selection
	public static bool HideArea = false;
	public static MapLua.SaveLua.Areas SelectedArea;

	public void ToggleSelected()
	{
		bool anyAreaSelected = IsAnySelected();
		bool noneSelected = !anyAreaSelected;
		HideArea &= noneSelected;
		
		if (noneSelected || HideArea)
		{
			DeselectAll();
		}
		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	public void SelectArea(int InstanceID)
	{
		HideArea = false;
		SelectedArea = MapLuaParser.Current.SaveLuaFile.Data.areas[InstanceID];
		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	public void DeselectArea(int InstanceID)
	{
		if (SelectedArea == MapLuaParser.Current.SaveLuaFile.Data.areas[InstanceID])
			SelectedArea = null;
		if (IsAnySelected())
			return;
		SetSelectionToHide();
	}

	private void DeselectAll()
	{
		foreach (var area in Created)
		{
			area.Selected.SetIsOnWithoutNotify(false);
		}
		SelectedArea = null;
		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	private void SetSelectionToHide()
	{
		HideArea = true;
		SelectedArea = null;
		MapLuaParser.Current.UpdateArea(Rounding.isOn);
	}

	private bool IsAnySelected()
	{
		foreach (var area in Created)
		{
			if (area.Selected.isOn)
				return true;
		}

		return false;
	}
#endregion


#region UI
	public void OnValuesChange(int instanceID)
	{
		var editedArea = Created[instanceID];
		var targetDataObject = MapLuaParser.Current.SaveLuaFile.Data.areas[instanceID];
		
		Undo.RegisterUndo(new UndoHistory.HistoryAreaChange(), new UndoHistory.HistoryAreaChange.AreaChangeParam(targetDataObject));

		
		if (!Sanctuary) targetDataObject.Name = editedArea.Name.text;

		targetDataObject.rectangle.x = TextNumbers.StringToFloat(editedArea.SizeX.text);
		targetDataObject.rectangle.y = TextNumbers.StringToFloat(editedArea.SizeY.text);

		if (BorderRelative.isOn)
		{
			targetDataObject.rectangle.width = ScmapEditor.Current.map.Width - TextNumbers.StringToFloat(editedArea.SizeWidth.text);
			targetDataObject.rectangle.height = ScmapEditor.Current.map.Height - TextNumbers.StringToFloat(editedArea.SizeHeight.text);
		}
		else
		{
			targetDataObject.rectangle.width = TextNumbers.StringToFloat(editedArea.SizeWidth.text);
			targetDataObject.rectangle.height = TextNumbers.StringToFloat(editedArea.SizeHeight.text);
		}

		if (SelectedArea == targetDataObject || Sanctuary)
		{
			MapLuaParser.Current.UpdateArea(Rounding.isOn);
		}
		if (Sanctuary) SanctuaryHint();
	}
#endregion


	public void AddNew()
	{

		Undo.RegisterUndo(new UndoHistory.HistoryAreasChange());

		List<MapLua.SaveLua.Areas> Areas = MapLuaParser.Current.SaveLuaFile.Data.areas.ToList();
		MapLua.SaveLua.Areas NewArea = new MapLua.SaveLua.Areas();

		string DefaultMapArea = "New Area";
		int NextAreaName = 0;
		bool FoundGoodName = false;
		while (!FoundGoodName)
		{
			FoundGoodName = true;
			string TestName = DefaultMapArea + ((NextAreaName > 0) ? (" " + NextAreaName.ToString()) : (""));
			for (int i = 0; i < MapLuaParser.Current.SaveLuaFile.Data.areas.Length; i++)
			{
				if(MapLuaParser.Current.SaveLuaFile.Data.areas[i].Name == TestName)
				{
					FoundGoodName = false;
					NextAreaName++;
					break;
				}
			}
		}

		NewArea.Name = DefaultMapArea + ((NextAreaName > 0)?(" " + NextAreaName.ToString()) :(""));
		NewArea.rectangle = new Rect(0, 0, ScmapEditor.Current.map.Width, ScmapEditor.Current.map.Height);
		Areas.Add(NewArea);

		MapLuaParser.Current.SaveLuaFile.Data.areas = Areas.ToArray();
		UpdateList();

	}

	public void Remove(int instanceID)
	{
		Undo.RegisterUndo(new UndoHistory.HistoryAreasChange());

		if (SelectedArea == MapLuaParser.Current.SaveLuaFile.Data.areas[instanceID])
		{
			SetSelectionToHide();
		}

		List<MapLua.SaveLua.Areas> Areas = MapLuaParser.Current.SaveLuaFile.Data.areas.ToList();

		Areas.RemoveAt(instanceID);

		MapLuaParser.Current.SaveLuaFile.Data.areas = Areas.ToArray();


		UpdateList();
	}
}
