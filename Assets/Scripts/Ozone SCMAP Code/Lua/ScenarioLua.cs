// ******************************************************************************
//
// * Scenario.lua Class
// * Can be loaded from LUA and saved as LUA using LuaParser
// * Parsing is done by hand, because I can't find good parser that will convert LUA to Class
// * Copyright ozonexo3 2017
//
// ******************************************************************************

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MapLua
{
	[System.Serializable]
	public class ScenarioLua
	{
		public ScenarioInfo Data = new ScenarioInfo();

		#region Structure Objects
		[System.Serializable]
		public class ScenarioInfo
		{
			public string name;
			public string description;
			public string type;
			public bool starts;
			public float map_version;
			public bool AdaptiveMap;
			public string preview;
			public string save;
			public string map;
			public string script;
			public int[] Size = new int[0];
			public float[] Reclaim = new float[0];
			public float MaxHeight = 128;
			public float norushradius = 40;
			//public NoRusnOffset[] NoRushOffsets = new NoRusnOffset[0];
			public Configuration[] Configurations = new Configuration[0];

			public const string KEY_NAME = "name";
			public const string KEY_DESCRIPTION = "description";
			public const string KEY_TYPE = "type";
			public const string KEY_STARTS = "starts";
			public const string KEY_MAPVERSION = "map_version";
			public const string KEY_ADAPTIVEMAP = "AdaptiveMap";
			public const string KEY_PREVIEW = "preview";
			public const string KEY_SAVE = "save";
			public const string KEY_MAP = "map";
			public const string KEY_SCRIPT = "script";
			public const string KEY_SIZE = "size";
			public const string KEY_RECLAIM = "reclaim";
			public const string KEY_CONFIGURATIONS = "Configurations";
			public const string KEY_EXTRAARMIES = "ExtraArmies";
		}

		[System.Serializable]
		public class Configuration
		{
			public string name;
			public Team[] Teams = new Team[0];
			public CustomProps[] customprops = new CustomProps[0];
			public List<Army> ExtraArmys = new List<Army>();
			public Factions[] factions = new Factions[0];


			public const string KEY_TEAMS = "teams";
			public const string KEY_CUSTOMPROPS = "customprops";
			public const string KEY_FACTIONS = "factions";
			// TODO customprops
		}

		[System.Serializable]
		public class Team
		{
			public string name = "";
			//public string[] Armys = new string[0];
			public List<Army> Armys = new List<Army>();


			public string[] GetArmys()
			{
				string[] ToReturn = new string[Armys.Count];
				for (int i = 0; i < ToReturn.Length; i++)
				{
					ToReturn[i] = Armys[i].Name;
				}
				return ToReturn;
			}

			public const string KEY_NAME = "name";
			public const string KEY_ARMIES = "armies";

		}

		[System.Serializable]
		public class Army
		{
			public string Name;
			public SaveLua.Army Data;
			public NoRusnOffset NoRush;

			public Army()
			{

			}

			public Army(string name)
			{
				Name = name;
				Data = new SaveLua.Army();
				NoRush = new NoRusnOffset();
			}
		}

		[System.Serializable]
		public class CustomProps
		{
			public string key;
			public string value;
		}

		[System.Serializable]
		public class Factions
		{
			public string[] Values;
		}

		[System.Serializable]
		public class NoRusnOffset
		{
			public float X;
			public float Y;

			public const float DefaultRadius = 40;

			public const string VALUE_RADIUS = "norushradius";
			public const string VALUE_X = "norushoffsetX_";
			public const string VALUE_Y = "norushoffsetY_";

			public string GetXKey(string ARMY)
			{
				return VALUE_X + ARMY;
			}

			public string GetYKey(string ARMY)
			{
				return VALUE_Y + ARMY;
			}
		}

		#endregion

		#region Keys
		const string TABLE_SCENARIOINFO = "ScenarioInfo";

		#endregion

		#region Armys
		public bool AddDataToArmy(SaveLua.Army ArmyData)
		{
			for(int c = 0; c < Data.Configurations.Length; c++)
			{
				for(int t = 0; t < Data.Configurations[c].Teams.Length; t++)
				{
					for(int a = 0; a < Data.Configurations[c].Teams[t].Armys.Count; a++)
					{
						if(Data.Configurations[c].Teams[t].Armys[a].Name == ArmyData.Name)
						{
							Data.Configurations[c].Teams[t].Armys[a].Data = ArmyData;
							return true;
						}

					}

				}

				for (int a = 0; a < Data.Configurations[c].ExtraArmys.Count; a++)
				{
					if (Data.Configurations[c].ExtraArmys[a].Name == ArmyData.Name)
					{
						Data.Configurations[c].ExtraArmys[a].Data = ArmyData;
						return true;
					}
				}
			}

			//No army found - Force new army
			if(Data.Configurations.Length > 0 && Data.Configurations[0].Teams.Length > 0)
			{
				Army NewArmy = new Army();
				NewArmy.Name = ArmyData.Name;
				NewArmy.NoRush = new NoRusnOffset();
				NewArmy.Data = ArmyData;

				Data.Configurations[0].Teams[0].Armys.Add(NewArmy);
				return true;

			}

			return false; // Unable to add new army
		}

		public void CheckForEmptyArmy()
		{
			Dictionary<string, SaveLua.Army> AllArmies = new Dictionary<string, SaveLua.Army>();
			for (int c = 0; c < Data.Configurations.Length; c++)
			{
				for (int t = 0; t < Data.Configurations[c].Teams.Length; t++)
				{
					for (int a = 0; a < Data.Configurations[c].Teams[t].Armys.Count; a++)
					{
						if (Data.Configurations[c].Teams[t].Armys[a].Data == null)
						{
							Debug.Log("Fix army: " + Data.Configurations[c].Teams[t].Armys[a].Name + " (" + a + ")");
							Data.Configurations[c].Teams[t].Armys[a].Data = new SaveLua.Army();
							Data.Configurations[c].Teams[t].Armys[a].Data.Name = Data.Configurations[c].Teams[t].Armys[a].Name;
						}
						AllArmies.Add(Data.Configurations[c].Teams[t].Armys[a].Name, Data.Configurations[c].Teams[t].Armys[a].Data);

					}

				}

				for (int a = 0; a < Data.Configurations[c].ExtraArmys.Count; a++)
				{
					if (Data.Configurations[c].ExtraArmys[a].Data == null)
					{
						Debug.Log("Fix extra army: " + Data.Configurations[c].ExtraArmys[a].Name + " (" + a +")");
						Data.Configurations[c].ExtraArmys[a].Data = new SaveLua.Army();
						Data.Configurations[c].ExtraArmys[a].Data.Name = Data.Configurations[c].ExtraArmys[a].Name;
					}
					AllArmies.Add(Data.Configurations[c].ExtraArmys[a].Name, Data.Configurations[c].ExtraArmys[a].Data);

				}
			}


			foreach(KeyValuePair<string, SaveLua.Army> ka in AllArmies)
			{
				for(int i = 0; i < ka.Value.Alliances.Count; i++)
				{
					if(AllArmies.ContainsKey(ka.Value.Alliances[i].Army))
						ka.Value.Alliances[i].ConnectedArmy = AllArmies[ka.Value.Alliances[i].Army];
				}
			}
		}

		public List<SaveLua.Army> GetAllArmies(bool extra = true)
		{
			List<SaveLua.Army> ToReturn = new List<SaveLua.Army>();

			for (int c = 0; c < Data.Configurations.Length; c++)
			{
				for (int t = 0; t < Data.Configurations[c].Teams.Length; t++)
				{
					for (int a = 0; a < Data.Configurations[c].Teams[t].Armys.Count; a++)
					{
						if (Data.Configurations[c].Teams[t].Armys[a].Data != null)
						{
							ToReturn.Add(Data.Configurations[c].Teams[t].Armys[a].Data);
						}

					}

				}

				if (extra)
				{
					for (int a = 0; a < Data.Configurations[c].ExtraArmys.Count; a++)
					{
						if (Data.Configurations[c].ExtraArmys[a].Data != null)
						{
							ToReturn.Add(Data.Configurations[c].ExtraArmys[a].Data);
						}
					}
				}
			}

			return ToReturn;
		}

		public List<ScenarioLua.Army> GetAllScenarioArmies(bool extra = true)
		{
			List<ScenarioLua.Army> ToReturn = new List<ScenarioLua.Army>();

			for (int c = 0; c < Data.Configurations.Length; c++)
			{
				for (int t = 0; t < Data.Configurations[c].Teams.Length; t++)
				{
					for (int a = 0; a < Data.Configurations[c].Teams[t].Armys.Count; a++)
					{
						if (Data.Configurations[c].Teams[t].Armys[a] != null)
						{
							ToReturn.Add(Data.Configurations[c].Teams[t].Armys[a]);
						}

					}

				}

				if (extra)
				{
					for (int a = 0; a < Data.Configurations[c].ExtraArmys.Count; a++)
					{
						if (Data.Configurations[c].ExtraArmys[a] != null)
						{
							ToReturn.Add(Data.Configurations[c].ExtraArmys[a]);
						}
					}
				}
			}

			return ToReturn;
		}

		public List<ScenarioLua.Army> GetSymmetryArmies(ScenarioLua.Army army)
		{
			var allArmies = GetAllScenarioArmies(false);

			string armyName = army.Name;

			SaveLua.Marker ArmyMarker = SaveLua.GetMarker(armyName);

			List<ScenarioLua.Army> ToReturn = new List<ScenarioLua.Army>();

			if (ArmyMarker == null)
				return ToReturn;

			for(int i = 0; i < Selection.SelectionManager.Current.SymetrySelection.Length; i++)
			{
				GameObject foundMarker = Selection.SelectionManager.Current.SymetrySelection[i].FindBestSymmetry(ArmyMarker.MarkerObj.gameObject);
				if (foundMarker == null)
					continue;

				Markers.MarkerObject markerObject = foundMarker.GetComponent<Markers.MarkerObject>();
				if (markerObject == null)
					continue;

				if (foundMarker.name == armyName)
					continue;

				for (int a = 0; a < allArmies.Count; a++)
				{
					if(allArmies[a].Name == foundMarker.name)
					{
						if (!ToReturn.Contains(allArmies[a]))
							ToReturn.Add(allArmies[a]);
						break;
					}
				}
			}


			return ToReturn;
		}



		#endregion

		#region Load
		#endregion

		#region Save
		#endregion
	}
}
