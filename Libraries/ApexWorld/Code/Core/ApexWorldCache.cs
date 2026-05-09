using System.Collections.Generic;
using System.Linq;
using Sandbox.Spawners;

namespace Sandbox.Core;

public class ApexWorldCache : Component, Component.ExecuteInEditor
{
	#region Terrain Cache

	private List<Terrain> _cachedTerrains  = new();
	private bool          _terrainCacheDirty = true;

	public List<Terrain> GetAllTerrains()
	{
		EnsureTerrainCache();
		return _cachedTerrains;
	}

	public void InvalidateTerrainCache() => _terrainCacheDirty = true;

	private void EnsureTerrainCache()
	{
		if ( !_terrainCacheDirty ) return;
		_cachedTerrains.Clear();

		foreach ( var terrain in Scene.GetAllComponents<Terrain>() )
		{
			if ( terrain.Storage == null ) continue;
			_cachedTerrains.Add( terrain );
		}

		Log.Info( $"Cached {_cachedTerrains.Count} terrains in the scene." );
		_terrainCacheDirty = false;
	}

	private BBox GetTerrainBounds( Terrain terrain )
	{
		var   origin = terrain.WorldPosition;
		float size   = terrain.Storage.TerrainSize;
		float height = terrain.Storage.TerrainHeight;

		return new BBox(
			origin,
			origin + new Vector3( size, size, height )
		);
	}

	public List<Terrain> GetTerrainsInBounds( BBox bounds )
	{
		InvalidateTerrainCache();
		EnsureTerrainCache();
		return _cachedTerrains
			.Where( t => t.IsValid() && GetTerrainBounds( t ).Overlaps( bounds ) )
			.ToList();
	}

	#endregion

	#region Spawner Drag Tracking

	private const float DragSettleDelay = 0.3f;

	private readonly Dictionary<BaseSpawner, Vector3> _spawnerLastPositions = new();
	private BaseSpawner    _pendingSpawner     = null;
	private RealTimeSince  _timeSinceLastMove;

	public void RegisterSpawners( IEnumerable<BaseSpawner> spawners )
	{
		UnregisterSpawners();

		if ( spawners == null ) return;

		foreach ( var spawner in spawners )
		{
			if ( spawner == null ) continue;
			_spawnerLastPositions[spawner] = spawner.WorldPosition;
			spawner.Transform.OnTransformChanged += () => OnSpawnerMoved( spawner );
		}
	}

	public void UnregisterSpawners()
	{
		foreach ( var spawner in _spawnerLastPositions.Keys )
		{
			if ( spawner == null ) continue;
			spawner.Transform.OnTransformChanged = null;
		}

		_spawnerLastPositions.Clear();
		_pendingSpawner = null;
	}

	private void OnSpawnerMoved( BaseSpawner spawner )
	{
		_pendingSpawner    = spawner;
		_timeSinceLastMove = 0;
	}

	protected override void OnUpdate()
	{
	
		if ( _pendingSpawner == null ) return;
		
		if ( _timeSinceLastMove > DragSettleDelay )
		{
			OnSpawnerDropped( _pendingSpawner );
			_pendingSpawner = null;
		}
	}

	private void OnSpawnerDropped( BaseSpawner spawner )
	{
		_spawnerLastPositions[spawner] = spawner.WorldPosition;

		ApexWorldManager manager = GameObject.GetComponent<ApexWorldManager>();
		if ( manager != null )
		{
			if ( manager.SmartRebuild )
			{
				manager.ResetAllTerrainsTextures();
				manager.RegenerateAllSpawners();
			}
		}
	}

	protected override void OnDisabled()
	{
		UnregisterSpawners();
	}


	#endregion
}
