using System.Collections.Generic;

namespace Sandbox.Resources;

[AssetType( Name = "ApexTerrainResources", Extension = "apex", Category = "Apex World" )]
public class ApexTerrainResources: GameResource
{
	public List<TerrainMaterial> TerrainMaterials { get; set; }
}
