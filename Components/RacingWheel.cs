
using System.Runtime.CompilerServices;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Controls;

namespace MarvinsAIRARefactored.Components;

public class RacingWheel
{
	public enum Algorithm
	{
		Native60Hz,
		Native360Hz,
		DetailBooster,
		DeltaLimiter,
		DetailBoosterOn60Hz,
		DeltaLimiterOn60Hz,
		ZeAlanLeTwist,
	};

	private const int _maxSteeringWheelTorque360HzIndex = Simulator.SamplesPerFrame360Hz + 1;

	private const float _unsuspendTimeMS = 1000f;
	private const float _fadeInTimeMS = 2000f;
	private const float _fadeOutTimeMS = 500f;
	private const float _testSignalTime = 2000f;
	private const float _crashProtectionRecoveryTime = 1000f;

	private Guid? _currentRacingWheelGuid = null;

	private bool _isSuspended = true;
	private bool _usingSteeringWheelTorqueData = false;

	public Guid? NextRacingWheelGuid { private get; set; } = null;
	public bool SuspendForceFeedback { get; set; } = true; // true if simulator is disconnected or if FFB is enabled in the simulator
	public bool ResetForceFeedback { private get; set; } = false; // set to true manually (via reset button)
	public bool UseSteeringWheelTorqueData { private get; set; } = false; // false if simulator is disconnected or if driver is not on track
	public bool UpdateSteeringWheelTorqueBuffer { private get; set; } = false; // true when simulator has new torque data to be copied
	public bool ActivateCrashProtection { private get; set; } = false; // set to true to activate crash protection
	public bool ActivateCurbProtection { private get; set; } = false; // set to true to activate curb protection
	public bool PlayTestSignal { private get; set; } = false; // set to true manually (via test button)
	public bool ClearPeakTorque { private get; set; } = false; // set to clear peak torque
	public bool AutoSetMaxForce { private get; set; } = false; // set to auto-set the max force setting

	private float _unsuspendTimerMS = 0f;
	private float _fadeTimerMS = 0f;
	private float _testSignalTimerMS = 0f;
	private float _crashProtectionTimerMS = 0f;
	private float _curbProtectionTimerMS = 0f;

	private readonly float[] _steeringWheelTorque360Hz = new float[ Simulator.SamplesPerFrame360Hz + 2 ];

	private float _lastSteeringWheelTorque500Hz = 0f;
	private float _runningSteeringWheelTorque500Hz = 0f;

	private float _lastUnfadedOutputTorque = 0f;
	private float _peakTorque = 0f;
	private float _autoTorque = 0f;

	private float _elapsedMilliseconds = 0f;

	public void Initialize()
	{
		var app = App.Instance;

		if ( app != null )
		{
			app.Logger.WriteLine( "[RacingWheel] Initialize >>>" );

			app.Graph.SetLayerColors( Graph.LayerIndex.InputTorque60Hz, 1f, 0f, 0f, 1f, 0f, 0f );
			app.Graph.SetLayerColors( Graph.LayerIndex.InputTorque, 1f, 0f, 1f, 1f, 0f, 1f );
			app.Graph.SetLayerColors( Graph.LayerIndex.InputLFE, 0.1f, 0.5f, 1f, 1f, 1f, 1f );
			app.Graph.SetLayerColors( Graph.LayerIndex.OutputTorque, 0f, 1f, 1f, 0f, 1f, 1f );

			app.Logger.WriteLine( "[RacingWheel] <<< Initialize" );
		}
	}

	public static void SetMairaComboBoxItemsSource( MairaComboBox mairaComboBox )
	{
		var app = App.Instance;

		if ( app != null )
		{
			app.Logger.WriteLine( "[RacingWheel] SetMairaComboBoxItemsSource >>>" );

			var dictionary = new Dictionary<Algorithm, string>
			{
				{ Algorithm.Native60Hz, DataContext.DataContext.Instance.Localization[ "Native60Hz" ] },
				{ Algorithm.Native360Hz, DataContext.DataContext.Instance.Localization[ "Native360Hz" ] },
				{ Algorithm.DetailBooster, DataContext.DataContext.Instance.Localization[ "DetailBooster" ] },
				{ Algorithm.DeltaLimiter, DataContext.DataContext.Instance.Localization[ "DeltaLimiter" ] },
				{ Algorithm.DetailBoosterOn60Hz, DataContext.DataContext.Instance.Localization[ "DetailBoosterOn60Hz" ] },
				{ Algorithm.DeltaLimiterOn60Hz, DataContext.DataContext.Instance.Localization[ "DeltaLimiterOn60Hz" ] },
				{ Algorithm.ZeAlanLeTwist, DataContext.DataContext.Instance.Localization[ "ZeAlanLeTwist" ] }
			};

			mairaComboBox.ItemsSource = dictionary;
			mairaComboBox.SelectedValue = DataContext.DataContext.Instance.Settings.RacingWheelAlgorithm;

			app.Logger.WriteLine( "[RacingWheel] <<< SetMairaComboBoxItemsSource" );
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void Update( float deltaMilliseconds )
	{
		var app = App.Instance;

		if ( app != null )
		{
			try
			{
				// easy reference to settings

				var settings = DataContext.DataContext.Instance.Settings;

				// test signal generator

				if ( PlayTestSignal )
				{
					_testSignalTimerMS = _testSignalTime;

					app.Logger.WriteLine( "[RacingWheel] Sending test signal" );

					PlayTestSignal = false;
				}

				var testSignalTorque = 0f;

				if ( _testSignalTimerMS > 0f )
				{
					testSignalTorque = MathF.Cos( _testSignalTimerMS * MathF.Tau / 20f ) * MathF.Sin( _testSignalTimerMS * MathF.Tau / _testSignalTime * 2f ) * 0.2f;

					_testSignalTimerMS -= deltaMilliseconds;
				}

				// check if we want to suspend or unsuspend force feedback

				if ( SuspendForceFeedback != _isSuspended )
				{
					_isSuspended = SuspendForceFeedback;

					if ( _isSuspended )
					{
						app.Logger.WriteLine( "[RacingWheel] Requesting suspend of force feedback" );

						_unsuspendTimerMS = _unsuspendTimeMS;
					}
					else
					{
						app.Logger.WriteLine( "[RacingWheel] Requesting resumption of force feedback" );
					}

					app.MainWindow.UpdateRacingWheelPowerButton();
				}

				// check if we want to fade in or out the steering wheel torque data

				if ( UseSteeringWheelTorqueData != _usingSteeringWheelTorqueData )
				{
					_usingSteeringWheelTorqueData = UseSteeringWheelTorqueData;

					app.MainWindow.UpdateRacingWheelPowerButton();

					if ( settings.RacingWheelFadeEnabled )
					{
						if ( _usingSteeringWheelTorqueData )
						{
							app.Logger.WriteLine( "[RacingWheel] Requesting fade in of steering wheel torque data" );

							_fadeTimerMS = _fadeInTimeMS;
						}
						else
						{
							app.Logger.WriteLine( "[RacingWheel] Requesting fade out of steering wheel torque data" );

							_fadeTimerMS = _fadeOutTimeMS;
						}
					}
				}

				// check if we want to reset the racing wheel device

				if ( ResetForceFeedback )
				{
					ResetForceFeedback = false;

					if ( NextRacingWheelGuid == null )
					{
						NextRacingWheelGuid = _currentRacingWheelGuid;

						app.Logger.WriteLine( "[RacingWheel] Requesting reset of force feedback device" );
					}
				}

				// if power button is off, or suspend is requested, or unsuspend counter is still counting down, then suspend the racing wheel force feedback

				if ( !settings.RacingWheelEnableForceFeedback || _isSuspended || ( _unsuspendTimerMS > 0f ) )
				{
					if ( _currentRacingWheelGuid != null )
					{
						app.Logger.WriteLine( "[RacingWheel] Suspending racing wheel force feedback" );

						app.DirectInput.ShutdownForceFeedback();

						NextRacingWheelGuid = _currentRacingWheelGuid;

						_currentRacingWheelGuid = null;
					}

					_unsuspendTimerMS -= deltaMilliseconds;

					return;
				}

				// if next racing wheel guid is set then re-initialize force feedback

				if ( NextRacingWheelGuid != null )
				{
					if ( _currentRacingWheelGuid != null )
					{
						app.Logger.WriteLine( "[RacingWheel] Uninitializing racing wheel force feedback" );

						app.DirectInput.ShutdownForceFeedback();

						_currentRacingWheelGuid = null;
					}

					if ( NextRacingWheelGuid != Guid.Empty )
					{
						app.Logger.WriteLine( "[RacingWheel] Initializing racing wheel force feedback" );

						_currentRacingWheelGuid = NextRacingWheelGuid;

						NextRacingWheelGuid = null;

						app.DirectInput.InitializeForceFeedback( (Guid) _currentRacingWheelGuid );
					}
				}

				// check if we want to auto set max force

				if ( AutoSetMaxForce )
				{
					AutoSetMaxForce = false;
					ClearPeakTorque = true;

					settings.RacingWheelMaxForce = _autoTorque;

					app.Logger.WriteLine( $"[RacingWheel] Max force auto set to {_autoTorque}" );
				}

				// update elapsed milliseconds

				_elapsedMilliseconds += deltaMilliseconds;

				// update steering wheel torque data

				if ( UpdateSteeringWheelTorqueBuffer )
				{
					if ( _usingSteeringWheelTorqueData )
					{
						_steeringWheelTorque360Hz[ 0 ] = _steeringWheelTorque360Hz[ 7 ];
						_steeringWheelTorque360Hz[ 1 ] = app.Simulator.SteeringWheelTorque_ST[ 0 ];
						_steeringWheelTorque360Hz[ 2 ] = app.Simulator.SteeringWheelTorque_ST[ 1 ];
						_steeringWheelTorque360Hz[ 3 ] = app.Simulator.SteeringWheelTorque_ST[ 2 ];
						_steeringWheelTorque360Hz[ 4 ] = app.Simulator.SteeringWheelTorque_ST[ 3 ];
						_steeringWheelTorque360Hz[ 5 ] = app.Simulator.SteeringWheelTorque_ST[ 4 ];
						_steeringWheelTorque360Hz[ 6 ] = app.Simulator.SteeringWheelTorque_ST[ 5 ];
						_steeringWheelTorque360Hz[ 7 ] = app.Simulator.SteeringWheelTorque_ST[ 5 ];
					}
					else
					{
						_steeringWheelTorque360Hz[ 0 ] = 0f;
						_steeringWheelTorque360Hz[ 1 ] = 0f;
						_steeringWheelTorque360Hz[ 2 ] = 0f;
						_steeringWheelTorque360Hz[ 3 ] = 0f;
						_steeringWheelTorque360Hz[ 4 ] = 0f;
						_steeringWheelTorque360Hz[ 5 ] = 0f;
						_steeringWheelTorque360Hz[ 6 ] = 0f;
						_steeringWheelTorque360Hz[ 7 ] = 0f;
					}

					_elapsedMilliseconds = 0f;

					UpdateSteeringWheelTorqueBuffer = false;
				}

				// get next 60Hz and 360Hz steering wheel torque samples

				var steeringWheelTorque360HzIndex = 1f + ( _elapsedMilliseconds * 360f / 1000f );

				var i1 = Math.Min( _maxSteeringWheelTorque360HzIndex, (int) MathF.Truncate( steeringWheelTorque360HzIndex ) );
				var i2 = Math.Min( _maxSteeringWheelTorque360HzIndex, i1 + 1 );
				var i3 = Math.Min( _maxSteeringWheelTorque360HzIndex, i2 + 1 );
				var i0 = Math.Max( 0, i1 - 1 );

				var t = MathF.Min( 1f, steeringWheelTorque360HzIndex - i1 );

				var m0 = _steeringWheelTorque360Hz[ i0 ];
				var m1 = _steeringWheelTorque360Hz[ i1 ];
				var m2 = _steeringWheelTorque360Hz[ i2 ];
				var m3 = _steeringWheelTorque360Hz[ i3 ];

				var steeringWheelTorque60Hz = _steeringWheelTorque360Hz[ 6 ];
				var steeringWheelTorque500Hz = Misc.InterpolateHermite( m0, m1, m2, m3, t );

				// update peak torque

				if ( ClearPeakTorque )
				{
					_peakTorque = 0f;

					ClearPeakTorque = false;
				}

				if ( app.Simulator.IsOnTrack && ( app.Simulator.PlayerTrackSurface == IRSDKSharper.IRacingSdkEnum.TrkLoc.OnTrack ) )
				{
					_peakTorque = MathF.Max( _peakTorque, Misc.Lerp( _peakTorque, MathF.Abs( steeringWheelTorque500Hz ), 0.01f ) );
				}

				// update auto torque

				_autoTorque = _peakTorque * ( 1f + settings.RacingWheelAutoMargin );

				// update crash protection

				if ( ActivateCrashProtection )
				{
					_crashProtectionTimerMS = settings.RacingWheelCrashProtectionDuration * 1000f + _crashProtectionRecoveryTime;

					ActivateCrashProtection = false;
				}

				app.Debug.Label_3 = $"_crashProtectionTimerMS = {_crashProtectionTimerMS:F0}";

				var crashProtectionScale = 1f;

				if ( _crashProtectionTimerMS > 0f )
				{
					crashProtectionScale = 1f - settings.RacingWheelCrashProtectionForceReduction * ( ( _crashProtectionTimerMS <= _crashProtectionRecoveryTime ) ? ( _crashProtectionTimerMS / _crashProtectionRecoveryTime ) : 1f );

					_crashProtectionTimerMS -= deltaMilliseconds;
				}

				app.Debug.Label_4 = $"crashProtectionScale = {crashProtectionScale * 100f:F0}";

				// update curb protection

				if ( ActivateCurbProtection )
				{
					_curbProtectionTimerMS = settings.RacingWheelCurbProtectionDuration * 1000f;

					ActivateCurbProtection = false;
				}

				app.Debug.Label_9 = $"_curbProtectionTimerMS = {_curbProtectionTimerMS:F0}";

				var curbProtectionLerpFactor = 0f;

				if ( _curbProtectionTimerMS > 0f )
				{
					curbProtectionLerpFactor = settings.RacingWheelCurbProtectionForceReduction;

					_curbProtectionTimerMS -= deltaMilliseconds;
				}

				app.Debug.Label_10 = $"curbProtectionLerpFactor = {curbProtectionLerpFactor * 100f:F0}%";

				// grab the next LFE magnitude

				var inputLFEMagnitude = app.LFE.CurrentMagnitude;

				// zero the output torque

				var outputTorque = 0f;

				// calculate output torque

				switch ( settings.RacingWheelAlgorithm )
				{
					case Algorithm.Native60Hz:
					{
						outputTorque = steeringWheelTorque60Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.Native360Hz:
					{
						outputTorque = steeringWheelTorque500Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.DetailBooster:
					{
						var detailBoost = Misc.Lerp( 1f + settings.RacingWheelDetailBoost, 1f, curbProtectionLerpFactor );

						_runningSteeringWheelTorque500Hz = Misc.Lerp( _runningSteeringWheelTorque500Hz + ( steeringWheelTorque500Hz - _lastSteeringWheelTorque500Hz ) * detailBoost, steeringWheelTorque500Hz, settings.RacingWheelDetailBoostBias );

						outputTorque = _runningSteeringWheelTorque500Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.DeltaLimiter:
					{
						var deltaLimit = Misc.Lerp( settings.RacingWheelDeltaLimit, 1f, curbProtectionLerpFactor ) / 500f;

						var limitedDeltaSteeringWheelTorque500Hz = Math.Clamp( steeringWheelTorque500Hz - _lastSteeringWheelTorque500Hz, -deltaLimit, deltaLimit );

						_runningSteeringWheelTorque500Hz = Misc.Lerp( _runningSteeringWheelTorque500Hz + limitedDeltaSteeringWheelTorque500Hz, steeringWheelTorque500Hz, settings.RacingWheelDeltaLimiterBias );

						outputTorque = _runningSteeringWheelTorque500Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.DetailBoosterOn60Hz:
					{
						var detailBoost = Misc.Lerp( 1f + settings.RacingWheelDetailBoost, 1f, curbProtectionLerpFactor );

						_runningSteeringWheelTorque500Hz = Misc.Lerp( _runningSteeringWheelTorque500Hz + ( steeringWheelTorque500Hz - _lastSteeringWheelTorque500Hz ) * detailBoost, steeringWheelTorque60Hz, settings.RacingWheelDetailBoostBias );

						outputTorque = _runningSteeringWheelTorque500Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.DeltaLimiterOn60Hz:
					{
						var deltaLimit = Misc.Lerp( settings.RacingWheelDeltaLimit, 1f, curbProtectionLerpFactor ) / 500f;

						var limitedDeltaSteeringWheelTorque500Hz = Math.Clamp( steeringWheelTorque500Hz - _lastSteeringWheelTorque500Hz, -deltaLimit, deltaLimit );

						_runningSteeringWheelTorque500Hz = Misc.Lerp( _runningSteeringWheelTorque500Hz + limitedDeltaSteeringWheelTorque500Hz, steeringWheelTorque500Hz, settings.RacingWheelDeltaLimiterBias );

						outputTorque = _runningSteeringWheelTorque500Hz / settings.RacingWheelMaxForce;

						break;
					}

					case Algorithm.ZeAlanLeTwist:
					{
						var runningTorquePct = _runningSteeringWheelTorque500Hz / DataContext.DataContext.Instance.Settings.RacingWheelMaxForce;

						var delta = (steeringWheelTorque500Hz - _runningSteeringWheelTorque500Hz) / DataContext.DataContext.Instance.Settings.RacingWheelMaxForce;

						var deltaAbs = MathF.Abs(delta);

						var deltaLimit = DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionThreshold / 100f / 500f;

						var deltaRate = (1f - (DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRate / 100f)) * ((MathF.Sign(delta) == MathF.Sign(steeringWheelTorque500Hz)) ? 1f : MathF.Max(0.75f, 0.25f + ((1f - DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRate / 100f) * 0.75f)));

						if ((deltaRate != 1) && (deltaAbs > deltaLimit))
						{
							runningTorquePct += (deltaLimit + ((deltaAbs - deltaLimit) * deltaRate)) * MathF.Sign(delta);
						} 
						else
						{
							runningTorquePct += delta;
						}

						var compressionThreshold = DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionThreshold / 100f;

						var compressionWidth = compressionThreshold;

						var compressionRate = 1 - (DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionRate / 100f);

						var runningTorquePctAbs = MathF.Abs(runningTorquePct);

						if ((runningTorquePctAbs > (compressionThreshold - (compressionWidth / 2f))) && (runningTorquePctAbs < (compressionThreshold + (compressionWidth / 2f))))
						{
							runningTorquePctAbs = runningTorquePctAbs - ((compressionRate / 2f) * (runningTorquePctAbs - compressionThreshold + (compressionWidth / 2f) - (compressionWidth / MathF.PI * MathF.Sin(MathF.PI * (runningTorquePctAbs - compressionThreshold + (compressionWidth / 2f)) / compressionWidth))));
						} 
						else if (runningTorquePctAbs >= (compressionThreshold + (compressionWidth / 2f)))
						{
							runningTorquePctAbs = compressionThreshold + ((runningTorquePctAbs - compressionThreshold) * compressionRate);
						}

						runningTorquePct = runningTorquePctAbs * MathF.Sign(runningTorquePct);

						_runningSteeringWheelTorque500Hz = runningTorquePct * DataContext.DataContext.Instance.Settings.RacingWheelMaxForce;

						outputTorque = runningTorquePct;

						break;
					}
				}

				// save last 500Hz steering wheel torque

				_lastSteeringWheelTorque500Hz = steeringWheelTorque500Hz;

				// apply output maximum

				if ( settings.RacingWheelOutputMaximum < 1f )
				{
					outputTorque *= settings.RacingWheelOutputMaximum;
				}

				// apply output curve

				if ( settings.RacingWheelOutputCurve != 0f )
				{
					var power = Misc.CurveToPower( settings.RacingWheelOutputCurve );

					outputTorque = MathF.Sign( outputTorque ) * MathF.Pow( MathF.Abs( outputTorque ), power );
				}

				// apply output minimum

				if ( settings.RacingWheelOutputMinimum > 0f )
				{
					if ( outputTorque >= 0f )
					{
						if ( outputTorque < settings.RacingWheelOutputMinimum )
						{
							outputTorque = settings.RacingWheelOutputMinimum;
						}
					}
					else
					{
						if ( outputTorque > -settings.RacingWheelOutputMinimum )
						{
							outputTorque = -settings.RacingWheelOutputMinimum;
						}
					}
				}

				// apply crash protection

				outputTorque *= crashProtectionScale;

				// reduce forces when parked

				if ( settings.RacingWheelParkedStrength < 1f )
				{
					outputTorque *= Misc.Lerp( settings.RacingWheelParkedStrength, 1f, app.Simulator.Velocity / 2.2352f ); // = 5 MPH
				}

				// add wheel LFE

				if ( settings.RacingWheelLFEStrength > 0f )
				{
					outputTorque += inputLFEMagnitude * settings.RacingWheelLFEStrength;
				}

				// add soft lock

				if ( settings.RacingWheelSoftLockStrength > 0f )
				{
					var deltaToMax = app.Simulator.SteeringWheelAngleMax - MathF.Abs( app.Simulator.SteeringWheelAngle );

					if ( deltaToMax < 0f )
					{
						var sign = MathF.Sign( app.Simulator.SteeringWheelAngle );

						outputTorque += sign * deltaToMax * 2f * settings.RacingWheelSoftLockStrength;

						if ( MathF.Sign( app.DirectInput.ForceFeedbackWheelVelocity ) != sign )
						{
							outputTorque += app.DirectInput.ForceFeedbackWheelVelocity * settings.RacingWheelSoftLockStrength;
						}
					}
				}

				// apply friction torque

				if ( settings.RacingWheelFriction > 0f )
				{
					outputTorque += app.DirectInput.ForceFeedbackWheelVelocity * settings.RacingWheelFriction;
				}

				// apply fade

				app.Debug.Label_5 = $"_fadeTimerMS = {_fadeTimerMS:F0}";
				app.Debug.Label_6 = $"_lastUnfadedOutputTorque = {_lastUnfadedOutputTorque:F2}";

				if ( _fadeTimerMS > 0f )
				{
					if ( _usingSteeringWheelTorqueData )
					{
						var fadeScale = _fadeTimerMS / _fadeInTimeMS;

						app.Debug.Label_7 = $"fadeScale = {fadeScale * 100f:F2}% (fading in)";

						outputTorque *= 1f - fadeScale;
					}
					else
					{
						var fadeScale = _fadeTimerMS / _fadeOutTimeMS;

						app.Debug.Label_7 = $"fadeScale = {fadeScale * 100f:F0}% (fading out)";

						outputTorque = _lastUnfadedOutputTorque * fadeScale;
					}

					_fadeTimerMS -= deltaMilliseconds;
				}
				else
				{
					app.Debug.Label_7 = $"fadeScale = OFF";

					_lastUnfadedOutputTorque = outputTorque;
				}

				// add test signal torque

				outputTorque += testSignalTorque;

				// update force feedback torque

				app.DirectInput.UpdateForceFeedbackEffect( outputTorque );

				// update graph

				app.Graph.UpdateLayer( Graph.LayerIndex.InputTorque60Hz, steeringWheelTorque60Hz, steeringWheelTorque60Hz / settings.RacingWheelMaxForce );
				app.Graph.UpdateLayer( Graph.LayerIndex.InputTorque, steeringWheelTorque500Hz, steeringWheelTorque500Hz / settings.RacingWheelMaxForce );
				app.Graph.UpdateLayer( Graph.LayerIndex.InputLFE, inputLFEMagnitude, inputLFEMagnitude );
				app.Graph.UpdateLayer( Graph.LayerIndex.OutputTorque, outputTorque, outputTorque );

				// update alan le dump

				app.Debug.AddFFBSample( deltaMilliseconds, steeringWheelTorque60Hz, steeringWheelTorque500Hz, inputLFEMagnitude, outputTorque );
			}
			catch ( Exception exception )
			{
				app.Logger.WriteLine( $"[RacingWheel] Exception caught: {exception.Message.Trim()}" );

				_unsuspendTimerMS = _unsuspendTimeMS;
			}
		}
	}

	public void Tick( App app )
	{
		app.MainWindow.RacingWheel_PeakForce_Label.Content = $"{_peakTorque:F2}{DataContext.DataContext.Instance.Localization[ "TorqueUnits" ]}";
		app.MainWindow.RacingWheel_AutoForce_Label.Content = $"{_autoTorque:F2}{DataContext.DataContext.Instance.Localization[ "TorqueUnits" ]}";
	}
}
