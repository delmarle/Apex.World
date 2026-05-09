using Editor;
using Sandbox.Spawners;

namespace Sandbox.Spawner;

public class BaseSpawnerEditor: EditorTool<BaseSpawner>
{

	public override void OnUpdate()
	{
		var selected = GetSelectedComponent<BaseSpawner>();

		foreach ( var spawner in Scene.GetAllComponents<BaseSpawner>() )
		{
			if ( spawner == null || spawner.Range <= 0 ) continue;

			bool isSelected = spawner == selected;
			float half = spawner.Range * 0.5f;


			int segments = 20;

			using ( Gizmo.ObjectScope( spawner.GameObject, spawner.WorldTransform ) )
			{
				Gizmo.Draw.Color = isSelected 
					? Color.Green 
					: Color.Black;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.IgnoreDepth = true;

				DrawProjectedEdge( new Vector2( -half, -half ), new Vector2(  half, -half ), segments, spawner );
				DrawProjectedEdge( new Vector2(  half, -half ), new Vector2(  half,  half ), segments, spawner );
				DrawProjectedEdge( new Vector2(  half,  half ), new Vector2( -half,  half ), segments, spawner );
				DrawProjectedEdge( new Vector2( -half,  half ), new Vector2( -half, -half ), segments, spawner );
			}
		}

	
	}
	
	private void DrawProjectedEdge( Vector2 from, Vector2 to, int segments, BaseSpawner spawner )
	{
		Vector3? prev = null;

		for ( int i = 0; i <= segments; i++ )
		{
			float t  = (float)i / segments;
			float lx = MathX.Lerp( from.x, to.x, t );
			float ly = MathX.Lerp( from.y, to.y, t );

			var worldXY = spawner.WorldTransform.PointToWorld( new Vector3( lx, ly, 0 ) );

			var tr = spawner.Scene.Trace
				.Ray( worldXY + Vector3.Up * 10000f, worldXY + Vector3.Down * 10000f )
				.WithoutTags( "player" )
				.Run();

			// stay in local space — ObjectScope handles the transform
			var hitWorld  = tr.Hit ? tr.HitPosition + Vector3.Up * 2f : worldXY;
			var localHit  = spawner.WorldTransform.PointToLocal( hitWorld );

			if ( prev.HasValue )
				Gizmo.Draw.Line( prev.Value, localHit );

			prev = localHit;
		}
	}
}
