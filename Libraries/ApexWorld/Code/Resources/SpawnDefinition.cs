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

[AssetType( Name = "SpawnDefinition", Extension = "sd", Category = "Apex World" )]
public class SpawnDefinition: GameResource
{
	/// <summary>
	/// defines how this object should be spawned/rendered.
	/// Used by the spawner system to determine instancing,
	/// collision handling, streaming behavior, etc.
	/// </summary>
	[Property] public SpawnObjectType ObjectType { get; set; }

	/// <summary>
	/// prefab that will be spawned or instanced.
	/// </summary>
	[Property] public GameObject Prefab { get; set; }
	
	[Property]
	public Model ClutterModel { get; set; }
	
	
	[Property, Range( -100f, 0f )] public float MinYOffset { get; set; } = 0f;

	[Property, Range(0f, 200f)]  public float MaxYOffset { get; set; } = 0f;
	
	[Property] public ScaleMode SpawnScale { get; set; }
	
	[Hide] private bool ShowRandomScale => SpawnScale != ScaleMode.Fixed;
	
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
	
	
}
