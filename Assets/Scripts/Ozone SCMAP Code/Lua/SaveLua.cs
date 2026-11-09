// ******************************************************************************
//
// * Save.lua Class
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
	public partial class SaveLua
	{

		public Scenario Data = new Scenario();

		#region Area controls
		public int DisplayAreaId = 0;

		public bool HasAreas()
		{
			return Data.areas.Length > 0;
		}

		public Rect GetAreaSize()
		{
			return Data.areas[DisplayAreaId].rectangle;
		}
		#endregion

		#region Structure Objects
		const string KEY_Scenario = "Scenario";

		[System.Serializable]
		public class Scenario
		{
			public int next_area_id = 0;
			public Areas[] areas = new Areas[0];
			public MasterChain[] MasterChains;
			public Chain[] Chains;
			public int next_queue_id = 0;

			public int next_platoon_id = 0;
			public Platoon[] Platoons;

			public int next_army_id = 0;
			public int next_group_id = 0;
			public int next_unit_id = 0;

			//public Army[] Armies;

			public const string KEY_NEXTAREAID = "next_area_id";
			public const string KEY_PROPS = "Props";
			public const string KEY_AREAS = "Areas";
			public const string KEY_MASTERCHAIN = "MasterChain";
			public const string KEY_CHAINS = "Chains";
			public const string KEY_NEXTQUEUEID = "next_queue_id";
			public const string KEY_ORDERS = "Orders";
			public const string KEY_NEXTPLATOONID = "next_platoon_id";
			public const string KEY_PLATOONS = "Platoons";
			public const string KEY_NEXTARMYID = "next_army_id";
			public const string KEY_NEXTGROUPID = "next_group_id";
			public const string KEY_NEXTUNITID = "next_unit_id";
			public const string KEY_ARMIES = "Armies";

			public Scenario()
			{
				areas = new Areas[0];
				MasterChains = new MasterChain[1];
				MasterChains[0] = new MasterChain();
				MasterChains[0].Name = "_MASTERCHAIN_" ;
				MasterChains[0].Markers = new List<Marker>();

				Chains = new Chain[0];
				Platoons = new Platoon[0];
			}

		}

		[System.Serializable]
		public class Areas
		{
			public string Name;
			public Rect rectangle;
			public const string KEY_RECTANGLE = "rectangle";
		}

		[System.Serializable]
		public class MasterChain
		{
			public string Name = "";
			public List<Marker> Markers = new List<Marker>();
			public const string KEY_MARKERS = "Markers";
		}


		#endregion

		public void Unload()
		{
			ClearMarkersDictionary();
			Data = new Scenario();

		}






	}
}
