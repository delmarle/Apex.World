using System;
using System.Collections.Generic;
using System.Linq;
using ApexWorld;
using Sandbox.Mask;
using Sandbox.Spawns;

namespace Sandbox.Spawners;

public class DetailsSpawner : BaseSpawner
{
	
	// ── Settings ────────────────────────────────────────────────────────────

	[Property, Group( "Settings" ), Range( 64, 1024 )]
	public int MaskResolution { get; set; } = 256;

	// Spawner-level masks applied globally before any rule mask
	[Property, Group( "Spawner Masks" )]
	public List<MaskModifier> SpawnerMasks { get; set; } = new();

	// Individual spawn rules
	[Property, Group( "Spawn Rules" )]
	public List<SpawnRule> SpawnRules { get; set; } = new();

	// ── Spawn Root ──────────────────────────────────────────────────────────

	/// <summary>
	/// Hierarchy: DetailsSpawner -> SpawnRoot -> spawned objects.
	/// Clearing is simply destroying SpawnRoot's children.
	/// </summary>
	private GameObject SpawnRoot => GetOrCreateSpawnRoot();

	private GameObject GetOrCreateSpawnRoot()
	{
		foreach ( var child in GameObject.Children )
			if ( child.Name == "SpawnRoot" )
				return child;

		var root = Scene.CreateObject();
		root.Name   = "SpawnRoot";
		root.Parent = GameObject;
		return root;
	}

	// ── BaseSpawner overrides ───────────────────────────────────────────────

	public override void OnBeforeGenerate()
	{
		_isFinished = false;
		ClearSpawns();
		base.OnBeforeGenerate();
	}

	public override void Generate()
	{
		base.Generate();
		GenerateDetails();
	}

	// ── Public buttons ──────────────────────────────────────────────────────

	[Button]
	public void GenerateDetails()
	{
		ClearSpawns();

		var bounds   = GenerateSpawnerBounds();
		var terrains = GetTerrainsInBounds( bounds );

		if ( terrains == null || terrains.Count == 0 )
		{
			Log.Warning( $"{this}: no terrains in bounds" );
			_isFinished = true;
			return;
		}

		foreach ( var terrain in terrains )
			ProcessTerrain( terrain, bounds );

		_isFinished = true;
		Log.Info( $"{this}: generation done" );
	}

	[Button]
	public void ClearSpawns()
	{
		foreach ( var child in SpawnRoot.Children.ToList() )
			child.Destroy();
	}

	// ── Core ────────────────────────────────────────────────────────────────

	private void ProcessTerrain( Terrain terrain, BBox bounds )
	{
		if ( terrain?.Storage == null ) return;

		// Terrain-local top-left corner of the spawner bounds = WorldOffset for masks
		var terrainLocalMins = terrain.WorldTransform.PointToLocal( bounds.Mins );
		var worldOffset      = new Vector2( terrainLocalMins.x, terrainLocalMins.y );
		float worldSize      = Range;

		// Build spawner-level fitness mask (1.0 everywhere if no masks set)
		var spawnerMask = BuildMask( SpawnerMasks, terrain, worldOffset, worldSize );

		foreach ( var rule in SpawnRules )
		{
			if ( !rule.Enabled || rule.Definition?.Prefab == null ) continue;
			ProcessRule( rule, terrain, bounds, spawnerMask, worldOffset, worldSize );
		}
	}

	private void ProcessRule(
		SpawnRule  rule,
		Terrain    terrain,
		BBox       bounds,
		MaskField  spawnerMask,
		Vector2    worldOffset,
		float      worldSize )
	{
		// Combined fitness = spawner mask * rule mask
		var ruleMask = rule.GenerateMask( terrain, MaskResolution, worldOffset, worldSize );
		var fitness  = spawnerMask.Multiply( ruleMask );

		var rng       = new Random( HashCode.Combine( SpawnerName, rule.RuleName ) );
		var placed    = new List<Vector3>(); // for self-collision
		List<Transform> clutterTransforms = null;

		if ( rule.Definition.ObjectType == SpawnObjectType.Clutter )
		{
			clutterTransforms = new List<Transform>();
		}
		float step      = rule.LocationIncrement;
		float jitterAmt = step * (rule.Jitter / 100f);

		for ( float wy = bounds.Mins.y; wy <= bounds.Maxs.y; wy += step )
		{
			for ( float wx = bounds.Mins.x; wx <= bounds.Maxs.x; wx += step )
			{
				// ── Jitter ────────────────────────────────────────────────
				float jx  = ((float)rng.NextDouble() * 2f - 1f) * jitterAmt;
				float jy  = ((float)rng.NextDouble() * 2f - 1f) * jitterAmt;
				var   pos = new Vector3( wx + jx, wy + jy, 0f );

				// Keep within spawner bounds
				if ( pos.x < bounds.Mins.x || pos.x > bounds.Maxs.x ||
				     pos.y < bounds.Mins.y || pos.y > bounds.Maxs.y )
					continue;

				// ── Fitness check ─────────────────────────────────────────
				float fit = SpawnUtils.SampleMaskAt( fitness, terrain, pos );
				if ( fit < rule.MinFitness ) continue;

				// ── Spawn probability ─────────────────────────────────────
				if ( (float)rng.NextDouble() * 100f > rule.SpawnProbabilityRate ) continue;

				// ── Self-collision check ───────────────────────────────────
				if ( rule.SelfCollisionCheck && HasCollision( placed, pos, rule.BoundRadius ) )
					continue;

				// ── Resolve world height ──────────────────────────────────
				float h        = SpawnUtils.GetTerrainHeightAt( terrain, pos );
				float yOffset  = MathX.Lerp( rule.Definition.MinYOffset, rule.Definition.MaxYOffset, (float)rng.NextDouble() );
				var spawnPos   = new Vector3( pos.x, pos.y, h + yOffset );

				// ── Transform ─────────────────────────────────────────────
				var rot   = SpawnUtils.GetSpawnRotation( rule.Definition, terrain, spawnPos, rng );
				var scale = SpawnUtils.GetSpawnScale( rule.Definition, fit, rng );

				// ── Spawn ─────────────────────────────────────────────────
				var transform = new Transform(
					spawnPos,
					rot,
					scale
				);

				if ( rule.Definition.ObjectType == SpawnObjectType.Clutter )
				{
					clutterTransforms.Add( transform );
				}
				else
				{
					var go           = rule.Definition.Prefab.Clone();
					go.Parent        = SpawnRoot;
					go.WorldPosition = spawnPos;
					go.WorldRotation = rot;
					go.WorldScale    = scale;
					go.Enabled       = true;
				}

				placed.Add( pos );
			}
		}

		Log.Info( $"{this} [{rule.RuleName}]: spawned {placed.Count} objects" );
		
		if ( rule.Definition.ObjectType == SpawnObjectType.Clutter
		     && clutterTransforms != null
		     && clutterTransforms.Count > 0 )
		{
			var clutterObject = Scene.CreateObject();
			clutterObject.Name = $"Clutter_{rule.RuleName}";
			clutterObject.Parent = SpawnRoot;

			var clutter = clutterObject.Components.Create<ApexClutterComponent>();

			var modelRenderer = rule.Definition.ClutterModel;

			if ( modelRenderer == null )
			{
				Log.Warning( $"{rule.RuleName}: clutter prefab missing ModelRenderer" );
				return;
			}

			clutter.Model = modelRenderer;

			clutter.BuildFromTransforms( clutterTransforms );
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private MaskField BuildMask( List<MaskModifier> masks, Terrain terrain, Vector2 worldOffset, float worldSize )
	{
		var field = new MaskField( MaskResolution, worldSize ) { WorldOffset = worldOffset };
		field.Fill( 1f );

		foreach ( var mask in masks )
		{
			mask.Terrain = terrain;
			var temp = new MaskField( MaskResolution, worldSize ) { WorldOffset = worldOffset };
			temp.Fill( 1f );
			mask.Apply( temp );
			field = field.Multiply( temp );
		}

		return field;
	}

	private static bool HasCollision( List<Vector3> placed, Vector3 pos, float radius )
	{
		float minDist = radius * 2f;
		foreach ( var p in placed )
		{
			float dx = p.x - pos.x;
			float dy = p.y - pos.y;
			if ( dx * dx + dy * dy < minDist * minDist )
				return true;
		}
		return false;
	}
}
