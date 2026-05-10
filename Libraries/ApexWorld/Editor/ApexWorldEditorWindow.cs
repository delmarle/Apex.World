using Editor;
using Sandbox;
using Sandbox.Core;
using Sandbox.Spawners;
using System;
using System.Linq;
using Sandbox.UI;
using Button = Editor.Button;
using Label  = Editor.Label;

[EditorApp( "Apex World", "landscape", "Apex World terrain generation tools" )]
public class ApexWorldEditorWindow : Widget
{
	#region Fields

	private ApexWorldManager _manager;
	private Label            _statusLabel;

	private Widget _tabConfigs;
	private Widget _tabTerrains;
	private Widget _tabSpawners;

	private Button _btnConfigs;
	private Button _btnTerrains;
	private Button _btnSpawners;

	private Widget _spawnerListContainer;
	private Widget _terrainListContainer;
	private Widget _createManagerBtnContainer;

	private enum SpawnerCreateType { TextureSpawner, DetailSpawner }
	private SpawnerCreateType _newSpawnerType = SpawnerCreateType.TextureSpawner;
	private bool _comboInitialized;

	private Widget _configListContainer;

	private const string StyleTabActive   = "background-color: #2d4a3a; color: #90ff90; border-bottom: 3px solid #70d090; border-radius: 0; padding: 10px 0; font-weight: bold; font-size: 13px;";
	private const string StyleTabInactive = "background-color: transparent; color: #6677aa; border-bottom: 3px solid transparent; border-radius: 0; padding: 10px 0; font-size: 13px;";
	private const string StyleRow         = "background-color: #1e2535; border-bottom: 1px solid #252c3a; padding: 6px 0;";
	private const string StyleBtnGreen    = "background-color: #1e3828; color: #70d090; border: 1px solid #2d5a3d; border-radius: 3px; padding: 8px 14px; font-size: 12px; font-weight: 600;";
	private const string StyleBtnRed      = "background-color: #2e1c1c; color: #d07070; border: 1px solid #4a2828; border-radius: 3px; padding: 8px 14px; font-size: 12px; font-weight: 600;";
	private const string StyleBtnNeutral  = "background-color: #1e2535; color: #8899bb; border: 1px solid #2a3550; border-radius: 3px; padding: 8px 14px; font-size: 12px; font-weight: 600;";

	#endregion

	#region Constructor

	public ApexWorldEditorWindow( Widget parent ) : base( parent )
	{
		WindowTitle = "Apex World";
		MinimumSize = new Vector2( 400, 500 );
		Layout = Layout.Column();
		Layout.Spacing = 0;

		BuildHeaderAndTabs();
		BuildTabConfigs();
		BuildTabTerrains();
		BuildTabSpawners();
		BuildFooter();

		Refresh();
		ShowTab( 0 );
	}

	#endregion

	#region Header & Tabs

	private void BuildHeaderAndTabs()
	{
		var header = new Widget( this );
		header.Layout = Layout.Column();
		header.Layout.Spacing = 0;
		header.Layout.Margin = 0;
		header.SetStyles( "background-color: #141820; border-bottom: 1px solid #222a38;" );

		// Title and status row
		var titleRow = new Widget( header );
		titleRow.Layout = Layout.Row();
		titleRow.Layout.Spacing = 8;
		titleRow.Layout.Margin = new Margin( 12, 10, 12, 10 );

		var title = new Label( "APEX WORLD", titleRow );
		title.SetStyles( "font-size: 18px; font-weight: bold; color: #e8d5a0; letter-spacing: 4px;" );

		_statusLabel = new Label( "", titleRow );
		_statusLabel.SetStyles( "font-size: 12px; color: #556; letter-spacing: 1px; margin-left: 8px;" );

		titleRow.Layout.Add( title );
		titleRow.Layout.Add( _statusLabel );
		titleRow.Layout.AddStretchCell();

		var refreshBtn = new IconButton( "refresh" );
		refreshBtn.ToolTip = "Refresh";
		refreshBtn.OnClick += Refresh;
		titleRow.Layout.Add( refreshBtn );

		header.Layout.Add( titleRow );

		// Tab bar - spread evenly
		var tabBar = new Widget( header );
		tabBar.Layout = Layout.Row();
		tabBar.Layout.Spacing = 0;
		tabBar.Layout.Margin = 0;
		tabBar.SetStyles( "background-color: #0f1218; border-top: 1px solid #222a38;" );

		_btnConfigs  = new Button( "Configs",  tabBar ); _btnConfigs.SetStyles(  StyleTabInactive ); _btnConfigs.Clicked  += () => ShowTab( 0 );
		_btnTerrains = new Button( "Terrains", tabBar ); _btnTerrains.SetStyles( StyleTabInactive ); _btnTerrains.Clicked += () => ShowTab( 1 );
		_btnSpawners = new Button( "Spawners", tabBar ); _btnSpawners.SetStyles( StyleTabInactive ); _btnSpawners.Clicked += () => ShowTab( 2 );

		tabBar.Layout.Add( _btnConfigs, 1 );
		tabBar.Layout.Add( _btnTerrains, 1 );
		tabBar.Layout.Add( _btnSpawners, 1 );

		header.Layout.Add( tabBar );
		Layout.Add( header );
	}

	#endregion

	#region Tab Management

	private void ShowTab( int index )
	{
		_tabConfigs.Visible  = index == 0;
		_tabTerrains.Visible = index == 1;
		_tabSpawners.Visible = index == 2;

		_btnConfigs.SetStyles(  index == 0 ? StyleTabActive : StyleTabInactive );
		_btnTerrains.SetStyles( index == 1 ? StyleTabActive : StyleTabInactive );
		_btnSpawners.SetStyles( index == 2 ? StyleTabActive : StyleTabInactive );

		if ( index == 0 ) RebuildConfigList();
		if ( index == 1 ) RebuildTerrainList();
		if ( index == 2 ) RebuildSpawnerList();
	}

	#endregion

	#region Tab — Configs

	private void BuildTabConfigs()
	{
		_tabConfigs = new Widget( this );
		_tabConfigs.Layout = Layout.Column();
		_tabConfigs.Layout.Spacing = 0;
		_tabConfigs.Visible = false;

		SectionLabel( _tabConfigs, "MANAGER" );

		_createManagerBtnContainer = new Widget( _tabConfigs );
		_createManagerBtnContainer.Layout = Layout.Column();
		_createManagerBtnContainer.Layout.Spacing = 6;
		_createManagerBtnContainer.Layout.Margin = 12;

		_tabConfigs.Layout.Add( _createManagerBtnContainer );

		SectionLabel( _tabConfigs, "PROJECT SETTINGS" );

		var scroll = new ScrollArea( _tabConfigs );
		scroll.MinimumHeight = 200;

		_configListContainer = new Widget( scroll );
		_configListContainer.Layout = Layout.Column();
		_configListContainer.Layout.Spacing = 1;

		scroll.Canvas = _configListContainer;
		_tabConfigs.Layout.Add( scroll );
		_tabConfigs.Layout.AddStretchCell();
		Layout.Add( _tabConfigs );
	}

	private void RebuildConfigList()
	{
		_createManagerBtnContainer.DestroyChildren();


		if ( _manager == null )
		{
			var createManagerBtn = new Button( "Create Manager in Scene", "add_circle", _createManagerBtnContainer );
			createManagerBtn.SetStyles( StyleBtnNeutral );
			createManagerBtn.Clicked += () =>
			{
				var scene = SceneEditorSession.Active?.Scene;
				if ( scene == null ) return;
				var go = scene.CreateObject();
				go.Name = "ApexWorldManager";
				go.Components.Create<ApexWorldManager>();
				Refresh();
			};
			_createManagerBtnContainer.Layout.Add( createManagerBtn );
		}

		_configListContainer.DestroyChildren();

		// List configuration files from ProjectSettings
		var projectSettingsPath = System.IO.Path.Combine( 
			System.IO.Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location ) ?? "",
			"..", "..", "..", "ProjectSettings" );

		try
		{
			if ( System.IO.Directory.Exists( projectSettingsPath ) )
			{
				var configFiles = System.IO.Directory.GetFiles( projectSettingsPath, "*.config" );
				
				if ( configFiles.Length == 0 )
				{
					EmptyLabel( _configListContainer, "No configuration files found" );
					return;
				}

				foreach ( var filePath in configFiles )
				{
					var fileName = System.IO.Path.GetFileName( filePath );

					var row = new Widget( _configListContainer );
					row.Layout = Layout.Row();
					row.Layout.Spacing = 8;
					row.Layout.Margin = new Margin( 12, 6, 8, 6 );
					row.SetStyles( StyleRow );
					row.MinimumHeight = 50;

					var col = new Widget( row );
					col.Layout = Layout.Column();
					col.Layout.Spacing = 2;

					var nameLabel = new Label( fileName, col );
					nameLabel.SetStyles( "font-size: 12px; color: #ccc; font-weight: bold;" );

					var infoLabel = new Label( $"Path: {filePath.Replace( "\\", "/" )}", col );
					infoLabel.SetStyles( "font-size: 9px; color: #556;" );

					col.Layout.Add( nameLabel );
					col.Layout.Add( infoLabel );
					row.Layout.Add( col, 1 );

					var openBtn = new IconButton( "open_in_new" );
					openBtn.ToolTip = "Open in editor";
					openBtn.OnClick += () =>
					{
						Log.Info( $"[ApexWorld] Opening config: {fileName}" );
					};

					row.Layout.Add( openBtn );
					_configListContainer.Layout.Add( row );
				}
			}
			else
			{
				EmptyLabel( _configListContainer, "ProjectSettings folder not found" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ApexWorld] Error reading config files: {e.Message}" );
			EmptyLabel( _configListContainer, "Error reading configuration files" );
		}
	}

	#endregion

	#region Tab — Terrains

	private void BuildTabTerrains()
	{
		_tabTerrains = new Widget( this );
		_tabTerrains.Layout = Layout.Column();
		_tabTerrains.Layout.Spacing = 0;
		_tabTerrains.Visible = false;

		SectionLabel( _tabTerrains, "TERRAINS IN SCENE" );

		var scroll = new ScrollArea( _tabTerrains );
		scroll.MinimumHeight = 300;

		_terrainListContainer = new Widget( scroll );
		_terrainListContainer.Layout = Layout.Column();
		_terrainListContainer.Layout.Spacing = 1;

		scroll.Canvas = _terrainListContainer;
		_tabTerrains.Layout.Add( scroll );
		_tabTerrains.Layout.AddStretchCell();
		Layout.Add( _tabTerrains );
	}

	private void RebuildTerrainList()
	{
		_terrainListContainer.DestroyChildren();

		if ( _manager == null ) { EmptyLabel( _terrainListContainer, "No manager in scene" ); return; }

		var terrains = _manager.GetWorldCache()?.GetAllTerrains();
		if ( terrains == null || terrains.Count == 0 ) { EmptyLabel( _terrainListContainer, "No terrains found" ); return; }

		foreach ( var terrain in terrains )
		{
			if ( terrain == null ) continue;

			var row = new Widget( _terrainListContainer );
			row.Layout = Layout.Row();
			row.Layout.Spacing = 6;
			row.Layout.Margin = new Margin( 12, 6, 8, 6 );
			row.SetStyles( StyleRow );
			row.MinimumHeight = 50;

			var col = new Widget( row );
			col.Layout = Layout.Column();
			col.Layout.Spacing = 2;

			var name = new Label( terrain.GameObject.Name, col );
			name.SetStyles( "font-size: 12px; color: #ccc; font-weight: bold;" );

			var info = new Label( $"Res: {terrain.Storage?.Resolution}  |  Size: {terrain.Storage?.TerrainSize:F0}", col );
			info.SetStyles( "font-size: 10px; color: #556;" );

			col.Layout.Add( name );
			col.Layout.Add( info );
			row.Layout.Add( col, 1 );

			var resetBtn = new IconButton( "layers_clear" );
			resetBtn.ToolTip = "Reset textures";
			resetBtn.OnClick += () =>
			{
				if ( terrain?.Storage == null ) return;
				var mat = new CompactTerrainMaterial { BaseTextureId = 0, OverlayTextureId = 0, BlendFactor = 0, IsHole = false };
				for ( int i = 0; i < terrain.Storage.ControlMap.Length; i++ )
					terrain.Storage.ControlMap[i] = mat.Packed;
				terrain.SyncGPUTexture();
				Log.Info( $"[ApexWorld] Reset {terrain.GameObject.Name}" );
			};

			var selectBtn = new IconButton( "my_location" );
			selectBtn.ToolTip = "Select in scene";
			selectBtn.OnClick += () => SceneEditorSession.Active?.Selection.Set( terrain.GameObject );

			row.Layout.Add( resetBtn );
			row.Layout.Add( selectBtn );
			_terrainListContainer.Layout.Add( row );
		}
	}

	#endregion

	#region Tab — Spawners

	private void BuildTabSpawners()
	{
		_tabSpawners = new Widget( this );
		_tabSpawners.Layout = Layout.Column();
		_tabSpawners.Layout.Spacing = 0;
		_tabSpawners.Visible = false;

		SectionLabel( _tabSpawners, "SPAWNERS IN SCENE" );

		var scroll = new ScrollArea( _tabSpawners );
		scroll.MinimumHeight = 260;

		_spawnerListContainer = new Widget( scroll );
		_spawnerListContainer.Layout = Layout.Column();
		_spawnerListContainer.Layout.Spacing = 0;

		scroll.Canvas = _spawnerListContainer;
		_tabSpawners.Layout.Add( scroll );

		SectionLabel( _tabSpawners, "CREATE SPAWNER" );

		var createRow = new Widget( _tabSpawners );
		createRow.Layout = Layout.Row();
		createRow.Layout.Spacing = 8;
		createRow.Layout.Margin = new Margin( 12, 10, 12, 10 );
		createRow.SetStyles( "background-color: #141820;" );

		var combo = new ComboBox( createRow );
		combo.AddItem( "TextureSpawner", null );
		combo.AddItem( "DetailSpawner", null );
		combo.ItemChanged += () =>
		{
			if ( _comboInitialized )
			{
				_newSpawnerType = (SpawnerCreateType)combo.CurrentIndex;
				
				Log.Info( (SpawnerCreateType)combo.CurrentIndex );
			}
		};
		_comboInitialized = true;
		createRow.Layout.Add( combo );

		var addBtn = new Button( "Add Spawner", "add", createRow );
		addBtn.SetStyles( StyleBtnGreen );
		addBtn.Clicked += CreateSpawner;
		createRow.Layout.Add( addBtn );

		_tabSpawners.Layout.Add( createRow );
		Layout.Add( _tabSpawners );
	}

	private void RebuildSpawnerList()
	{
		_spawnerListContainer.DestroyChildren();

		if ( _manager?.Spawners == null || _manager.Spawners.Length == 0 )
		{
			EmptyLabel( _spawnerListContainer, "No spawners assigned" );
			return;
		}

		var spawners = _manager.Spawners;

		for ( int i = 0; i < spawners.Length; i++ )
		{
			var spawner = spawners[i];
			if ( spawner == null ) continue;

			int index = i;

			var row = new Widget( _spawnerListContainer );
			row.Layout = Layout.Row();
			row.Layout.Spacing = 6;
			row.Layout.Margin = new Margin( 12, 6, 8, 6 );
			row.SetStyles( StyleRow );
			row.MinimumHeight = 50;

			var col = new Widget( row );
			col.Layout = Layout.Column();
			col.Layout.Spacing = 2;

			var nameLabel = new Label( spawner.SpawnerName ?? spawner.GetType().Name, col );
			nameLabel.SetStyles( "font-size: 12px; color: #ccc; font-weight: bold;" );

			var typeLabel = new Label( $"{spawner.GetType().Name}  |  Range: {spawner.Range:F0}", col );
			typeLabel.SetStyles( "font-size: 10px; color: #556;" );

			col.Layout.Add( nameLabel );
			col.Layout.Add( typeLabel );
			row.Layout.Add( col, 1 );

			var upBtn = new IconButton( "arrow_upward" );
			upBtn.ToolTip = "Move up";
			upBtn.Enabled = index > 0;
			upBtn.OnClick += () => MoveSpawner( index, -1 );

			var downBtn = new IconButton( "arrow_downward" );
			downBtn.ToolTip = "Move down";
			downBtn.Enabled = index < spawners.Length - 1;
			downBtn.OnClick += () => MoveSpawner( index, 1 );

			var generateBtn = new IconButton( "play_arrow" );
			generateBtn.ToolTip = "Generate";
			generateBtn.SetStyles( "color: #70d090;" );
			generateBtn.OnClick += () =>
			{
				spawner.OnBeforeGenerate();
				spawner.Generate();
				spawner.OnGenerationFinished();
				Log.Info( $"[ApexWorld] Generated: {spawner}" );
			};

			var selectBtn = new IconButton( "my_location" );
			selectBtn.ToolTip = "Select in scene";
			selectBtn.OnClick += () => SceneEditorSession.Active?.Selection.Set( spawner.GameObject );

			var destroyBtn = new IconButton( "delete" );
			destroyBtn.ToolTip = "Destroy spawner";
			destroyBtn.SetStyles( "color: #d07070;" );
			destroyBtn.OnClick += () => DestroySpawner( spawner );

			row.Layout.Add( upBtn );
			row.Layout.Add( downBtn );
			row.Layout.Add( generateBtn );
			row.Layout.Add( selectBtn );
			row.Layout.Add( destroyBtn );
			_spawnerListContainer.Layout.Add( row );
		}
	}

	private void MoveSpawner( int index, int direction )
	{
		var list = _manager.Spawners.ToList();
		int newIdx = index + direction;
		if ( newIdx < 0 || newIdx >= list.Count ) return;
		(list[index], list[newIdx]) = (list[newIdx], list[index]);
		_manager.Spawners = list.ToArray();
		RebuildSpawnerList();
	}

	private void CreateSpawner()
	{
		if ( _manager == null ) return;

		// Create GameObject in the same scene as the manager
		var scene = _manager.GameObject.Scene;
		if ( scene == null ) return;

		var go = scene.CreateObject( false );
		go.Name = _newSpawnerType.ToString();
		go.Parent = _manager.GameObject;

		BaseSpawner spawner = _newSpawnerType switch
		{
			SpawnerCreateType.TextureSpawner => go.Components.Create<TextureSpawner>(),
			SpawnerCreateType.DetailSpawner  => go.Components.Create<DetailsSpawner>(),
			_                                => null
		};

		if ( spawner == null ) { go.Destroy(); return; }

		var list = (_manager.Spawners ?? Array.Empty<BaseSpawner>()).ToList();
		list.Add( spawner );
		_manager.Spawners = list.ToArray();

		Log.Info( $"[ApexWorld] Created {_newSpawnerType}" );
		RebuildSpawnerList();
	}

	private void DestroySpawner( BaseSpawner spawner )
	{
		var list = _manager.Spawners.ToList();
		list.Remove( spawner );
		_manager.Spawners = list.ToArray();
		spawner.GameObject.Destroy();
		RebuildSpawnerList();
	}

	#endregion

	#region Footer

	private void BuildFooter()
	{
		var footer = new Widget( this );
		footer.Layout = Layout.Row();
		footer.Layout.Spacing = 8;
		footer.Layout.Margin = new Margin( 12, 10, 12, 10 );
		footer.SetStyles( "background-color: #141820; border-top: 1px solid #222a38;" );

		var generateAll = new Button( "Generate All", "play_arrow", footer );
		generateAll.SetStyles( StyleBtnGreen );
		generateAll.Clicked += OnGenerateAll;

		var resetAll = new Button( "Reset Textures", "layers_clear", footer );
		resetAll.SetStyles( StyleBtnRed );
		resetAll.Clicked += OnResetAll;

		footer.Layout.Add( generateAll );
		footer.Layout.Add( resetAll );
		footer.Layout.AddStretchCell();

		Layout.Add( footer );
	}

	private void OnGenerateAll()
	{
		if ( _manager?.Spawners == null ) return;
		foreach ( var spawner in _manager.Spawners )
		{
			if ( spawner == null ) continue;
			spawner.OnBeforeGenerate();
			spawner.Generate();
			spawner.OnGenerationFinished();
		}
		Log.Info( "[ApexWorld] Generated all spawners" );
	}

	private void OnResetAll() => _manager?.ResetAllTerrainsTextures();

	#endregion

	#region Manager & Refresh

	private ApexWorldManager FindManager() =>
		SceneEditorSession.Active?.Scene?
			.GetAllComponents<ApexWorldManager>()
			.FirstOrDefault();

	private void Refresh()
	{
		_manager = FindManager();

		if ( _manager != null )
		{
			_statusLabel.Text = $"Manager: {_manager.GameObject.Name}";
			_statusLabel.SetStyles( "font-size: 11px; color: #70c080; letter-spacing: 1px;" );
			_manager.RegisterSpawners();
		}
		else
		{
			_statusLabel.Text = "No manager found";
			_statusLabel.SetStyles( "font-size: 11px; color: #c07070; letter-spacing: 1px;" );
		}

		RebuildConfigList();
		RebuildSpawnerList();
		RebuildTerrainList();
	}

	#endregion

	#region Helpers

	private static void SectionLabel( Widget parent, string text )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Margin = new Margin( 12, 6, 12, 6 );
		row.SetStyles( "background-color: #161b26; border-bottom: 1px solid #222a38; border-top: 1px solid #222a38;" );
		var label = new Label( text, row );
		label.SetStyles( "font-size: 10px; font-weight: bold; color: #445; letter-spacing: 3px;" );
		parent.Layout.Add( row );
	}

	private static void EmptyLabel( Widget parent, string text )
	{
		var label = new Label( text, parent );
		label.SetStyles( "color: #445; font-size: 11px; padding: 20px; text-align: center;" );
		parent.Layout.Add( label );
	}

	#endregion
}
