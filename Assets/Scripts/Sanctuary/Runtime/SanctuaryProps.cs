// Sanctuary props in the editor: drawn with GPU instancing (no GameObject per
// prop - The Forge has 63,538), painted and erased with a brush, saved back
// as the map's prop groups.
//
// Coordinates: a prop's .sanmap position is Sanctuary world metres (x east,
// z north, origin south-west). The editor shows the map at 1/10 scale with
// world z = (sanZ - Size) / 10, so a prop's display matrix is
// T(x/10, y/10, (z-Size)/10) * R(rotation) * S(scale/10).
using System;
using System.Collections.Generic;
using System.Linq;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public class SanctuaryProps : MonoBehaviour
	{
		sealed class PropType
		{
			public string BlueprintPath;
			public SanPropBlueprint Blueprint;
			public Mesh Mesh;
			public Material Material;
			public readonly List<SanTransform> Transforms = new List<SanTransform>();
			public Matrix4x4[][] Batches;
			public bool Failed;
		}

		static SanctuaryProps current;
		static readonly Dictionary<string, PropType> types = new Dictionary<string, PropType>(StringComparer.OrdinalIgnoreCase);
		static readonly List<string> order = new List<string>();

		/// True when the props differ from the last load or save; the saver
		/// writes props only then, so an unedited map's prop list is never
		/// rewritten. Each edit gets a new version number and undo puts the
		/// old one back, so undoing to the saved state is clean again.
		public static bool Dirty => version != savedVersion;
		static int version, savedVersion, nextVersion;
		static void Edited() => version = ++nextVersion;
		public static bool Visible = true;

		public static void MarkSaved() => savedVersion = version;
		public static int Count => types.Values.Sum(t => t.Transforms.Count);

		static SanctuaryProps Current
		{
			get
			{
				if (current == null) current = new GameObject("SanctuaryProps").AddComponent<SanctuaryProps>();
				return current;
			}
		}

		public static void Clear()
		{
			foreach (var t in types.Values)
			{
				if (t.Mesh != null) Destroy(t.Mesh);
				if (t.Material != null) Destroy(t.Material);
			}
			types.Clear();
			order.Clear();
			version = savedVersion = ++nextVersion;
		}

		/// For undo: the lists as they are now (props are never changed in
		/// place, only replaced, so copying the lists is enough).
		public static Action Snapshot()
		{
			var lists = types.ToDictionary(kv => kv.Key, kv => kv.Value.Transforms.ToList(), StringComparer.OrdinalIgnoreCase);
			int v = version;
			return () =>
			{
				foreach (var kv in types)
				{
					var t = kv.Value;
					var want = lists.TryGetValue(kv.Key, out var l) ? l : new List<SanTransform>();
					if (t.Transforms.Count == want.Count && t.Transforms.SequenceEqual(want)) continue;
					t.Transforms.Clear();
					t.Transforms.AddRange(want);
					Rebuild(t);
				}
				version = v;
			};
		}

		/// The prop nearest (x, z) within `maxDistance` metres, if any.
		public static bool Pick(float x, float z, float maxDistance, out string type, out SanTransform prop)
		{
			type = null; prop = null;
			double best = maxDistance * maxDistance;
			foreach (var kv in types)
			{
				if (kv.Value.Failed) continue;
				foreach (var s in kv.Value.Transforms)
				{
					double d = (s.Position.X - x) * (s.Position.X - x) + (s.Position.Z - z) * (s.Position.Z - z);
					if (d < best) { best = d; type = kv.Key; prop = s; }
				}
			}
			return prop != null;
		}

		/// Swaps `old` for `replacement` (same type); false if `old` is gone.
		public static bool Replace(string type, SanTransform old, SanTransform replacement)
		{
			if (!types.TryGetValue(type, out var t)) return false;
			int i = t.Transforms.IndexOf(old);
			if (i < 0) return false;
			t.Transforms[i] = replacement;
			Rebuild(t);
			Edited();
			return true;
		}

		public static bool Remove(string type, SanTransform prop)
		{
			if (!types.TryGetValue(type, out var t) || !t.Transforms.Remove(prop)) return false;
			Rebuild(t);
			Edited();
			return true;
		}

		public static bool Contains(string type, SanTransform prop) =>
			type != null && types.TryGetValue(type, out var t) && t.Transforms.Contains(prop);

		/// Ground height in metres at Sanctuary (x, z).
		public static double GroundAt(double x, double z)
		{
			var terrain = ScmapEditor.Current.Teren;
			var world = new Vector3((float)x / 10f, 0, ((float)z - SanctuaryMapLoader.Size) / 10f);
			return (terrain.SampleHeight(world) + terrain.transform.position.y) * 10.0;
		}

		public static void LoadFromMap(SanMapDocument doc)
		{
			Clear();
			int failed = 0;
			foreach (var g in doc.GetProps())
			{
				var t = Type(g.BlueprintPath);
				t.Transforms.AddRange(g.Transforms);
				if (t.Failed) failed++;
				Rebuild(t);
			}
			Current.enabled = true;
			Debug.Log($"Sanctuary props: {Count} instances of {types.Count} types" + (failed > 0 ? $" ({failed} types could not be loaded and are kept unseen)" : ""));
		}

		/// The groups to save, in the map's original order, new types after.
		public static List<SanMapDocument.PropGroup> Groups() =>
			order.Select(k => types[k]).Where(t => t.Transforms.Count > 0)
				.Select(t => new SanMapDocument.PropGroup { BlueprintPath = t.BlueprintPath, Transforms = t.Transforms.ToList() }).ToList();

		static PropType Type(string blueprintPath)
		{
			if (types.TryGetValue(blueprintPath, out var t)) return t;
			t = new PropType { BlueprintPath = blueprintPath };
			types[blueprintPath] = t;
			order.Add(blueprintPath);
			try { LoadAssets(t); }
			catch (Exception e)
			{
				t.Failed = true;
				Debug.LogWarning("Sanctuary prop " + blueprintPath + ": " + e.Message);
			}
			return t;
		}

		static void LoadAssets(PropType t)
		{
			var bpBytes = SanGamedata.ReadExact(t.BlueprintPath) ?? throw new Exception("blueprint not found");
			t.Blueprint = SanPropBlueprint.Parse(System.Text.Encoding.UTF8.GetString(bpBytes));
			var model = SanModel.Read(SanGamedata.ReadExact(t.Blueprint.ModelPath) ?? throw new Exception("model not found: " + t.Blueprint.ModelPath), t.Blueprint.ModelPath);

			var mesh = new Mesh { name = model.Name };
			if (model.VertexCount > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var v = new Vector3[model.VertexCount];
			for (int i = 0; i < v.Length; i++) v[i] = new Vector3(model.Vertices[i * 3], model.Vertices[i * 3 + 1], model.Vertices[i * 3 + 2]);
			mesh.vertices = v;
			if (model.Normals.Length == model.Vertices.Length)
			{
				var n = new Vector3[v.Length];
				for (int i = 0; i < n.Length; i++) n[i] = new Vector3(model.Normals[i * 3], model.Normals[i * 3 + 1], model.Normals[i * 3 + 2]);
				mesh.normals = n;
			}
			if (model.Uv.Length == v.Length * 2)
			{
				var uv = new Vector2[v.Length];
				for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(model.Uv[i * 2], model.Uv[i * 2 + 1]);
				mesh.uv = uv;
			}
			mesh.triangles = model.Triangles;
			if (model.Normals.Length != model.Vertices.Length) mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			t.Mesh = mesh;

			var shader = Shader.Find("Sanctuary/Prop");
			var mat = new Material(shader) { enableInstancing = true, name = t.Blueprint.TpId };
			SanMaterial sm = null;
			var matBytes = SanGamedata.ReadExact(t.Blueprint.MaterialPath);
			if (matBytes != null) sm = SanMaterial.Parse(System.Text.Encoding.UTF8.GetString(matBytes));
			var albedo = sm?.BaseColorMap != null ? SanGamedata.LoadTexture(sm.BaseColorMap, false) : null;
			if (albedo != null) mat.SetTexture("_MainTex", albedo);
			else mat.SetColor("_Color", new Color(0.42f, 0.40f, 0.37f)); // wrecks: the unit shader's maps are not read
			mat.SetFloat("_AlphaTest", sm != null && sm.AlphaTest ? 1f : 0f);
			t.Material = mat;
		}

		static Matrix4x4 Display(SanTransform s)
		{
			float size = SanctuaryMapLoader.Size;
			var pos = new Vector3((float)s.Position.X / 10f, (float)s.Position.Y / 10f, ((float)s.Position.Z - size) / 10f);
			var rot = new Quaternion((float)s.Rotation.X, (float)s.Rotation.Y, (float)s.Rotation.Z, (float)s.Rotation.W);
			if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0) rot = Quaternion.identity;
			var scl = new Vector3((float)s.Scale.X, (float)s.Scale.Y, (float)s.Scale.Z) / 10f;
			return Matrix4x4.TRS(pos, rot.normalized, scl);
		}

		static void Rebuild(PropType t)
		{
			const int batch = 1023;
			int n = t.Transforms.Count;
			t.Batches = new Matrix4x4[(n + batch - 1) / batch][];
			for (int b = 0; b < t.Batches.Length; b++)
			{
				int len = Math.Min(batch, n - b * batch);
				var arr = new Matrix4x4[len];
				for (int i = 0; i < len; i++) arr[i] = Display(t.Transforms[b * batch + i]);
				t.Batches[b] = arr;
			}
		}

		void Update()
		{
			if (!Visible || !SanctuaryMapLoader.IsActive) return;
			foreach (var t in types.Values)
			{
				if (t.Failed || t.Mesh == null || t.Batches == null) continue;
				for (int sub = 0; sub < t.Mesh.subMeshCount; sub++)
					foreach (var b in t.Batches)
						Graphics.DrawMeshInstanced(t.Mesh, sub, t.Material, b, b.Length, null, UnityEngine.Rendering.ShadowCastingMode.On, true, 0);
			}
		}

		// ---- editing ----------------------------------------------------

		/// Every prop blueprint in the game's packs, by biome folder.
		public static SortedDictionary<string, List<string>> Catalog()
		{
			var by = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var stem in SanGamedata.StemsWithExtension(".santp"))
			{
				var parts = stem.Split('/');
				string group = parts.Length > 1 ? parts[1] : "Other";
				if (!by.TryGetValue(group, out var l)) by[group] = l = new List<string>();
				l.Add(stem + ".santp");
			}
			foreach (var l in by.Values) l.Sort(StringComparer.OrdinalIgnoreCase);
			return by;
		}

		/// Places `count` props of a type at random within `radius` metres of
		/// (x, z), on the ground, with random heading and the blueprint's
		/// random scale range.
		public static int Paint(string blueprintPath, float x, float z, float radius, int count, System.Random rng)
		{
			var t = Type(blueprintPath);
			if (t.Failed) return 0;
			var terrain = ScmapEditor.Current.Teren;
			int placed = 0;
			for (int i = 0; i < count; i++)
			{
				double a = rng.NextDouble() * Math.PI * 2, r = radius * Math.Sqrt(rng.NextDouble());
				double px = x + Math.Cos(a) * r, pz = z + Math.Sin(a) * r;
				if (px < 0 || pz < 0 || px > SanctuaryMapLoader.Size || pz > SanctuaryMapLoader.Size) continue;
				var world = new Vector3((float)px / 10f, 0, ((float)pz - SanctuaryMapLoader.Size) / 10f);
				double py = (terrain.SampleHeight(world) + terrain.transform.position.y) * 10.0;
				double yaw = rng.NextDouble() * Math.PI * 2;
				double s = t.Blueprint.ScaleMin + rng.NextDouble() * (t.Blueprint.ScaleMax - t.Blueprint.ScaleMin);
				t.Transforms.Add(new SanTransform
				{
					Position = new SanVec3(Math.Round(px, 3), Math.Round(py, 3), Math.Round(pz, 3)),
					Rotation = new SanQuat(0, Math.Round(Math.Sin(yaw / 2), 7), 0, Math.Round(Math.Cos(yaw / 2), 7)),
					Scale = new SanVec3(Math.Round(s, 4), Math.Round(s, 4), Math.Round(s, 4)),
				});
				placed++;
			}
			if (placed > 0) { Rebuild(t); Edited(); }
			return placed;
		}

		/// Removes every prop (of any type, or only `onlyType`) within
		/// `radius` metres of (x, z).
		public static int Erase(float x, float z, float radius, string onlyType = null)
		{
			int removed = 0;
			double r2 = radius * radius;
			foreach (var t in types.Values)
			{
				if (onlyType != null && !string.Equals(onlyType, t.BlueprintPath, StringComparison.OrdinalIgnoreCase)) continue;
				int before = t.Transforms.Count;
				t.Transforms.RemoveAll(s => (s.Position.X - x) * (s.Position.X - x) + (s.Position.Z - z) * (s.Position.Z - z) <= r2);
				if (t.Transforms.Count != before) { removed += before - t.Transforms.Count; Rebuild(t); }
			}
			if (removed > 0) Edited();
			return removed;
		}
	}
}
