using System;
using System.Collections.Generic;
using Sandbox.Mask;

namespace Sandbox.Spawners;

public class TextureSpawner: BaseSpawner
{
	[Property] public int BrushSize { get; set; } = 2;
	[Property, Range(0, 64)] public int TextureId { get; set; }
	[Property] public List<SpawnLayer> TextureLayers { get; set; } = new();
	[Property, Range( 1, 5 )]  public int ResolutionLevel { get; set; } = 3;
	[Property] public GameObject target { get; set; }
	private int GetResolutionFromLevel( int level )
	{
		level = level.Clamp( 1, 5 );

		return level switch
		{
			1 => 128,
			2 => 256,
			3 => 512,
			4 => 1024,
			5 => 2048,
			_ => 512
		};
	}
	[Button]
	public void UpdateResolution()
	{
		if ( Manager.Terrain?.Storage == null )
			return;

		var storage = Manager.Terrain.Storage;

		
		int resolution = GetResolutionFromLevel( ResolutionLevel );
		
		storage.SetResolution( resolution );
		
		float metersPerPixel = storage.TerrainSize / storage.Resolution;
		Log.Info( $"Resolution = {resolution}" );
		Log.Info( $"TerrainSize = {storage.TerrainSize}" );
		Log.Info( $"Meters per pixel = {metersPerPixel}" );
	}
	[Button]
	public void Dooit()
	{
		PaintLayerAtWorldPosition(target.WorldPosition, TextureId);
	}
	
	[Button]
	public void DooitBounds()
	{
		PaintLayerInArea(
			GenerateSpawnerBounds(),
			TextureId
		);
	}

	private void PaintLayerInArea( BBox bounds, int textureId )
	{
		if ( Manager.Terrain?.Storage == null )
			return;

		var terrain = Manager.Terrain;
		var storage = terrain.Storage;

		float terrainSize = storage.TerrainSize;
		int resolution = storage.Resolution;

		Rect rect = ApexWorldUtils.GetTerrainRectFromBounds( bounds, terrain );
		var material = new CompactTerrainMaterial
		{
			BaseTextureId = (byte)Math.Clamp( textureId, 0, 63 ),
			OverlayTextureId = 0,
			BlendFactor = 0,
			IsHole = false
		};

		
		for ( int y = (int)rect.Top; y <= (int)rect.Bottom; y++ )
		{
			for ( int x = (int)rect.Left; x <= (int)rect.Right; x++ )
			{
				int index = y * resolution + x;

				storage.ControlMap[index] = material.Packed;
			}
		}

		terrain.SyncGPUTexture();
	}
	
	[Button]
	public void GenerateTextureLayers()
	{
		if ( Manager.Terrain?.Storage == null )
			return;

		var terrain = Manager.Terrain;
		var storage = terrain.Storage;

		int resolution = storage.Resolution;

		// Generate all masks first
		var generated = new List<(SpawnLayer layer, MaskField mask)>();

		foreach ( var layer in TextureLayers )
		{
			var mask = layer.GenerateMask(
				terrain,
				resolution
			);

			generated.Add( (layer, mask) );
		}

		BBox bounds = GenerateSpawnerBounds();

		Rect rect = ApexWorldUtils.GetTerrainRectFromBounds(
			bounds,
			terrain
		);

		// Sequential paint compositing
		for ( int y = (int)rect.Top; y <= (int)rect.Bottom; y++ )
		{
			for ( int x = (int)rect.Left; x <= (int)rect.Right; x++ )
			{
				int index = y * resolution + x;

				int baseTex = 0;
				int overlayTex = 0;
				float blend = 0f;

				bool hasBase = false;

				foreach ( var pair in generated )
				{
					float value = pair.mask.Get( x, y );

					if ( value <= 0.001f )
						continue;

					if ( !hasBase )
					{
						baseTex = pair.layer.TextureId;
						hasBase = true;
					}
					else
					{
						overlayTex = pair.layer.TextureId;
						blend = value;
					}
				}

				// Skip untouched pixels
				if ( !hasBase )
					continue;

				var material = new CompactTerrainMaterial
				{
					BaseTextureId = (byte)Math.Clamp( baseTex, 0, 63 ),
					OverlayTextureId = (byte)Math.Clamp( overlayTex, 0, 63 ),
					BlendFactor = (byte)(Math.Clamp( blend, 0f, 1f ) * 255f),
					IsHole = false
				};

				storage.ControlMap[index] = material.Packed;
			}
		}

		terrain.SyncGPUTexture();
	}
	
	///IGNORE THAT FOR NOW
	/// <summary>
	/// Paint a single layer at a specific position
	/// Uses CompactTerrainMaterial to encode texture ID + blend data
	/// </summary>
	private void PaintLayerAtWorldPosition( Vector3 worldPos, int textureId )
	{
		if ( Manager.Terrain?.Storage == null )
			return;

		var terrain = Manager.Terrain;
		var storage = terrain.Storage;

		Vector3 local = terrain.WorldTransform.PointToLocal( worldPos );

		float halfTerrain = storage.TerrainSize * 0.5f;

		float u = (local.x + halfTerrain) / storage.TerrainSize;
		float v = (local.y + halfTerrain) / storage.TerrainSize;

		int centerX = (int)(u * storage.Resolution);
		int centerY = (int)(v * storage.Resolution);

		centerX = centerX.Clamp( 0, storage.Resolution - 1 );
		centerY = centerY.Clamp( 0, storage.Resolution - 1 );

		var material = new CompactTerrainMaterial
		{
			BaseTextureId = (byte)Math.Clamp( textureId, 0, 63 ),
			OverlayTextureId = 0,
			BlendFactor = 255,	//TODO this need to be passed in each layer
			IsHole = false
		};

		for ( int y = -BrushSize; y <= BrushSize; y++ )
		{
			for ( int x = -BrushSize; x <= BrushSize; x++ )
			{
				// circular brush
				if ( x * x + y * y > BrushSize * BrushSize )
					continue;

				int px = centerX + x;
				int py = centerY + y;

				if ( px < 0 || py < 0 || px >= storage.Resolution || py >= storage.Resolution )
					continue;

				int index = py * storage.Resolution + px;

				storage.ControlMap[index] = material.Packed;
			}
		}

		terrain.SyncGPUTexture();
	}

}
