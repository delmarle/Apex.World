using System;
using System.Collections.Generic;

namespace Sandbox.Spawns;

public enum ScaleMode
{
	Fixed, //use min value for static scale
	FitnessRandom,//use fitness value for dynamic scale and * by min max
	Random // multiply by min max
}

public enum SpawnObjectType
{
	GameObject,
	Clutter
}

[Serializable]
public class SpawnEntry
{
	
	[Hide] private bool IsClutter => ObjectType == SpawnObjectType .Clutter;
	
	[Property] public SpawnObjectType ObjectType { get; set; }
	[Property,  ShowIf( nameof( IsClutter ), false)] public GameObject Prefab       { get; set; }
	[Property,  ShowIf( nameof( IsClutter ), true)] public Model      ClutterModel { get; set; }
	[Property, Range( 0f, 10f )] public float Weight { get; set; } = 1f;

	public override string ToString() =>
		Prefab       != null ? Prefab.Name            :
		ClutterModel != null ? ClutterModel.ResourceName :
		"Empty";
}

[AssetType( Name = "SpawnDefinition", Extension = "sd", Category = "Apex World" )]
public class SpawnDefinition: GameResource
{
	[Hide] private bool ShowRandomScale => SpawnScale != ScaleMode.Fixed;
	

	[Property] public List<SpawnEntry>  Entries    { get; set; } = new();
	
	
	[Property, Range( -100f, 0f )] public float MinYOffset { get; set; } = 0f;

	[Property, Range(0f, 200f)]  public float MaxYOffset { get; set; } = 0f;
	
	[Property] public ScaleMode SpawnScale { get; set; }
	

	
	/// <summary>
	/// add a percentage to existing scale
	/// </summary>
	[Property, Range(0f, 100f), ShowIf( nameof( ShowRandomScale ), true )]  public float WidthRandomPercent { get; set; } = 0;
	
	[Property, Range( 0f, 10f ), ShowIf( nameof( ShowRandomScale ), true )] public float WidthMinScale { get; set; } = 1f;

	[Property, Range(0f, 10f)]  public float WidthMaxScale { get; set; } = 1f;
	
	/// <summary>
	///add a percentage to existing scale
	/// </summary>
	[Property, Range(0f, 100f), ShowIf( nameof( ShowRandomScale ), true )]  public float HeightRandomPercent { get; set; } = 0;
	
	[Property, Range( 0f, 10f), ShowIf( nameof( ShowRandomScale ), true )] public float HeightMinScale { get; set; } = 1f;

	[Property, Range(0f, 10f)]  public float HeightMaxScale { get; set; } = 1f;
	
	
	/// <summary>
	/// Rotates the object so its up vector matches terrain normal.
	/// Useful for rocks and props on uneven terrain.
	/// </summary>
	[Property]
	public bool AlignToSlope { get; set; }

	/// <summary>
	/// Rotates the forward direction toward the terrain slope direction.
	/// Useful for fallen logs, directional props, etc.
	/// </summary>
	[Property]
	public bool ForwardToSlope { get; set; }

	/// <summary>
	/// Minimum random rotation offset applied after alignment.
	/// </summary>
	[Property]
	public Angles MinRotationOffset { get; set; } = Angles.Zero;

	/// <summary>
	/// Maximum random rotation offset applied after alignment.
	/// </summary>
	[Property]
	public Angles MaxRotationOffset { get; set; } = Angles.Zero;
	
	public SpawnEntry PickEntry( Random rng )
	{
		if ( Entries == null || Entries.Count == 0 ) return null;
		if ( Entries.Count == 1 ) return Entries[0];

		float total = 0f;
		foreach ( var e in Entries ) total += e.Weight;
		if ( total <= 0f ) return Entries[0];

		float roll = (float)rng.NextDouble() * total;
		float acc  = 0f;
		foreach ( var e in Entries )
		{
			acc += e.Weight;
			if ( roll <= acc ) return e;
		}
		return Entries[^1];
	}

}
