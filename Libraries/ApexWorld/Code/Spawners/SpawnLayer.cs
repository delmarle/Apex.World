using System;
using System.Collections.Generic;
using Sandbox.Mask;

namespace Sandbox.Spawners;

[Serializable]
public class SpawnLayer
{
	[Property] public string LayerName { get; set; }
	[Property, Range(0, 64)] public int TextureId { get; set; }

	[Property]
	[Editor( "MaskListPropertyEditor" )]
	public List<MaskModifier> Masks { get; set; } = new();
	
	public override string ToString()
	{
		return LayerName+" - TextureId: " + TextureId;
	}
	
	public MaskField GenerateMask( Terrain terrain, int resolution )
	{
		var field = new MaskField(
			resolution,
			terrain.Storage.TerrainSize
		);

		// start fully white
		field.Fill( 1f );

		foreach ( var mask in Masks )
		{
			mask.Terrain = terrain;
			var temp = new MaskField(
				resolution,
				terrain.Storage.TerrainSize
			);

			temp.Fill( 1f );
			mask.Apply( temp );
			field = field.Multiply( temp );
		}

		return field;
	}
}


