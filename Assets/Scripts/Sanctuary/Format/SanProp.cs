// Sanctuary prop assets, read without Unity (the dotnet harness tests them).
//
//   .santp       prop blueprint: a Lua table as text ("propTemplate = { ... }").
//                Only a few fields are needed here, read by pattern rather
//                than by running Lua.
//   .sanmodel    binary mesh: a null-terminated name, then int32-count arrays
//                of vertices (vec3), normals (vec3), tangents (vec4), uv, uv2,
//                uv3 (vec2), colours (vec4), int32 triangle indices (the count
//                is of indices), and an int32 bind-pose count with 4x4
//                matrices. Format as documented in the sanctuary-model-print
//                repository; Read checks every byte is accounted for.
//   .sanmaterial JSON: shader, keywords, textures by key (_BaseColorMap...).
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Sanctuary.MapFormat
{
	public sealed class SanPropBlueprint
	{
		public string TpId, Name;
		public string ModelPath, MaterialPath;
		public float ColliderCenterX, ColliderCenterY, ColliderCenterZ;
		public float ColliderSizeX, ColliderSizeY, ColliderSizeZ;
		public float FootprintX, FootprintY;
		public float ScaleMin = 1, ScaleMax = 1;
		public bool SnapToGround = true;
		public string[] Tags = new string[0];

		static float F(Match m, int g) => float.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
		const string Num = @"(-?[\d.]+(?:[eE][-+]?\d+)?)";

		public static SanPropBlueprint Parse(string text)
		{
			var bp = new SanPropBlueprint();
			Match m;
			if ((m = Regex.Match(text, @"tpId\s*=\s*""([^""]*)""")).Success) bp.TpId = m.Groups[1].Value;
			if ((m = Regex.Match(text, @"general\s*=\s*\{[\s\S]*?name\s*=\s*""([^""]*)""")).Success) bp.Name = m.Groups[1].Value;
			// First LOD's model and material.
			if ((m = Regex.Match(text, @"lods\s*=\s*\{[\s\S]*?model\s*=\s*""([^""]*)""[\s\S]*?material\s*=\s*""([^""]*)""")).Success)
			{
				bp.ModelPath = m.Groups[1].Value;
				bp.MaterialPath = m.Groups[2].Value;
			}
			if ((m = Regex.Match(text, @"collider\s*=\s*\{\s*center\s*=\s*\{\s*x\s*=\s*" + Num + @",\s*y\s*=\s*" + Num + @",\s*z\s*=\s*" + Num + @"\s*\},\s*size\s*=\s*\{\s*x\s*=\s*" + Num + @",\s*y\s*=\s*" + Num + @",\s*z\s*=\s*" + Num)).Success)
			{
				bp.ColliderCenterX = F(m, 1); bp.ColliderCenterY = F(m, 2); bp.ColliderCenterZ = F(m, 3);
				bp.ColliderSizeX = F(m, 4); bp.ColliderSizeY = F(m, 5); bp.ColliderSizeZ = F(m, 6);
			}
			if ((m = Regex.Match(text, @"footprint\s*=\s*\{\s*x\s*=\s*" + Num + @",\s*y\s*=\s*" + Num)).Success)
			{
				bp.FootprintX = F(m, 1); bp.FootprintY = F(m, 2);
			}
			if ((m = Regex.Match(text, @"randomScaleRange\s*=\s*\{\s*x\s*=\s*" + Num + @",\s*y\s*=\s*" + Num)).Success)
			{
				bp.ScaleMin = F(m, 1); bp.ScaleMax = F(m, 2);
			}
			if ((m = Regex.Match(text, @"snapToGround\s*=\s*(true|false)")).Success) bp.SnapToGround = m.Groups[1].Value == "true";
			if ((m = Regex.Match(text, @"\btags\s*=\s*\{([^}]*)\}")).Success)
				bp.Tags = Regex.Matches(m.Groups[1].Value, @"""([^""]*)""").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
			return bp;
		}
	}

	public sealed class SanModel
	{
		public string Name;
		public float[] Vertices, Normals, Tangents, Uv, Uv2, Uv3, Colors;
		public int[] Triangles;
		public int BindPoses;

		public int VertexCount => Vertices.Length / 3;

		public static SanModel Read(byte[] b, string nameForErrors = "sanmodel")
		{
			int pos = 0;
			int end = Array.IndexOf(b, (byte)0);
			if (end < 0) throw new InvalidDataException(nameForErrors + ": no name terminator");
			var m = new SanModel { Name = Encoding.UTF8.GetString(b, 0, end) };
			pos = end + 1;

			float[] Floats(int width)
			{
				int n = BitConverter.ToInt32(b, pos); pos += 4;
				if (n < 0 || pos + (long)n * width * 4 > b.Length) throw new InvalidDataException($"{nameForErrors}: array of {n} x {width} floats overruns the file at {pos}");
				var a = new float[n * width];
				Buffer.BlockCopy(b, pos, a, 0, a.Length * 4);
				pos += a.Length * 4;
				return a;
			}

			m.Vertices = Floats(3);
			m.Normals = Floats(3);
			m.Tangents = Floats(4);
			m.Uv = Floats(2);
			m.Uv2 = Floats(2);
			m.Uv3 = Floats(2);
			m.Colors = Floats(4);
			int ni = BitConverter.ToInt32(b, pos); pos += 4;
			if (ni < 0 || ni % 3 != 0 || pos + (long)ni * 4 > b.Length) throw new InvalidDataException($"{nameForErrors}: {ni} triangle indices");
			m.Triangles = new int[ni];
			Buffer.BlockCopy(b, pos, m.Triangles, 0, ni * 4);
			pos += ni * 4;
			m.BindPoses = BitConverter.ToInt32(b, pos); pos += 4;
			pos += m.BindPoses * 64;
			if (pos != b.Length) throw new InvalidDataException($"{nameForErrors}: read {pos} of {b.Length} bytes; the format has changed");
			int vc = m.VertexCount;
			foreach (int t in m.Triangles)
				if (t < 0 || t >= vc) throw new InvalidDataException($"{nameForErrors}: index {t} outside {vc} vertices");
			return m;
		}
	}

	public sealed class SanMaterial
	{
		public string Shader, BaseColorMap, NormalMap, MaskMap;
		public bool AlphaTest, DoubleSided;

		public static SanMaterial Parse(string json)
		{
			var o = JObject.Parse(json);
			var mat = new SanMaterial { Shader = (string)o["shader"] };
			var kw = (o["keywords"] as JArray)?.Select(k => (string)k).ToList();
			mat.AlphaTest = kw?.Contains("_ALPHATEST_ON") == true;
			mat.DoubleSided = kw?.Contains("_DOUBLESIDED_ON") == true;
			foreach (var t in (o["textures"] as JArray ?? new JArray()).OfType<JObject>())
			{
				switch ((string)t["key"])
				{
					case "_BaseColorMap": mat.BaseColorMap = (string)t["path"]; break;
					case "_NormalMap": mat.NormalMap = (string)t["path"]; break;
					case "_MaskMap": mat.MaskMap = (string)t["path"]; break;
				}
			}
			return mat;
		}
	}
}
