
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Controls;

namespace MarvinsAIRARefactored.Components;

public class Graph
{
	public enum LayerIndex
	{
		InputTorque60Hz,
		InputTorque500Hz,
		InputLFE500Hz,
		OutputTorque500Hz,
		TimerJitter500Hz,
		Count
	}

	private int _bitmapWidth;
	private int _bitmapStride;
	private int _bitmapHeight;
	private int _bitmapHeightMinusOne;

	private WriteableBitmap? _writeableBitmap = null;

	private int _x = 0;

	private uint[,]? _colorArray = null;
	private float[,]? _colorMixArray = null;

	private readonly Layer[] _layerArray = new Layer[ (int) LayerIndex.Count ];
	private readonly Statistics[] _statisticsArray = new Statistics[ (int) LayerIndex.Count ];

	public void Initialize()
	{
		var app = App.Instance;

		if ( app != null )
		{
			app.Logger.WriteLine( "[Graph] Initialize >>>" );

			var image = app.MainWindow.Graph_Image;

			_bitmapWidth = (int) image.Width;
			_bitmapStride = _bitmapWidth * 4;
			_bitmapHeight = (int) image.Height;
			_bitmapHeightMinusOne = _bitmapHeight - 1;

			_writeableBitmap = new( _bitmapWidth, _bitmapHeight, 96f, 96f, PixelFormats.Bgra32, null );

			_colorArray = new uint[ _bitmapHeight, _bitmapWidth ];
			_colorMixArray = new float[ _bitmapHeight, 4 ];

			image.Source = _writeableBitmap;

			for ( var layerIndex = 0; layerIndex < (int) LayerIndex.Count; layerIndex++ )
			{
				_layerArray[ layerIndex ] = new Layer();
				_statisticsArray[ layerIndex ] = new Statistics( 500 );
			}

			app.Logger.WriteLine( "[Graph] Initialize >>>" );
		}
	}

	public static void SetMairaComboBoxItemsSource( MairaComboBox mairaComboBox )
	{
		var app = App.Instance;

		if ( app != null )
		{
			app.Logger.WriteLine( "[Graph] SetMairaComboBoxItemsSource >>>" );

			var selectedItem = mairaComboBox.SelectedValue as KeyValuePair<LayerIndex, string>?;

			var dictionary = new Dictionary<LayerIndex, string>
			{
				{ LayerIndex.InputTorque60Hz, DataContext.DataContext.Instance.Localization[ "TorqueInput60Hz" ] },
				{ LayerIndex.InputTorque500Hz, DataContext.DataContext.Instance.Localization[ "TorqueInput500Hz" ] },
				{ LayerIndex.InputLFE500Hz, DataContext.DataContext.Instance.Localization[ "LFEInput500Hz" ] },
				{ LayerIndex.OutputTorque500Hz, DataContext.DataContext.Instance.Localization[ "TorqueOutput500Hz" ] },
				{ LayerIndex.TimerJitter500Hz, DataContext.DataContext.Instance.Localization[ "TimerJitter500Hz" ] }
			};

			mairaComboBox.ItemsSource = dictionary;

			if ( selectedItem.HasValue )
			{
				mairaComboBox.SelectedValue = dictionary.FirstOrDefault( keyValuePair => keyValuePair.Key.Equals( selectedItem.Value.Key ) );
			}

			app.Logger.WriteLine( "[Graph] <<< SetMairaComboBoxItemsSource" );
		}
	}

	public void SetLayerColors( LayerIndex layerIndex, float minR, float minG, float minB, float maxR, float maxG, float maxB )
	{
		var layer = _layerArray[ (int) layerIndex ];

		layer.minR = minR;
		layer.minG = minG;
		layer.minB = minB;

		layer.maxR = maxR;
		layer.maxG = maxG;
		layer.maxB = maxB;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void UpdateLayer( LayerIndex layerIndex, float rawValue, float normalizedValue )
	{
		var app = App.Instance;

		if ( ( app != null ) && app.MainWindow.GraphTabItemIsVisible )
		{
			_statisticsArray[ (int) layerIndex ].Update( rawValue );

			_layerArray[ (int) layerIndex ].value = normalizedValue;
		}
	}

	public void Update()
	{
		var app = App.Instance;

		if ( ( app != null ) && app.MainWindow.GraphTabItemIsVisible && ( _colorArray != null ) && ( _colorMixArray != null ) )
		{
			var settings = DataContext.DataContext.Instance.Settings;

			Array.Clear( _colorMixArray );

			for ( var layerIndex = LayerIndex.InputTorque60Hz; layerIndex < LayerIndex.Count; layerIndex++ )
			{
				var showLayer = layerIndex switch
				{
					LayerIndex.InputTorque60Hz => settings.GraphTorqueInput60Hz,
					LayerIndex.InputTorque500Hz => settings.GraphTorqueInput500Hz,
					LayerIndex.InputLFE500Hz => settings.GraphLFEInput500Hz,
					LayerIndex.OutputTorque500Hz => settings.GraphTorqueOutput500Hz,
					LayerIndex.TimerJitter500Hz => settings.GraphTimerJitter500Hz,
					_ => false
				};

				if ( showLayer )
				{
					var layer = _layerArray[ (int) layerIndex ];

					var y = Math.Clamp( layer.value, -1f, 1f );

					var absY = Math.Abs( y );

					var r = Misc.Lerp( layer.minR, layer.maxR, absY );
					var g = Misc.Lerp( layer.minG, layer.maxG, absY );
					var b = Misc.Lerp( layer.minB, layer.maxB, absY );

					y = y * -0.5f + 0.5f;

					var iy = _bitmapHeightMinusOne / 2;
					var delta = (int) Math.Round( y * _bitmapHeight ) - iy;
					var sign = Math.Sign( delta );
					var range = Math.Abs( delta );

					for ( var i = 1; i <= range; i++ )
					{
						var multiplier = MathF.Pow( (float) i / range, 4f );

						_colorMixArray[ iy, 0 ] = 1f;
						_colorMixArray[ iy, 1 ] += r * multiplier;
						_colorMixArray[ iy, 2 ] += g * multiplier;
						_colorMixArray[ iy, 3 ] += b * multiplier;

						iy += sign;
					}
				}
			}

			for ( var y = 0; y < _bitmapHeight; y++ )
			{
				var a = (uint) ( MathF.Min( 1f, _colorMixArray[ y, 0 ] ) * 255f );
				var r = (uint) ( MathF.Min( 1f, _colorMixArray[ y, 1 ] ) * 255f );
				var g = (uint) ( MathF.Min( 1f, _colorMixArray[ y, 2 ] ) * 255f );
				var b = (uint) ( MathF.Min( 1f, _colorMixArray[ y, 3 ] ) * 255f );

				_colorArray[ y, _x ] = ( a << 24 ) | ( r << 16 ) | ( g << 8 ) | b;
			}

			_colorArray[ _bitmapHeightMinusOne / 2, _x ] = 0xFFFFFFFF;

			_x = ( _x + 1 ) % _bitmapWidth;
		}
	}

	public void Tick( App app )
	{
		if ( _writeableBitmap != null )
		{
			var x = _x;

			var leftX = x;
			var leftWidth = _bitmapWidth - leftX;

			var rightX = 0;
			var rightWidth = x - rightX;

			if ( leftWidth > 0 )
			{
				var int32Rect = new Int32Rect( leftX, 0, leftWidth, _bitmapHeight );

				_writeableBitmap.WritePixels( int32Rect, _colorArray, _bitmapStride, 0, 0 );
			}

			if ( rightWidth > 0 )
			{
				var int32Rect = new Int32Rect( rightX, 0, rightWidth, _bitmapHeight );

				_writeableBitmap.WritePixels( int32Rect, _colorArray, _bitmapStride, leftWidth, 0 );
			}
		}

		var statistics = _statisticsArray[ (int) DataContext.DataContext.Instance.Settings.GraphStatisticsLayerIndex ];

		app.MainWindow.Graph_Minimum_Label.Content = $"{statistics.MinimumValue:F2}";
		app.MainWindow.Graph_Maximum_Label.Content = $"{statistics.MaximumValue:F2}";
		app.MainWindow.Graph_Average_Label.Content = $"{statistics.AverageValue:F2}";
		app.MainWindow.Graph_Variance_Label.Content = $"{statistics.Variance:F2}";
		app.MainWindow.Graph_StandardDeviation_Label.Content = $"{statistics.StandardDeviation:F2}";
	}

	private class Layer
	{
		public float value;

		public float minR;
		public float minG;
		public float minB;

		public float maxR;
		public float maxG;
		public float maxB;
	}
}
