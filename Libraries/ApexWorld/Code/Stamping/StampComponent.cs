using System;
using Sandbox;

namespace Sandbox.Stamping;

public class StampComponent : Component
{
	[Property, Group( "Setup" )] public Texture HeightmapTexture { get; set; }
	[Property, Group( "Setup" )] public Terrain Terrain { get; set; }

	[Property, Group( "Height" ), Range( 0f, 1f )] public float HeightScale { get; set; } = 1f;
	[Property, Group( "Height" ), Range( 0f, 1f )] public float HeightOffset { get; set; } = 0f;

	[Property, Group( "Smoothing" ), Range( 0, 20 )] public int SmoothIterations { get; set; } = 5;
	[Property, Group( "Smoothing" ), Range( 1, 8 )]  public int SmoothRadius { get; set; } = 1;

	[Button]
	public void LoadHeightmap()
	{
		if ( Terrain?.Storage == null ) { Log.Warning( "No terrain" ); return; }
		if ( HeightmapTexture == null ) { Log.Warning( "No texture" ); return; }
		
		var storage = Terrain.Storage;
		int res = storage.Resolution;
		var pixels = HeightmapTexture.GetPixels();

		if ( pixels == null ) { Log.Warning( "No pixels" ); return; }

		Log.Info( $"Loading heightmap {HeightmapTexture.Width}x{HeightmapTexture.Height} -> terrain {res}x{res}" );

		// Write heights
		for ( int y = 0; y < res; y++ )
		{
			for ( int x = 0; x < res; x++ )
			{
				int px = (int)((float)x / res * HeightmapTexture.Width);
				int py = (int)((float)y / res * HeightmapTexture.Height);

				var c = pixels[py * HeightmapTexture.Width + px];
				float luminance = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
				float height = Math.Clamp( luminance * HeightScale + HeightOffset, 0f, 1f );

				storage.HeightMap[y * res + x] = (ushort)(height * ushort.MaxValue);
			}
		}

		// Smooth pass
		for ( int iter = 0; iter < SmoothIterations; iter++ )
		{
			for ( int y = SmoothRadius; y < res - SmoothRadius; y++ )
			{
				for ( int x = SmoothRadius; x < res - SmoothRadius; x++ )
				{
					float sum = 0f;
					int count = 0;

					for ( int ky = -SmoothRadius; ky <= SmoothRadius; ky++ )
					{
						for ( int kx = -SmoothRadius; kx <= SmoothRadius; kx++ )
						{
							sum += storage.HeightMap[(y + ky) * res + (x + kx)];
							count++;
						}
					}

					storage.HeightMap[y * res + x] = (ushort)(sum / count);
				}
			}
		}

		Terrain.SyncGPUTexture();
		Log.Info( "Heightmap loaded successfully" );
	}

	[Button]
	public void ClearHeightmap()
	{
		if ( Terrain?.Storage == null ) { Log.Warning( "No terrain" ); return; }

		var storage = Terrain.Storage;
		for ( int i = 0; i < storage.HeightMap.Length; i++ )
			storage.HeightMap[i] = 0;

		Terrain.SyncGPUTexture();
		Log.Info( "Heightmap cleared" );
	}
}
