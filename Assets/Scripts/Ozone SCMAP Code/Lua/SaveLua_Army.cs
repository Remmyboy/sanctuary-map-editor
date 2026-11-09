using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MapLua
{
	public partial class SaveLua
	{

		[System.Serializable]
		public class Army
		{
			public string Name;
			public string personality = "";
			public string plans = "/lua/ai/OpAI/DefaultBlankPlanlist.lua";
			public int color = 0;
			public int faction = 0;
			public EconomyTab Economy;
			public List<Aliance> Alliances;
			public UnitsGroup Units;
			public string nextPlatoonBuilderId = "";
			public PlatoonBuilder[] PlatoonBuilders;

			const string KEY_PERSONALITY = "personality";
			const string KEY_PLANS = "plans";
			const string KEY_COLOR = "color";
			const string KEY_FACTION = "faction";
			const string KEY_ECONOMY = "Economy";
			const string KEY_ALLIANCES = "Alliances";
			const string KEY_UNITS = "Units";
			const string KEY_PLATOONBUILDERS = "PlatoonBuilders";

			#region Classes
			public class EconomyTab
			{
				public float mass = 0;
				public float energy = 0;

				public const string KEY_MASS = "mass";
				public const string KEY_ENERGY = "energy";
			}

			public class Aliance
			{
				public string Army = "";
				public Army ConnectedArmy;
				public string AllianceType = "Enemy";
			}

			public enum AllianceTypes
			{
				None, Ally, Enemy
			}


			public Color ArmyColor
			{
				get
				{
					return GetArmyColor(color);
				}
			}


			//[System.Serializable]
			public class UnitsGroup
			{
				public string Name;
				public string Prefix;

				public string orders;
				public string platoon;
				public HashSet<UnitsGroup> UnitGroups;
				public HashSet<Unit> Units;

				#region Editor Values
				public bool Expanded;
				public bool IsWreckage = false;
				#endregion

				#region Parents
				public Army Owner;
				public UnitsGroup Parent;
				public void AddGroup(UnitsGroup ug)
				{
					ug.Owner = Owner;
					ug.Parent = this;
					UnitGroups.Add(ug);
				}
				public void RemoveGroup(UnitsGroup ug)
				{
					ug.Owner = null;
					ug.Parent = null;
					UnitGroups.Remove(ug);
				}
				public void AddUnit(Unit u)
				{
					if(u.Parent != null && u.Parent != this)
					{
						// Already used by other group!
						u.Parent.RemoveUnit(u);
					}

					u.Parent = this;
					u.Owner = Owner;
					Units.Add(u);
				}
				public void RemoveUnit(Unit u)
				{
					if (u.Parent == this)
					{
						u.Parent = null;
						u.Owner = null;
					}
					Units.Remove(u);
				}
				public void ClearUnitInstances()
				{

				}
				#endregion

				public const string KEY_PREFIX = "prefix";
				public const string KEY_ORDERS = "orders";
				public const string KEY_PLATOON = "platoon";
				public const string KEY_UNITS = "Units";
				public const string KEY_TYPE = "type";
				public const string KEY_POSITION = "Position";
				public const string KEY_ORIENTATION = "Orientation";


				public string NoPrefixName
				{
					get
					{
						if (string.IsNullOrEmpty(Prefix))
							return Name;

						string ToReturn = Name.Remove(0, Prefix.Length);

						if (ToReturn.StartsWith("_"))
							ToReturn = ToReturn.Remove(0, 1);

						return ToReturn;
					}
					set
					{
						if (string.IsNullOrEmpty(Prefix))
						{
							Prefix = "";
							Name = value;
						}
						else
							Name = Prefix + "_" + value;

						UpdateGroupTags();
					}
				}

				public string PrefixName
				{
					get
					{
						return Prefix;
					}
					set
					{
						if (string.IsNullOrEmpty(value))
						{
							Name = NoPrefixName;
							Prefix = "";
						}
						else
						{
							Name = value + "_" + NoPrefixName;
							Prefix = value;
						}
						UpdateGroupTags();
					}
				}

				public void UpdateGroupTags()
				{
					bool ShouldBeWreckage = NoPrefixName.ToUpper().Contains("WRECKAGE");

					if (IsWreckage == ShouldBeWreckage)
						return;

					IsWreckage = ShouldBeWreckage;

				}

				public UnitsGroup(Army Owner, string Name = "Units")
				{
					this.Owner = Owner;
					this.Name = Name;
					orders = "";
					platoon = "";

					UnitGroups = new HashSet<UnitsGroup>();
					Units = new HashSet<Unit>();
				}



				public void ClearGroup(bool childs)
				{
					foreach (UnitsGroup ug in UnitGroups)
						ug.ClearGroup(childs);
				}

				public void InstantiateGroup(bool childs)
				{
					if(childs)
					foreach (UnitsGroup ug in UnitGroups)
						ug.InstantiateGroup(childs);
				}


				public void UpdateGroupArmy(Army SourceOwner)
				{
					Owner = SourceOwner;

					foreach (UnitsGroup ug in UnitGroups)
						ug.UpdateGroupArmy(SourceOwner);
				}


			}

			//[System.Serializable]
			public class Unit
			{
				public string Name;
				public string type;
				public string orders;
				public string platoon;
				public Vector3 Position;
				public Vector3 Orientation;
				public Army Owner;
				public UnitsGroup Parent;


				static HashSet<string> AllNames = new HashSet<string>();
				public static string GetFreeName(string Tag)
				{
					int Id = 0;
					while(AllNames.Contains(Tag + Id))
					{
						Id++;
					}

					return Tag + Id;
				}

				public static bool NameExist(string NameToCheck)
				{
					return AllNames.Contains(NameToCheck);
				}

				public static bool ReplaceName(string oldName, string newName)
				{
					AllNames.Add(newName);

					if (AllNames.Contains(oldName))
					{
						AllNames.Remove(oldName);
						return true;
					}
					return false;
				}


			}

			[System.Serializable]
			public class PlatoonBuilder
			{
				public string Name;
				public string PlatoonTemplate;
				public int Priority;
				public int InstanceCount;
				public string LocationType;
				public int BuildTimeOut;
				public string PlatoonType;
				public bool RequiresConstruction;
				public PlatoonAIFunction PlatoonAiFunctions;
				public BuildCondition[] BuildConditions;
				public PlatoonAddFunction[] PlatoonAddFunctions;
				public PlatoonData[] PlatoonDatas;


				public const string KEY_NEXTPLATOONBUILDERID = "next_platoon_builder_id";
				public const string KEY_BUILDERS = "Builders";
				public const string KEY_PLATOONTEMPLATE = "PlatoonTemplate";
				public const string KEY_PRIORITY = "Priority";
				public const string KEY_INSTANCECOUNT = "InstanceCount";
				public const string KEY_LOCATIONTYPE = "LocationType";
				public const string KEY_BUILDTIMEOUT = "BuildTimeOut";
				public const string KEY_PLATOONTYPE = "PlatoonType";
				public const string KEY_REQUIRESCONSTRUCTION = "RequiresConstruction";
				public const string KEY_PLATOONAIFUNCTION = "PlatoonAIFunction";
				public const string KEY_BUILDCONDITIONS = "BuildConditions";
				public const string KEY_PLATOONADDFUNCTION = "PlatoonAddFunctions";
				public const string KEY_PLATOONDATA = "PlatoonData";



				public class PlatoonAIFunction
				{

				}

				public class BuildCondition
				{
					int ID;
					string lua;
					string Name;
					// Brains;

				}

				public class PlatoonAddFunction
				{

				}

				public class PlatoonData
				{
					int type;
					string name;
					bool value;
					string stringValue;
				}

				public PlatoonBuilder()
				{

				}


			}


			#endregion

			public Army(bool Initial = false)
			{
				Name = "";
				personality = "";
				if(MapLuaParser.Current == null || MapLuaParser.Current.ScenarioLuaFile.Data == null)
					plans = "";
				else if (MapLuaParser.Current.ScenarioLuaFile.Data.type == "campaign_coop" || MapLuaParser.Current.ScenarioLuaFile.Data.type == "campaign")
					plans = "/lua/ai/OpAI/DefaultBlankPlanlist.lua";
				else
					plans = "";
				color = 0;
				faction = 0;
				Economy = new EconomyTab();
				Alliances = new List<Aliance>();
				Units = new UnitsGroup(this);
				nextPlatoonBuilderId = "1";
				PlatoonBuilders = new PlatoonBuilder[0];

				if (Initial)
				{
					Units.AddGroup(new UnitsGroup(this, "INITIAL"));
				}
			}


		}
	}
}
