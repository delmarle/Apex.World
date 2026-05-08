using Editor;
using Sandbox.Spawners;

namespace Sandbox.Spawner;

public class BaseSpawnerEditor: EditorTool<BaseSpawner>
{
	public override void OnUpdate()
	{
		var spawner = GetSelectedComponent<BaseSpawner>();
		if (spawner == null || spawner.Range <= 0) return;
        
		using (Gizmo.ObjectScope(spawner.GameObject, spawner.WorldTransform))
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Black.WithAlpha(0.7f);
			Gizmo.Draw.LineThickness = 2;
			
			
			BBox bbox = new BBox(
				-Vector3.One * spawner.Range * 0.5f,
				Vector3.One * spawner.Range * 0.5f
			);
		
			Gizmo.Draw.LineBBox(bbox  );
			
			Gizmo.Draw.Color = Color.White;
			//Gizmo.Draw.Text($"Range: {spawner.Range:F1}", spawner.WorldTransform, "Roboto", 52);
			
			
		}
	}
}
