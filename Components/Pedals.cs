﻿
using Simagic;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Controls;

namespace MarvinsAIRARefactored.Components;

public class Pedals
{
	public enum Effect
	{
		None,
		GearChange,
		ABSEngaged,
		RPM,
		SteeringEffects,
		WheelLock,
		WheelSpin,
		ClutchSlip
	};

	private const float DeltaSeconds = 1f / 20f;
	private const float TestDuration = 3f;
	private const int UpdateInterval = 3;

	public HPR.PedalsDevice PedalsDevice { get; private set; }

	private int _pedalUpdateCounter = UpdateInterval;

	private bool _testing = false;
	private bool _testJustStarted = false;
	private int _testPedalIndex = 0;
	private int _testEffectIndex = 0;
	private float _testTimer = 0f;

	private readonly HPR _hpr = new();

	private int _gearLastFrame = 0;

	private float _gearChangeFrequency = 0f;
	private float _gearChangeAmplitude = 0f;
	private float _gearChangeTimer = 0f;

	private readonly float[] _frequency = [ 0f, 0f, 0f ];
	private readonly float[] _amplitude = [ 0f, 0f, 0f ];
	private readonly float[] _cycles = [ 0f, 0f, 0f ];

	public void Initialize()
	{
		var app = App.Instance!;

		app.Graph.SetLayerColors( Graph.LayerIndex.ClutchPedalHaptics, 0f, 0f, 0.5f, 0f, 0f, 1f );
		app.Graph.SetLayerColors( Graph.LayerIndex.BrakePedalHaptics, 0.5f, 0f, 0f, 1f, 0f, 0f );
		app.Graph.SetLayerColors( Graph.LayerIndex.ThrottlePedalHaptics, 0f, 0.5f, 0f, 0f, 1f, 0f );
	}

	public void Refresh()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Pedals] Refresh >>>" );

		PedalsDevice = _hpr.Initialize( DataContext.DataContext.Instance.Settings.PedalsEnabled );

		app.Logger.WriteLine( $"[Pedals] Simagic HPR API reports: {PedalsDevice}" );

		app.MainWindow.UpdatePedalsDevice();

		app.Logger.WriteLine( "[Pedals] <<< Refresh" );
	}

	public static void SetMairaComboBoxItemsSource( MairaComboBox mairaComboBox )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[Pedals] SetMairaComboBoxItemsSource >>>" );

		var selectedEffect = mairaComboBox.SelectedValue as Effect?;

		var dictionary = new Dictionary<Effect, string>
			{
				{ Effect.None, DataContext.DataContext.Instance.Localization[ "None" ] },
				{ Effect.GearChange, DataContext.DataContext.Instance.Localization[ "GearChange" ] },
				{ Effect.ABSEngaged, DataContext.DataContext.Instance.Localization[ "ABSEngaged" ] },
				{ Effect.RPM, DataContext.DataContext.Instance.Localization[ "RPM" ] },
				{ Effect.SteeringEffects, DataContext.DataContext.Instance.Localization[ "SteeringEffects" ] },
				{ Effect.WheelLock, DataContext.DataContext.Instance.Localization[ "WheelLock" ] },
				{ Effect.WheelSpin, DataContext.DataContext.Instance.Localization[ "WheelSpin" ] },
				{ Effect.ClutchSlip, DataContext.DataContext.Instance.Localization[ "ClutchSlip" ] },
			};

		mairaComboBox.ItemsSource = dictionary;

		if ( selectedEffect != null )
		{
			mairaComboBox.SelectedValue = selectedEffect;
		}
		else
		{
			mairaComboBox.SelectedValue = Effect.None;
		}

		app.Logger.WriteLine( "[Pedals] <<< SetMairaComboBoxItemsSource" );
	}

	public void StartTest( int pedalIndex, int effectIndex )
	{
		_testPedalIndex = pedalIndex;
		_testEffectIndex = effectIndex;
		_testTimer = 0f;
		_testJustStarted = true;
		_testing = true;
	}

	public void UpdateGraph()
	{
		var app = App.Instance!;

		for ( var i = 0; i < 3; i++ )
		{
			_cycles[ i ] += _frequency[ i ] * MathF.Tau / 500f;

			var amplitude = MathF.Sin( _cycles[ i ] ) * _amplitude[ i ];

			app.Graph.UpdateLayer( Graph.LayerIndex.ClutchPedalHaptics + i, amplitude, amplitude );
		}
	}

	private void Update( App app )
	{
		// update gear change effect timer

		if ( _gearChangeTimer > 0f )
		{
			_gearChangeTimer -= DeltaSeconds;
		}

		// update test timer

		if ( _testing )
		{
			_testTimer += DeltaSeconds;

			if ( _testTimer >= TestDuration )
			{
				_testing = false;
			}
		}

		// if we aren't on track then just shut off all HPRs (unless we are testing)

		if ( !_testing )
		{
			if ( !app.Simulator.IsOnTrack || ( app.Simulator.SimMode != "full" ) )
			{
				_hpr.VibratePedal( HPR.Channel.Clutch, HPR.State.Off, 0, 0 );
				_hpr.VibratePedal( HPR.Channel.Brake, HPR.State.Off, 0, 0 );
				_hpr.VibratePedal( HPR.Channel.Throttle, HPR.State.Off, 0, 0 );

				_frequency[ 0 ] = 0f;
				_frequency[ 1 ] = 0f;
				_frequency[ 2 ] = 0f;

				_amplitude[ 0 ] = 0f;
				_amplitude[ 1 ] = 0f;
				_amplitude[ 2 ] = 0f;

				_cycles[ 0 ] = 0f;
				_cycles[ 1 ] = 0f;
				_cycles[ 2 ] = 0f;

				return;
			}
		}

		// shortcut to settings

		var settings = DataContext.DataContext.Instance.Settings;

		// update gear change effect

		if ( !app.Simulator.WasOnTrack )
		{
			_gearLastFrame = app.Simulator.Gear;
		}

		if ( _testJustStarted || ( app.Simulator.Gear != _gearLastFrame ) )
		{
			if ( _testJustStarted || ( app.Simulator.Gear != 0 ) )
			{
				_gearChangeFrequency = Misc.Lerp( settings.PedalsMinimumFrequency, settings.PedalsMaximumFrequency, settings.PedalsShiftIntoGearFrequency );
				_gearChangeAmplitude = settings.PedalsShiftIntoGearAmplitude;
				_gearChangeTimer = settings.PedalsShiftIntoGearDuration;
			}
			else
			{
				_gearChangeFrequency = Misc.Lerp( settings.PedalsMinimumFrequency, settings.PedalsMaximumFrequency, settings.PedalsShiftIntoNeutralFrequency );
				_gearChangeAmplitude = settings.PedalsShiftIntoNeutralAmplitude;
				_gearChangeTimer = settings.PedalsShiftIntoNeutralDuration;
			}
		}

		_gearLastFrame = app.Simulator.Gear;

		// generate and apply effects

		(Effect, float)[,] effectConfiguration =
		{
			{
				( settings.PedalsClutchEffect1, settings.PedalsClutchStrength1 ),
				( settings.PedalsClutchEffect2, settings.PedalsClutchStrength2 ),
				( settings.PedalsClutchEffect3, settings.PedalsClutchStrength3 )
			},
			{
				( settings.PedalsBrakeEffect1, settings.PedalsBrakeStrength1 ),
				( settings.PedalsBrakeEffect2, settings.PedalsBrakeStrength2 ),
				( settings.PedalsBrakeEffect3, settings.PedalsBrakeStrength3 )
			},
			{
				( settings.PedalsThrottleEffect1, settings.PedalsThrottleStrength1 ),
				( settings.PedalsThrottleEffect2, settings.PedalsThrottleStrength2 ),
				( settings.PedalsThrottleEffect3, settings.PedalsThrottleStrength3 )
			}
		};

		for ( var pedalIndex = 0; pedalIndex < 3; pedalIndex++ )
		{
			if ( _testing && ( _testPedalIndex != pedalIndex ) )
			{
				continue;
			}

			var effectActive = false;
			var frequency = 0f;
			var amplitude = 0f;

			for ( var effectIndex = 0; effectIndex < 3; effectIndex++ )
			{
				if ( _testing && ( _testEffectIndex != effectIndex ) )
				{
					continue;
				}

				(effectActive, frequency, amplitude) = DoEffect( app, effectConfiguration[ pedalIndex, effectIndex ].Item1, effectConfiguration[ pedalIndex, effectIndex ].Item2 );

				if ( effectActive )
				{
					break;
				}
			}

			if ( effectActive )
			{
				amplitude *= MathF.Pow( frequency / 50f, Misc.CurveToPower( settings.PedalsNoiseDamper ) );

				_hpr.VibratePedal( (HPR.Channel) pedalIndex, HPR.State.On, frequency, amplitude * 100f );

				_frequency[ pedalIndex ] = (int) ( Math.Clamp( frequency, 1f, 50f ) );
				_amplitude[ pedalIndex ] = amplitude;
			}
			else
			{
				_hpr.VibratePedal( (HPR.Channel) pedalIndex, HPR.State.Off, 0f, 0f );

				app.Graph.UpdateLayer( Graph.LayerIndex.ClutchPedalHaptics + pedalIndex, 0f, 0f );

				_frequency[ pedalIndex ] = 0f;
				_amplitude[ pedalIndex ] = 0f;
				_cycles[ pedalIndex ] = 0f;
			}
		}

		// update test just started

		_testJustStarted = false;
	}

	private (bool, float, float) DoEffect( App app, Effect effect, float amplitude )
	{
		return effect switch
		{
			Effect.GearChange => DoGearChangeEffect( app, amplitude ),
			Effect.ABSEngaged => DoABSEngagedEffect( app, amplitude ),
			Effect.RPM => DoRPMEffect( app, amplitude ),
			Effect.ClutchSlip => DoClutchSlipEffect( app, amplitude ),
			_ => (false, 0f, 0f),
		};
	}

	private (bool, float, float) DoGearChangeEffect( App app, float amplitude )
	{
		if ( _gearChangeTimer > 0f )
		{
			return (true, _gearChangeFrequency, _gearChangeAmplitude * amplitude);
		}

		return (false, 0f, 0f);
	}

	private (bool, float, float) DoABSEngagedEffect( App app, float amplitude )
	{
		if ( _testing || app.Simulator.BrakeABSactive )
		{
			var settings = DataContext.DataContext.Instance.Settings;

			var frequency = Misc.Lerp( settings.PedalsMinimumFrequency, settings.PedalsMaximumFrequency, MathF.Pow( settings.PedalsABSEngagedFrequency, Misc.CurveToPower( settings.PedalsFrequencyCurve ) ) );

			amplitude *= settings.PedalsABSEngagedAmplitude;

			if ( !_testing && settings.PedalsABSEngagedFadeWithBrakeEnabled )
			{
				amplitude *= app.Simulator.Brake;
			}

			amplitude = Math.Clamp( amplitude, 0f, 1f );

			amplitude = Misc.Lerp( settings.PedalsMinimumAmplitude, settings.PedalsMaximumAmplitude, MathF.Pow( amplitude, Misc.CurveToPower( settings.PedalsAmplitudeCurve ) ) );

			return (true, frequency, amplitude);
		}

		return (false, 0f, 0f);
	}

	private (bool, float, float) DoRPMEffect( App app, float amplitude )
	{
		var settings = DataContext.DataContext.Instance.Settings;

		if ( _testing || settings.PedalsRPMVibrateInTopGearEnabled || ( app.Simulator.Gear < app.Simulator.NumForwardGears ) )
		{
			var rpm = app.Simulator.RPM;

			var shiftLightsShiftRPM = app.Simulator.ShiftLightsShiftRPM > 0f ? app.Simulator.ShiftLightsShiftRPM : 8000f;

			if ( _testing )
			{
				rpm = _testTimer / TestDuration * shiftLightsShiftRPM;
			}

			var startingRPM = shiftLightsShiftRPM * settings.PedalsStartingRPM;
			var rpmRange = shiftLightsShiftRPM - startingRPM;

			if ( rpm >= startingRPM )
			{
				rpm = Math.Clamp( ( rpm - startingRPM ) / rpmRange, 0f, 1f );

				var frequency = Misc.Lerp( settings.PedalsMinimumFrequency, settings.PedalsMaximumFrequency, MathF.Pow( rpm, Misc.CurveToPower( settings.PedalsFrequencyCurve ) ) );

				if ( !_testing && settings.PedalsRPMFadeWithThrottleEnabled )
				{
					amplitude *= app.Simulator.Throttle;
				}

				amplitude = Math.Clamp( amplitude, 0f, 1f );

				amplitude = Misc.Lerp( settings.PedalsMinimumAmplitude, settings.PedalsMaximumAmplitude, MathF.Pow( amplitude, Misc.CurveToPower( settings.PedalsAmplitudeCurve ) ) );

				return (true, frequency, amplitude);
			}
		}

		return (false, 0f, 0f);
	}

	private (bool, float, float) DoClutchSlipEffect( App app, float amplitude )
	{
		var settings = DataContext.DataContext.Instance.Settings;

		if ( _testing || ( app.Simulator.Clutch >= settings.PedalsClutchSlipStart ) && ( app.Simulator.Clutch < settings.PedalsClutchSlipEnd ) )
		{
			var frequency = Misc.Lerp( settings.PedalsMinimumFrequency, settings.PedalsMaximumFrequency, MathF.Pow( settings.PedalsClutchSlipFrequency, Misc.CurveToPower( settings.PedalsFrequencyCurve ) ) );

			amplitude = Math.Clamp( amplitude, 0f, 1f );

			amplitude = MathF.Min( settings.PedalsMaximumAmplitude, MathF.Max( settings.PedalsMinimumAmplitude, amplitude ) );

			return (true, frequency, amplitude);
		}

		return (false, 0f, 0f);
	}

	public void Tick(App app)
	{
		_pedalUpdateCounter--;

		if ( _pedalUpdateCounter == 0 )
		{
			_pedalUpdateCounter = UpdateInterval;

			Update( app );
		}
	}
}

/*

#region Steering effects

if ( Settings.SteeringEffectsEnabled && ( ( Settings.USEffectStyle == 4 ) || ( Settings.OSEffectStyle == 4 ) ) )
{
	var effectAmount = 0f;

	if ( Settings.USEffectStyle == 4 )
	{
		var absUndersteerAmount = MathF.Abs( _ffb_understeerAmount );

		effectAmount = absUndersteerAmount * Settings.USEffectStrength / 100f;
		effectFrequency[ 5 ] = HPR_MAX_FREQUENCY;
	}

	if ( Settings.OSEffectStyle == 4 )
	{
		var absOversteerAmount = MathF.Abs( _ffb_oversteerAmount );

		if ( absOversteerAmount > effectAmount )
		{
			effectAmount = absOversteerAmount * Settings.OSEffectStrength / 100f;
			effectFrequency[ 5 ] = ( HPR_MAX_FREQUENCY - HPR_MIN_FREQUENCY ) / 2f + HPR_MIN_FREQUENCY;
		}
	}

	if ( effectAmount > 0f )
	{
		effectEngaged[ 5 ] = true;

		effectAmplitude[ 5 ] = ( HPR_MAX_AMPLITUDE - DataContext.Instance.Settings.PedalsMinimumAmplitude ) * effectAmount + DataContext.Instance.Settings.PedalsMinimumAmplitude;
	}
}

#endregion

#region Wheel lock and wheel spin

// update rpm vs speed ratios for wheel lock and spin effects

if ( ( app.Simulator.Gear > 0 ) && ( app.Simulator.RPM > 100f ) && ( _irsdk_velocityX > 5f ) )
{
	_hpr_currentRpmSpeedRatio = _irsdk_velocityX / app.Simulator.RPM;

	if ( ( _irsdk_brake == 0f ) && ( app.Simulator.Clutch == 1f ) )
	{
		if ( _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] == 0.0f )
		{
			_hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] = _hpr_currentRpmSpeedRatio;
		}
		else
		{
			_hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] = _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] * 0.95f + _hpr_currentRpmSpeedRatio * 0.05f;
		}
	}

	// wheel lock (6)

	if ( _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] != 0f )
	{
		if ( app.Simulator.Clutch == 1f )
		{
			if ( _hpr_currentRpmSpeedRatio > _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] * 1.05f )
			{
				effectEngaged[ 6 ] = true;

				effectFrequency[ 6 ] = HPR_MAX_FREQUENCY;
				effectAmplitude[ 6 ] = HPR_MAX_AMPLITUDE;
			}
		}
	}

	// wheel spin (7)

	if ( _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] != 0f )
	{
		if ( app.Simulator.Clutch == 1f )
		{
			if ( _hpr_currentRpmSpeedRatio < _hpr_averageRpmSpeedRatioPerGear[ app.Simulator.Gear ] * 0.95f )
			{
				effectEngaged[ 7 ] = true;

				effectFrequency[ 7 ] = HPR_MAX_FREQUENCY;
				effectAmplitude[ 7 ] = HPR_MAX_AMPLITUDE;
			}
		}
	}
}
else
{
	_hpr_currentRpmSpeedRatio = 0f;
}

#endregion

#region Clutch slip

if ( ( app.Simulator.Clutch > 0.25f ) && ( app.Simulator.Clutch < 0.75f ) )
{
	rpm = app.Simulator.RPM;

	rpmRange = app.Simulator.ShiftLightsShiftRPM * 0.5f;
	thresholdRPM = app.Simulator.ShiftLightsShiftRPM - rpmRange;

	if ( rpm > thresholdRPM )
	{
		rpm = Math.Clamp( ( rpm - thresholdRPM ) / rpmRange, 0f, 1f );

		effectEngaged[ 8 ] = true;

		effectFrequency[ 8 ] = Misc.Lerp( DataContext.DataContext.Instance.Settings.PedalsMinimumFrequency, DataContext.DataContext.Instance.Settings.PedalsMaximumFrequency, MathF.Pow( rpm, Misc.CurveToPower( DataContext.DataContext.Instance.Settings.PedalsFrequencyCurve ) ) );
		effectAmplitude[ 8 ] = MathF.Min( DataContext.DataContext.Instance.Settings.PedalsMaximumAmplitude, MathF.Max( DataContext.DataContext.Instance.Settings.PedalsMinimumAmplitude, _clutchSlipAmplitude ) );
	}
}

#endregion

*/