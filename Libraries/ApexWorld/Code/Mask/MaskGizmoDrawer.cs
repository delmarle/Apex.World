using System;
using Sandbox.Spawners;

namespace Sandbox.Mask;

public static class MaskGizmoDrawer
{
	/// <summary>
	/// Size of each debug cube.
	/// </summary>
	public const float CubeSize = 20.0f;

	/// <summary>
	/// Space between samples.
	/// </summary>
	public const float SampleSpacing = 30.0f;

	public static void Draw(
		MaskField maskField,
		Terrain terrain,
		Vector3 center,
		BBox bounds )
	{
		
		for ( float x = 0; x < maskField.WorldSize; x += SampleSpacing )
		{
			for ( float y = 0; y < maskField.WorldSize; y += SampleSpacing )
			{
				var terrainLocal = new Vector3(
					maskField.WorldOffset.x + x,
					maskField.WorldOffset.y + y,
					0
				);

				var worldPos = terrain.WorldTransform.PointToWorld( terrainLocal );

				float value = SpawnUtils.SampleMaskAt(
					maskField,
					terrain,
					worldPos
				);

				if ( value <= 0.001f )
					continue;

				value = value.Clamp( 0.0f, 1.0f );

				Gizmo.Draw.Color = Color.Lerp(
					Color.Black,
					Color.White,
					value
				);

				Gizmo.Draw.SolidBox(
					BBox.FromPositionAndSize(
						worldPos,
						CubeSize
					)
				);
			}
		}
	}
}
