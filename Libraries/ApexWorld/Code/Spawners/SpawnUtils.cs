using System;
using Sandbox.Mask;
using Sandbox.Spawns;

namespace Sandbox.Spawners;

public static class SpawnUtils
{
	/// <summary>Bilinear-sampled terrain world height at a world XY position.</summary>
	public static float GetTerrainHeightAt( Terrain terrain, Vector3 worldPos )
	{
		if ( terrain?.Storage == null ) return 0f;
		var storage = terrain.Storage;
		var local   = terrain.WorldTransform.PointToLocal( worldPos );

		float fx = Math.Clamp( local.x / storage.TerrainSize, 0f, 1f ) * (storage.Resolution - 1);
		float fy = Math.Clamp( local.y / storage.TerrainSize, 0f, 1f ) * (storage.Resolution - 1);

		int x0 = Math.Clamp( (int)fx, 0, storage.Resolution - 2 );
		int y0 = Math.Clamp( (int)fy, 0, storage.Resolution - 2 );
		float tx = fx - x0, ty = fy - y0;

		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;
		float h00 = storage.HeightMap[ y0      * storage.Resolution + x0     ] * hScale;
		float h10 = storage.HeightMap[ y0      * storage.Resolution + x0 + 1 ] * hScale;
		float h01 = storage.HeightMap[(y0 + 1) * storage.Resolution + x0     ] * hScale;
		float h11 = storage.HeightMap[(y0 + 1) * storage.Resolution + x0 + 1 ] * hScale;

		return terrain.WorldPosition.z
		       + MathX.Lerp( MathX.Lerp( h00, h10, tx ), MathX.Lerp( h01, h11, tx ), ty );
	}

	/// <summary>Terrain surface normal at a world XY position.</summary>
	public static Vector3 GetTerrainNormalAt( Terrain terrain, Vector3 worldPos )
	{
		if ( terrain?.Storage == null ) return Vector3.Up;
		var storage = terrain.Storage;
		var local   = terrain.WorldTransform.PointToLocal( worldPos );

		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;
		float sScale = storage.TerrainSize   / storage.Resolution;

		int x = Math.Clamp( (int)(local.x / storage.TerrainSize * storage.Resolution), 1, storage.Resolution - 2 );
		int y = Math.Clamp( (int)(local.y / storage.TerrainSize * storage.Resolution), 1, storage.Resolution - 2 );

		float c = storage.HeightMap[ y      * storage.Resolution + x     ] * hScale;
		float r = storage.HeightMap[ y      * storage.Resolution + x + 1 ] * hScale;
		float f = storage.HeightMap[(y + 1) * storage.Resolution + x     ] * hScale;

		// tangentX goes along +X, tangentY along +Y, Z is height
		var tangentX = new Vector3( sScale, 0,      r - c ).Normal;
		var tangentY = new Vector3( 0,      sScale, f - c ).Normal;

		// Cross(X, Y) gives upward-facing normal
		return Vector3.Cross( tangentX, tangentY ).Normal;
	}

	/// <summary>
	/// Slope direction = direction water would flow (downhill), projected on XY plane.
	/// </summary>
	public static Vector3 GetTerrainSlopeDirectionAt( Terrain terrain, Vector3 worldPos )
	{
		if ( terrain?.Storage == null ) return Vector3.Forward;
		var storage = terrain.Storage;
		var local   = terrain.WorldTransform.PointToLocal( worldPos );

		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;

		int x = Math.Clamp( (int)(local.x / storage.TerrainSize * storage.Resolution), 1, storage.Resolution - 2 );
		int y = Math.Clamp( (int)(local.y / storage.TerrainSize * storage.Resolution), 1, storage.Resolution - 2 );

		float c = storage.HeightMap[ y      * storage.Resolution + x     ] * hScale;
		float r = storage.HeightMap[ y      * storage.Resolution + x + 1 ] * hScale;
		float f = storage.HeightMap[(y + 1) * storage.Resolution + x     ] * hScale;

		// Gradient points uphill, negate for downhill
		var gradient = new Vector3( r - c, f - c, 0f );
		return gradient.IsNearZeroLength ? Vector3.Forward : (-gradient).Normal;
	}

	/// <summary>Sample a MaskField at a world position, correctly accounting for WorldOffset.</summary>
	public static float SampleMaskAt( MaskField mask, Terrain terrain, Vector3 worldPos )
	{
		var local     = terrain.WorldTransform.PointToLocal( worldPos );
		var maskLocal = new Vector2( local.x, local.y ) - mask.WorldOffset;
		return mask.Sample( maskLocal );
	}

	/// <summary>Build a rotation for a spawned object based on SpawnDefinition alignment settings.</summary>
	public static Rotation GetSpawnRotation( SpawnDefinition def, Terrain terrain, Vector3 worldPos, Random rng )
	{
		var rot = Rotation.Identity;

		if ( def.AlignToSlope )
		{
			var normal = GetTerrainNormalAt( terrain, worldPos );
			rot = Rotation.FromToRotation( Vector3.Up, normal );
		}

		if ( def.ForwardToSlope )
		{
			var slopeDir = GetTerrainSlopeDirectionAt( terrain, worldPos );
			// Build a yaw-only rotation toward slope direction, preserving existing tilt
			if ( !slopeDir.IsNearZeroLength )
			{
				float yaw      = MathF.Atan2( slopeDir.y, slopeDir.x ) * (180f / MathF.PI);
				var   yawRot   = Rotation.FromYaw( yaw );
				rot = rot * yawRot;
			}
		}

		// Random offset on top
		var offsets = new Angles(
			MathX.Lerp( def.MinRotationOffset.pitch, def.MaxRotationOffset.pitch, (float)rng.NextDouble() ),
			MathX.Lerp( def.MinRotationOffset.yaw,   def.MaxRotationOffset.yaw,   (float)rng.NextDouble() ),
			MathX.Lerp( def.MinRotationOffset.roll,  def.MaxRotationOffset.roll,  (float)rng.NextDouble() )
		);

		return rot * offsets.ToRotation();
	}

	/// <summary>Compute spawn scale from SpawnDefinition scale mode.</summary>
	public static Vector3 GetSpawnScale( SpawnDefinition def, float fitness, Random rng )
	{
		float t = (float)rng.NextDouble();
		float w, h;

		switch ( def.SpawnScale )
		{
			case ScaleMode.Fixed:
				w = def.WidthMinScale;
				h = def.HeightMinScale;
				break;
			case ScaleMode.Random:
				w = MathX.Lerp( def.WidthMinScale,  def.WidthMaxScale,  t );
				h = MathX.Lerp( def.HeightMinScale, def.HeightMaxScale, t );
				break;
			case ScaleMode.FitnessRandom:
				w = MathX.Lerp( def.WidthMinScale,  def.WidthMaxScale,  t ) * (1f + def.WidthRandomPercent  / 100f * fitness);
				h = MathX.Lerp( def.HeightMinScale, def.HeightMaxScale, t ) * (1f + def.HeightRandomPercent / 100f * fitness);
				break;
			default:
				w = h = 1f;
				break;
		}

		return new Vector3( w, h, w );
	}
}
