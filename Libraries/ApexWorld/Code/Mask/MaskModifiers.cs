

namespace Sandbox.Mask;
using System.Linq;
using System;



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
				int tx = (int)((float)x / field.Resolution * res);
				int ty = (int)((float)y / field.Resolution * res);

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
				// Map field texel -> terrain texel
				int tx = (int)((float)x / field.Resolution * res);
				int ty = (int)((float)y / field.Resolution * res);

				tx = Math.Clamp( tx, 1, res - 2 );
				ty = Math.Clamp( ty, 1, res - 2 );

				float c = storage.HeightMap[ty * res + tx]       * hScale;
				float r = storage.HeightMap[ty * res + tx + 1]   * hScale;
				float u = storage.HeightMap[(ty + 1) * res + tx] * hScale;

				float slopeX     = MathF.Abs( r - c ) / sScale;
				float slopeY     = MathF.Abs( u - c ) / sScale;
				float slopeAngle = MathF.Atan( MathF.Max( slopeX, slopeY ) ) * (180f / MathF.PI);

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
	[Property] public float  Scale      { get; set; } = 500f;
	[Property] public int    Octaves    { get; set; } = 4;
	[Property, Range(0f, 1f)] public float Persistence { get; set; } = 0.5f;
	[Property] public float  Lacunarity { get; set; } = 2f;
	[Property] public int    Seed       { get; set; } = 0;
	[Property] public Vector2 Offset    { get; set; } = Vector2.Zero;

	public override void Apply( MaskField field )
	{
		var rng    = new Random( Seed );
		float offX = (float)rng.NextDouble() * 10000f + Offset.x;
		float offY = (float)rng.NextDouble() * 10000f + Offset.y;

		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				var world = field.TexelToWorld( x, y );
				float nx  = (world.x + offX) / Scale;
				float ny  = (world.y + offY) / Scale;

				float value     = 0f;
				float amplitude = 1f;
				float frequency = 1f;
				float total     = 0f;

				for ( int o = 0; o < Octaves; o++ )
				{
					value     += Perlin( nx * frequency, ny * frequency ) * amplitude;
					total     += amplitude;
					amplitude *= Persistence;
					frequency *= Lacunarity;
				}

				field.Set( x, y, value / total );
			}
		}
	}

	private static float Perlin( float x, float y )
	{
		// Simple gradient noise — avoids System.Numerics dependency
		int xi = (int)MathF.Floor( x ) & 255;
		int yi = (int)MathF.Floor( y ) & 255;
		float xf = x - MathF.Floor( x );
		float yf = y - MathF.Floor( y );
		float u  = Fade( xf );
		float v  = Fade( yf );

		int aa = _p[_p[xi] + yi];
		int ab = _p[_p[xi] + yi + 1];
		int ba = _p[_p[xi + 1] + yi];
		int bb = _p[_p[xi + 1] + yi + 1];

		float res = Lerp(
			Lerp( Grad( aa, xf,     yf     ), Grad( ba, xf - 1f, yf     ), u ),
			Lerp( Grad( ab, xf,     yf - 1f), Grad( bb, xf - 1f, yf - 1f), u ),
			v );

		return (res + 1f) * 0.5f;
	}

	private static float Fade( float t ) => t * t * t * (t * (t * 6f - 15f) + 10f);
	private static float Lerp( float a, float b, float t ) => a + t * (b - a);
	private static float Grad( int hash, float x, float y )
	{
		int h = hash & 3;
		float u = h < 2 ? x : y;
		float v = h < 2 ? y : x;
		return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
	}

	private static readonly int[] _p;
	static NoiseMask()
	{
		_p = new int[512];
		int[] perm = new int[256];
		for ( int i = 0; i < 256; i++ ) perm[i] = i;
		var rng = new Random( 42 );
		for ( int i = 255; i > 0; i-- )
		{
			int j   = rng.Next( i + 1 );
			(perm[i], perm[j]) = (perm[j], perm[i]);
		}
		for ( int i = 0; i < 512; i++ ) _p[i] = perm[i & 255];
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
	[Property] public Vector2 Center     { get; set; } = Vector2.Zero;
	[Property] public float   Radius     { get; set; } = 500f;
	[Property] public bool    Invert     { get; set; } = false;

	public override void Apply( MaskField field )
	{
		for ( int y = 0; y < field.Resolution; y++ )
		{
			for ( int x = 0; x < field.Resolution; x++ )
			{
				var world = field.TexelToWorld( x, y );
				float dist = Vector2.Distance( world, Center );
				float value = Math.Clamp( 1f - dist / Radius, 0f, 1f );
				value = value * value * (3f - 2f * value); // smoothstep
				field.Set( x, y, Invert ? 1f - value : value );
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
public class CurvatureMask : MaskModifier
{
	public enum CurvatureMode { Concave, Convex, Both }

	[Property] public CurvatureMode Mode      { get; set; } = CurvatureMode.Concave;
	[Property, Range(0f, 1f)] public float Strength { get; set; } = 1f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null ) return;

		var storage  = Terrain.Storage;
		int res      = storage.Resolution;
		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;

		for ( int y = 1; y < field.Resolution - 1; y++ )
		{
			for ( int x = 1; x < field.Resolution - 1; x++ )
			{
				int tx = Math.Clamp( (int)((float)x / field.Resolution * res), 1, res - 2 );
				int ty = Math.Clamp( (int)((float)y / field.Resolution * res), 1, res - 2 );

				float c  = storage.HeightMap[ty * res + tx]           * hScale;
				float l  = storage.HeightMap[ty * res + tx - 1]       * hScale;
				float r  = storage.HeightMap[ty * res + tx + 1]       * hScale;
				float d  = storage.HeightMap[(ty - 1) * res + tx]     * hScale;
				float u  = storage.HeightMap[(ty + 1) * res + tx]     * hScale;

				// Laplacian curvature
				float curv = (l + r + d + u) * 0.25f - c;

				float value = Mode switch
				{
					CurvatureMode.Concave => Math.Clamp( -curv * Strength * 10f, 0f, 1f ),
					CurvatureMode.Convex  => Math.Clamp(  curv * Strength * 10f, 0f, 1f ),
					CurvatureMode.Both    => Math.Clamp( MathF.Abs( curv ) * Strength * 10f, 0f, 1f ),
					_                     => 0f
				};

				field.Set( x, y, value );
			}
		}
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
	[Property, Range(1, 8)] public int   Iterations { get; set; } = 3;
	[Property, Range(0f, 1f)] public float Threshold { get; set; } = 0.3f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null ) return;

		var storage  = Terrain.Storage;
		int res      = storage.Resolution;
		float hScale = storage.TerrainHeight / (float)ushort.MaxValue;
		float[] flow = new float[field.Resolution * field.Resolution];

		for ( int iter = 0; iter < Iterations; iter++ )
		{
			for ( int y = 1; y < field.Resolution - 1; y++ )
			{
				for ( int x = 1; x < field.Resolution - 1; x++ )
				{
					int tx = Math.Clamp( (int)((float)x / field.Resolution * res), 1, res - 2 );
					int ty = Math.Clamp( (int)((float)y / field.Resolution * res), 1, res - 2 );

					float c = storage.HeightMap[ty * res + tx] * hScale;
					float l = storage.HeightMap[ty * res + tx - 1] * hScale;
					float r = storage.HeightMap[ty * res + tx + 1] * hScale;
					float d = storage.HeightMap[(ty - 1) * res + tx] * hScale;
					float u = storage.HeightMap[(ty + 1) * res + tx] * hScale;

					float minNeighbour = MathF.Min( MathF.Min( l, r ), MathF.Min( d, u ) );
					float drop = Math.Clamp( c - minNeighbour, 0f, 1f );
					flow[y * field.Resolution + x] += drop;
				}
			}
		}

		float maxFlow = flow.Max();
		if ( maxFlow < 0.0001f ) return;

		for ( int i = 0; i < flow.Length; i++ )
			field.Values[i] = Math.Clamp( flow[i] / maxFlow - Threshold, 0f, 1f );
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
	[Property, Range(0f, 1f)] public float WetnessThreshold { get; set; } = 0.4f;
	[Property, Range(0f, 1f)] public float Softness         { get; set; } = 0.2f;

	public override void Apply( MaskField field )
	{
		if ( Terrain?.Storage == null ) return;

		var erosion   = new ErosionMask { Terrain = Terrain, Iterations = 4, Threshold = 0f };
		var curvature = new CurvatureMask { Terrain = Terrain, Mode = CurvatureMask.CurvatureMode.Concave, Strength = 1f };

		var erosionField   = new MaskField( field.Resolution, field.WorldSize );
		var curvatureField = new MaskField( field.Resolution, field.WorldSize );

		erosion.Apply( erosionField );
		curvature.Apply( curvatureField );

		var combined = erosionField.Multiply( curvatureField );

		float soft = Softness;
		for ( int i = 0; i < field.Values.Length; i++ )
		{
			float v = combined.Values[i];
			float t = Math.Clamp( (v - WetnessThreshold + soft) / (soft * 2f + 0.0001f), 0f, 1f );
			field.Values[i] = t * t * (3f - 2f * t);
		}
	}
}

#endregion
