using UnityEngine;
using SFB;
using System.IO;

public partial class AppMenu : MonoBehaviour
{

	#region SaveAs
	/// Sanctuary: pick (or make) a folder in the game's Maps folder; the
	/// folder name is the map's key, so it is made safe, and the map is
	/// written there with all its textures. An existing different map is
	/// never overwritten.
	void SaveSanctuaryMapAs()
	{
		var current = Sanctuary.SanctuaryMapLoader.Folder;
		string start = Sanctuary.SanctuaryPaths.MapsDir ?? Path.GetDirectoryName(current.Directory);
		var paths = StandaloneFileBrowser.OpenFolderPanel("Save Sanctuary map as (pick or create its folder)", start, false);
		if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
			return;
		SaveSanctuaryMapTo(paths[0]);
	}

	/// Also the -saveas command-line path, so the menu's logic is testable.
	public static void SaveSanctuaryMapTo(string chosen)
	{
		var current = Sanctuary.SanctuaryMapLoader.Folder;
		chosen = Path.GetFullPath(chosen).TrimEnd('\\', '/');
		if (string.Equals(chosen, current.Directory.TrimEnd('\\', '/'), System.StringComparison.OrdinalIgnoreCase))
		{
			MapLuaParser.Current.SaveMap();
			return;
		}

		string safe = Sanctuary.SanctuaryMapSaver.SafeFolderName(Path.GetFileName(chosen));
		if (string.IsNullOrEmpty(safe))
		{
			GenericPopup.ShowPopup(GenericPopup.PopupTypes.OneButton, "Save as", "That folder name has no usable characters.", "OK", null);
			return;
		}
		string target = Path.Combine(Path.GetDirectoryName(chosen), safe);

		if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0)
		{
			GenericPopup.ShowPopup(GenericPopup.PopupTypes.OneButton, "Save as",
				"The folder is not empty:\n" + target + "\nPick a new or empty folder; another map is never overwritten.", "OK", null);
			return;
		}
		// The dialog may have created a folder with an unsafe name; leave
		// only the safe one behind.
		if (target != chosen && Directory.Exists(chosen) && Directory.GetFileSystemEntries(chosen).Length == 0)
			Directory.Delete(chosen);

		MapLuaParser.Current.StartCoroutine(Sanctuary.SanctuaryMapSaver.Save(target));
	}

	/// File > Save As.
	public void SaveMapAs()
	{
		if (!MapLuaParser.IsMapLoaded)
			return;
		LateUpdate();
		SaveSanctuaryMapAs();
	}

	/// File > Save as new version: FAF vault versioning means nothing to
	/// Sanctuary; a new version is a Save As.
	public void SaveAsNewVersion()
	{
		if (!MapLuaParser.IsMapLoaded)
			return;
		SaveSanctuaryMapAs();
	}
	#endregion
}
