using System.Text.Json.Serialization;

namespace Sandbox.Mask;

using System;


public enum MaskCombineOp
{
	Add,
	Subtract,
	Multiply,
	Max,
	Min,
	Lerp,
	Override
}

[JsonDerivedType( typeof( SplineMask ), "spline" )]
[JsonDerivedType( typeof( SlopeMask ),     "slope"     )]
[JsonDerivedType( typeof( HeightMask ),    "height"    )]
[JsonDerivedType( typeof( NoiseMask ),     "noise"     )]
[JsonDerivedType( typeof( DistanceMask ),  "distance"  )]
[JsonDerivedType( typeof( CurvatureMask ), "curvature" )]
[JsonDerivedType( typeof( ErosionMask ),   "erosion"   )]
[JsonDerivedType( typeof( FlowMask ),      "flow"      )]
[Serializable]
public class MaskModifier
{
	[Hide]public Terrain Terrain { get; set; }

	public virtual void Apply( MaskField field ) { }
}
/// <summary>
/// Combines a generated MaskField into an existing one using a blend operation.
/// Wrap any IMaskModifier with this to control how it stacks with others.
/// </summary>
[Serializable]
public class MaskModifierEntry
{
	[Property]
	public MaskModifier Modifier { get; set; }
	[Property] public MaskCombineOp CombineOp   { get; set; } = MaskCombineOp.Multiply;
	[Property, Range(0f, 1f)] public float Strength { get; set; } = 1f;
	[Property] public bool           Enabled    { get; set; } = true;
	
	
	/// <summary>
	/// Applies the source field onto the target using this entry's CombineOp and Strength.
	/// </summary>
	public void Blend( MaskField target, MaskField source )
	{
		if ( !Enabled ) return;

		var resampled = source.ResampleTo( target.Resolution, target.WorldSize );

		for ( int i = 0; i < target.Values.Length; i++ )
		{
			float a = target.Values[i];
			float b = resampled.Values[i] * Strength;

			target.Values[i] = CombineOp switch
			{
				MaskCombineOp.Add      => Math.Clamp( a + b, 0f, 1f ),
				MaskCombineOp.Subtract => Math.Clamp( a - b, 0f, 1f ),
				MaskCombineOp.Multiply => a * b,
				MaskCombineOp.Max      => MathF.Max( a, b ),
				MaskCombineOp.Min      => MathF.Min( a, b ),
				MaskCombineOp.Lerp     => MathX.Lerp( a, b, Strength ),
				MaskCombineOp.Override => b,
				_                      => a
			};
		}
	}
}
