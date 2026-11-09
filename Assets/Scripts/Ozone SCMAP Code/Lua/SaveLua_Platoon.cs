using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MapLua
{
	public partial class SaveLua
	{

		[System.Serializable]
		public class Platoon
		{
			public string Name;
			public string PlatoonName;
			public string PlatoonFunction;
			public PlatoonAction Action;

			[System.Serializable]
			public class PlatoonAction
			{
				public bool Loaded = false;
				public string Unit;
				public int Id;
				public int count;
				public string Action;
				public string Formation;

				public PlatoonAction()
				{
					Loaded = false;
					Unit = "";
					Id = 0;
					count = 0;
					Action = "";
					Formation = "";
				}

			}

			public Platoon()
			{
			}

		}

	}
}
