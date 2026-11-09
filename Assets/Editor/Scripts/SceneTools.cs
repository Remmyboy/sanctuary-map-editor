using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Headless scene inspection:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod SceneTools.Dump -out <file>
/// Writes every GameObject path in MapEditor.unity and Preload.unity with its
/// components and, for buttons and toggles, the persistent calls they make.
public static class SceneTools
{
	public static void Dump()
	{
		string outPath = Arg("-out") ?? "scene-dump.txt";
		var sb = new StringBuilder();
		foreach (var scenePath in new[] { "Assets/Preload.unity", "Assets/MapEditor.unity" })
		{
			var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
			sb.AppendLine("### " + scenePath);
			foreach (var root in scene.GetRootGameObjects())
				Walk(root.transform, "", sb);
		}
		File.WriteAllText(outPath, sb.ToString());
		Debug.Log("SceneTools: wrote " + outPath);
	}

	static void Walk(Transform t, string parent, StringBuilder sb)
	{
		string path = parent.Length == 0 ? t.name : parent + "/" + t.name;
		var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is RectTransform) && !(c is CanvasRenderer))
			.Select(c => c.GetType().Name);
		string extra = "";
		var text = t.GetComponent<Text>();
		if (text != null) extra += " text=\"" + text.text.Replace("\n", "\\n") + "\"";
		var button = t.GetComponent<Button>();
		if (button != null) extra += Calls(button, "m_OnClick");
		var toggle = t.GetComponent<Toggle>();
		if (toggle != null) extra += Calls(toggle, "onValueChanged");
		sb.AppendLine((t.gameObject.activeSelf ? "" : "(off) ") + path + " [" + string.Join(",", comps) + "]" + extra);
		for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path, sb);
	}

	/// Persistent calls with their string argument, read from serialized
	/// data (UnityEvent has no public accessor for arguments).
	static string Calls(Component owner, string eventField)
	{
		var sb = new StringBuilder();
		var calls = new SerializedObject(owner).FindProperty(eventField + ".m_PersistentCalls.m_Calls");
		if (calls == null) return "";
		for (int i = 0; i < calls.arraySize; i++)
		{
			var c = calls.GetArrayElementAtIndex(i);
			var target = c.FindPropertyRelative("m_Target").objectReferenceValue;
			string method = c.FindPropertyRelative("m_MethodName").stringValue;
			string arg = c.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
			sb.Append(" -> " + (target != null ? target.GetType().Name : "null") + "." + method + (string.IsNullOrEmpty(arg) ? "" : "(\"" + arg + "\")"));
		}
		return sb.ToString();
	}

	public static string Arg(string name)
	{
		var a = System.Environment.GetCommandLineArgs();
		for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
		return null;
	}
}
