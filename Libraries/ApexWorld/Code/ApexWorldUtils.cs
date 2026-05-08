namespace Sandbox;

public static class ApexWorldUtils
{

	public static float GetTexelize(TerrainStorage storage)
	{
		return storage.TerrainSize / storage.Resolution;
	}
	
	public static Rect GetTerrainRectFromBounds( BBox bounds, Terrain terrain )
	{
		var storage = terrain.Storage;

		float terrainSize = storage.TerrainSize;
		int resolution = storage.Resolution;

		Vector3 localMins = terrain.WorldTransform.PointToLocal( bounds.Mins );
		Vector3 localMaxs = terrain.WorldTransform.PointToLocal( bounds.Maxs );

		int minX = (int)((localMins.x / terrainSize) * resolution);
		int minY = (int)((localMins.y / terrainSize) * resolution);

		int maxX = (int)((localMaxs.x / terrainSize) * resolution);
		int maxY = (int)((localMaxs.y / terrainSize) * resolution);

		minX = minX.Clamp( 0, resolution - 1 );
		minY = minY.Clamp( 0, resolution - 1 );

		maxX = maxX.Clamp( 0, resolution - 1 );
		maxY = maxY.Clamp( 0, resolution - 1 );

		return new Rect(
			minX,
			minY,
			maxX - minX,
			maxY - minY
		);
	}
}
