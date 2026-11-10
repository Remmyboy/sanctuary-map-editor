using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MapLuaParser : MonoBehaviour
{

	static string[] Args;
	void Start () {
		Args = System.Environment.GetCommandLineArgs();

		// -setInstallationPath <Sanctuary install, engine or Sanctuary_Data folder>
		for (int i = 1; i < Args.Length - 1; i++)
			if (Args[i] == "-setInstallationPath")
				Sanctuary.SanctuaryPaths.SetEngineDir(Args[i + 1]);

		// -newmap <maps dir> <name> <playable m> <armies> <ground m> <water m or -1>
		for (int i = 1; i < Args.Length - 6; i++)
		{
			if (Args[i] == "-newmap")
			{
				var o = new Sanctuary.MapFormat.NewMapOptions
				{
					Name = Args[i + 2],
					PlayableWidth = int.Parse(Args[i + 3]), PlayableLength = int.Parse(Args[i + 3]),
					Armies = int.Parse(Args[i + 4]),
					GroundHeight = double.Parse(Args[i + 5], System.Globalization.CultureInfo.InvariantCulture),
				};
				double w = double.Parse(Args[i + 6], System.Globalization.CultureInfo.InvariantCulture);
				o.Water = w >= 0; o.WaterLevel = w;
				StartCoroutine(NewMapFromCommandLine(Args[i + 1], o));
				break;
			}
		}

		// -sanmap <folder or .sanmap> [-screenshot <png>] [-quit]
		for (int i = 1; i < Args.Length - 1; i++)
		{
			if (Args[i] == "-sanmap")
			{
				StartCoroutine(OpenSanmapFromCommandLine(Args[i + 1]));
				break;
			}
		}
	}

	IEnumerator NewMapFromCommandLine(string mapsDir, Sanctuary.MapFormat.NewMapOptions o)
	{
		yield return null;
		yield return null;
		yield return StartCoroutine(Sanctuary.SanctuaryNewMap.Create(mapsDir, o));
		for (int i = 1; i < Args.Length; i++)
			if (Args[i] == "-screenshot" && i + 1 < Args.Length)
			{
				for (int f = 0; f < 30; f++) yield return null;
				yield return new WaitForEndOfFrame();
				ScreenCapture.CaptureScreenshot(Args[i + 1]);
				yield return null; yield return null;
			}
		for (int i = 1; i < Args.Length; i++)
			if (Args[i] == "-quit") OnFafEditorQuit.ForceQuit();
	}

	IEnumerator OpenSanmapFromCommandLine(string path)
	{
		// Let the preload scene and UI settle first.
		yield return null;
		yield return null;
		yield return StartCoroutine(Sanctuary.SanctuaryMapLoader.Load(path));

		string shot = null, saveTo = null, saveAs = null;
		bool quit = false, testEdit = false, menuSave = false;
		for (int i = 1; i < Args.Length; i++)
		{
			if (Args[i] == "-screenshot" && i + 1 < Args.Length) shot = Args[i + 1];
			if (Args[i] == "-saveto" && i + 1 < Args.Length) saveTo = Args[i + 1];
			if (Args[i] == "-saveas" && i + 1 < Args.Length) saveAs = Args[i + 1];
			if (Args[i] == "-menusave") menuSave = true;
			if (Args[i] == "-testedit") testEdit = true;
			if (Args[i] == "-testmarkers") Sanctuary.SanctuaryTestEdit.ApplyMarkers();
			if (Args[i] == "-testlayers") Sanctuary.SanctuaryTestEdit.ApplyLayers();
			if (Args[i] == "-testprops") Sanctuary.SanctuaryTestEdit.ApplyProps();
			if (Args[i] == "-testtools") Sanctuary.SanctuaryTestEdit.ApplyTools();
			if (Args[i] == "-testplace") Sanctuary.SanctuaryTestEdit.ApplyPlace();
				if (Args[i] == "-testdrag") Sanctuary.SanctuaryTestEdit.ApplyDrag();
			if (Args[i] == "-quit") quit = true;
		}
		if (testEdit) Sanctuary.SanctuaryTestEdit.Apply();
		for (int i = 1; i < Args.Length - 1; i++)
			if (Args[i] == "-previewdebug")
			{
				for (int f = 0; f < 5; f++) yield return new WaitForEndOfFrame();
				var persp = ScmapEditor.Current.PreviewRenderer.RenderPreview(0, 400, 400, false, false);
				CameraControler.SaveTexture(persp, Args[i + 1] + "-persp.png");
				yield return new WaitForEndOfFrame();
				Sanctuary.SanctuaryPreview.WriteTo(Sanctuary.SanctuaryMapLoader.Folder, Args[i + 1] + "-ortho.png");
			}
		if (saveTo != null && Sanctuary.SanctuaryMapLoader.IsActive)
			yield return StartCoroutine(Sanctuary.SanctuaryMapSaver.Save(saveTo));
		// The menu's own entry points, minus the dialogs.
		if (saveAs != null) { AppMenu.SaveSanctuaryMapTo(saveAs); yield return null; while (SavingMapProcess) yield return null; }
		if (menuSave) { SaveMap(); yield return null; while (SavingMapProcess) yield return null; }
		for (int i = 1; i < Args.Length - 1; i++)
			if (Args[i] == "-uitour")
				yield return StartCoroutine(Sanctuary.SanctuaryUiTour.Run(Args[i + 1]));
		if (shot != null)
		{
			for (int f = 0; f < 30; f++) yield return null;
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(shot);
			yield return null;
			yield return null;
			Debug.Log("Screenshot: " + shot);
		}
		if (quit) OnFafEditorQuit.ForceQuit();
	}

}
