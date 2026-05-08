using Sandbox;
using Sandbox.Spawners;

/// <summary>
/// This need to be a root component of everything it manager, terrains, gameObjects ...
/// </summary>
[Title( "Apex World - Manager" )]
public class ApexWorldManager : Component
{
	#region  FIELDS

	[Property] public BaseSpawner[] Spawners { get; set; }

	//for now just one terrain but later we need better way to do that.
	[Property] public Terrain Terrain { get; set; }
	#endregion
}
