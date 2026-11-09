using UnityEngine;
using UnityEngine.UI;
using SFB;
using Ozone.UI;

/// Edit > Preferences. (Class name kept from the FAF editor: the scene binds
/// to it.) Install folder of Sanctuary: Shattered Sun, backup folder, undo
/// depth, 2D markers, UI scale. The FA install, FAF data folder, FA maps
/// folder and "play in FA" options are gone; their fields are hidden in the
/// scene and left unread.
public class FafEditorSettings : MonoBehaviour
{
	public InputField PathField;
	public InputField FafPathField;
	public InputField MapsPathField;
	public InputField BackupPathField;
	public Slider HistorySlider;
	public Undo History;

	public Dropdown PlayAs;
	public Toggle FogOfWar;
	public Toggle Markers2D;

	public UiTextField UiScale;

	public const int DefaultUndoHistory = 50;
	public const string UndoHistory = "UndoHistrySteps";

	void OnEnable()
	{
		PathField.text = Sanctuary.SanctuaryPaths.EngineDir ?? "";
		BackupPathField.text = Sanctuary.SanctuaryMapSaver.BackupRoot;
		Markers2D.isOn = GetMarkers2D();

		UiScale.SetValue(GetUiScale());
		UiScaler.UpdateUiScale();
	}

	private void OnDisable()
	{
		UiScaler.UpdateUiScale();
	}

	public void Open()
	{
		HistorySlider.value = PlayerPrefs.GetInt(UndoHistory, DefaultUndoHistory);
		gameObject.SetActive(true);
	}

	public void Close()
	{
		OnUiScaleChanged();
		gameObject.SetActive(false);
	}

	public void Save()
	{
		if (!string.IsNullOrEmpty(PathField.text) && PathField.text != Sanctuary.SanctuaryPaths.EngineDir)
		{
			Sanctuary.SanctuaryPaths.SetEngineDir(PathField.text);
			if (Sanctuary.SanctuaryPaths.EngineDir == null)
				GenericInfoPopup.ShowInfo("Not a Sanctuary: Shattered Sun install:\n" + PathField.text);
		}

		if (!string.IsNullOrEmpty(BackupPathField.text))
			Sanctuary.SanctuaryMapSaver.BackupRoot = BackupPathField.text;

		PlayerPrefs.SetInt(UndoHistory, (int)HistorySlider.value);
		if (History) History.MaxHistoryLength = (int)HistorySlider.value;

		if (GetMarkers2D() != Markers2D.isOn)
		{
			if (!Markers2D.isOn)
				Markers.MarkersControler.ForceResetMarkers2D();
			PlayerPrefs.SetInt("Markers_2D", Markers2D.isOn ? 1 : 0);
		}

		PlayerPrefs.SetFloat("UiScale", UiScale.value);

		PlayerPrefs.Save();
		gameObject.SetActive(false);
	}

	public void BrowseGamedataPath()
	{
		var paths = StandaloneFileBrowser.OpenFolderPanel("Select the Sanctuary: Shattered Sun install folder", PathField.text, false);
		if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
			PathField.text = paths[0];
	}

	public void BrowseBackupPath()
	{
		var paths = StandaloneFileBrowser.OpenFolderPanel("Select the folder for map backups", BackupPathField.text, false);
		if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
			BackupPathField.text = paths[0];
	}

	/// "Default": forget the saved path and find the game through Steam again.
	public void ResetGamedata()
	{
		PlayerPrefs.DeleteKey("SanctuaryInstallPath");
		PathField.text = Sanctuary.SanctuaryPaths.EngineDir ?? "";
	}

	public void ResetBackup()
	{
		PlayerPrefs.DeleteKey(Sanctuary.SanctuaryMapSaver.BackupPrefKey);
		BackupPathField.text = Sanctuary.SanctuaryMapSaver.BackupRoot;
	}

	public static bool GetMarkers2D()
	{
		return PlayerPrefs.GetInt("Markers_2D", 0) == 1;
	}

	public static float GetUiScale()
	{
		return PlayerPrefs.GetFloat("UiScale", 1f);
	}

	public void OnUiScaleChanged()
	{
		UiScaler.TempChangeUiScale(UiScale.value);
	}
}
