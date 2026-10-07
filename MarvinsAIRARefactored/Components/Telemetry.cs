
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using MarvinsAIRARefactored.FFB;

namespace MarvinsAIRARefactored.Components;

public class Telemetry
{
	private const int Version = 9;

	private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";
	private const int MaxStringLengthInBytes = 256;
	private const int MaxGraphNodes = 20;
	private const int MaxGraphNodeParameters = 6;

	private const string GraphNodeParameterTrueValue = "true";
	private const string GraphNodeParameterFalseValue = "false";

	[StructLayout( LayoutKind.Sequential, Pack = 4 )]
	public unsafe struct DataBufferStruct
	{
		// header telemetry

		public int tickCount;

		// output telemetry

		public float racingWheelAutoTorque;
		public float racingWheelOutputTorque;
		public bool racingWheelOutputTorqueIsClipping;
		public bool racingWheelCrashProtectionIsActive;
		public bool racingWheelCurbProtectionIsActive;
		public bool racingWheelFadingIsActive;

		public float steeringEffectsUndersteerEffect;
		public float steeringEffectsOversteerEffect;
		public float steeringEffectsSeatOfPantsEffect;
		public float steeringEffectsSkidSlip;

		public float pedalsClutchFrequency;
		public float pedalsClutchAmplitude;

		public float pedalsBrakeFrequency;
		public float pedalsBrakeAmplitude;

		public float pedalsThrottleFrequency;
		public float pedalsThrottleAmplitude;

		// racing wheel settings telemetry

		public float racingWheelStrength;
		public float racingWheelMaxForce;

		// steering effects settings telemetry

		public fixed byte steeringEffectsCalibrationFileName[ MaxStringLengthInBytes ];

		public float steeringEffectsUndersteerMinThreshold;
		public float steeringEffectsUndersteerMaxThreshold;
		public float steeringEffectsUndersteerPedalVibrationMinFrequency;
		public float steeringEffectsUndersteerPedalVibrationMaxFrequency;
		public float steeringEffectsUndersteerPedalVibrationCurve;

		public float steeringEffectsOversteerMinThreshold;
		public float steeringEffectsOversteerMaxThreshold;
		public float steeringEffectsOversteerPedalVibrationMinFrequency;
		public float steeringEffectsOversteerPedalVibrationMaxFrequency;
		public float steeringEffectsOversteerPedalVibrationCurve;

		public float steeringEffectsSeatOfPantsMinThreshold;
		public float steeringEffectsSeatOfPantsMaxThreshold;
		public fixed byte steeringEffectsSeatOfPantsAlgorithm[ MaxStringLengthInBytes ];
		public float steeringEffectsSeatOfPantsPedalVibrationMinFrequency;
		public float steeringEffectsSeatOfPantsPedalVibrationMaxFrequency;
		public float steeringEffectsSeatOfPantsPedalVibrationCurve;

		public bool soundsWheelLockIsPlaying;
		public float soundsWheelLockVolume;
		public bool soundsWheelSpinIsPlaying;
		public float soundsWheelSpinVolume;

		// ffb graph settings telemetry (pinned settings of the active graph, grouped by node)
		// node n is at index n; parameter p of node n is at index ( n * MaxGraphNodeParameters + p )

		public fixed byte graphName[ MaxStringLengthInBytes ];

		public int graphNodeCount;
		public fixed int graphNodeParameterCounts[ MaxGraphNodes ];

		public fixed byte graphNodeNames[ MaxGraphNodes * MaxStringLengthInBytes ];
		public fixed byte graphNodeTypes[ MaxGraphNodes * MaxStringLengthInBytes ];
		public fixed byte graphNodeParameterNames[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];
		public fixed byte graphNodeParameterValues[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];
		public fixed byte graphNodeParameterTexts[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];

		// graph node count setters

		public void SetGraphNodeParameterCount( int nodeIndex, int value )
		{
			if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return;

			graphNodeParameterCounts[ nodeIndex ] = value;
		}

		// string setters

		public void SetSteeringEffectsCalibrationFileName( string? value )
		{
			fixed ( byte* bytePtr = steeringEffectsCalibrationFileName )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetSteeringEffectsSeatOfPantsAlgorithm( string? value )
		{
			fixed ( byte* bytePtr = steeringEffectsSeatOfPantsAlgorithm )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphName( string? value )
		{
			fixed ( byte* bytePtr = graphName )
			{
				WriteString( bytePtr, 0, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphNodeName( int nodeIndex, string? value )
		{
			if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return;

			fixed ( byte* bytePtr = graphNodeNames )
			{
				WriteString( bytePtr, nodeIndex, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphNodeType( int nodeIndex, string? value )
		{
			if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return;

			fixed ( byte* bytePtr = graphNodeTypes )
			{
				WriteString( bytePtr, nodeIndex, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphNodeParameterName( int nodeIndex, int parameterIndex, string? value )
		{
			if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return;

			fixed ( byte* bytePtr = graphNodeParameterNames )
			{
				WriteString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphNodeParameterValue( int nodeIndex, int parameterIndex, string? value )
		{
			if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return;

			fixed ( byte* bytePtr = graphNodeParameterValues )
			{
				WriteString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes, value );
			}
		}

		public void SetGraphNodeParameterText( int nodeIndex, int parameterIndex, string? value )
		{
			if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return;

			fixed ( byte* bytePtr = graphNodeParameterTexts )
			{
				WriteString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes, value );
			}
		}

		private static bool IsValidGraphNodeParameter( int nodeIndex, int parameterIndex )
		{
			return ( nodeIndex >= 0 ) && ( nodeIndex < MaxGraphNodes ) && ( parameterIndex >= 0 ) && ( parameterIndex < MaxGraphNodeParameters );
		}

		public static unsafe void WriteString( byte* bytePtr, int index, int capacity, string? value )
		{
			if ( bytePtr == null || capacity <= 0 ) return;

			var offset = index * capacity;

			if ( string.IsNullOrEmpty( value ) )
			{
				bytePtr[ offset ] = 0;
				return;
			}

			var bytes = Encoding.UTF8.GetBytes( value );

			var length = Math.Min( bytes.Length, capacity - 1 );

			Marshal.Copy( bytes, 0, (IntPtr) bytePtr + offset, length );

			bytePtr[ offset + length ] = 0;
		}
	}

	[StructLayout( LayoutKind.Sequential, Pack = 4 )]
	public unsafe struct DataStruct
	{
		public int version;
		public int bufferIndex;

		public DataBufferStruct buffer0;
		public DataBufferStruct buffer1;
		public DataBufferStruct buffer2;

		public static ref DataBufferStruct GetDataBuffer( ref DataStruct dataStruct, int index )
		{
			switch ( index )
			{
				case 0: return ref dataStruct.buffer0;
				case 1: return ref dataStruct.buffer1;
				default: return ref dataStruct.buffer2;
			}
		}
	}

	private DataStruct _data = new();
	private int _currentBufferIndex = 0;
	private int _settingsUpdatesRemaining = 0;

	private MemoryMappedFile? _memoryMappedFile = null;
	private MemoryMappedViewAccessor? _memoryMappedFileViewAccessor = null;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Telemetry] Initialize >>>" );

		// Unsafe.SizeOf (the managed layout) is what MemoryMappedViewAccessor.Write actually copies - Marshal.SizeOf
		// can't size this struct (the marshaler rejects the nested large buffers) and would over-count bools as 4 bytes
		var sizeOfTelemetryData = Unsafe.SizeOf<DataStruct>();

		_memoryMappedFile = MemoryMappedFile.CreateOrOpen( MemoryMappedFileName, sizeOfTelemetryData );
		_memoryMappedFileViewAccessor = _memoryMappedFile.CreateViewAccessor();

		// CreateOrOpen returns the EXISTING mapping when another process (e.g. SimHub) still holds one open from an
		// older, smaller layout - it can't be resized, so writing our struct would run past its end. Disable the
		// telemetry output for this session instead of throwing every tick; restarting that process fixes it.
		if ( _memoryMappedFileViewAccessor.Capacity < sizeOfTelemetryData )
		{
			app.Logger.WriteLine( $"[Telemetry] Existing memory mapped file is too small ({_memoryMappedFileViewAccessor.Capacity} < {sizeOfTelemetryData} bytes) - another process has an older layout open; telemetry output is disabled until it is restarted" );

			_memoryMappedFileViewAccessor.Dispose();
			_memoryMappedFile.Dispose();

			_memoryMappedFileViewAccessor = null;
			_memoryMappedFile = null;
		}

		app.Logger.WriteLine( "[Telemetry] <<< Initialize" );
	}

	public void Shutdown()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Telemetry] Shutdown >>>" );

		_memoryMappedFileViewAccessor = null;
		_memoryMappedFile = null;

		app.Logger.WriteLine( "[Telemetry] <<< Shutdown" );
	}

	public void UpdateSettings()
	{
		_settingsUpdatesRemaining = 3;
	}

	public void Tick( App app )
	{
		var localization = DataContext.DataContext.Instance.Localization;
		var settings = DataContext.DataContext.Instance.Settings;

		// get the buffer to write to

		_currentBufferIndex = ( _currentBufferIndex + 1 ) % 3;

		ref var dataBuffer = ref DataStruct.GetDataBuffer( ref _data, _currentBufferIndex );

		// header telemetry

		dataBuffer.tickCount++;

		// output telemetry

		dataBuffer.racingWheelAutoTorque = app.RacingWheel.AutoTorque;
		dataBuffer.racingWheelOutputTorque = app.RacingWheel.OutputTorque;
		dataBuffer.racingWheelOutputTorqueIsClipping = ( app.RacingWheel.OutputTorque < -1f ) || ( app.RacingWheel.OutputTorque > 1f );
		dataBuffer.racingWheelCrashProtectionIsActive = app.RacingWheel.CrashProtectionIsActive;
		dataBuffer.racingWheelCurbProtectionIsActive = app.RacingWheel.CurbProtectionIsActive;
		dataBuffer.racingWheelFadingIsActive = app.RacingWheel.FadingIsActive;

		dataBuffer.steeringEffectsUndersteerEffect = app.SteeringEffects.UndersteerEffect;
		dataBuffer.steeringEffectsOversteerEffect = app.SteeringEffects.OversteerEffect;
		dataBuffer.steeringEffectsSeatOfPantsEffect = app.SteeringEffects.SeatOfPantsEffect;
		dataBuffer.steeringEffectsSkidSlip = app.SteeringEffects.SkidSlip;

		dataBuffer.pedalsClutchFrequency = app.Pedals.ClutchFrequency;
		dataBuffer.pedalsClutchAmplitude = app.Pedals.ClutchAmplitude;

		dataBuffer.pedalsBrakeFrequency = app.Pedals.BrakeFrequency;
		dataBuffer.pedalsBrakeAmplitude = app.Pedals.BrakeAmplitude;

		dataBuffer.pedalsThrottleFrequency = app.Pedals.ThrottleFrequency;
		dataBuffer.pedalsThrottleAmplitude = app.Pedals.ThrottleAmplitude;

		//Wheel lock and wheel spin sounds status

		dataBuffer.soundsWheelLockIsPlaying = app.Sounds.SoundEffects[Sounds.SoundEffectType.WheelLock].IsPlaying;
		dataBuffer.soundsWheelLockVolume = app.Sounds.SoundEffects[Sounds.SoundEffectType.WheelLock].Volume;
		dataBuffer.soundsWheelSpinIsPlaying = app.Sounds.SoundEffects[Sounds.SoundEffectType.WheelSpin].IsPlaying;
		dataBuffer.soundsWheelSpinVolume = app.Sounds.SoundEffects[Sounds.SoundEffectType.WheelSpin].Volume;

		if ( _settingsUpdatesRemaining > 0 )
		{
			// update settings 3 times because we have 3 buffers to fill then we no longer need to update settings

			_settingsUpdatesRemaining--;

			// racing wheel settings telemetry

			dataBuffer.racingWheelStrength = settings.RacingWheelStrength;
			dataBuffer.racingWheelMaxForce = settings.RacingWheelMaxForce;

			// steering effects settings telemetry

			dataBuffer.SetSteeringEffectsCalibrationFileName( app.SteeringEffects.CalibrationFileName );

			dataBuffer.steeringEffectsUndersteerMinThreshold = settings.SteeringEffectsUndersteerMinimumThreshold;
			dataBuffer.steeringEffectsUndersteerMaxThreshold = settings.SteeringEffectsUndersteerMaximumThreshold;
			dataBuffer.steeringEffectsUndersteerPedalVibrationMinFrequency = settings.SteeringEffectsUndersteerPedalVibrationMinimumFrequency;
			dataBuffer.steeringEffectsUndersteerPedalVibrationMaxFrequency = settings.SteeringEffectsUndersteerPedalVibrationMaximumFrequency;
			dataBuffer.steeringEffectsUndersteerPedalVibrationCurve = settings.SteeringEffectsUndersteerPedalVibrationCurve;

			dataBuffer.steeringEffectsOversteerMinThreshold = settings.SteeringEffectsOversteerMinimumThreshold;
			dataBuffer.steeringEffectsOversteerMaxThreshold = settings.SteeringEffectsOversteerMaximumThreshold;
			dataBuffer.steeringEffectsOversteerPedalVibrationMinFrequency = settings.SteeringEffectsOversteerPedalVibrationMinimumFrequency;
			dataBuffer.steeringEffectsOversteerPedalVibrationMaxFrequency = settings.SteeringEffectsOversteerPedalVibrationMaximumFrequency;
			dataBuffer.steeringEffectsOversteerPedalVibrationCurve = settings.SteeringEffectsOversteerPedalVibrationCurve;

			dataBuffer.steeringEffectsSeatOfPantsMinThreshold = settings.SteeringEffectsSeatOfPantsMinimumThreshold;
			dataBuffer.steeringEffectsSeatOfPantsMaxThreshold = settings.SteeringEffectsSeatOfPantsMaximumThreshold;

			switch ( settings.SteeringEffectsSeatOfPantsAlgorithm )
			{
				case SteeringEffects.SeatOfPantsAlgorithm.YAcceleration:
					dataBuffer.SetSteeringEffectsSeatOfPantsAlgorithm( localization[ "LateralAcceleration" ] );
					break;

				case SteeringEffects.SeatOfPantsAlgorithm.YVelocity:
					dataBuffer.SetSteeringEffectsSeatOfPantsAlgorithm( localization[ "LateralVelocity" ] );
					break;

				case SteeringEffects.SeatOfPantsAlgorithm.YVelocityOverXVelocity:
					dataBuffer.SetSteeringEffectsSeatOfPantsAlgorithm( localization[ "RatioOfVelocities" ] );
					break;
			}

			dataBuffer.steeringEffectsSeatOfPantsPedalVibrationMinFrequency = settings.SteeringEffectsSeatOfPantsPedalVibrationMinimumFrequency;
			dataBuffer.steeringEffectsSeatOfPantsPedalVibrationMaxFrequency = settings.SteeringEffectsSeatOfPantsPedalVibrationMaximumFrequency;
			dataBuffer.steeringEffectsSeatOfPantsPedalVibrationCurve = settings.SteeringEffectsSeatOfPantsPedalVibrationCurve;

			// ffb graph settings telemetry

			UpdateGraphNodeTelemetry( ref dataBuffer );
		}

		// let SimHub know this buffer is ready for reading

		_data.version = Version;
		_data.bufferIndex = _currentBufferIndex;

		_memoryMappedFileViewAccessor?.Write( 0, ref _data );
	}

	private static void UpdateGraphNodeTelemetry( ref DataBufferStruct dataBuffer )
	{
		var graphViewModel = DataContext.DataContext.Instance.RacingWheelGraphViewModel;

		// the active graph's user-facing name (built-ins localized), as shown in the graph selector

		dataBuffer.SetGraphName( FFBGraphViewModel.GraphDisplayName( graphViewModel.GraphName, graphViewModel.IsFFBGraphBuiltIn ) );

		// blank every slot first so nodes / parameters that no longer exist (or were unpinned) don't linger

		dataBuffer.graphNodeCount = 0;

		for ( var nodeIndex = 0; nodeIndex < MaxGraphNodes; nodeIndex++ )
		{
			dataBuffer.SetGraphNodeParameterCount( nodeIndex, 0 );
			dataBuffer.SetGraphNodeName( nodeIndex, null );
			dataBuffer.SetGraphNodeType( nodeIndex, null );

			for ( var parameterIndex = 0; parameterIndex < MaxGraphNodeParameters; parameterIndex++ )
			{
				dataBuffer.SetGraphNodeParameterName( nodeIndex, parameterIndex, null );
				dataBuffer.SetGraphNodeParameterValue( nodeIndex, parameterIndex, null );
				dataBuffer.SetGraphNodeParameterText( nodeIndex, parameterIndex, null );
			}
		}

		// PinnedGroups is exactly what the FFB graph settings section shows (whether or not it is currently visible):
		// only nodes with pins, in display order, each with its pinned entries in display order (a pinned Enabled toggle first)

		var pinnedGroups = graphViewModel.PinnedGroups;

		var nodeCount = Math.Min( pinnedGroups.Count, MaxGraphNodes );

		dataBuffer.graphNodeCount = nodeCount;

		for ( var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++ )
		{
			var pinnedGroup = pinnedGroups[ nodeIndex ];

			dataBuffer.SetGraphNodeName( nodeIndex, pinnedGroup.Module.NodeName );
			dataBuffer.SetGraphNodeType( nodeIndex, pinnedGroup.Module.ModuleTypeName );

			var parameterCount = Math.Min( pinnedGroup.Settings.Count, MaxGraphNodeParameters );

			dataBuffer.SetGraphNodeParameterCount( nodeIndex, parameterCount );

			for ( var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++ )
			{
				var (parameterName, parameterValue, parameterText) = DescribePinnedEntry( pinnedGroup.Settings[ parameterIndex ] );

				dataBuffer.SetGraphNodeParameterName( nodeIndex, parameterIndex, parameterName );
				dataBuffer.SetGraphNodeParameterValue( nodeIndex, parameterIndex, parameterValue );
				dataBuffer.SetGraphNodeParameterText( nodeIndex, parameterIndex, parameterText );
			}
		}
	}

	private static (string Name, string Value, string Text) DescribePinnedEntry( object pinnedEntry )
	{
		switch ( pinnedEntry )
		{
			case FFBPinnedEnabledViewModel enabledEntry:

				return (enabledEntry.Label, FormatBooleanValue( enabledEntry.Module.Enabled ), FormatBooleanText( enabledEntry.Module.Enabled ));

			case FFBModuleSettingViewModel setting:

				switch ( setting.SettingType )
				{
					case FFBSettingType.Switch:

						return (setting.Label, FormatBooleanValue( setting.IsOn ), FormatBooleanText( setting.IsOn ));

					case FFBSettingType.Choice:

						var choiceText = setting.ChoiceOptions.FirstOrDefault( option => option.Key == setting.ChoiceIndex ).Value ?? setting.ValueString;

						return (setting.Label, setting.ChoiceIndex.ToString( CultureInfo.InvariantCulture ), choiceText);

					default:

						return (setting.Label, setting.Value.ToString( CultureInfo.InvariantCulture ), setting.ValueString);
				}

			default:

				return (string.Empty, string.Empty, string.Empty);
		}
	}

	private static string FormatBooleanValue( bool value ) => value ? GraphNodeParameterTrueValue : GraphNodeParameterFalseValue;

	private static string FormatBooleanText( bool value ) => value ? FFBDisplayNames.Localize( "ON", "ON" ) : FFBDisplayNames.Localize( "OFF", "OFF" );
}
