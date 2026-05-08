namespace Sandbox;

public static class ApexWorldUtils
{

	public static float GetTexelize(TerrainStorage storage)
	{
		return storage.TerrainSize / storage.Resolution;
	}
}
