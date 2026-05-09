
using Editor;
using Sandbox;

public static class ApexEditorMenu
{

	[Menu( "Editor", "Apex World/Open Editor" )]
	public static void OpenMyMenu()
	{
		var window = new ApexWorldEditorWindow( null );
		window.Show();
	}
}
