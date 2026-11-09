using UnityEngine;

/// Folders the editor's file dialogs start in. (Name kept from the FAF
/// editor; the Forged Alliance install/gamedata/FAF paths it used to hold
/// are gone - Sanctuary.SanctuaryPaths finds the game.)
public static class EnvPaths
{
	const string ImagePathKey = "ImagePath";

	/// The game's Maps folder, or My Documents when the game is not found.
	public static string GetMapsPath()
	{
		string maps = Sanctuary.SanctuaryPaths.MapsDir;
		return string.IsNullOrEmpty(maps) ? MyDocuments + "/" : maps.Replace("\\", "/") + "/";
	}

	public static string GetImagePath() => PlayerPrefs.GetString(ImagePathKey, MyDocuments);

	public static void SetImagePath(string value) => PlayerPrefs.SetString(ImagePathKey, value);

	public static string MyDocuments =>
		System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments).Replace("\\", "/");

	/// Last folder used by a given dialog, if it still exists.
	public static string GetLastPath(string key, string defaultPath)
	{
		if (string.IsNullOrEmpty(defaultPath))
			defaultPath = MyDocuments;
		string saved = PlayerPrefs.GetString(key, defaultPath);
		if (System.IO.Directory.Exists(saved))
			return saved;
		return System.IO.Directory.Exists(MyDocuments) ? MyDocuments : "";
	}

	public static void SetLastPath(string key, string path) => PlayerPrefs.SetString(key, path);
}
