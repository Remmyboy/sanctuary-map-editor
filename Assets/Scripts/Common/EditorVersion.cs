using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System;

public class EditorVersion : MonoBehaviour
{
	private const string EditorBuildVersion = "v0.3"; // Sanctuary Map Editor; the FAF editor it grew from was v1.2
	private const string EditorPrereleaseTag = "";  // add rcX here for release candidates
	private static string FoundUrl;
	public bool SearchForNew = true;

	string VersionString
	{
		get
		{
			if (EditorPrereleaseTag.Length == 0)
				return EditorBuildVersion;

			return EditorBuildVersion + "-" + EditorPrereleaseTag;
		}
	}

	void Start()
	{
		GetComponent<Text>().text = VersionString;
		if(SearchForNew)
			StartCoroutine(FindLatest());
	}

	// Update check against this fork's releases (none yet: the check finds no tag and stays quiet).
	[System.NonSerialized] public string url = "https://github.com/Remmyboy/sanctuary-map-editor/releases/latest";
	IEnumerator FindLatest()
	{
		DownloadHandler dh = new DownloadHandlerBuffer();
        using UnityWebRequest www = new(url, "GET", dh, null);
        yield return www.SendWebRequest();
        string[] Tags = www.url.Replace("\\", "/").Split("/".ToCharArray());
        if (Tags.Length > 0)
        {
	        string LatestRelease = Tags[^1];
            FoundUrl = www.url;
            Debug.Log("Editor version: " + VersionString);
            if (shouldUpdate(LatestRelease))
            {
                Debug.Log("New version available: " + LatestRelease);
                GenericPopup.ShowPopup(GenericPopup.PopupTypes.TwoButton, "New version",
                    "New version of Map Editor is avaiable.\nCurrent: " + VersionString + "\t\tNew: " + LatestRelease + "\nDo you want to download it now?",
                    "Download", DownloadLatest,
                    "Cancel", CancelDownload
                    );
            }
        }
    }

	static bool shouldUpdate(string LatestRelease)
	{
		// Tags are v0.2, v0.2.1, ...: compare them as versions, not floats.
		Version latest = ParseTag(LatestRelease), current = ParseTag(EditorBuildVersion);
		if (latest == null || current == null) return false;
		int c = current.CompareTo(latest);
		return c < 0 || (c == 0 && EditorPrereleaseTag.Length > 0);
	}

	static Version ParseTag(string tag)
	{
		string s = tag.ToLower().Replace(" ", "").TrimStart('v');
		int dash = s.IndexOf('-');
		if (dash >= 0) s = s.Substring(0, dash);
		if (!s.Contains(".")) s += ".0";
		if (Version.TryParse(s, out Version v)) return v;
		Debug.LogWarning("Can't read a version from release tag: " + tag);
		return null;
	}

	public void DownloadLatest()
	{
		Application.OpenURL(FoundUrl);
	}

	public void CancelDownload()
	{

	}
}
