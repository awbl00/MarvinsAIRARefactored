
using System.Runtime.CompilerServices;

using MarvinsAIRARefactored.Classes;

using Image = System.Windows.Controls.Image;

namespace MarvinsAIRARefactored.Components;

public class Graph : GraphBase
{
	private const int UpdateInterval = 6;

	// the graph overlay window's bitmap - one column per playout tick, so 1080 columns is 3 seconds of history at 360 Hz
	public const int OverlayBitmapWidth = 1080;
	public const int OverlayBitmapHeight = 401;

	public enum LayerIndex
	{
		InputTorque,
		OutputTorque,
		InputTorque60Hz,
		InputLFE,
		ClutchPedalHaptics,
		BrakePedalHaptics,
		ThrottlePedalHaptics,
		TimerJitter,
		Count
	}

	private readonly Layer[] _layerArray = new Layer[ (int) LayerIndex.Count ];

	private int _updateCounter = UpdateInterval + 2;

	// true while the graph overlay window is open - the layers are only recorded and drawn while someone can see them
	// (written on the UI thread, read on the playout thread)
	private volatile bool _isShown = false;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Graph] Initialize >>>" );

		Initialize( OverlayBitmapWidth, OverlayBitmapHeight );

		for ( var layerIndex = 0; layerIndex < (int) LayerIndex.Count; layerIndex++ )
		{
			_layerArray[ layerIndex ] = new Layer();
		}

		app.Logger.WriteLine( "[Graph] <<< Initialize" );
	}

	// called by the graph overlay window when it opens
	public void Show( Image image )
	{
		AttachImage( image );

		_isShown = true;
	}

	// called by the graph overlay window when it closes
	public void Hide()
	{
		_isShown = false;
	}

	public void SetLayerColors( LayerIndex layerIndex, float r, float g, float b )
	{
		var layer = _layerArray[ (int) layerIndex ];

		layer.r = r;
		layer.g = g;
		layer.b = b;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void UpdateLayer( LayerIndex layerIndex, float normalizedValue )
	{
		if ( _isShown )
		{
			_layerArray[ (int) layerIndex ].value = normalizedValue;
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void Update()
	{
		if ( _isShown )
		{
			var settings = DataContext.DataContext.Instance.Settings;

			for ( var layerIndex = LayerIndex.InputTorque; layerIndex < LayerIndex.Count; layerIndex++ )
			{
				var showLayer = layerIndex switch
				{
					LayerIndex.InputTorque => settings.GraphInputTorque,
					LayerIndex.OutputTorque => settings.GraphOutputTorque,
					LayerIndex.InputTorque60Hz => settings.GraphInputTorque60Hz,
					LayerIndex.InputLFE => settings.GraphInputLFE,
					LayerIndex.ClutchPedalHaptics => settings.GraphClutchPedalHaptics,
					LayerIndex.BrakePedalHaptics => settings.GraphBrakePedalHaptics,
					LayerIndex.ThrottlePedalHaptics => settings.GraphThrottlePedalHaptics,
					LayerIndex.TimerJitter => settings.GraphTimerJitter,
					_ => false
				};

				if ( showLayer )
				{
					var layer = _layerArray[ (int) layerIndex ];

					UpdateSolidFill( layer.value, layer.r, layer.g, layer.b );
				}
			}

			FinishUpdates();
		}
	}

	public void Tick( App app )
	{
		if ( _isShown )
		{
			WritePixels();

			_updateCounter--;

			if ( _updateCounter <= 0 )
			{
				_updateCounter = UpdateInterval;
			}
		}
	}

	private class Layer
	{
		public float value;

		public float r;
		public float g;
		public float b;
	}
}
