using System;
using System.Collections.Generic;
using Sandbox.Mask;
using Sandbox.Spawns;

namespace Sandbox.Spawners;

[Serializable]
public class SpawnRule
{
	[Property] public string          RuleName   { get; set; } = "New Rule";
	[Property] public bool            Enabled    { get; set; } = true;
	[Property] public SpawnDefinition Definition { get; set; }

	// Per-rule masks, multiplied on top of the spawner-level mask
	[Property] [Editor( "MaskListPropertyEditor" )]public List<MaskModifier> Masks { get; set; } = new();

	[Property, Group( "Spawn" ), Range( 0f, 100f  )] public float SpawnProbabilityRate { get; set; } = 100f;
	[Property, Group( "Spawn" ), Range( 0.1f, 500f)] public float LocationIncrement    { get; set; } = 3f;
	[Property, Group( "Spawn" ), Range( 0f, 100f  )] public float Jitter               { get; set; } = 50f;
	[Property, Group( "Spawn" ), Range( 0f, 1f    )] public float MinFitness           { get; set; } = 0.1f;
	[Property, Group( "Spawn" )] public float BoundRadius        { get; set; } = 1f;
	[Property, Group( "Spawn" )] public bool  SelfCollisionCheck { get; set; } = true;

	public MaskField GenerateMask( Terrain terrain, int resolution, Vector2 worldOffset, float worldSize )
	{
		var field = new MaskField( resolution, worldSize ) { WorldOffset = worldOffset };
		field.Fill( 1f );

		foreach ( var mask in Masks )
		{
			mask.Terrain = terrain;
			var temp = new MaskField( resolution, worldSize ) { WorldOffset = worldOffset };
			temp.Fill( 1f );
			mask.Apply( temp );
			field = field.Multiply( temp );
		}

		return field;
	}
}
