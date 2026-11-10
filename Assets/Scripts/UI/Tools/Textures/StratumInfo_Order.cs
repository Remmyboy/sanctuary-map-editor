using UnityEngine;

namespace EditMap
{
	public partial class StratumInfo
	{


		public void MoveSelectedUp()
		{
			if (Selected <= 0 || Selected >= 8)
				return;

			int NewSelected = Selected + 1;

			Undo.RegisterUndo(new UndoHistory.HistoryStratumReorder(), new UndoHistory.HistoryStratumReorder.StratumReorderParam(Selected, NewSelected));

			SwitchLayers(Selected, NewSelected);
		}

		public void MoveSelectedDown()
		{
			if (Selected <= 1 || Selected >= 9)
				return;

			int NewSelected = Selected - 1;

			Undo.RegisterUndo(new UndoHistory.HistoryStratumReorder(), new UndoHistory.HistoryStratumReorder.StratumReorderParam(Selected, NewSelected));

			SwitchLayers(Selected, NewSelected);
		}

		public void SwitchLayers(int FromLayer, int ToLayer)
		{
			Color[] StratumData = GetPixels(FromLayer);

			if (FromLayer > 4 != ToLayer > 4)
			{ // Different tex
				Color[] StratumDataPrev = GetPixels(ToLayer);

				for (int i = 0; i < StratumData.Length; i++)
				{
					float from = GetChannelByLayer(ToLayer, StratumDataPrev[i]);
					SetChannelByLayer(ToLayer, ref StratumDataPrev[i], GetChannelByLayer(FromLayer, StratumData[i]));
					SetChannelByLayer(FromLayer, ref StratumData[i], from);
				}


				SetPixels(FromLayer, StratumData);
				SetPixels(ToLayer, StratumDataPrev);
			}
			else
			{ // Same
				for (int i = 0; i < StratumData.Length; i++)
				{
					float from = GetChannelByLayer(ToLayer, StratumData[i]);
					SetChannelByLayer(ToLayer, ref StratumData[i], GetChannelByLayer(FromLayer, StratumData[i]));
					SetChannelByLayer(FromLayer, ref StratumData[i], from);
				}

				SetPixels(ToLayer, StratumData);
			}

			// The Sanctuary layer list (textures, tint, tiling) moves with its
			// mask; without this a saved map put each mask under the other
			// layer's texture.
			var layers = Sanctuary.SanctuaryMapLoader.IsActive ? Sanctuary.SanctuaryMapLoader.Folder?.Map.StratumLayers : null;
			if (layers != null && FromLayer < layers.Count && ToLayer < layers.Count)
			{
				var a = layers[FromLayer];
				layers[FromLayer] = layers[ToLayer].DeepClone();
				layers[ToLayer] = a.DeepClone();
			}

			ScmapEditor.TerrainTexture Prev = ScmapEditor.Current.Textures[FromLayer];
			ScmapEditor.Current.Textures[FromLayer] = ScmapEditor.Current.Textures[ToLayer];
			ScmapEditor.Current.Textures[ToLayer] = Prev;

			ScmapEditor.Current.SetTextures(FromLayer);
			ScmapEditor.Current.SetTextures(ToLayer);

			ReloadStratums();
			SelectStratum(ToLayer);
		}
	}
}