// Where Sanctuary: Shattered Sun is installed. Found through Steam's library
// folders; a path saved in preferences wins.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryPaths
	{
		const string PrefKey = "SanctuaryInstallPath";
		const string EnginePrefix = "Sanctuary_Data";

		/// Steam folder names of the builds, newest first.
		static readonly string[] SteamFolders = { "Sanctuary Shattered Sun Playtest", "Sanctuary Shattered Sun" };

		static string cached;

		/// The `engine` folder (holds Sanctuary_Data), or null when not found.
		public static string EngineDir
		{
			get
			{
				if (cached != null && Directory.Exists(cached)) return cached;
				string saved = PlayerPrefs.GetString(PrefKey, "");
				if (IsEngineDir(saved)) return cached = saved;
				foreach (var d in Candidates())
					if (IsEngineDir(d)) return cached = d;
				return null;
			}
		}

		public static void SetEngineDir(string dir)
		{
			// Accept the install root, the engine folder or Sanctuary_Data.
			foreach (var d in new[] { dir, Path.Combine(dir ?? "", "engine"), Path.GetDirectoryName(dir ?? "") })
				if (IsEngineDir(d)) { PlayerPrefs.SetString(PrefKey, d); cached = d; return; }
			Debug.LogWarning("Not a Sanctuary install: " + dir);
		}

		public static string MapsDir => EngineDir == null ? null : Path.Combine(EngineDir, EnginePrefix, "Maps");
		public static string GamedataDir => EngineDir == null ? null : Path.Combine(EngineDir, EnginePrefix, "Gamedata");

		static bool IsEngineDir(string d) =>
			!string.IsNullOrEmpty(d) && Directory.Exists(Path.Combine(d, EnginePrefix, "Gamedata"));

		static IEnumerable<string> Candidates()
		{
			foreach (var lib in SteamLibraries())
				foreach (var f in SteamFolders)
					yield return Path.Combine(lib, "steamapps", "common", f, "engine");
		}

		static IEnumerable<string> SteamLibraries()
		{
			var libs = new List<string>();
			string steam = null;
			try
			{
				using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
					steam = k?.GetValue("SteamPath") as string;
			}
			catch (Exception) { }
			if (string.IsNullOrEmpty(steam)) steam = @"C:\Program Files (x86)\Steam";
			libs.Add(steam);

			string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
			if (File.Exists(vdf))
			{
				foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
					libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
			}
			return libs;
		}
	}
}
