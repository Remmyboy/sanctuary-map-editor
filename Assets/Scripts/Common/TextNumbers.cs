/// Number parsing for UI text fields, moved out of the removed Lua parser
/// (LuaParser.Read) with its exact behaviour: invariant culture, and on a
/// parse failure the result is 0 - TryParse overwrites the "empty" argument,
/// so it was never a real fallback. Kept that way so no field changes how
/// it reads.
public static class TextNumbers
{
	public static float StringToFloat(string value, float empty = 0f)
	{
		float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out empty);
		return empty;
	}

	public static int StringToInt(string value, int empty = 0)
	{
		int.TryParse(value, out empty);
		return empty;
	}
}
