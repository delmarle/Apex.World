using System.Collections.Generic;
using System.Linq;
using Sandbox.Mask;
using Sandbox.Spawners;

namespace Sandbox.Stamping;

using System;

/// <summary>
/// Applies hydraulic erosion simulation to the terrain heightmap within the spawner bounds.
/// Supports the full mask stack — erosion strength is modulated per-texel by the combined mask.
/// </summary>
public class ErosionModifier : BaseSpawner
{
	// ── Mask ────────────────────────────────────────────────────────────────
	
	[Property, Group( "Masks" )]
	public List<MaskModifier> Masks { get; set; } = new();

	// ── Erosion Settings ────────────────────────────────────────────────────

	// Controls overall depth of erosion — kept small, world units are large
	[Property, Group( "Erosion" ), Range( 0.01f, 5f )]
	public float ErosionStrength { get; set; } = 0.5f;

	[Property, Group( "Erosion" ), Range( 1, 600000 )]
	public int DropletCount { get; set; } = 50000;

	// Longer lifetime = longer channels, more natural flow
	[Property, Group( "Erosion" ), Range( 1, 256 )]
	public int MaxLifetime { get; set; } = 48;

	// Higher inertia = smoother, more flowing channels (less vertical cuts)
	[Property, Group( "Erosion" ), Range( 0f, 1f )]
	public float Inertia { get; set; } = 0.3f;

	[Property, Group( "Erosion" ), Range( 0f, 32f )]
	public float SedimentCapacityFactor { get; set; } = 4f;

	[Property, Group( "Erosion" ), Range( 0f, 1f )]
	public float MinSedimentCapacity { get; set; } = 0.01f;

	[Property, Group( "Erosion" ), Range( 0f, 1f )]
	public float ErodeSpeed { get; set; } = 0.3f;

	// Higher deposit = material fills back in, softer channels
	[Property, Group( "Erosion" ), Range( 0f, 1f )]
	public float DepositSpeed { get; set; } = 0.3f;

	[Property, Group( "Erosion" ), Range( 0f, 1f )]
	public float EvaporateSpeed { get; set; } = 0.02f;

	[Property, Group( "Erosion" ), Range( 1f, 20f )]
	public float Gravity { get; set; } = 4f;

	// Larger radius = softer, more spread erosion brush
	[Property, Group( "Erosion" ), Range( 1, 8 )]
	public int ErosionRadius { get; set; } = 4;

	[Property, Group( "Erosion" )]
	public int Seed { get; set; } = 42;

	// ── Smoothing ───────────────────────────────────────────────────────────

	[Property, Group( "Smoothing" ), Range( 0, 10 )]
	public int SmoothIterations { get; set; } = 1;

	[Property, Group( "Smoothing" ), Range( 1, 4 )]
	public int SmoothRadius { get; set; } = 1;

	// ── BaseSpawner ─────────────────────────────────────────────────────────

	public override void OnBeforeGenerate()
	{
		_isFinished = false;
		base.OnBeforeGenerate();
	}

	public override void Generate()
	{
		base.Generate();
		ApplyErosion();
	}

	// ── Buttons ─────────────────────────────────────────────────────────────

	[Button]
	public void ApplyErosion()
	{
		var bounds   = GenerateSpawnerBounds();
		var terrains = GetTerrainsInBounds( bounds );

		if ( terrains == null || terrains.Count == 0 )
		{
			Log.Warning( $"{this}: no terrains in bounds" );
			_isFinished = true;
			return;
		}

		foreach ( var terrain in terrains )
			ProcessTerrain( terrain, bounds );

		_isFinished = true;
		Log.Info( $"{this}: erosion done" );
	}

	[Button]
	public void FlattenArea()
	{
		var bounds   = GenerateSpawnerBounds();
		var terrains = GetTerrainsInBounds( bounds );
		if ( terrains == null || terrains.Count == 0 ) return;

		foreach ( var terrain in terrains )
		{
			var storage = terrain.Storage;
			if ( storage == null ) continue;
			var rect = ApexWorldUtils.GetTerrainRectFromBounds( bounds, terrain );
			for ( int y = (int)rect.Top;  y <= (int)rect.Bottom; y++ )
			for ( int x = (int)rect.Left; x <= (int)rect.Right;  x++ )
				storage.HeightMap[y * storage.Resolution + x] = 0;
			terrain.SyncGPUTexture();
		}
	}

	// ── Core ────────────────────────────────────────────────────────────────

	private void ProcessTerrain( Terrain terrain, BBox bounds )
	{
		var storage = terrain.Storage;
		if ( storage == null ) return;

		int   res    = storage.Resolution;
		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;

		var rect = ApexWorldUtils.GetTerrainRectFromBounds( bounds, terrain );
		int x0 = (int)rect.Left,  x1 = (int)rect.Right;
		int y0 = (int)rect.Top,   y1 = (int)rect.Bottom;
		int w  = x1 - x0 + 1,    h  = y1 - y0 + 1;

		if ( w < 2 || h < 2 ) { Log.Warning( $"{this}: bounds too small" ); return; }

		// Build mask — same pattern as DetailsSpawner
		var terrainLocalMins = terrain.WorldTransform.PointToLocal( bounds.Mins );
		var worldOffset      = new Vector2( terrainLocalMins.x, terrainLocalMins.y );
		var maskField        = BuildMask( Masks, terrain, worldOffset, Range );

		// Extract heightmap region into float array
		var original  = new float[w * h];
		var heightmap = new float[w * h];

		for ( int y = 0; y < h; y++ )
		for ( int x = 0; x < w; x++ )
		{
			float v = storage.HeightMap[(y0 + y) * res + (x0 + x)] * hScale;
			original[y * w + x]  = v;
			heightmap[y * w + x] = v;
		}

		// Run erosion on the full extracted region
		SimulateErosion( heightmap, w, h );

		// Smooth pass
		for ( int iter = 0; iter < SmoothIterations; iter++ )
			SmoothPass( heightmap, w, h );

		// Write back — lerp between original and eroded using mask value
		for ( int y = 0; y < h; y++ )
		{
			for ( int x = 0; x < w; x++ )
			{
				// Sample mask at this texel's world position — same as SampleMaskAt in SpawnUtils
				var terrainLocalPos = new Vector3(
					(x0 + x) / (float)res * storage.TerrainSize,
					(y0 + y) / (float)res * storage.TerrainSize,
					0f
				);
				var worldPos  = terrain.WorldTransform.PointToWorld( terrainLocalPos );
				var maskLocal = new Vector2( terrainLocalPos.x, terrainLocalPos.y ) - maskField.WorldOffset;
				float mask    = Masks.Count > 0 ? maskField.Sample( maskLocal ) : 1f;

				// Blend: mask=1 -> full erosion, mask=0 -> original height
				float eroded = MathX.Lerp( original[y * w + x], heightmap[y * w + x], mask );
				float v      = Math.Clamp( eroded / storage.TerrainHeight, 0f, 1f );
				storage.HeightMap[(y0 + y) * res + (x0 + x)] = (ushort)(v * ushort.MaxValue);
			}
		}

		terrain.SyncGPUTexture();
		Log.Info( $"{this}: eroded {w}x{h} region on {terrain.GameObject.Name}" );
	}

	// ── Mask builder — identical to DetailsSpawner.BuildMask ────────────────

	private MaskField BuildMask( List<MaskModifier> masks, Terrain terrain, Vector2 worldOffset, float worldSize )
	{
		var field = new MaskField( MaskResolution, worldSize ) { WorldOffset = worldOffset };
		field.Fill( 1f );

		foreach ( var mask in masks )
		{
			mask.Terrain = terrain;
			var temp = new MaskField( MaskResolution, worldSize ) { WorldOffset = worldOffset };
			temp.Fill( 1f );
			mask.Apply( temp );
			field = field.Multiply( temp );
		}

		return field;
	}

	// ── Hydraulic Erosion ───────────────────────────────────────────────────

	private void SimulateErosion( float[] map, int w, int h )
	{
		var rng = new Random( Seed );

		// Precompute brush
		int   brushLen   = (2 * ErosionRadius + 1) * (2 * ErosionRadius + 1);
		var   brushX     = new int[brushLen];
		var   brushY     = new int[brushLen];
		var   brushW     = new float[brushLen];
		int   brushCount = 0;
		float weightSum  = 0f;

		for ( int dy = -ErosionRadius; dy <= ErosionRadius; dy++ )
		for ( int dx = -ErosionRadius; dx <= ErosionRadius; dx++ )
		{
			float dist = MathF.Sqrt( dx * dx + dy * dy );
			if ( dist >= ErosionRadius ) continue;
			float wt = 1f - dist / ErosionRadius;
			brushX[brushCount] = dx;
			brushY[brushCount] = dy;
			brushW[brushCount] = wt;
			weightSum += wt;
			brushCount++;
		}

		for ( int i = 0; i < brushCount; i++ ) brushW[i] /= weightSum;

		for ( int d = 0; d < DropletCount; d++ )
		{
			float posX = (float)(rng.NextDouble() * (w - 1));
			float posY = (float)(rng.NextDouble() * (h - 1));
			float dirX = 0f, dirY = 0f;
			float speed = 1f, water = 1f, sediment = 0f;

			for ( int life = 0; life < MaxLifetime; life++ )
			{
				int   nodeX = (int)posX, nodeY = (int)posY;
				float cellX = posX - nodeX, cellY = posY - nodeY;

				if ( nodeX < 0 || nodeX >= w - 1 || nodeY < 0 || nodeY >= h - 1 ) break;

				float h00 = map[ nodeY      * w + nodeX     ];
				float h10 = map[ nodeY      * w + nodeX + 1 ];
				float h01 = map[(nodeY + 1) * w + nodeX     ];
				float h11 = map[(nodeY + 1) * w + nodeX + 1 ];

				float gx = (h10 - h00) * (1f - cellY) + (h11 - h01) * cellY;
				float gy = (h01 - h00) * (1f - cellX) + (h11 - h10) * cellX;

				dirX = dirX * Inertia - gx * (1f - Inertia);
				dirY = dirY * Inertia - gy * (1f - Inertia);

				float len = MathF.Sqrt( dirX * dirX + dirY * dirY );
				if ( len < 0.0001f ) break;
				dirX /= len; dirY /= len;

				float newX = posX + dirX, newY = posY + dirY;
				if ( newX < 0 || newX >= w - 1 || newY < 0 || newY >= h - 1 ) break;

				float newH = Bilinear( map, w, h, newX, newY );
				float oldH = Bilinear( map, w, h, posX, posY );
				float dH   = newH - oldH;

				float capacity = MathF.Max( -dH * speed * water * SedimentCapacityFactor, MinSedimentCapacity );

				if ( sediment > capacity || dH > 0f )
				{
					float deposit = dH > 0f
						? MathF.Min( dH, sediment )
						: (sediment - capacity) * DepositSpeed;
					sediment -= deposit;
					DepositAt( map, w, h, posX, posY, deposit );
				}
				else
				{
					float erode = MathF.Min( (capacity - sediment) * ErodeSpeed, -dH );
					sediment += erode;

					for ( int b = 0; b < brushCount; b++ )
					{
						int bx = nodeX + brushX[b], by = nodeY + brushY[b];
						if ( bx < 0 || bx >= w || by < 0 || by >= h ) continue;
						float delta = erode * brushW[b] * ErosionStrength;
						map[by * w + bx] = MathF.Max( 0f, map[by * w + bx] - delta );
					}
				}

				speed  = MathF.Sqrt( MathF.Max( 0f, speed * speed + dH * Gravity ) );
				water *= 1f - EvaporateSpeed;
				posX   = newX; posY = newY;
			}
		}
	}

	private static float Bilinear( float[] map, int w, int h, float x, float y )
	{
		int   x0 = Math.Clamp( (int)x, 0, w - 2 );
		int   y0 = Math.Clamp( (int)y, 0, h - 2 );
		float tx = x - x0, ty = y - y0;
		return MathX.Lerp(
			MathX.Lerp( map[y0 * w + x0],       map[y0 * w + x0 + 1],       tx ),
			MathX.Lerp( map[(y0+1) * w + x0],   map[(y0+1) * w + x0 + 1],   tx ), ty );
	}

	private static void DepositAt( float[] map, int w, int h, float x, float y, float amount )
	{
		int   x0 = Math.Clamp( (int)x, 0, w - 2 );
		int   y0 = Math.Clamp( (int)y, 0, h - 2 );
		float tx = x - x0, ty = y - y0;
		map[ y0      * w + x0     ] += amount * (1f - tx) * (1f - ty);
		map[ y0      * w + x0 + 1 ] += amount *       tx  * (1f - ty);
		map[(y0 + 1) * w + x0     ] += amount * (1f - tx) *       ty;
		map[(y0 + 1) * w + x0 + 1 ] += amount *       tx  *       ty;
	}

	private void SmoothPass( float[] map, int w, int h )
	{
		var copy = new float[map.Length];
		Array.Copy( map, copy, map.Length );
		for ( int y = SmoothRadius; y < h - SmoothRadius; y++ )
		for ( int x = SmoothRadius; x < w - SmoothRadius; x++ )
		{
			float sum = 0f; int count = 0;
			for ( int ky = -SmoothRadius; ky <= SmoothRadius; ky++ )
			for ( int kx = -SmoothRadius; kx <= SmoothRadius; kx++ )
			{ sum += copy[(y + ky) * w + (x + kx)]; count++; }
			map[y * w + x] = sum / count;
		}
	}
}
