// New Map, Sanctuary flavour: builds the map with SanMapFactory, opens it,
// and saves it once so it gets a preview.png.
using System;
using System.Collections;
using System.IO;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryNewMap
	{
		/// The map whose look a new map copies: the open Sanctuary map, else
		/// Two Step Shuffle (Highlands), else the first map in the folder.
		public static string TemplatePath()
		{
			if (SanctuaryMapLoader.Folder != null) return SanctuaryMapLoader.Folder.Directory;
			string maps = SanctuaryPaths.MapsDir;
			if (maps == null) return null;
			string preferred = Path.Combine(maps, "Two_Step_Shuffle");
			if (Directory.Exists(preferred)) return preferred;
			foreach (var d in Directory.GetDirectories(maps))
				if (Directory.GetFiles(d, "*" + SanMapDocument.Extension).Length > 0) return d;
			return null;
		}

		/// Returns null on success, else a message for the user.
		public static string Validate(string name, string mapsDir)
		{
			if (mapsDir == null) return "Sanctuary install not found; set it in preferences.";
			string safe = SanctuaryMapSaver.SafeFolderName(name ?? "");
			if (string.IsNullOrEmpty(safe)) return "Name must contain letters or digits.";
			string dir = Path.Combine(mapsDir, safe);
			if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0) return "A map folder already exists:\n" + dir;
			if (TemplatePath() == null) return "No Sanctuary map found to take textures and lighting from.";
			return null;
		}

		public static IEnumerator Create(string mapsDir, NewMapOptions o)
		{
			var parser = MapLuaParser.Current;
			string error = Validate(o.Name, mapsDir);
			if (error != null) { parser.ReturnLoadingWithError(error); yield break; }

			string dir = Path.Combine(mapsDir, SanctuaryMapSaver.SafeFolderName(o.Name));
			string template = TemplatePath();
			parser.InfoPopup.Show(true, "Creating map...\n" + Path.GetFileName(dir));
			yield return null;
			try
			{
				SanMapFactory.Create(dir, SanMapFolder.Open(template), o);
				Debug.Log($"Sanctuary new map {dir} from template {template}");
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				parser.InfoPopup.Show(false);
				parser.ReturnLoadingWithError("Cannot create the map:\n" + e.Message);
				yield break;
			}

			yield return parser.StartCoroutine(SanctuaryMapLoader.Load(dir));
			if (SanctuaryMapLoader.IsActive)
				yield return parser.StartCoroutine(SanctuaryMapSaver.Save());
		}
	}
}
