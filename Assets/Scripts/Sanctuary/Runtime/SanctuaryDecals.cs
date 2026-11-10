// The map's decals ("decals": [{blueprintPath, transforms: [{position,
// rotation, scale}]}], as SanMap.DecalType/DecalTransform). The editor can't
// draw them (they're HDRP decal projectors); it shows each as a ring and can
// add, erase and list them, and keeps alloy_spot decals with their markers.
// Edits go straight into the map's JSON, which the save writes as it is.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryDecals
	{
		public const string AlloySpot = "Environment/Common/Decals/alloy_spot.sandecal";

		static JArray Array()
		{
			var doc = SanctuaryMapLoader.Folder?.Map;
			if (doc == null) return null;
			if (!(doc.Json["decals"] is JArray a)) doc.Json["decals"] = a = new JArray();
			return a;
		}

		public static IEnumerable<(string type, int count)> Types()
		{
			var a = Array();
			if (a == null) yield break;
			foreach (var t in a.OfType<JObject>())
				yield return (t.Value<string>("blueprintPath") ?? "", (t["transforms"] as JArray)?.Count ?? 0);
		}

		public static int Count => Types().Sum(t => t.count);

		/// Every decal's map position (metres, origin south-west) and size.
		public static IEnumerable<(string type, float x, float z, float size)> All()
		{
			var a = Array();
			if (a == null) yield break;
			foreach (var t in a.OfType<JObject>())
				foreach (var tr in (t["transforms"] as JArray ?? new JArray()).OfType<JObject>())
					yield return (t.Value<string>("blueprintPath"), P(tr, "x"), P(tr, "z"), (float?)tr["scale"]?["x"] ?? 1f);
		}

		static float P(JObject tr, string axis) => (float?)tr["position"]?[axis] ?? 0f;

		/// For undo: the whole decal list as it is now.
		public static Action Snapshot()
		{
			var copy = (JArray)(Array()?.DeepClone() ?? new JArray());
			return () =>
			{
				var doc = SanctuaryMapLoader.Folder?.Map;
				if (doc != null) doc.Json["decals"] = copy.DeepClone();
			};
		}

		static JObject TypeEntry(string blueprint, bool create)
		{
			var a = Array();
			var t = a?.OfType<JObject>().FirstOrDefault(o => string.Equals(o.Value<string>("blueprintPath"), blueprint, StringComparison.OrdinalIgnoreCase));
			if (t == null && create && a != null)
			{
				t = new JObject { ["blueprintPath"] = blueprint, ["transforms"] = new JArray() };
				a.Add(t);
			}
			return t;
		}

		// A decal lying flat, taken from There Is Time's (they project down,
		// rotated about the vertical by a random angle).
		static readonly Quaternion Flat = new Quaternion(-0.559102f, 0.432903f, -0.432903f, -0.559102f);

		/// A decal of `blueprint` at (x, z), `size` metres, turned `yaw`
		/// degrees. Copies the rotation of an existing decal of that type
		/// (turned by yaw), so it projects the same way.
		public static void Add(string blueprint, float x, float z, float size, float yaw)
		{
			var t = TypeEntry(blueprint, true);
			var list = (JArray)t["transforms"];
			var template = list.OfType<JObject>().FirstOrDefault()?["rotation"] as JObject;
			var q = template == null ? Flat : new Quaternion((float)template["x"], (float)template["y"], (float)template["z"], (float)template["w"]);
			q = Quaternion.AngleAxis(yaw, Vector3.up) * q;
			list.Add(Transform(x, (float)SanctuaryProps.GroundAt(x, z), z, q, size));
		}

		static JObject Transform(float x, float y, float z, Quaternion q, float s) => new JObject
		{
			["position"] = new JObject { ["x"] = x, ["y"] = y, ["z"] = z },
			["rotation"] = new JObject { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w },
			["scale"] = new JObject { ["x"] = s, ["y"] = s, ["z"] = s },
		};

		/// Removes decals centred within `radius` of (x, z); only `onlyType`
		/// when given. Types left empty are removed too.
		public static int Erase(float x, float z, float radius, string onlyType = null)
		{
			var a = Array();
			if (a == null) return 0;
			int n = 0;
			foreach (var t in a.OfType<JObject>().ToList())
			{
				if (onlyType != null && !string.Equals(t.Value<string>("blueprintPath"), onlyType, StringComparison.OrdinalIgnoreCase)) continue;
				var list = t["transforms"] as JArray;
				if (list == null) continue;
				foreach (var tr in list.OfType<JObject>().ToList())
				{
					float dx = P(tr, "x") - x, dz = P(tr, "z") - z;
					if (dx * dx + dz * dz <= radius * radius) { tr.Remove(); n++; }
				}
				if (list.Count == 0) t.Remove();
			}
			return n;
		}

		/// Keeps alloy_spot decals with the alloy markers: a decal within 4 m
		/// of a marker's old position follows it, goes when it is deleted, and
		/// a new marker gets one. Only on maps that draw alloy spots this way
		/// (There Is Time does; generated and converted maps don't).
		public static int SyncAlloySpots(List<(Vector2 from, Vector2? to)> changes, List<Vector2> added)
		{
			var t = TypeEntry(AlloySpot, false);
			var list = t?["transforms"] as JArray;
			if (list == null || list.Count == 0) return 0;
			int n = 0;
			float size = list.OfType<JObject>().Select(o => (float?)o["scale"]?["x"] ?? 8f).OrderBy(v => v).ElementAt(list.Count / 2);
			foreach (var (from, to) in changes)
			{
				var near = list.OfType<JObject>().Where(o => { float dx = P(o, "x") - from.x, dz = P(o, "z") - from.y; return dx * dx + dz * dz < 16f; }).ToList();
				foreach (var o in near)
				{
					if (to == null) { o.Remove(); n++; continue; }
					var dst = to.Value + new Vector2(P(o, "x") - from.x, P(o, "z") - from.y);
					o["position"] = new JObject { ["x"] = dst.x, ["y"] = (float)SanctuaryProps.GroundAt(dst.x, dst.y), ["z"] = dst.y };
					n++;
				}
			}
			foreach (var p in added) { Add(AlloySpot, p.x, p.y, size, UnityEngine.Random.Range(0f, 360f)); n++; }
			return n;
		}

		/// Decal blueprints in the game's data, by folder.
		public static List<string> Catalog() =>
			SanGamedata.StemsWithExtension(".sandecal").Select(s => s + ".sandecal").OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
	}
}
