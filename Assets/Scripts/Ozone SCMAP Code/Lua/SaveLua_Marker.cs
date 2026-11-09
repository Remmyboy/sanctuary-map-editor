using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MapLua
{
	public partial class SaveLua
	{

		#region Marker
		//[System.Serializable]
		public class Marker
		{
			public string Name = "";
			public MarkerTypes MarkerType;
			public Markers.MarkerObject MarkerObj;
			public List<Chain> ConnectedToChains;

			public float size = 1f;
			public bool resource = false;
			public float amount = 100;
			public string color = "ff808080";
			public string type = "";
			public string prop = "";
			public Vector3 orientation = Vector3.zero;
			public Vector3 position = Vector3.zero;
			public string editorIcon = "";
			public bool hint;

			// Path
			public string graph = "";
			public string adjacentTo = "";
			public List<Marker> AdjacentToMarker = new List<Marker>();

			// Camera
			public float zoom = 30;
			public bool canSetCamera = true;
			public bool canSyncCamera = true;

			// Effect
			public Vector3 offset = Vector3.zero;
			public string EffectTemplate = "";
			public float scale = 1;

			//WeatherGenerator
			public float cloudCount = 10f;
			public float cloudCountRange = 0f;
			public float cloudEmitterScale = 1f;
			public float cloudEmitterScaleRange = 0.0f;
			public float cloudSpread = 150f;
			public float cloudHeightRange = 15;
			public float spawnChance = 1;
			public string ForceType = "None";
			public float cloudHeight = 180;

			//WeatherDefinition
			public Vector3 WeatherDriftDirection = Vector3.right;
			public string MapStyle = "Tundra";
			public string WeatherType04 = "WhitePatchyClouds";
			public string WeatherType03 = "None";
			public string WeatherType02 = "WhiteThickClouds";
			public string WeatherType01 = "SnowClouds";
			public float WeatherType04Chance = 0.1f;
			public float WeatherType03Chance = 0.3f;
			public float WeatherType02Chance = 0.3f;
			public float WeatherType01Chance = 0.3f;

			//Adaptive
			public List<int> SpawnWithArmy = new List<int>();
			public List<string> AdaptiveKeys = new List<string>();

			public const string KEY_SIZE = "size";
			public const string KEY_RESOURCE = "resource";
			public const string KEY_AMOUNT = "amount";
			public const string KEY_COLOR = "color";
			public const string KEY_TYPE = "type";
			public const string KEY_PROP = "prop";
			public const string KEY_ORIENTATION = "orientation";
			public const string KEY_POSITION = "position";

			public const string KEY_EDITORICON = "editorIcon";
			public const string KEY_HINT = "hint";

			public const string KEY_OFFSET = "offset";
			public const string KEY_EFFECTTEMPLATE = "EffectTemplate";
			public const string KEY_SCALE = "scale";

			public const string KEY_ZOOM = "zoom";
			public const string KEY_CANSETCAMERA = "canSetCamera";
			public const string KEY_CANSYNCCAMERA = "canSyncCamera";

			public const string KEY_GRAPH = "graph";
			public const string KEY_ADJACENTTO = "adjacentTo";

			public const string KEY_CLOUDCOUNT = "cloudCount", KEY_CLOUDEMITTERSCALE = "cloudEmitterScale", KEY_CLOUDEMITTERSCALERANGE = "cloudEmitterScaleRange",
				KEY_CLOUDCOUNTRANGE = "cloudCountRange", KEY_CLOUDSPREAD = "cloudSpread", KEY_CLOUDHEIGHTRANGE = "cloudHeightRange",
				KEY_SPAWNCHANCE = "spawnChance", KEY_FORCETYPE = "ForceType", KEY_CLOUDHEIGHT = "cloudHeight";

			public const string KEY_WEATHERDRIFTDIRECTION = "WeatherDriftDirection", KEY_MAPSTYLE = "MapStyle";
			public const string KEY_WEATHERTYPE01 = "WeatherType01", KEY_WEATHERTYPE02 = "WeatherType02", KEY_WEATHERTYPE03 = "WeatherType03", KEY_WEATHERTYPE04 = "WeatherType04";
			public const string KEY_WEATHERTYPE01CHANCE = "WeatherType01Chance", KEY_WEATHERTYPE02CHANCE = "WeatherType02Chance", KEY_WEATHERTYPE03CHANCE = "WeatherType03Chance", KEY_WEATHERTYPE04CHANCE = "WeatherType04Chance";

			public enum MarkerTypes
			{
				None,
				Mass, Hydrocarbon, BlankMarker, CameraInfo,
				CombatZone,
				DefensivePoint, NavalDefensivePoint,
				ProtectedExperimentalConstruction,
				ExpansionArea, LargeExpansionArea, NavalArea,
				RallyPoint, NavalRallyPoint,
				LandPathNode, AirPathNode, WaterPathNode, AmphibiousPathNode, AutoPathNode,
				NavalLink,
				TransportMarker,
				Island,
				WeatherGenerator, WeatherDefinition,
				Effect,
				NavalExclude,
				Objective,
				Count
			}

			public enum MarkerLayers
			{
				All, NoAI, Land, Air, Naval, AnyPath, Other
			}

			public static string MarkerTypeToString(MarkerTypes MType)
			{
				string str1 = MType.ToString();
				string newstring = "";
				for (int i = 0; i < str1.Length; i++)
				{
					if (i > 0 && char.IsUpper(str1[i]))
						newstring += " ";
					newstring += str1[i].ToString();
				}
				return newstring;
			}

			public bool AllowByType(string Key)
			{
				switch (MarkerType)
				{
					case MarkerTypes.Mass:
						return Key == KEY_SIZE || Key == KEY_RESOURCE || Key == KEY_AMOUNT || Key == KEY_EDITORICON;
					case MarkerTypes.Hydrocarbon:
						return Key == KEY_SIZE || Key == KEY_RESOURCE || Key == KEY_AMOUNT;
					case MarkerTypes.BlankMarker:
						return false;
					case MarkerTypes.LandPathNode:
					case MarkerTypes.AirPathNode:
					case MarkerTypes.WaterPathNode:
					case MarkerTypes.AmphibiousPathNode:
					case MarkerTypes.AutoPathNode:
						return Key == KEY_HINT || Key == KEY_GRAPH || Key == KEY_ADJACENTTO;
					case MarkerTypes.NavalLink:
						return false;
					case MarkerTypes.NavalExclude:
						return Key == KEY_HINT;
					case MarkerTypes.TransportMarker:
						return false;
					case MarkerTypes.CameraInfo:
						return Key == KEY_ZOOM || Key == KEY_CANSETCAMERA || Key == KEY_CANSYNCCAMERA;
					case MarkerTypes.Effect:
						return Key == KEY_OFFSET || Key == KEY_EFFECTTEMPLATE || Key == KEY_COLOR || Key == KEY_SCALE;
					case MarkerTypes.WeatherGenerator:
						return Key == KEY_CLOUDCOUNT || Key == KEY_CLOUDEMITTERSCALE || Key == KEY_CLOUDEMITTERSCALERANGE || Key == KEY_CLOUDCOUNTRANGE || Key == KEY_CLOUDSPREAD || Key == KEY_CLOUDHEIGHTRANGE
							|| Key == KEY_SPAWNCHANCE || Key == KEY_FORCETYPE || Key == KEY_CLOUDHEIGHT;
					case MarkerTypes.WeatherDefinition:
						return Key == KEY_WEATHERDRIFTDIRECTION || Key == KEY_MAPSTYLE
							|| Key == KEY_WEATHERTYPE01 || Key == KEY_WEATHERTYPE02 || Key == KEY_WEATHERTYPE03 || Key == KEY_WEATHERTYPE04
							|| Key == KEY_WEATHERTYPE01CHANCE || Key == KEY_WEATHERTYPE02CHANCE || Key == KEY_WEATHERTYPE03CHANCE || Key == KEY_WEATHERTYPE04CHANCE;
					default:
						return Key == KEY_HINT;
				}
			}

			void DefaultsByType()
			{
				switch (MarkerType)
				{
					case MarkerTypes.Mass:
						resource = true;
						amount = 100;
						prop = "/env/common/props/markers/M_Mass_prop.bp";
						color = "ff808080";
						break;
					case MarkerTypes.Hydrocarbon:
						size = 3;
						resource = true;
						amount = 100;
						prop = "/env/common/props/markers/M_Hydrocarbon_prop.bp";
						color = "ff808080";
						break;
					case MarkerTypes.CameraInfo:
						prop = "/env/common/props/markers/M_Camera_prop.bp";
						color = "ff808000";
						canSyncCamera = true;
						canSetCamera = true;
						zoom = 30;
						break;
					case MarkerTypes.Effect:
						prop = "/env/common/props/markers/M_Defensive_prop.bp";
						color = "ffbffcd0";
						scale = 1;
						offset = Vector3.zero;
						EffectTemplate = "";
						break;
					case MarkerTypes.CombatZone:
						prop = "/env/common/props/markers/M_CombatZone_prop.bp";
						hint = true;
						color = "ff800000";
						break;
					case MarkerTypes.Objective:
						prop = "/env/common/props/markers/M_CombatZone_prop.bp";
						hint = true;
						color = "ff9c0000";
						break;
					case MarkerTypes.DefensivePoint:
						prop = "/env/common/props/markers/M_Defensive_prop.bp";
						hint = true;
						color = "ff008000";
						break;
					case MarkerTypes.NavalDefensivePoint:
						prop = "/env/common/props/markers/M_Defensive_prop.bp";
						hint = true;
						color = "ff0080FF";
						break;
					case MarkerTypes.ProtectedExperimentalConstruction:
						prop = "/env/common/props/markers/M_Expansion_prop.bp";
						hint = true;
						color = "ff0000AA";
						break;
					case MarkerTypes.ExpansionArea:
						prop = "/env/common/props/markers/M_Expansion_prop.bp";
						hint = true;
						color = "ff008080";
						break;
					case MarkerTypes.LargeExpansionArea:
						prop = "/env/common/props/markers/M_Expansion_prop.bp";
						hint = true;
						color = "ff008080";
						break;
					case MarkerTypes.NavalArea:
						prop = "/env/common/props/markers/M_Expansion_prop.bp";
						hint = true;
						color = "ff0000FF";
						break;
					case MarkerTypes.RallyPoint:
						prop = "/env/common/props/markers/M_Defensive_prop.bp";
						hint = true;
						color = "FF808000";
						break;
					case MarkerTypes.NavalRallyPoint:
						prop = "/env/common/props/markers/M_Defensive_prop.bp";
						hint = true;
						color = "ff00FFFF";
						break;
					case MarkerTypes.LandPathNode:
						prop = "/env/common/props/markers/M_Path_prop.bp";
						hint = true;
						color = "ff00ff00";
						break;
					case MarkerTypes.AirPathNode:
						prop = "/env/common/props/markers/M_Path_prop.bp";
						hint = true;
						color = "ffffffff";
						break;
					case MarkerTypes.WaterPathNode:
						prop = "/env/common/props/markers/M_Path_prop.bp";
						hint = true;
						color = "ff0000ff";
						break;
					case MarkerTypes.AmphibiousPathNode:
						prop = "/env/common/props/markers/M_Path_prop.bp";
						hint = true;
						color = "ff00ffff";
						break;
					case MarkerTypes.AutoPathNode:
						prop = "/env/common/props/markers/M_Path_prop.bp";
						hint = true;
						color = "ff800000";
						break;
					case MarkerTypes.NavalLink:
						prop = "/env/common/props/markers/M_Blank_prop.bp";
						hint = false;
						color = "ffff0000";
						break;
					case MarkerTypes.NavalExclude:
						prop = "/env/common/props/markers/M_CombatZone_prop.bp";
						hint = true;
						color = "ff0000AA";
						break;
					case MarkerTypes.TransportMarker:
						prop = "/env/common/props/markers/M_Blank_prop.bp";
						hint = false;
						color = "ff80A088";
						break;
					case MarkerTypes.Island:
						prop = "/env/common/props/markers/M_Expansion_prop.bp";
						hint = true;
						color = "ffffff";
						break;
				}


				prop = "/env/common/props/markers/M_Blank_prop.bp";

			}

			public MarkerLayers LayerByType(MarkerTypes Type)
			{
				if (Type == MarkerTypes.BlankMarker || Type == MarkerTypes.Mass || Type == MarkerTypes.Hydrocarbon || Type == MarkerTypes.CameraInfo)
					return MarkerLayers.NoAI;
				else if (Type == MarkerTypes.LandPathNode || Type == MarkerTypes.RallyPoint || Type == MarkerTypes.AmphibiousPathNode || Type == MarkerTypes.AutoPathNode)
					return MarkerLayers.Land;
				else if (Type == MarkerTypes.WaterPathNode || Type == MarkerTypes.NavalRallyPoint || Type == MarkerTypes.NavalLink || Type == MarkerTypes.AutoPathNode)
					return MarkerLayers.Naval;
				else if (Type == MarkerTypes.AirPathNode)
					return MarkerLayers.Air;
				else
					return MarkerLayers.Other;
			}

			public void ForceDefaultValues()
			{
				if (MarkerType == MarkerTypes.BlankMarker)
					color = "ff800080";
				else if (MarkerType == MarkerTypes.Mass)
				{
					orientation = Vector3.zero;
					color = "ff808080";
				}
				else if (MarkerType == MarkerTypes.Hydrocarbon)
				{
					orientation = Vector3.zero;
					color = "ff008000";
				}
				else if (MarkerType == MarkerTypes.CameraInfo)
				{

				}
				else
				{
					orientation = Vector3.zero;
				}

			}

			public Marker()
			{
			}

			public Marker(MarkerTypes Type, string NewName = "")
			{
				ConnectedToChains = new List<Chain>();
				AdjacentToMarker = new List<Marker>();

				if (string.IsNullOrEmpty(NewName))
					Name = GetLowestName(Type);
				else
					Name = NewName;

				AddNewMarker(this);
				size = 1;
				position = Vector3.zero;
				orientation = Vector3.zero;

				MarkerType = Type;
				type = MarkerTypeToString(Type);

				DefaultsByType();
			}

			public Marker(Marker CopyMarker, string NewName = "")
			{
				ConnectedToChains = new List<Chain>();
				AdjacentToMarker = new List<Marker>();

				for (int i = 0; i < CopyMarker.ConnectedToChains.Count; i++)
					ConnectedToChains.Add(CopyMarker.ConnectedToChains[i]);

				for (int i = 0; i < CopyMarker.AdjacentToMarker.Count; i++)
					AdjacentToMarker.Add(CopyMarker.AdjacentToMarker[i]);

				if (string.IsNullOrEmpty(NewName))
				{
					Name = GetLowestName(CopyMarker.MarkerType);
				}
				else
					Name = NewName;

				AddNewMarker(this);

				size = CopyMarker.size;
				amount = CopyMarker.amount;


				position = ScmapEditor.WorldPosToScmap(CopyMarker.MarkerObj.transform.position);
				orientation = Markers.MarkerObject.MarkerRotToScmapRot(CopyMarker.MarkerObj.Tr.localRotation, CopyMarker.MarkerType);
				prop = "/env/common/props/markers/M_Blank_prop.bp";

				MarkerType = CopyMarker.MarkerType;
				type = MarkerTypeToString(CopyMarker.MarkerType);

				if (MarkerType == MarkerTypes.Mass)
				{
					resource = true;
					//amount = 100;
					prop = "/env/common/props/markers/M_Mass_prop.bp";
					color = "ff808080";
				}
				else if (MarkerType == MarkerTypes.Hydrocarbon)
				{
					//size = 3;
					resource = true;
					//amount = 100;
					prop = "/env/common/props/markers/M_Hydrocarbon_prop.bp";
					color = "ff808080";
				}
				else if (MarkerType == MarkerTypes.CameraInfo)
				{
					canSyncCamera = CopyMarker.canSyncCamera;
					canSetCamera = CopyMarker.canSetCamera;
					zoom = CopyMarker.zoom;
				}
			}


			public Marker AutoMarker_Land;
			public Marker AutoMarker_Water;
			public Marker AutoMarker_Amphibious;

			public static MarkerTypes StringToMarkerType(string value)
			{
				return (MarkerTypes)System.Enum.Parse(typeof(MarkerTypes), value.Replace(" ", ""));
			}

			public bool IsOnWater()
			{
				return ScmapEditor.Current.Teren.SampleHeight(MarkerObj.transform.position) < ScmapEditor.GetWaterLevel() && ScmapEditor.Current.map.Water.HasWater;
			}



		}
		#endregion

		static Dictionary<string, Marker> AllMarkersDictionary = new Dictionary<string, Marker>();

		public static void ClearMarkersDictionary()
		{
			if (AllMarkersDictionary == null)
				AllMarkersDictionary = new Dictionary<string, Marker>();
			else
				AllMarkersDictionary.Clear();
		}

		public static bool AddNewMarker(SaveLua.Marker NewMarker)
		{
			if (!AllMarkersDictionary.ContainsKey(NewMarker.Name))
			{
				AllMarkersDictionary.Add(NewMarker.Name, NewMarker);
				return true;
			}
			return false;
		}

		public static void RemoveMarker(string Name)
		{
			AllMarkersDictionary.Remove(Name);
		}

		public static SaveLua.Marker GetMarker(string Name)
		{
			if (AllMarkersDictionary.ContainsKey(Name))
				return AllMarkersDictionary[Name];

			return null;
		}

		public static bool MarkerExist(string Name, bool WithObject = true)
		{
			if (WithObject)
				return AllMarkersDictionary.ContainsKey(Name) && AllMarkersDictionary[Name].MarkerObj != null;

			return AllMarkersDictionary.ContainsKey(Name);
		}

		public static string GetPrefixByType(Marker.MarkerTypes Type)
		{
			switch (Type)
			{
				case Marker.MarkerTypes.BlankMarker:
				case Marker.MarkerTypes.Mass:
				case Marker.MarkerTypes.Hydrocarbon:
					return Marker.MarkerTypeToString(Type) + " ";
				case Marker.MarkerTypes.LandPathNode:
					return "LandPN";
				case Marker.MarkerTypes.WaterPathNode:
					return "WaterPN";
				case Marker.MarkerTypes.AmphibiousPathNode:
					return "AmphPN";
				case Marker.MarkerTypes.AirPathNode:
					return "AirPN";
				case Marker.MarkerTypes.NavalArea:
					return "Naval Area ";
				case Marker.MarkerTypes.ExpansionArea:
					return "Expansion Area ";
				case Marker.MarkerTypes.LargeExpansionArea:
					return "Large Expansion ";
				case Marker.MarkerTypes.CombatZone:
					return "Combat Zone ";
				case Marker.MarkerTypes.DefensivePoint:
					return "Defensive Point ";
				default:
					return Type.ToString() + "_";
			}
		}

		public static string GetLowestName(Marker.MarkerTypes Type)
		{
			// Sanctuary: spawns are Army_1, Army_2...; resource spots follow
			// the map's own naming (Alloys_7, AlloyMarker_12...).
			string sanPrefix = Sanctuary.SanctuaryMapLoader.NewMarkerPrefix(Type);
			if (sanPrefix != null)
				return Sanctuary.SanctuaryMapLoader.LowestFreeName(sanPrefix);

			string prefix = GetPrefixByType(Type);

			int ID = 0;
			while (MarkerExist(prefix + ID.ToString("00")))
				ID++;

			return prefix + ID.ToString("00");
		}

		public static string GetLowestName(string oldName)
		{
			string prefix = oldName;
			while(prefix.Length > 0 && char.IsDigit(prefix[prefix.Length - 1]))
			{
				prefix = prefix.Remove(prefix.Length - 1);
			}

			if(prefix.Length <= 0)
			{
				prefix = "Marker_";
			}

			int ID = 0;
			while (MarkerExist(prefix + ID.ToString("00")))
				ID++;

			return prefix + ID.ToString("00");
		}

		private void ConnectAdjacentMarkers()
		{
			int mc = 0;
			int Mcount = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers.Count;

			for (int m = 0; m < Mcount; m++)
			{
				if (MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[m].adjacentTo.Length > 0)
				{
					string[] Names = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[m].adjacentTo.Split(" ".ToCharArray());
					//Transform Tr = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[m].MarkerObj.Tr;

					for (int e = 0; e < Names.Length; e++)
					{
						int ConM = MarkerIdByName(mc, Names[e], Mcount);

						if (ConM >= 0)
						{
							MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[m].AdjacentToMarker.Add(MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[ConM]);
						}

					}
				}
			}
		}

		int MarkerIdByName(int mc, string SearchName, int Mcount)
		{
			for (int m = 0; m < Mcount; m++)
			{
				if (MapLuaParser.Current.SaveLuaFile.Data.MasterChains[mc].Markers[m].MarkerObj.name == SearchName)
					return m;
			}
			return -1;
		}

		public void GenerateAutoMarkers()
		{
			for (int mc = 0; mc < Data.MasterChains.Length; mc++)
			{

				if (Data.MasterChains[mc].Markers == null)
					Data.MasterChains[mc].Markers = new List<Marker>();

				int Mcount = Data.MasterChains[mc].Markers.Count;
				for (int m = 0; m < Mcount; m++)
				{
					if (Data.MasterChains[mc].Markers[m].MarkerType == Marker.MarkerTypes.AutoPathNode)
					{
						if (Data.MasterChains[mc].Markers[m].IsOnWater())
						{
							if (Data.MasterChains[mc].Markers[m].AutoMarker_Water != null)
								Data.MasterChains[mc].Markers[m].AutoMarker_Water = null;

							Data.MasterChains[mc].Markers[m].AutoMarker_Water = new Marker(Data.MasterChains[mc].Markers[m], "APM_Water_" + Data.MasterChains[mc].Markers[m].Name);
							Data.MasterChains[mc].Markers[m].AutoMarker_Water.MarkerType = Marker.MarkerTypes.WaterPathNode;
						}
						else
						{
							if (Data.MasterChains[mc].Markers[m].AutoMarker_Land != null)
								Data.MasterChains[mc].Markers[m].AutoMarker_Land = null;
							Data.MasterChains[mc].Markers[m].AutoMarker_Land = new Marker(Data.MasterChains[mc].Markers[m], "APM_Land_" + Data.MasterChains[mc].Markers[m].Name);
							Data.MasterChains[mc].Markers[m].AutoMarker_Land.MarkerType = Marker.MarkerTypes.LandPathNode;
						}


						if (Data.MasterChains[mc].Markers[m].AutoMarker_Amphibious != null)
							Data.MasterChains[mc].Markers[m].AutoMarker_Amphibious = null;
						Data.MasterChains[mc].Markers[m].AutoMarker_Amphibious = new Marker(Data.MasterChains[mc].Markers[m], "APM_Amphibious_" + Data.MasterChains[mc].Markers[m].Name);
						Data.MasterChains[mc].Markers[m].AutoMarker_Amphibious.MarkerType = Marker.MarkerTypes.AmphibiousPathNode;
					}


				}
			}
		}


	}
}
