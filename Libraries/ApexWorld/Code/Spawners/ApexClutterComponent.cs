using Sandbox.Rendering;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ApexWorld;

public sealed class ApexClutterComponent : Component, Component.ExecuteInEditor
{
	#region Properties

	[Property] public Model   Model     { get; set; }
	[Property] public Color   Tint      { get; set; } = Color.White;

	[Property, Range( 1, 512 )]    public int    GridSize             { get; set; } = 128;
	[Property, Range( 10f, 500f )] public float  Spacing              { get; set; } = 100f;
	[Property, Range( 0f, 100f )]  public float  Jitter               { get; set; } = 25f;
	[Property]                     public Vector2 ScaleRange          { get; set; } = new( 0.8f, 1.2f );
	[Property, Range( 256f, 8192f )] public float ChunkWorldSize      { get; set; } = 1024f;
	[Property]                     public float  BoundsPadding        { get; set; } = 256f;
	[Property]                     public float  VerticalBoundsPadding { get; set; } = 5000f;

	
	private static RenderAttributes _attributes = new();
	#endregion

	#region Chunk Scene Object

	/// <summary>
	/// Proper SceneCustomObject subclass that renders instanced models directly.
	/// Stores transforms and tint, draws on render thread with Graphics.DrawModelInstanced.
	/// </summary>
	private sealed class ChunkSceneObject : SceneCustomObject
	{
		private Model _model;
		private List<Transform> _transforms = new();


		public ChunkSceneObject( SceneWorld world ) : base( world )
		{
			Flags.IsOpaque    = true;
			Flags.IsTranslucent = false;
			Flags.CastShadows = true;
			Flags.WantsPrePass = true;
		}

		/// <summary>
		/// Stores the model, transforms, and tint for rendering.
		/// Call this after creating or whenever any parameter changes.
		/// </summary>
		public void Build( Model model, List<Transform> transforms, Color tint )
		{
			_model = model;
			_transforms = transforms;
		}

		public override void RenderSceneObject()
		{
			if ( _model == null || _transforms.Count == 0 ) return;

			Graphics.DrawModelInstanced( _model, CollectionsMarshal.AsSpan( _transforms ), _attributes );
		}

		public new void Delete()
		{
			_model = null;
			_transforms.Clear();
			base.Delete();
		}
	}

	#endregion

	#region State

	private sealed class ChunkData
	{
		public readonly List<Transform> Transforms = new();
		public BBox              Bounds;
		public ChunkSceneObject  SceneObject;
	}

	private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
	private SceneWorld _cachedSceneWorld;

	#endregion

	#region Lifecycle

	protected override void OnUpdate()
	{
		if ( Scene == null ) return;

		// SceneWorld changes when entering/exiting play mode — rebuild scene objects only
		if ( Scene.SceneWorld != _cachedSceneWorld )
		{
			_cachedSceneWorld = Scene.SceneWorld;
			if ( _chunks.Count > 0 )
				RebuildSceneObjects();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Clear();
	}

	#endregion

	#region Public API

	[Button]
	public void Spawn()
	{
		Clear();

		if ( Model == null ) { Log.Warning( "[ClutterTest] No model assigned." ); return; }

		float half = GridSize * 0.5f;

		for ( int y = 0; y < GridSize; y++ )
		{
			for ( int x = 0; x < GridSize; x++ )
			{
				float px = (x - half) * Spacing + Game.Random.Float( -Jitter, Jitter );
				float py = (y - half) * Spacing + Game.Random.Float( -Jitter, Jitter );

				var pos   = WorldPosition + new Vector3( px, py, 0f );
				var coord = WorldToChunk( pos );

				if ( !_chunks.TryGetValue( coord, out var chunk ) )
				{
					chunk = new ChunkData { Bounds = GetChunkBounds( coord ) };
					_chunks[coord] = chunk;
				}

				chunk.Transforms.Add( new Transform(
					pos,
					Rotation.FromYaw( Game.Random.Float( 0f, 360f ) ),
					Game.Random.Float( ScaleRange.x, ScaleRange.y )
				) );
			}
		}

		_cachedSceneWorld = Scene.SceneWorld;
		RebuildSceneObjects();

		Log.Info( $"[ClutterTest] {_chunks.Count} chunks | {GridSize * GridSize} instances" );
	}

	[Button]
	public void Clear()
	{
		foreach ( var chunk in _chunks.Values )
			chunk.SceneObject?.Delete();

		_chunks.Clear();
	}

	#endregion

	#region Internal

	/// <summary>
	/// Creates or recreates SceneCustomObjects from existing ChunkData.
	/// Called after Spawn and whenever SceneWorld changes.
	/// Transforms are NOT regenerated — only render objects are rebuilt.
	/// </summary>
	private void RebuildSceneObjects()
	{
		if ( Scene?.SceneWorld == null ) return;

		foreach ( var chunk in _chunks.Values )
		{
			chunk.SceneObject?.Delete();
			chunk.SceneObject = null;

			if ( chunk.Transforms.Count == 0 ) continue;

			var obj = new ChunkSceneObject( Scene.SceneWorld );
			obj.Bounds = chunk.Bounds;
			obj.Build( Model, chunk.Transforms, Tint );

			chunk.SceneObject = obj;
		}
	}

	private BBox GetChunkBounds( Vector2Int coord )
	{
		return new BBox(
			new Vector3(
				coord.x       * ChunkWorldSize - BoundsPadding,
				coord.y       * ChunkWorldSize - BoundsPadding,
				-VerticalBoundsPadding
			),
			new Vector3(
				(coord.x + 1) * ChunkWorldSize + BoundsPadding,
				(coord.y + 1) * ChunkWorldSize + BoundsPadding,
				VerticalBoundsPadding
			)
		);
	}

	private Vector2Int WorldToChunk( Vector3 pos ) => new(
		(int)MathF.Floor( pos.x / ChunkWorldSize ),
		(int)MathF.Floor( pos.y / ChunkWorldSize )
	);

	#endregion
}
