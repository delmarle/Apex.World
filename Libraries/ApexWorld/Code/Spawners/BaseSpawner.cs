namespace Sandbox.Spawners;

public abstract class BaseSpawner: Component
{
	[Property] public string SpawnerName { get; set; }

	public override string ToString()
	{
		return $"{GetType().Name} - {SpawnerName}";
	}
}
