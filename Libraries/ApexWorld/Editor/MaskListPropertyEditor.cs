using Editor;
using Sandbox;
using Sandbox.Mask;
using System.Collections.Generic;
using System.Linq;
using Sandbox.UI;
using Button = Editor.Button;
using ControlSheet = Editor.ControlSheet;
using Label  = Editor.Label;

namespace ApexWorld.Editor;

[CustomEditor( typeof( List<MaskModifier> ) )]
public class MaskListPropertyEditor : ControlWidget
{
	private readonly SerializedProperty _property;

	public MaskListPropertyEditor( SerializedProperty property ) : base( property )
	{
		_property = property;
		Layout = Layout.Column();
		Layout.Spacing = 2;
		Rebuild();
	}

	private void Rebuild()
	{
		// Clear layout — destroys all child widgets
		Layout.Clear( true );

		var list = _property.GetValue<List<MaskModifier>>();
		if ( list == null )
		{
			list = new List<MaskModifier>();
			_property.SetValue( list );
		}

		// Render each modifier as a collapsible section
		foreach ( var modifier in list.ToList() )
		{
			if ( modifier == null ) continue;

			var captured = modifier;

			// ── Section header ─────────────────────────────
			var header = new Widget();
			header.Layout = Layout.Row();
			header.Layout.Spacing = 6;
			header.Layout.Margin  = new Margin( 4, 4, 4, 2 );
			header.SetStyles( "background-color: #1e2535; border-radius: 3px;" );

			var typeLabel = new Label( FormatTypeName( modifier.GetType().Name ), header );
			typeLabel.SetStyles( "font-size: 11px; font-weight: bold; color: #aabbcc;" );
			header.Layout.Add( typeLabel );
			header.Layout.AddStretchCell();

			var removeBtn = new Button( "✕", header );
			removeBtn.SetStyles( "color: #c07070; background: transparent; border: none; font-size: 10px; padding: 2px 6px;" );
			removeBtn.Clicked += () =>
			{
				list.Remove( captured );
				_property.SetValue( list );
				Rebuild();
			};
			header.Layout.Add( removeBtn );
			Layout.Add( header );

			// ── Sub-properties via ControlSheet ────────────
			var container = new Widget();
			container.Layout = Layout.Column();
			container.Layout.Margin = new Margin( 12, 0, 0, 0 );
			
			var so = TypeLibrary.GetSerializedObject( captured );
			var sheet = new ControlSheet();
			sheet.AddObject( so );
			container.Layout.Add( sheet );
			Layout.Add( container );

			// ── Divider ────────────────────────────────────
			var divider = new Widget();
			divider.SetStyles( "height: 1px; background-color: #252c3a; margin: 2px 0;" );
			Layout.Add( divider );
		}

		// ── Add buttons ────────────────────────────────────
		BuildAddButtons( list );
	}

	private void BuildAddButtons( List<MaskModifier> list )
	{
		var label = new Label( "ADD MASK" );
		label.SetStyles( "font-size: 9px; color: #445; letter-spacing: 2px; margin: 6px 4px 2px 4px;" );
		Layout.Add( label );

		var row = new Widget();
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;
		row.FixedHeight = 36;
		
		Layout.Add( row );

		AddMaskButton<HeightMask>( row, list, "Height", 0 );
		AddMaskButton<SlopeMask>( row, list, "Slope", 1 );
		AddMaskButton<NoiseMask>( row, list, "Noise", 2 );
		AddMaskButton<DistanceMask>( row, list, "Distance", 3 );
		AddMaskButton<CurvatureMask>( row, list, "Curvature", 4 );
		AddMaskButton<ErosionMask>( row, list, "Erosion", 5 );
		AddMaskButton<FlowMask>( row, list, "Flow", 6 );
		AddMaskButton<SplineMask>( row, list, "Spline", 7 );
	}

	private void AddMaskButton<T>( Widget parent, List<MaskModifier> list, string label , int index) where T : MaskModifier, new()
	{
		var button = new Button( parent );

		button.Text = $"{label}";
		button.Position = new Vector2( index * 75, 0 );
	//	button.HorizontalSizeMode = SizeMode.CanGrow;
	//	button.VerticalSizeMode = SizeMode.CanGrow;

		button.MinimumWidth = 30;
		button.MinimumHeight = 16;

		button.Clicked += () =>
		{
			list.Add( new T() );

			_property.SetValue( list );

			Rebuild();
		};

	
	}

	private static string FormatTypeName( string typeName ) =>
		typeName.Replace( "Mask", "" ).Trim() + " Mask";
}
