using System.Threading.Tasks;
using Sandbox;
using Sandbox.Core;
using Sandbox.Spawners;

/// <summary>
/// This need to be a root component of everything it manager, terrains, gameObjects ...
/// </summary>
[Title( "Apex World - Manager" )]
public class ApexWorldManager : Component
{
	#region  FIELDS
	[Property] public bool SmartRebuild { get; set; }
	[Property] public BaseSpawner[] Spawners { get; set; }
	
	
	[RequireComponent] private ApexWorldCache _worldCache { get; set; }
	
	public ApexWorldCache GetWorldCache() => _worldCache;
	#endregion

	protected override Task OnLoad()
	{
		Log.Info( "ApexWorldManager loaded." );
		return base.OnLoad();
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		
	}
[Button]
	void RegisterSpawners()
	{
		Log.Warning( "RegisterSpawners: "+Spawners.Length );
		_worldCache.RegisterSpawners( Spawners );
	}

	protected override void OnEnabled()
	{
		if ( _worldCache == null )
		{
			_worldCache = Components.GetOrCreate<ApexWorldCache>();
		}
		
	}
	
	[Button]
	public void ResetAllTerrainsTextures()
	{
		foreach ( var terrain in GetWorldCache().GetAllTerrains() )
		{
			if ( terrain?.Storage == null ) continue;

			var storage = terrain.Storage;
			var defaultMaterial = new CompactTerrainMaterial
			{
				BaseTextureId    = 0,
				OverlayTextureId = 0,
				BlendFactor      = 0,
				IsHole           = false
			};

			for ( int i = 0; i < storage.ControlMap.Length; i++ )
				storage.ControlMap[i] = defaultMaterial.Packed;

			terrain.SyncGPUTexture();
			Log.Info( $"Reset terrain {terrain.GameObject.Name}" );
		}
	}

	public async void RegenerateAllSpawners()
	{
		foreach ( var spawner in Spawners )
		{
			spawner.OnBeforeGenerate();
			spawner.Generate();

			while ( spawner.IsFinished() == false )
			{
				await Task.Yield();
			}
			spawner.OnGenerationFinished();
		}
		
		Log.Info( "All spawners regenerated." );
	}
}
