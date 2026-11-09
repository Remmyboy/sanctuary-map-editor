// The .sanmap JSON. The game populates EM.Map.SanMap from it with Newtonsoft
// (JsonConvert.PopulateObject) and writes it back with Formatting.Indented.
//
// The document keeps the whole JObject, so the ~80 lighting, fog, water and
// sky fields this editor has no UI for survive a load/save untouched; the
// typed accessors below cover what the editor reads and edits. Field names
// and defaults are the game's (EM.Map.SanMap, Playtest build 25856019).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sanctuary.MapFormat
{
	public struct SanVec3
	{
		public double X, Y, Z;
		public SanVec3(double x, double y, double z) { X = x; Y = y; Z = z; }
		public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
	}

	public struct SanQuat
	{
		public double X, Y, Z, W;
		public SanQuat(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
		public static SanQuat Identity => new SanQuat(0, 0, 0, 1);
	}

	public sealed class SanTransform
	{
		public SanVec3 Position;
		public SanQuat Rotation = SanQuat.Identity;
		public SanVec3 Scale = new SanVec3(1, 1, 1);
	}

	public sealed class SanArea
	{
		public double X, Y, Width, Height;
	}

	public sealed class SanMapDocument
	{
		public const string Extension = ".sanmap";
		public const int FormatVersion = 3;
		public const int StratumCount = 9;
		public const string PlayableAreaKey = "PlayableArea";

		public JObject Json { get; }

		public SanMapDocument(JObject json) { Json = json ?? throw new ArgumentNullException(nameof(json)); }

		// ---- io ---------------------------------------------------------

		static readonly JsonSerializerSettings ReadSettings = new JsonSerializerSettings
		{
			// Keep numbers as written: 17.5 stays a double, 4097 an integer.
			FloatParseHandling = FloatParseHandling.Double,
			DateParseHandling = DateParseHandling.None,
		};

		public static SanMapDocument Load(string path)
		{
			using (var sr = new StreamReader(path, Encoding.UTF8))
			using (var jr = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
			{
				var tok = JToken.ReadFrom(jr);
				if (!(tok is JObject o)) throw new InvalidDataException($"{path}: top level is {tok.Type}, not an object");
				return new SanMapDocument(o);
			}
		}

		public static SanMapDocument Parse(string json)
		{
			var o = JsonConvert.DeserializeObject<JObject>(json, ReadSettings);
			return new SanMapDocument(o);
		}

		/// Writes indented JSON (as the game does), UTF-8 without BOM.
		public void Save(string path)
		{
			Json["fileVersion"] = FormatVersion;
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(path, ToJsonString(), new UTF8Encoding(false));
		}

		public string ToJsonString() => Json.ToString(Formatting.Indented);

		// ---- header -----------------------------------------------------

		public string Name { get => Str("name"); set => Json["name"] = value; }
		public string Credits { get => Str("credits"); set => Json["credits"] = value; }
		public int MapVersion { get => Int("mapVersion", 1); set => Json["mapVersion"] = value; }

		/// Terrain extent in metres (x). The playable area may be smaller.
		public int Width { get => Int("width", 0); set => Json["width"] = value; }
		/// Terrain extent in metres (z).
		public int Length { get => Int("length", 0); set => Json["length"] = value; }
		/// Metres a raw heightmap value of 65536 would reach.
		public int Height { get => Int("height", 0); set => Json["height"] = value; }

		/// The game falls back to width + 1 when the field is absent.
		public int HeightmapResolution
		{
			get { int r = Int("heightmapResolution", 0); return r != 0 ? r : Width + 1; }
			set => Json["heightmapResolution"] = value;
		}

		public bool HasWater { get => Bool("hasWater", false); set => Json["hasWater"] = value; }
		public double WaterLevel { get => Dbl("waterLevel", 0); set => Json["waterLevel"] = value; }
		public double WaterDepth { get => Dbl("waterDepth", 8); set => Json["waterDepth"] = value; }

		public string Shader { get => Str("shader") ?? "RTS/TerrainLit"; set => Json["shader"] = value; }

		// ---- areas ------------------------------------------------------

		/// The engine fences play with the area named exactly "PlayableArea";
		/// a rectangle under any other name is ignored.
		public SanArea PlayableArea
		{
			get
			{
				var a = (Json["areas"] as JObject)?[PlayableAreaKey] as JObject;
				if (a == null) return null;
				return new SanArea { X = D(a, "x"), Y = D(a, "y"), Width = D(a, "width"), Height = D(a, "height") };
			}
			set
			{
				var areas = Json["areas"] as JObject;
				if (areas == null) Json["areas"] = areas = new JObject();
				if (value == null) { areas.Remove(PlayableAreaKey); return; }
				areas[PlayableAreaKey] = new JObject
				{
					["x"] = value.X, ["y"] = value.Y, ["width"] = value.Width, ["height"] = value.Height,
				};
			}
		}

		// ---- armies and markers ----------------------------------------

		/// Army keys in file order. This, not the spawn-marker count, is the
		/// player count the lobby uses.
		public IReadOnlyList<string> ArmyNames =>
			(Json["armies"] as JObject)?.Properties().Select(p => p.Name).ToList() ?? new List<string>();

		/// Marker type names ("Spawn", "Alloys", ...) in file order.
		public IReadOnlyList<string> MarkerTypes =>
			(Json["markers"] as JObject)?.Properties().Select(p => p.Name).ToList() ?? new List<string>();

		public bool IsResourceMarkerType(string type) =>
			((Json["markers"] as JObject)?[type] as JObject)?["resource"]?.Value<bool>() ?? false;

		/// Markers of one type, name -> transform, in file order.
		public List<KeyValuePair<string, SanTransform>> GetMarkers(string type)
		{
			var list = new List<KeyValuePair<string, SanTransform>>();
			var t = ((Json["markers"] as JObject)?[type] as JObject)?["transforms"] as JObject;
			if (t == null) return list;
			foreach (var p in t.Properties())
				if (p.Value is JObject o) list.Add(new KeyValuePair<string, SanTransform>(p.Name, ReadTransform(o)));
			return list;
		}

		/// Replaces every marker of one type. Each marker object keeps any
		/// fields beyond position/rotation/scale that it already had.
		public void SetMarkers(string type, bool resource, IEnumerable<KeyValuePair<string, SanTransform>> markers)
		{
			var all = Json["markers"] as JObject;
			if (all == null) Json["markers"] = all = new JObject();
			var mt = all[type] as JObject;
			if (mt == null) all[type] = mt = new JObject();
			var old = mt["transforms"] as JObject ?? new JObject();
			mt["resource"] = resource;
			var t = new JObject();
			foreach (var kv in markers)
			{
				var o = old[kv.Key] as JObject ?? new JObject();
				WriteTransform(o, kv.Value);
				t[kv.Key] = o;
			}
			mt["transforms"] = t;
		}

		/// Spawn markers are "Spawn" type; their names are ARMY_n (converted)
		/// or Army_n (stock), so match army names case-insensitively.
		public SanTransform FindSpawn(string armyName)
		{
			foreach (var kv in GetMarkers("Spawn"))
				if (string.Equals(kv.Key, armyName, StringComparison.OrdinalIgnoreCase)) return kv.Value;
			return null;
		}

		// ---- props ------------------------------------------------------

		public sealed class PropGroup
		{
			public string BlueprintPath;
			public List<SanTransform> Transforms = new List<SanTransform>();
		}

		public List<PropGroup> GetProps()
		{
			var list = new List<PropGroup>();
			if (!(Json["props"] is JArray a)) return list;
			foreach (var g in a.OfType<JObject>())
			{
				var pg = new PropGroup { BlueprintPath = (string)g["blueprintPath"] };
				if (g["transforms"] is JArray ts)
					foreach (var t in ts.OfType<JObject>()) pg.Transforms.Add(ReadTransform(t));
				list.Add(pg);
			}
			return list;
		}

		public void SetProps(IEnumerable<PropGroup> groups)
		{
			var a = new JArray();
			foreach (var g in groups)
			{
				if (g.Transforms.Count == 0) continue;
				var ts = new JArray();
				foreach (var t in g.Transforms) { var o = new JObject(); WriteTransform(o, t); ts.Add(o); }
				a.Add(new JObject { ["blueprintPath"] = g.BlueprintPath, ["transforms"] = ts });
			}
			Json["props"] = a;
		}

		public int PropInstanceCount =>
			(Json["props"] as JArray)?.OfType<JObject>().Sum(g => (g["transforms"] as JArray)?.Count ?? 0) ?? 0;

		// ---- stratum layers --------------------------------------------

		/// The nine stratum layer objects (0 = base, 1..8 = masked layers),
		/// for direct editing; fields: albedo/normal/mask {path}, tileSize
		/// {x,y}, tileSizeFar, normalScale, diffuseRemap {r,g,b,a}, ...
		public JArray StratumLayers => Json["stratumLayers"] as JArray;

		/// Texture path of one stratum slot ("albedo", "normal" or "mask").
		/// "map/..." paths are relative to the map folder.
		public string GetStratumTexture(int layer, string slot) =>
			(StratumLayers?[layer]?[slot] as JObject)?["path"]?.Value<string>();

		// ---- helpers ----------------------------------------------------

		static SanTransform ReadTransform(JObject o)
		{
			var t = new SanTransform();
			if (o["position"] is JObject p) t.Position = new SanVec3(D(p, "x"), D(p, "y"), D(p, "z"));
			if (o["rotation"] is JObject r) t.Rotation = new SanQuat(D(r, "x"), D(r, "y"), D(r, "z"), D(r, "w", 1));
			if (o["scale"] is JObject s) t.Scale = new SanVec3(D(s, "x", 1), D(s, "y", 1), D(s, "z", 1));
			return t;
		}

		static void WriteTransform(JObject o, SanTransform t)
		{
			o["position"] = new JObject { ["x"] = t.Position.X, ["y"] = t.Position.Y, ["z"] = t.Position.Z };
			o["rotation"] = new JObject { ["x"] = t.Rotation.X, ["y"] = t.Rotation.Y, ["z"] = t.Rotation.Z, ["w"] = t.Rotation.W };
			o["scale"] = new JObject { ["x"] = t.Scale.X, ["y"] = t.Scale.Y, ["z"] = t.Scale.Z };
		}

		static double D(JObject o, string k, double def = 0) => o[k] != null && o[k].Type != JTokenType.Null ? o[k].Value<double>() : def;
		string Str(string k) => Json[k]?.Type == JTokenType.String ? (string)Json[k] : null;
		int Int(string k, int def) => Json[k] != null && Json[k].Type != JTokenType.Null ? Json[k].Value<int>() : def;
		double Dbl(string k, double def) => Json[k] != null && Json[k].Type != JTokenType.Null ? Json[k].Value<double>() : def;
		bool Bool(string k, bool def) => Json[k] != null && Json[k].Type != JTokenType.Null ? Json[k].Value<bool>() : def;
	}
}
