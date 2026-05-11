

using System.Collections.Generic;
using Sandbox.Utility;

namespace Sandbox.Mask;
using System.Linq;
using System;

#region SplineMask

/// <summary>
/// Outputs 1 inside the spline boundary, 0 outside.
/// Uses signed distance to polygon for soft edge support.
/// </summary>
[Serializable]
public class SplineMask : MaskModifier
{
    [Property] public ApexWorld.Spline.SplineComponent Spline { get; set; }
    [Property, Range( 0f, 500f )] public float EdgeSoftness { get; set; } = 0f;

    public override void Apply( MaskField field )
    {
        if ( Spline == null ) { Log.Warning( "SplineMask: no spline" ); return; }

        var polyLocal = new List<Vector3>();
        Spline.Spline.ConvertToPolyline( ref polyLocal );
        if ( polyLocal.Count < 3 ) return;

        var poly2D = new Vector2[polyLocal.Count];
        for ( int i = 0; i < polyLocal.Count; i++ )
        {
            var worldPos = Spline.WorldTransform.PointToWorld( polyLocal[i] );
            var local    = Terrain != null
                ? Terrain.WorldTransform.PointToLocal( worldPos )
                : worldPos;
            poly2D[i] = new Vector2( local.x, local.y );
        }

        for ( int y = 0; y < field.Resolution; y++ )
        {
            for ( int x = 0; x < field.Resolution; x++ )
            {
                var   texelPos   = field.TexelToWorld( x, y );
                float signedDist = SignedDistToPolygon( texelPos, poly2D );
                float value      = signedDist < 0f ? 1f : 0f;

                if ( EdgeSoftness > 0.001f )
                    value = Math.Clamp( -signedDist / EdgeSoftness, 0f, 1f );

                field.Set( x, y, value );
            }
        }
    }

    private static float SignedDistToPolygon( Vector2 p, Vector2[] poly )
    {
	    float minDist = float.MaxValue;
	    bool  inside  = false;
	    int   n       = poly.Length;

	    for ( int i = 0, j = n - 1; i < n; j = i++ )
	    {
		    var a = poly[i];
		    var b = poly[j];

		    if ( (a.y > p.y) != (b.y > p.y) &&
		         p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x )
			    inside = !inside;

		    var   ab    = b - a;
		    float lenSq = Vector2.Dot( ab, ab );
		    if ( lenSq < 0.0001f ) continue;

		    var   ap      = p - a;
		    float t       = Math.Clamp( Vector2.Dot( ap, ab ) / lenSq, 0f, 1f );
		    var   closest = a + ab * t;
		    minDist = MathF.Min( minDist, (p - closest).Length );
	    }
	    

	    return inside ? -minDist : minDist;
    }
}

#endregion

#region HeightMask

/// <summary>
/// Outputs 1 where terrain height is within [MinHeight, MaxHeight], 0 outside.
/// Softness controls the falloff width at each edge.
/// </summary>
[Serializable]
public class HeightMask : MaskModifier
{
	[Property] public float MinHeight  { get; set; } = 0f;
	[Property] public float MaxHeight  { get; set; } = 500f;
	[Property, Range(0f, 1f)] public float Softness { get; set; } = 0.1f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null )
			return;

		var storage = Terrain.Storage;

		int res = storage.Resolution;

		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;

		float soft = Softness * (MaxHeight - MinHeight);

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				var local = field.TexelToWorld( x, y );

				int tx = (int)(local.x / storage.TerrainSize * res);
				int ty = (int)(local.y / storage.TerrainSize * res);

				tx = Math.Clamp( tx, 0, res - 1 );
				ty = Math.Clamp( ty, 0, res - 1 );

				float worldHeight =
					storage.HeightMap[ty * res + tx] * hScale;

				float lo = SmoothStep(
					MinHeight - soft,
					MinHeight,
					worldHeight
				);

				float hi = SmoothStep(
					MaxHeight + soft,
					MaxHeight,
					worldHeight
				);

				field.Set(
					x,
					y,
					Math.Clamp( lo * hi, 0f, 1f )
				);
			}
		}
	}

	private static float SmoothStep( float edge0, float edge1, float x )
	{
		float t = Math.Clamp( (x - edge0) / (edge1 - edge0), 0f, 1f );
		return t * t * (3f - 2f * t);
	}
}

#endregion

#region SlopeMask

/// <summary>
/// Outputs 1 where terrain slope angle is within [MinAngle, MaxAngle] degrees.
/// Requires the terrain height storage to compute gradients.
/// </summary>
[Serializable]
public class SlopeMask : MaskModifier
{
	[Property, Range(0f, 90f)] public float MinAngle  { get; set; } = 0f;
	[Property, Range(0f, 90f)] public float MaxAngle  { get; set; } = 30f;
	[Property, Range(0f, 1f)]  public float Softness  { get; set; } = 0.1f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null ) return;

		var storage   = Terrain.Storage;
		int  res      = storage.Resolution;
		float hScale  = storage.TerrainHeight / (float)ushort.MaxValue;
		float sScale  = storage.TerrainSize   / (float)res;
		float soft    = Softness * (MaxAngle - MinAngle);

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				var local = field.TexelToWorld( x, y );

				int tx = (int)(local.x / storage.TerrainSize * res);
				int ty = (int)(local.y / storage.TerrainSize * res);

				tx = Math.Clamp( tx, 1, res - 2 );
				ty = Math.Clamp( ty, 1, res - 2 );

				float c = storage.HeightMap[ty * res + tx]       * hScale;
				float r = storage.HeightMap[ty * res + tx + 1]   * hScale;
				float u = storage.HeightMap[(ty + 1) * res + tx] * hScale;

				float slopeX     = MathF.Abs( r - c ) / sScale;
				float slopeY     = MathF.Abs( u - c ) / sScale;
				float slope = MathF.Sqrt(
					slopeX * slopeX +
					slopeY * slopeY
				);

				float slopeAngle =
					MathF.Atan( slope ) * (180f / MathF.PI);

				float lo = SmoothStep( MinAngle - soft, MinAngle, slopeAngle );
				float hi = SmoothStep( MaxAngle + soft, MaxAngle, slopeAngle );

				field.Set( x, y, Math.Clamp( lo * hi, 0f, 1f ) );
			}
		}
	}

	private static float SmoothStep( float edge0, float edge1, float x )
	{
		float t = Math.Clamp( (x - edge0) / (edge1 - edge0), 0f, 1f );
		return t * t * (3f - 2f * t);
	}
}

#endregion

#region NoiseMask

/// <summary>
/// Fills the field with layered Perlin noise (FBM).
/// </summary>
[Serializable]
public class NoiseMask : MaskModifier
{
	[Property]
	[Range( 2f, 2000f, 1f )]
	public float Scale { get; set; } = 55;

	[Property]
	[Range( 0.25f, 8f, 0.01f )]
	public float Contrast { get; set; } = 2f;

	[Property]
	[Range( 0f, 1f, 0.001f )]
	public float Threshold { get; set; } = 0.58f;

	[Property]
	[Range( 0.001f, 0.5f, 0.001f )]
	public float Blend { get; set; } = 0.05f;

	[Property]
	public int Seed { get; set; } = 12345;

	[Property]
	public bool Invert { get; set; }

	public override void Apply( MaskField field )
	{
		int resolution = field.Resolution;

		float invScale = 1.0f / MathF.Max( Scale, 0.001f );

		for ( int x = 0; x < resolution; x++ )
		{
			for ( int y = 0; y < resolution; y++ )
			{
				float u = x / (float)(resolution - 1);
				float v = y / (float)(resolution - 1);

				float worldX = field.WorldOffset.x + (u * field.WorldSize);
				float worldY = field.WorldOffset.y + (v * field.WorldSize);

				float nx = (worldX + Seed * 17.13f) * invScale;
				float ny = (worldY + Seed * 9.37f) * invScale;

				float noise = 0f;

				float amplitude = 1f;
				float frequency = 1f;
				float totalAmplitude = 0f;

				for ( int i = 0; i < 4; i++ )
				{
					float n = Noise.Simplex(
						nx * frequency,
						ny * frequency
					);

					// remap -1..1 to 0..1
					n = (n * 0.5f) + 0.5f;

					noise += n * amplitude;

					totalAmplitude += amplitude;

					amplitude *= 0.5f;
					frequency *= 2f;
				}

				noise /= totalAmplitude;

				noise = MathF.Pow(
					Math.Clamp( noise, 0f, 1f ),
					Contrast
				);

				float value = Math.Clamp(
					(noise - (Threshold - Blend)) / ((Threshold + Blend) - (Threshold - Blend)),
					0f,
					1f
				);

				if ( Invert )
					value = 1f - value;

				float current = field.Get( x, y );

				field.Set(
					x,
					y,
					current * value
				);
			}
		}
	}
}

#endregion

#region DistanceMask

/// <summary>
/// Outputs 1 at the origin world point, falling off to 0 at Radius.
/// Useful for spawn exclusion zones or point-of-interest weighting.
/// </summary>
[Serializable]
public class DistanceMask : MaskModifier
{
	[Property] public float InnerRadius { get; set; } = 200f;
	[Property] public float OuterRadius { get; set; } = 1000f;
	[Property] public bool Invert { get; set; }

	public override void Apply( MaskField field )
	{
		var center = new Vector2(
			field.WorldOffset.x + field.WorldSize * 0.5f,
			field.WorldOffset.y + field.WorldSize * 0.5f
		);

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				var pos = field.TexelToWorld( x, y );

				float dist = Vector2.DistanceBetween(
					pos,
					center
				);

				float t = (dist - InnerRadius) /
				          (OuterRadius - InnerRadius);

				t = Math.Clamp( t, 0f, 1f );

				float value = 1f - t;

				value = Math.Clamp( value, 0f, 1f );

				if ( Invert )
					value = 1f - value;

				field.Set( x, y, value );
			}
		}
	}
}

#endregion

#region CurvatureMask

/// <summary>
/// Outputs high values at concave areas (valleys) or convex areas (ridges)
/// depending on Mode.
/// </summary>
[Serializable]
public sealed class CurvatureMask : MaskModifier
{
	[Property, Range(16f, 1024f)]
	public float Radius { get; set; } = 256f;

	[Property, Range(0f, 64f)]
	public float Strength { get; set; } = 8f;

	[Property] public bool RidgesOnly { get; set; }
	[Property] public bool ValleysOnly { get; set; }

	public override void Apply( MaskField field )
	{
		var storage = Terrain.Storage;
		int res = storage.Resolution;

		float terrainSize = storage.TerrainSize;
		float hScale = storage.TerrainHeight;

		float texelWorldSize =
			field.WorldSize / field.Resolution;

		int sampleOffset = Math.Max(
			1,
			(int)(Radius / texelWorldSize)
		);

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				float center = SampleHeight(
					field,
					storage,
					x,
					y,
					res,
					hScale
				);

				float left = SampleHeight(
					field,
					storage,
					x - sampleOffset,
					y,
					res,
					hScale
				);

				float right = SampleHeight(
					field,
					storage,
					x + sampleOffset,
					y,
					res,
					hScale
				);

				float down = SampleHeight(
					field,
					storage,
					x,
					y - sampleOffset,
					res,
					hScale
				);

				float up = SampleHeight(
					field,
					storage,
					x,
					y + sampleOffset,
					res,
					hScale
				);

				float average =
					(left + right + up + down) * 0.25f;

				float curvature =
					(center - average) * Strength;

				if ( RidgesOnly )
					curvature = Math.Max( curvature, 0f );

				if ( ValleysOnly )
					curvature = Math.Max( -curvature, 0f );

				float value = Math.Clamp(
					(curvature * 0.5f) + 0.5f,
					0f,
					1f
				);

				field.Set( x, y, value );
			}
		}
	}

	private float SampleHeight(
		MaskField field,
		TerrainStorage storage,
		int x,
		int y,
		int res,
		float hScale
	)
	{
		x = Math.Clamp(
			x,
			0,
			field.Resolution - 1
		);

		y = Math.Clamp(
			y,
			0,
			field.Resolution - 1
		);

		var local = field.TexelToWorld( x, y );

		int tx = (int)(
			local.x / storage.TerrainSize * res
		);

		int ty = (int)(
			local.y / storage.TerrainSize * res
		);

		tx = Math.Clamp( tx, 0, res - 1 );
		ty = Math.Clamp( ty, 0, res - 1 );

		return storage.HeightMap[
			ty * res + tx
		] * hScale;
	}
}

#endregion

#region ErosionMask

/// <summary>
/// Simulates simple hydraulic erosion by computing flow accumulation.
/// High values = erosion channels / valleys where water flows.
/// </summary>
[Serializable]
public class ErosionMask : MaskModifier
{
	[Property, Range( 32f, 1024f )]
	public float Radius { get; set; } = 256f;

	[Property, Range( 0f, 32f )]
	public float Strength { get; set; } = 8f;

	[Property, Range( 0f, 90f )]
	public float MinSlope { get; set; } = 5f;

	[Property, Range( 0f, 90f )]
	public float MaxSlope { get; set; } = 45f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null )
			return;

		var storage = Terrain.Storage;

		int res = storage.Resolution;

		float terrainHeight = storage.TerrainHeight;

		float texelWorldSize =
			field.WorldSize / field.Resolution;

		int sampleOffset = Math.Max(
			1,
			(int)(Radius / texelWorldSize)
		);

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				float center =
					SampleHeight(
						field,
						storage,
						x,
						y,
						res,
						terrainHeight
					);

				float left =
					SampleHeight(
						field,
						storage,
						x - sampleOffset,
						y,
						res,
						terrainHeight
					);

				float right =
					SampleHeight(
						field,
						storage,
						x + sampleOffset,
						y,
						res,
						terrainHeight
					);

				float down =
					SampleHeight(
						field,
						storage,
						x,
						y - sampleOffset,
						res,
						terrainHeight
					);

				float up =
					SampleHeight(
						field,
						storage,
						x,
						y + sampleOffset,
						res,
						terrainHeight
					);

				float dx =
					(right - left) /
					(sampleOffset * texelWorldSize * 2f);

				float dy =
					(up - down) /
					(sampleOffset * texelWorldSize * 2f);

				float slope =
					MathF.Sqrt( dx * dx + dy * dy );

				float slopeAngle =
					MathF.Atan( slope ) *
					(180f / MathF.PI);

				float average =
					(left + right + up + down) * 0.25f;

				float curvature =
					average - center;

				curvature =
					MathF.Max( curvature, 0f );

				float slopeMask =
					SmoothStep(
						MinSlope,
						MaxSlope,
						slopeAngle
					);

				float erosion =
					curvature *
					slopeMask *
					Strength;

				field.Set(
					x,
					y,
					Math.Clamp(
						erosion,
						0f,
						1f
					)
				);
			}
		}
	}

	private float SampleHeight(
		MaskField field,
		TerrainStorage storage,
		int x,
		int y,
		int res,
		float terrainHeight
	)
	{
		x = Math.Clamp(
			x,
			0,
			field.Resolution - 1
		);

		y = Math.Clamp(
			y,
			0,
			field.Resolution - 1
		);

		var local =
			field.TexelToWorld( x, y );

		int tx = (int)(
			local.x /
			storage.TerrainSize *
			res
		);

		int ty = (int)(
			local.y /
			storage.TerrainSize *
			res
		);

		tx = Math.Clamp( tx, 0, res - 1 );
		ty = Math.Clamp( ty, 0, res - 1 );

		return (
			storage.HeightMap[
				ty * res + tx
			] / 65535f
		) * terrainHeight;
	}

	private float SmoothStep(
		float edge0,
		float edge1,
		float x
	)
	{
		float t = Math.Clamp(
			(x - edge0) /
			(edge1 - edge0),
			0f,
			1f
		);

		return t * t * (3f - 2f * t);
	}
}
#endregion

#region FlowMask

/// <summary>
/// Marks areas where water would pool or flow based on height + curvature.
/// Good for wetness / mud / river bed placement.
/// </summary>
[Serializable]
public class FlowMask : MaskModifier
{
	[Property, Range( 1, 128 )]
	public int Iterations { get; set; } = 32;

	[Property, Range( 0f, 32f )]
	public float Strength { get; set; } = 8f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null )
			return;

		var storage = Terrain.Storage;

		int res = field.Resolution;

		float[] heights = new float[res * res];
		float[] flow = new float[res * res];

		// Cache terrain heights
		for ( int y = 0; y < res; y++ )
		{
			for ( int x = 0; x < res; x++ )
			{
				heights[y * res + x] =
					SampleHeight(
						field,
						storage,
						x,
						y
					);

				flow[y * res + x] = 1f;
			}
		}

		// Flow simulation
		for ( int i = 0; i < Iterations; i++ )
		{
			for ( int y = 1; y < res - 1; y++ )
			{
				for ( int x = 1; x < res - 1; x++ )
				{
					int index = y * res + x;

					float current =
						heights[index];

					int bestX = x;
					int bestY = y;

					float lowest = current;

					// 8-neighbor downhill search
					for ( int oy = -1; oy <= 1; oy++ )
					{
						for ( int ox = -1; ox <= 1; ox++ )
						{
							if ( ox == 0 && oy == 0 )
								continue;

							int nx = x + ox;
							int ny = y + oy;

							float neighbor =
								heights[
									ny * res + nx
								];

							if ( neighbor < lowest )
							{
								lowest = neighbor;
								bestX = nx;
								bestY = ny;
							}
						}
					}

					if ( bestX != x || bestY != y )
					{
						int dst =
							bestY * res + bestX;

						flow[dst] +=
							flow[index] * 0.25f;
					}
				}
			}
		}

		// Normalize
		float maxFlow = 0f;

		for ( int i = 0; i < flow.Length; i++ )
			maxFlow = MathF.Max(
				maxFlow,
				flow[i]
			);

		if ( maxFlow <= 0f )
			maxFlow = 1f;

		for ( int y = 0; y < res; y++ )
		{
			for ( int x = 0; x < res; x++ )
			{
				float value =
					flow[y * res + x] /
					maxFlow;

				value *= Strength;

				field.Set(
					x,
					y,
					Math.Clamp(
						value,
						0f,
						1f
					)
				);
			}
		}
	}

	private float SampleHeight(
		MaskField field,
		TerrainStorage storage,
		int x,
		int y
	)
	{
		x = Math.Clamp(
			x,
			0,
			field.Resolution - 1
		);

		y = Math.Clamp(
			y,
			0,
			field.Resolution - 1
		);

		var world =
			field.TexelToWorld( x, y );

		int tx = (int)(
			world.x /
			storage.TerrainSize *
			storage.Resolution
		);

		int ty = (int)(
			world.y /
			storage.TerrainSize *
			storage.Resolution
		);

		tx = Math.Clamp(
			tx,
			0,
			storage.Resolution - 1
		);

		ty = Math.Clamp(
			ty,
			0,
			storage.Resolution - 1
		);

		return (
			storage.HeightMap[
				ty * storage.Resolution + tx
			] / 65535f
		) * storage.TerrainHeight;
	}
}

#endregion
