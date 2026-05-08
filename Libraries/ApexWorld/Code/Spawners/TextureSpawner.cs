using System;
using System.Collections.Generic;
using Sandbox.Mask;

namespace Sandbox.Spawners;

public class TextureSpawner: BaseSpawner
{
	[Property, Range(0, 64)] public int TextureId { get; set; }
	[Property] public SpawnLayer[]  TextureLayers { get; set; }
	
	[Property] public GameObject target { get; set; }


	[Button]
	public void Dooit()
	{
		PaintLayerAtWorldPosition(target.WorldPosition, TextureId);
	}

	[Button]
public void PaintLayerInArea()
{
	if ( Manager.Terrain?.Storage == null )
		return;

	var terrain = Manager.Terrain;
	var storage = terrain.Storage;
	int resolution = storage.Resolution;
	float terrainSize = storage.TerrainSize;

	// Interpret Range as the full side length (centered on the spawner origin)
	float half = Range * 0.5f;

	// Spawner transform
	var spawnerTx = this.WorldTransform;

	// Spawner-local corners (on the terrain plane)
	var cornersLocal = new[]
	{
		new Vector3(-half, -half, 0f),
		new Vector3( half, -half, 0f),
		new Vector3(-half,  half, 0f),
		new Vector3( half,  half, 0f)
	};

	// Transform corners -> world -> terrain-local, collect min/max
	bool gotAny = false;
	float minX = float.MaxValue, minY = float.MaxValue;
	float maxX = float.MinValue, maxY = float.MinValue;

	foreach ( var cLocal in cornersLocal )
	{
		// world point of the corner
		var worldPt = spawnerTx.PointToWorld( cLocal );

		Vector3 terrainLocalPt;
		try
		{
			// preferred: uses terrain transform properly (rotation, translation)
			terrainLocalPt = terrain.WorldTransform.PointToLocal( worldPt );
		}
		catch
		{
			// fallback: translation-only
			terrainLocalPt = worldPt - terrain.WorldTransform.Position;
		}

		// track bounds (terrain-local XY)
		minX = Math.Min( minX, terrainLocalPt.x );
		minY = Math.Min( minY, terrainLocalPt.y );
		maxX = Math.Max( maxX, terrainLocalPt.x );
		maxY = Math.Max( maxY, terrainLocalPt.y );
		gotAny = true;
	}

	if ( !gotAny )
		return;

	// Convert terrain-local XY to UV (0..1) and clamp
	float halfTerrain = terrainSize * 0.5f;

	float uMin = Math.Clamp( (minX + halfTerrain) / terrainSize, 0f, 1f );
	float vMin = Math.Clamp( (minY + halfTerrain) / terrainSize, 0f, 1f );
	float uMax = Math.Clamp( (maxX + halfTerrain) / terrainSize, 0f, 1f );
	float vMax = Math.Clamp( (maxY + halfTerrain) / terrainSize, 0f, 1f );
	// Convert to texel indices. Use Floor for min, Ceil-1 for max to include boundaries.
	int x0 = Math.Clamp( (int)Math.Floor( uMin * resolution ), 0, resolution - 1 );
	int x1 = Math.Clamp( (int)Math.Ceiling( uMax * resolution ) - 1, 0, resolution - 1 );
	int y0 = Math.Clamp( (int)Math.Floor( vMin * resolution ), 0, resolution - 1 );
	int y1 = Math.Clamp( (int)Math.Ceiling( vMax * resolution ) - 1, 0, resolution - 1 );

	if ( x1 < x0 || y1 < y0 )
		return;

	// Enumerate texels inside bounds and call existing helper
	int localResolution = x1 - x0 + 1;

	foreach ( var layer in TextureLayers )
	{
		var mask = layer.GenerateMask(
			terrain,
			localResolution
		);

		for ( int y = y0; y <= y1; y++ )
		{
			for ( int x = x0; x <= x1; x++ )
			{
				int mx = x - x0;
				int my = y - y0;
				Log.Info( $"x0={x0} x1={x1} y0={y0} y1={y1} resolution={resolution}" );
				float value = mask.Get( mx, my );

				if ( value <= 0.01f )
					continue;
/*
				PaintLayerAtWorldPosition(
					x,
					y,
					layer.TextureId
				);
				*/
			}
		}
	}

	// Sync once after batch
	terrain.SyncGPUTexture();
}
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

		// Proper terrain-local conversion
		Vector3 local = terrain.WorldTransform.PointToLocal( worldPos );

		// Convert local XY into normalized UVs
		float u = local.x / storage.TerrainSize;
		float v = local.y / storage.TerrainSize;

		// Convert UV -> texture coords
		int x = (int)(u * storage.Resolution);
		int y = (int)(v * storage.Resolution);

		x = x.Clamp( 0, storage.Resolution - 1 );
		y = y.Clamp( 0, storage.Resolution - 1 );

		int index = y * storage.Resolution + x;

		var material = new CompactTerrainMaterial
		{
			BaseTextureId = (byte)Math.Clamp( textureId, 0, 63 ),
			OverlayTextureId = 0,
			BlendFactor = 0,
			IsHole = false
		};

		storage.ControlMap[index] = material.Packed;

		terrain.SyncGPUTexture();

		Log.Info( $"Painted at {x},{y}" );
	}
	/// <summary>
	/// More advanced: blend between two layers based on a condition
	/// </summary>
	public void BlendLayersAtSlope( float slopeThreshold, int baseLayerId, int slopeLayerId )
	{
		if ( Manager.Terrain?.Storage == null )
			return;

		var storage = Manager.Terrain.Storage;
		int resolution = storage.Resolution;
		float sizeScale = storage.TerrainSize / (float)resolution;
		float heightScale = storage.TerrainHeight / (float)ushort.MaxValue;

		for ( int y = 1; y < resolution - 1; y++ )
		{
			for ( int x = 1; x < resolution - 1; x++ )
			{
				float centerHeight = storage.HeightMap[y * resolution + x] * heightScale;
				float rightHeight = storage.HeightMap[y * resolution + (x + 1)] * heightScale;
				float forwardHeight = storage.HeightMap[(y + 1) * resolution + x] * heightScale;

				float slopeX = Math.Abs( rightHeight - centerHeight ) / sizeScale;
				float slopeY = Math.Abs( forwardHeight - centerHeight ) / sizeScale;
				float slopeAngle = MathF.Atan( MathF.Max( slopeX, slopeY ) ) * (180f / MathF.PI);

				int index = y * resolution + x;

				// Blend factor: 0 = base layer, 255 = slope layer
				float blendValue = ((slopeAngle - slopeThreshold) / 20f * 255f).Clamp( 0f, 255f );
				byte blendFactor = (byte)blendValue;

				var material = new CompactTerrainMaterial
				{
					BaseTextureId = (byte)baseLayerId,
					OverlayTextureId = (byte)slopeLayerId,
					BlendFactor = blendFactor,
					IsHole = false
				};

				storage.ControlMap[index] = material.Packed;
			}
		}

		Manager.Terrain.SyncGPUTexture();
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

		// Sequential paint compositing
		for ( int y = 0; y < resolution; y++ )
		{
			for ( int x = 0; x < resolution; x++ )
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
}
