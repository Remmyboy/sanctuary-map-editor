using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Ozone.UI;

namespace EditMap
{

	public class NewMap : MonoBehaviour
	{

		public UiTextField Name;
		public UiTextField Desc;
		public Dropdown MapType;
		public Dropdown ArmyCount;
		public Dropdown Width;
		public Dropdown Height;
		public UiTextField InitialHeight;

		public Toggle Water;
		public UiTextField WaterElv;
		public UiTextField DepthElevation;
		public UiTextField AbyssElevation;

		private void OnEnable()
		{
			Clean();
		}

		public void Clean()
		{
			Name.SetValue("");
			Desc.SetValue("");
			MapType.value = 0;
			Width.value = 3;
			Height.value = 3;
			InitialHeight.SetValue(35);
            WaterElv.SetValue(30);
			DepthElevation.SetValue(25);
			AbyssElevation.SetValue(15);
		}

		public void ToggleWater()
		{
			WaterElv.gameObject.SetActive(Water.isOn);
			DepthElevation.gameObject.SetActive(Water.isOn);
			AbyssElevation.gameObject.SetActive(Water.isOn);
		}

		public void WaterChange()
		{			float water = WaterElv.value;
			float depth = DepthElevation.value;
			float abyss = AbyssElevation.value;

			if (water < 0)
				water = 0;
			else if (water > 256)
				water = 256;

			if (depth > water)
				depth = water;
			else if (depth < 0)
				depth = 0;

			if (abyss > depth)
				abyss = depth;
			else if (abyss < 0)
				abyss = 0;


			WaterElv.SetValue(water);
			DepthElevation.SetValue(depth);
			AbyssElevation.SetValue(abyss);
		}


		public void CreateMap()
		{
			if (string.IsNullOrEmpty(Name.text))
			{
				GenericPopup.ShowPopup(GenericPopup.PopupTypes.OneButton, "Warning", "Name must not be empty!", "OK", null);
				return;
			}

			// With a Sanctuary install, New Map makes a Sanctuary map. Width
			// and height are the playable area; the look comes from the open
			// Sanctuary map (or Two Step Shuffle).
			string sanMaps = Sanctuary.SanctuaryPaths.MapsDir;
			{
				string problem = Sanctuary.SanctuaryNewMap.Validate(Name.text, sanMaps);
				if (problem != null)
				{
					GenericPopup.ShowPopup(GenericPopup.PopupTypes.OneButton, "Warning", problem, "OK", null);
					return;
				}
				var o = new Sanctuary.MapFormat.NewMapOptions
				{
					Name = Name.text,
					PlayableWidth = SizeByValue(Width.value),
					PlayableLength = SizeByValue(Height.value),
					Armies = ArmyCount.value + 1,
					GroundHeight = InitialHeight.intValue,
					Water = Water.isOn,
					WaterLevel = WaterElv.intValue,
				};
				MapLuaParser.Current.StartCoroutine(Sanctuary.SanctuaryNewMap.Create(sanMaps, o));
				gameObject.SetActive(false);
				return;
			}
		}

		int SizeByValue(int value)
		{
			switch (value)
			{
				case 0:
					return 64;
				case 1:
					return 128;
				case 2:
					return 256;
				case 3:
					return 512;
				case 4:
					return 1024;
				case 5:
					return 2048;
				case 6:
					return 4096;

			}
			return 512;
		}
	}
}
