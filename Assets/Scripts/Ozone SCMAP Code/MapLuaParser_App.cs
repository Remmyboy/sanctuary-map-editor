using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MapLuaParser : MonoBehaviour
{

	static string[] Args;
	void Start () {
		Args = System.Environment.GetCommandLineArgs();

		if (Args.Length > 0)

			if (Args.Length == 3 && Args[1] == "-setInstallationPath")
			{
				EnvPaths.SetInstallationPath(Args[2]);
			}

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

		if (Args.Length >= 6)
		{
			if (Args[1] == "-renderPreviewImage" || Args[1] == "-renderPreviewImageNoProps" || Args[1] == "-renderPreviewImageNoDecals" || Args[1] == "-renderPreviewImageNoPropsDecals")
			{

				GetGamedataFile.MipmapBias = -0.9f;

				bool Props = Args[1] == "-renderPreviewImage" || Args[1] == "-renderPreviewImageNoDecals";
				bool Decals = Args[1] == "-renderPreviewImage" || Args[1] == "-renderPreviewImageNoProps";

				int Widht = int.Parse(Args[2]);
				int Height = int.Parse(Args[3]);
				Debug.Log("Begin coroutine");
				StartCoroutine(RenderImageAndClose(Props, Decals, Widht, Height, Args[4], Args[5]));
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

	public IEnumerator RenderImageAndClose(bool Props, bool Decals, int Width, int Height, string MapPath, string ImagePath)
	{
		var LoadScmapFile = MapLuaParser.Current.StartCoroutine(ForceLoadMapAtPath(MapPath, Props, Decals));
		yield return LoadScmapFile;

		QualitySettings.lodBias = 100000;
		QualitySettings.shadowDistance = CameraControler.Current.transform.localPosition.y * 1.1f;

		if (Decals)
		{
			OzoneDecals.OzoneDecalRenderer.CutoffMultiplier = 100000;


		}

		CameraControler.Current.RestartCam(true);
yield return new WaitForEndOfFrame();
yield return new WaitForEndOfFrame();
yield return new WaitForEndOfFrame();

yield return CameraControler.Current.StartCoroutine(
    CameraControler.Current.RenderCamera(Width, Height, ImagePath));


		if (Decals)
		{
			OzoneDecals.OzoneDecalRenderer.CutoffMultiplier = 1;


		}

		Debug.Log("Success! Preview rendered to: " + ImagePath);

		OnFafEditorQuit.ForceQuit();
	}

}
