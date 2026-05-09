using System.Collections.Generic;

namespace Sandbox.Spawners;

public abstract class BaseSpawner: Component
{
	#region FIELDS
	[Property] public string SpawnerName { get; set; }
	[Property, Range(0, 4096)] public float Range { get; set; }

	
	#endregion

	public override string ToString()
	{
		return $"{GetType().Name} - {SpawnerName}";
	}

	#region VIRTUAL FUNCTIONS

	/// <summary>
	/// handle cleaning or init
	/// </summary>
	public virtual void OnBeforeGenerate() { }

	public virtual void Generate() {}
	
	/// <summary>
	/// will see
	/// </summary>
	public virtual void OnGenerationFinished() { }

	public bool IsFinished() => _isFinished;

	protected bool _isFinished = false;
	#endregion

	#region UTILS

	public BBox GenerateSpawnerBounds()
	{
		float half = Range * 0.5f;

		BBox bounds = new BBox(
			WorldPosition - new Vector3( half, half, 0 ),
			WorldPosition + new Vector3( half, half, 0 )
		);

		return bounds;
	}
	
	protected ApexWorldManager Manager
	{
		get
		{
			if (Scene == null) return null;
			
			foreach (var gameObject in Scene.GetAllObjects(false))
			{
				var manager = gameObject.Components.Get<ApexWorldManager>();
				if (manager != null) return manager;
			}
			return null;
		}
	}
	
	
	protected List<Terrain> GetTerrainsInBounds( BBox bounds )
	{
		var manager = Manager;
		if ( manager == null ) return new List<Terrain>();

		return manager.GetWorldCache().GetTerrainsInBounds( bounds );
	}
	#endregion
	
	
}
