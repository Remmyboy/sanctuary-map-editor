// ***************************************************************************************
// * Simple SupCom map LUA parser
// * TODO : should read all values. Right now it only search for known, hardcoded values
// ***************************************************************************************

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EditMap;
using NLua;
using MapLua;
using UnityEngine.Events;

public partial class MapLuaParser : MonoBehaviour
{
	public static MapLuaParser Current;
	public static UnityEvent OnMapLoaded = new();

	[Header("LUA")]
	public string LoadedMapFolder;
	public ScenarioLua ScenarioLuaFile;
	public SaveLua SaveLuaFile;
	public TablesLua TablesLuaFile;
	public TextAsset SaveLuaHeader;
	public TextAsset SaveLuaFooter;
	public TextAsset DefaultScript;
	public TextAsset AdaptiveScript;
	public TextAsset AdaptiveOptions;

	#region Variables
	[Header("Core")]
	public ScmapEditor HeightmapControler;
	public Editing EditMenu;
	public Undo History;

	[Header("Map")]
	public string FolderParentPath;
	public string FolderName;
	public string ScenarioFileName;

	public static string LoadedMapFolderPath
	{
		get
		{
			return Current.FolderParentPath + Current.FolderName + "/";
		}
	}

	public static string RelativeLoadedMapFolderPath
	{
		get
		{
			return "maps/" + Current.FolderName + "/";
		}
	}

	//public		CameraControler	CamControll;
	[Header("UI")]
	public GameObject Background;
	public GenericInfoPopup InfoPopup;
	public GenericInfoPopup ErrorPopup;

	[Header("Local Data")]
	public Vector3 MapCenterPoint;

	public int ScriptId = 0;

	public static string BackupPath;
	public static string StructurePath;


	#endregion


	void Awake()
	{
		Current = this;
		LoadStructurePaths();

	}

	public static void LoadStructurePaths()
	{
		ICSharpCode.SharpZipLib.Zip.ZipConstants.DefaultCodePage = 0;
		StructurePath = GetDataPath() + "/Structure/";
	}

	public static bool IsMapLoaded
	{
		get
		{
			return !string.IsNullOrEmpty(Current.FolderName) && !string.IsNullOrEmpty(Current.ScenarioFileName) && !string.IsNullOrEmpty(Current.FolderParentPath);
		}
	}

	public void ResetUI()
	{
		WindowStateSever.WindowStateSaver.ChangeWindowName("");

		EditMenu.ButtonFunction("Map");
		EditMenu.gameObject.SetActive(false);
		Background.SetActive(true);

		FolderParentPath = "";
		FolderName = "";
		ScenarioFileName = "";

		EditMenu.TexturesMenu.ResetVisibility();
		EditMenu.WaterMenu.ResetUi();

	}

	#region Loading
	// Maps are opened by Sanctuary.SanctuaryMapLoader. The SupCom
	// scenario/scmap loader that lived here is gone.

	public static bool LoadingMapProcess = false;

	public void ReturnLoadingWithError(string Error)
	{
		ErrorPopup.Show(true, Error);
		ErrorPopup.InvokeHide();
	}
	#endregion

	#region SaveMap
	public bool BackupFiles = true;

	/// File > Save (and Ctrl+S): writes the open Sanctuary map in place,
	/// with a backup of the files it rewrites (Sanctuary.SanctuaryMapSaver).
	public void SaveMap(bool Backup = true)
	{
		if (!IsMapLoaded || !Sanctuary.SanctuaryMapLoader.IsActive)
			return;
		BackupFiles = Backup;
		StartCoroutine(Sanctuary.SanctuaryMapSaver.Save());
	}

	public static bool SavingMapProcess = false;

	public static string GetUniqueId()
	{
		return System.DateTime.Now.Month.ToString() + System.DateTime.Now.Day.ToString() + System.DateTime.Now.Hour.ToString() + System.DateTime.Now.Minute.ToString() + System.DateTime.Now.Second.ToString();
	}

	public static string GetDataPath()
	{
		string Path = Application.dataPath;
#if UNITY_EDITOR
		Path = Path.Replace("/Assets", "");
#endif
		return Path;
	}
	#endregion

	#region Map functions
	public void SortArmys()
	{

	}

	public Rect GetAreaRect(bool Round = false)
	{
		if (SaveLuaFile.Data.areas.Length > 0 && !AreaInfo.HideArea)
		{
			//int bigestAreaId = 0;
			Rect bigestAreaRect = new Rect(SaveLuaFile.Data.areas[0].rectangle);

			if (AreaInfo.SelectedArea != null)
			{
				bigestAreaRect = AreaInfo.SelectedArea.rectangle;
			}
			else
			{
				for (int i = 1; i < SaveLuaFile.Data.areas.Length; i++)
				{
					if (bigestAreaRect.x > SaveLuaFile.Data.areas[i].rectangle.x)
						bigestAreaRect.x = SaveLuaFile.Data.areas[i].rectangle.x;

					if (bigestAreaRect.y > SaveLuaFile.Data.areas[i].rectangle.y)
						bigestAreaRect.y = SaveLuaFile.Data.areas[i].rectangle.y;

					if (bigestAreaRect.width < SaveLuaFile.Data.areas[i].rectangle.width)
						bigestAreaRect.width = SaveLuaFile.Data.areas[i].rectangle.width;

					if (bigestAreaRect.height < SaveLuaFile.Data.areas[i].rectangle.height)
						bigestAreaRect.height = SaveLuaFile.Data.areas[i].rectangle.height;
				}
			}

			if (Round)
			{
				bigestAreaRect.x -= bigestAreaRect.x % 4;
				bigestAreaRect.width -= bigestAreaRect.width % 4;

				bigestAreaRect.y -= bigestAreaRect.y % 4;
				bigestAreaRect.height -= bigestAreaRect.height % 4;

			}

			float LastY = bigestAreaRect.y;
			bigestAreaRect.y = ScmapEditor.Current.map.Width - bigestAreaRect.height;
			bigestAreaRect.height = ScmapEditor.Current.map.Width - LastY;
			return bigestAreaRect;
		}
		else
		{

			return new Rect(0, 0, ScmapEditor.Current.map.Width, ScmapEditor.Current.map.Height);
		}

	}


	static Bounds PlayableAreaBounds = new Bounds();
	void SetBounds(Rect CurrentArea)
	{
		Vector3 Point0 = CurrentArea.min * 0.1f;
		Vector3 Point1 = CurrentArea.max * 0.1f;

		Point0.z = -Point1.y;
		Point1.z = -Point0.y;

		Point0.y = -0.1f;
		// FA terrain tops out at 25.6 units; Sanctuary maps go up to 160.
		Point1.y = Mathf.Max(26f, ScmapEditor.TerrainHeight + 1f);

		//Debug.Log(Point0);
		//Debug.Log(Point1);

		PlayableAreaBounds.min = Point0;
		PlayableAreaBounds.max = Point1;
	}

	void ClearBounds()
	{
		PlayableAreaBounds.center = MapCenterPoint;
		PlayableAreaBounds.size = new Vector3(ScenarioLuaFile.Data.Size[0] * 0.1f, 26, ScenarioLuaFile.Data.Size[1]);
	}

	public static bool IsInArea(Vector3 point)
	{
		return PlayableAreaBounds.Contains(point);
	}

	public void UpdateArea()
	{
		UpdateArea(LastRounding);
	}

	bool LastRounding = true;
	public void UpdateArea(bool Round)
	{
		LastRounding = Round;
		ClearBounds();
		if (SaveLuaFile.Data.areas.Length > 0 && !AreaInfo.HideArea)
		{
			//int bigestAreaId = 0;
			Rect bigestAreaRect = GetAreaRect(Round);


			if (bigestAreaRect.width > 0 && bigestAreaRect.height > 0)
			{

				// Set shaders
				Shader.SetGlobalInt("_Area", 1);
				// _AreaRect uses the params (x,y,width,height) as (x1,y1,x2,y2), ie: Upper Left Corner and Lower Right Corner
				Shader.SetGlobalVector("_AreaRect", new Vector4(bigestAreaRect.x / 10f, bigestAreaRect.y / 10f, bigestAreaRect.width / 10f, bigestAreaRect.height / 10f));
				SetBounds(bigestAreaRect);
			}
			else
			{
				Shader.SetGlobalInt("_Area", 0);
			}

		}
		else
		{
			Shader.SetGlobalInt("_Area", 0);
		}
	}
	#endregion



	#region Lua values
	public static Vector2 GetMapSize()
	{
		return new Vector2(Current.ScenarioLuaFile.Data.Size[0], Current.ScenarioLuaFile.Data.Size[1]);
	}

	public static float GetMapSizeX()
	{
		return Current.ScenarioLuaFile.Data.Size[0];
	}

	public static float GetMapSizeY()
	{
		return Current.ScenarioLuaFile.Data.Size[1];
	}

	public static string MapRelativePath(string luaPath)
	{
		return luaPath.Replace("/maps/", Current.FolderParentPath);
	}

	#endregion
}
