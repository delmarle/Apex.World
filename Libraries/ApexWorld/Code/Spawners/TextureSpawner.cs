using System;
using System.Collections.Generic;
using Sandbox.Mask;

namespace Sandbox.Spawners;

public class TextureSpawner: BaseSpawner
{
	
	[Property] public List<SpawnLayer> TextureLayers { get; set; } = new();
	[Property] public ApexWorldUtils.TerrainResolution Resolution { get; set; } = ApexWorldUtils.TerrainResolution.R512;

	public override void OnBeforeGenerate()
	{
		_isFinished = false;
		base.OnBeforeGenerate();
	}

	public override void Generate()
	{
		base.Generate();
		GenerateTextureLayers();
	}

	[Button]
	public void UpdateResolution()
	{
		var terrainInBounds = Manager.GetWorldCache().GetTerrainsInBounds( GenerateSpawnerBounds() );
		foreach ( var currentTerrain in terrainInBounds )
		{	
			if ( currentTerrain?.Storage == null )
				return;

			var storage = currentTerrain.Storage;
			int resolution =(int)Resolution;
		
			storage.SetResolution( resolution );
		
			float metersPerPixel = storage.TerrainSize / storage.Resolution;
			Log.Info( $"Resolution = {resolution}" );
			Log.Info( $"TerrainSize = {storage.TerrainSize}" );
			Log.Info( $"Meters per pixel = {metersPerPixel}" );
			
		}
	
	}
	
	[Button]
	public void GenerateTextureLayers()
	{
		var terrainInBounds = Manager.GetWorldCache().GetTerrainsInBounds( GenerateSpawnerBounds() );
		foreach ( var currentTerrain in terrainInBounds )
		{
			if ( currentTerrain?.Storage == null )
				return;

			var terrain = currentTerrain;
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

		_isFinished = true;
	}
}
