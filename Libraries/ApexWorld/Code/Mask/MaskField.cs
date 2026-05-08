using System;
using System.Linq;

namespace Sandbox.Mask;


public class MaskField
{
	#region Fields

	public int    Resolution { get; }
	public float  WorldSize  { get; }
	public float[] Values    { get; }

	#endregion

	#region Constructor

	public MaskField( int resolution, float worldSize )
	{
		Resolution = resolution;
		WorldSize  = worldSize;
		Values     = new float[resolution * resolution];
	}

	private MaskField( int resolution, float worldSize, float[] values )
	{
		Resolution = resolution;
		WorldSize  = worldSize;
		Values     = values;
	}

	#endregion

	#region Access

	public float Get( int x, int y )
	{
		x = Math.Clamp( x, 0, Resolution - 1 );
		y = Math.Clamp( y, 0, Resolution - 1 );
		return Values[y * Resolution + x];
	}

	public void Set( int x, int y, float value )
	{
		x = Math.Clamp( x, 0, Resolution - 1 );
		y = Math.Clamp( y, 0, Resolution - 1 );
		Values[y * Resolution + x] = Math.Clamp( value, 0f, 1f );
	}

	public void SetRaw( int x, int y, float value )
	{
		Values[y * Resolution + x] = value;
	}

	#endregion

	#region Sampling

	/// <summary>
	/// Samples the field at a world position using bilinear interpolation.
	/// WorldPos origin (0,0) maps to texel (0,0). 
	/// </summary>
	public float Sample( Vector2 worldPos )
	{
		float u = worldPos.x / WorldSize * (Resolution - 1);
		float v = worldPos.y / WorldSize * (Resolution - 1);

		int x0 = Math.Clamp( (int)MathF.Floor( u ), 0, Resolution - 1 );
		int y0 = Math.Clamp( (int)MathF.Floor( v ), 0, Resolution - 1 );
		int x1 = Math.Clamp( x0 + 1,                0, Resolution - 1 );
		int y1 = Math.Clamp( y0 + 1,                0, Resolution - 1 );

		float tx = u - x0;
		float ty = v - y0;

		float v00 = Get( x0, y0 );
		float v10 = Get( x1, y0 );
		float v01 = Get( x0, y1 );
		float v11 = Get( x1, y1 );

		float top    = MathX.Lerp( v00, v10, tx );
		float bottom = MathX.Lerp( v01, v11, tx );
		return MathX.Lerp( top, bottom, ty );
	}

	public float Sample( Vector3 worldPos ) => Sample( new Vector2( worldPos.x, worldPos.y ) );

	#endregion

	#region Combine Operations

	public MaskField Add( MaskField other )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = Math.Clamp( Values[i] + resampled.Values[i], 0f, 1f );
		return result;
	}

	public MaskField Subtract( MaskField other )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = Math.Clamp( Values[i] - resampled.Values[i], 0f, 1f );
		return result;
	}

	public MaskField Multiply( MaskField other )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = Values[i] * resampled.Values[i];
		return result;
	}

	public MaskField Max( MaskField other )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = MathF.Max( Values[i], resampled.Values[i] );
		return result;
	}

	public MaskField Min( MaskField other )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = MathF.Min( Values[i], resampled.Values[i] );
		return result;
	}

	public MaskField Lerp( MaskField other, float t )
	{
		var result = Clone();
		var resampled = other.ResampleTo( Resolution, WorldSize );
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = MathX.Lerp( Values[i], resampled.Values[i], t );
		return result;
	}

	public MaskField Invert()
	{
		var result = Clone();
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = 1f - Values[i];
		return result;
	}

	public MaskField Clamp( float min = 0f, float max = 1f )
	{
		var result = Clone();
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = Math.Clamp( Values[i], min, max );
		return result;
	}

	public MaskField Power( float exponent )
	{
		var result = Clone();
		for ( int i = 0; i < Values.Length; i++ )
			result.Values[i] = MathF.Pow( Values[i], exponent );
		return result;
	}

	#endregion

	#region Resampling

	/// <summary>
	/// Resamples this field to a different resolution or world size using bilinear interpolation.
	/// </summary>
	public MaskField ResampleTo( int targetResolution, float targetWorldSize )
	{
		if ( targetResolution == Resolution && MathF.Abs( targetWorldSize - WorldSize ) < 0.001f )
			return this;

		var result = new MaskField( targetResolution, targetWorldSize );
		float step = targetWorldSize / (targetResolution - 1);

		for ( int y = 0; y < targetResolution; y++ )
		{
			for ( int x = 0; x < targetResolution; x++ )
			{
				var worldPos = new Vector2( x * step, y * step );
				result.Values[y * targetResolution + x] = Sample( worldPos );
			}
		}

		return result;
	}

	#endregion

	#region Utilities

	public MaskField Clone()
	{
		var copy = new float[Values.Length];
		Array.Copy( Values, copy, Values.Length );
		return new MaskField( Resolution, WorldSize, copy );
	}

	public void Fill( float value )
	{
		value = Math.Clamp( value, 0f, 1f );
		for ( int i = 0; i < Values.Length; i++ )
			Values[i] = value;
	}

	public float GetMin() => Values.Min();
	public float GetMax() => Values.Max();

	public void Normalize()
	{
		float min = GetMin();
		float max = GetMax();
		float range = max - min;
		if ( range < 0.0001f ) return;
		for ( int i = 0; i < Values.Length; i++ )
			Values[i] = (Values[i] - min) / range;
	}

	public Vector2 TexelToWorld( int x, int y )
	{
		float step = WorldSize / (Resolution - 1);
		return new Vector2( x * step, y * step );
	}

	public (int x, int y) WorldToTexel( Vector2 worldPos )
	{
		int x = Math.Clamp( (int)(worldPos.x / WorldSize * (Resolution - 1)), 0, Resolution - 1 );
		int y = Math.Clamp( (int)(worldPos.y / WorldSize * (Resolution - 1)), 0, Resolution - 1 );
		return (x, y);
	}

	public override string ToString() => $"MaskField [{Resolution}x{Resolution}] world={WorldSize}";

	#endregion
}
