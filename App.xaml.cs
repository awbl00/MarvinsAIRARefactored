﻿
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Components;
using MarvinsAIRARefactored.Windows;

namespace MarvinsAIRARefactored;

public partial class App : Application
{
	public const string APP_FOLDER_NAME = "MarvinsAIRA Refactored";

	public static string DocumentsFolder { get; } = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), APP_FOLDER_NAME );

	public static App? Instance { get; private set; }

	public Logger Logger { get; private set; }
	public CloudService CloudService { get; private set; }
	public SettingsFile SettingsFile { get; private set; }
	public Graph Graph { get; private set; }
	public Pedals Pedals { get; private set; }
	public AdminBoxx AdminBoxx { get; private set; }
	public Debug Debug { get; private set; }
	public new MainWindow MainWindow { get; private set; }
	public RacingWheel RacingWheel { get; private set; }
	public ChatQueue ChatQueue { get; private set; }
	public AudioManager AudioManager { get; private set; }
	public DirectInput DirectInput { get; private set; }
	public LFE LFE { get; private set; }
	public MultimediaTimer MultimediaTimer { get; private set; }
	public Simulator Simulator { get; private set; }

	public const int TimerPeriodInMilliseconds = 17;
	public const int TimerTicksPerSecond = 1000 / TimerPeriodInMilliseconds;

	private readonly AutoResetEvent _autoResetEvent = new( false );

	private readonly Thread _workerThread = new( WorkerThread ) { IsBackground = true, Priority = ThreadPriority.Normal };

	private bool _running = true;

	private readonly Timer _timer = new( TimerPeriodInMilliseconds );

	App()
	{
		Instance = this;

		Logger = new();
		CloudService = new();
		SettingsFile = new();
		Graph = new();
		Pedals = new();
		AdminBoxx = new();
		Debug = new();
		MainWindow = new();
		RacingWheel = new();
		ChatQueue = new();
		AudioManager = new();
		DirectInput = new();
		LFE = new();
		MultimediaTimer = new();
		Simulator = new();

		_timer.Elapsed += OnTimer;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void TriggerWorkerThread()
	{
		_autoResetEvent.Set();
	}

	private async void App_Startup( object sender, StartupEventArgs e )
	{
		Logger.WriteLine( "[App] App_Startup >>>" );

		Misc.DisableThrottling();

		if ( !Directory.Exists( DocumentsFolder ) )
		{
			Directory.CreateDirectory( DocumentsFolder );
		}

		Logger.Initialize();
		CloudService.Initialize();
		SettingsFile.Initialize();
		Graph.Initialize();
		Pedals.Initialize();
		AdminBoxx.Initialize();
		RacingWheel.Initialize();
		AudioManager.Initialize();
		DirectInput.Initialize();
		LFE.Initialize();
		MultimediaTimer.Initialize();
		Simulator.Initialize();

		DirectInput.OnInput += OnInput;

		GC.Collect();

		MainWindow.Resources = Current.Resources;

		MainWindow.Initialize();
		MainWindow.Show();

		if ( DataContext.DataContext.Instance.Settings.AdminBoxxConnectOnStartup )
		{
			AdminBoxx.Connect();
		}

		if ( DataContext.DataContext.Instance.Settings.AppCheckForUpdates )
		{
			await CloudService.CheckForUpdates( false );
		}

		_workerThread.Start();

		_timer.Start();

		Simulator.Start();

		GC.Collect();

		Logger.WriteLine( "[App] <<< App_Startup" );
	}

	private void App_Exit( object sender, EventArgs e )
	{
		Logger.WriteLine( "[App] App_Exit >>>" );

		_timer.Stop();

		_running = false;

		_autoResetEvent.Set();

		Simulator.Shutdown();
		MultimediaTimer.Shutdown();
		AdminBoxx.Shutdown();
		LFE.Shutdown();
		DirectInput.Shutdown();
		Logger.Shutdown();

		Logger.WriteLine( "[App] <<< App_Exit" );
	}

	private void OnInput( string deviceProductName, Guid deviceInstanceGuid, int buttonNumber, bool isPressed )
	{
		if ( !UpdateButtonMappingsWindow.WindowIsOpen && isPressed )
		{
			// racing wheel power button

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedbackButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback = !DataContext.DataContext.Instance.Settings.RacingWheelEnableForceFeedback;
			}

			// racing wheel test button

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelTestButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				RacingWheel.PlayTestSignal = true;
			}

			// racing wheel reset button

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelResetButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				RacingWheel.ResetForceFeedback = true;
			}

			// racing wheel max force knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelMaxForcePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelMaxForce += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelMaxForceMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelMaxForce -= 1f;
			}

			// racing wheel auto margin knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelAutoMarginPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelAutoMargin += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelAutoMarginMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelAutoMargin -= 1f;
			}

			// racing wheel auto button

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelAutoButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				RacingWheel.AutoSetMaxForce = true;
			}

			// racing wheel clear button

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelClearButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				RacingWheel.ClearPeakTorque = true;
			}

			// racing wheel detail boost knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDetailBoost += 0.1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDetailBoost -= 0.1f;
			}

			// racing wheel delta limit knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimitPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimit += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimitMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimit -= 0.01f;
			}

			// racing wheel detail boost bias knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostBiasPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostBias += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostBiasMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDetailBoostBias -= 0.01f;
			}

			// racing wheel delta limiter bias knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimiterBiasPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimiterBias += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimiterBiasMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelDeltaLimiterBias -= 0.01f;
			}

			// racing wheel slew compression threshold knob

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionThresholdPlusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionThreshold += 100f;
			}

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionThresholdMinusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionThreshold -= 100f;
			}

			// racing wheel slew compression rate knob

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRatePlusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRate += 1f;
			}

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRateMinusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSlewCompressionRate -= 1f;
			}

			// racing wheel total compression threshold knob

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionThresholdPlusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionThreshold += 1f;
			}

			if (CheckMappedButtons(DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionThresholdMinusButtonMappings, deviceInstanceGuid, buttonNumber))
			{
				DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionThreshold -= 1f;
			}

			// racing wheel total compression rate knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionRatePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionRate += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionRateMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelTotalCompressionRate -= 1f;
			}

			// racing wheel output minimum knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputMinimumPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputMinimum += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputMinimumMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputMinimum -= 0.01f;
			}

			// racing wheel output maximum knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputMaximumPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputMaximum += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputMaximumMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputMaximum -= 0.01f;
			}

			// racing wheel output curve knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputCurvePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputCurve += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelOutputCurveMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelOutputCurve -= 0.01f;
			}

			// racing wheel lfe strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelLFEStrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelLFEStrength += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelLFEStrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelLFEStrength -= 0.01f;
			}

			// racing wheel crash protection g force knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionGForcePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionGForce += 0.5f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionGForceMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionGForce -= 0.5f;
			}

			// racing wheel crash protection duration knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionDurationPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionDuration += 0.5f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionDurationMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionDuration -= 0.5f;
			}

			// racing wheel crash protection force reduction knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionForceReductionPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionForceReduction += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionForceReductionMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCrashProtectionForceReduction -= 0.05f;
			}

			// racing wheel curb protection shock velocity knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionShockVelocityPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionShockVelocity += 0.1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionShockVelocityMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionShockVelocity -= 0.1f;
			}

			// racing wheel curb protection duration knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionDurationPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionDuration += 0.1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionDurationMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionDuration -= 0.1f;
			}

			// racing wheel curb protection force reduction knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionForceReductionPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionForceReduction += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionForceReductionMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelCurbProtectionForceReduction -= 0.05f;
			}

			// racing wheel parked strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelParkedStrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelParkedStrength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelParkedStrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelParkedStrength -= 0.05f;
			}

			// racing wheel soft lock knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelSoftLockStrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSoftLockStrength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelSoftLockStrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelSoftLockStrength -= 0.05f;
			}

			// racing wheel friction knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelFrictionPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelFriction += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.RacingWheelFrictionMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.RacingWheelFriction -= 0.05f;
			}

			// pedals clutch effect 1 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect1StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect1Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect1StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect1Strength -= 0.05f;
			}

			// pedals clutch effect 2 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect2StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect2Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect2StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect2Strength -= 0.05f;
			}

			// pedals clutch effect 3 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect3StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect3Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchEffect3StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchEffect3Strength -= 0.05f;
			}

			// pedals brake effect 1 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect1StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect1Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect1StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect1Strength -= 0.05f;
			}

			// pedals brake effect 2 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect2StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect2Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect2StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect2Strength -= 0.05f;
			}

			// pedals brake effect 3 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect3StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect3Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsBrakeEffect3StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsBrakeEffect3Strength -= 0.05f;
			}

			// pedals throttle effect 1 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect1StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect1Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect1StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect1Strength -= 0.05f;
			}

			// pedals throttle effect 2 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect2StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect2Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect2StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect2Strength -= 0.05f;
			}

			// pedals throttle effect 3 strength knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect3StrengthPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect3Strength += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsThrottleEffect3StrengthMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsThrottleEffect3Strength -= 0.05f;
			}

			// pedals shift into gear frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearFrequency += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearFrequency -= 1f;
			}

			// pedals shift into gear amplitude knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearAmplitudePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearAmplitude += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearAmplitudeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearAmplitude -= 0.01f;
			}

			// pedals shift into gear duration knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearDurationPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearDuration += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearDurationMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoGearDuration -= 0.05f;
			}

			// pedals shift into neutral frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralFrequency += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralFrequency -= 1f;
			}

			// pedals shift into neutral amplitude knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralAmplitudePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralAmplitude += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralAmplitudeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralAmplitude -= 0.01f;
			}

			// pedals shift into neutral duration knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralDurationPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralDuration += 0.05f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralDurationMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsShiftIntoNeutralDuration -= 0.05f;
			}

			// pedals abs engaged frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsABSEngagedFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsABSEngagedFrequency += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsABSEngagedFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsABSEngagedFrequency -= 1f;
			}

			// pedals abs engaged amplitude knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsABSEngagedAmplitudePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsABSEngagedAmplitude += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsABSEngagedAmplitudeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsABSEngagedAmplitude -= 0.01f;
			}

			// pedals starting rpm knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsStartingRPMPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsStartingRPM += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsStartingRPMMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsStartingRPM -= 0.01f;
			}

			// pedals clutch slip start knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipStartPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipStart += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipStartMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipStart -= 0.01f;
			}

			// pedals clutch slip end knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipEndPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipEnd += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipEndMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipEnd -= 0.01f;
			}

			// pedals clutch slip frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipFrequency += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsClutchSlipFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsClutchSlipFrequency -= 0.01f;
			}

			// pedals minimum frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMinimumFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMinimumFrequency += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMinimumFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMinimumFrequency -= 1f;
			}

			// pedals maximum frequency knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMaximumFrequencyPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMaximumFrequency += 1f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMaximumFrequencyMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMaximumFrequency -= 1f;
			}

			// pedals frequency curve knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsFrequencyCurvePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsFrequencyCurve += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsFrequencyCurveMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsFrequencyCurve -= 0.01f;
			}

			// pedals minimum amplitude knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMinimumAmplitudePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMinimumAmplitude += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMinimumAmplitudeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMinimumAmplitude -= 0.01f;
			}

			// pedals maximum amplitude knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMaximumAmplitudePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMaximumAmplitude += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsMaximumAmplitudeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsMaximumAmplitude -= 0.01f;
			}

			// pedals amplitude curve knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsAmplitudeCurvePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsAmplitudeCurve += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsAmplitudeCurveMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsAmplitudeCurve -= 0.01f;
			}

			// pedals noise damper

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsNoiseDamperPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsNoiseDamper += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.PedalsNoiseDamperMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.PedalsNoiseDamper -= 0.01f;
			}

			// adminboxx brightness knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBrightnessPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBrightness += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBrightnessMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBrightness -= 0.01f;
			}

			// adminboxx black flag r

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagGPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagR += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagRMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagR -= 0.01f;
			}

			// adminboxx black flag g

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagGPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagG += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagGMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagG -= 0.01f;
			}

			// adminboxx black flag b

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagGPlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagB += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagBMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxBlackFlagB -= 0.01f;
			}

			// adminboxx volume knob

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxVolumePlusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxVolume += 0.01f;
			}

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.AdminBoxxVolumeMinusButtonMappings, deviceInstanceGuid, buttonNumber ) )
			{
				DataContext.DataContext.Instance.Settings.AdminBoxxVolume -= 0.01f;
			}

			// debug alan le reset

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.DebugAlanLeResetMappings, deviceInstanceGuid, buttonNumber ) )
			{
				Debug.ResetFFBSamples();
			}

			// debug alan le dump

			if ( CheckMappedButtons( DataContext.DataContext.Instance.Settings.DebugAlanLeDumpMappings, deviceInstanceGuid, buttonNumber ) )
			{
				Debug.DumpFFBSamplesToCSVFile();
			}
		}
	}

	private bool CheckMappedButtons( ButtonMappings buttonMappings, Guid deviceInstanceGuid, int buttonNumber )
	{
		foreach ( var mappedButton in buttonMappings.MappedButtons )
		{
			if ( mappedButton.ClickButton.DeviceInstanceGuid == deviceInstanceGuid )
			{
				if ( mappedButton.ClickButton.ButtonNumber == buttonNumber )
				{
					if ( mappedButton.HoldButton.DeviceInstanceGuid == Guid.Empty )
					{
						return true;
					}
					else
					{
						if ( DirectInput.IsButtonDown( deviceInstanceGuid, mappedButton.HoldButton.ButtonNumber ) )
						{
							return true;
						}
					}
				}
			}
		}

		return false;
	}

	private void OnTimer( object? sender, EventArgs e )
	{
		var app = Instance;

		if ( app != null )
		{
			if ( !app.Simulator.IsConnected )
			{
				app.DirectInput.PollDevices( 1f );

				TriggerWorkerThread();
			}
		}
	}

	private static void WorkerThread()
	{
		var app = Instance;

		if ( app != null )
		{
			while ( app._running )
			{
				app._autoResetEvent.WaitOne();

				app.Dispatcher.BeginInvoke( () =>
				{
					app.RacingWheel.Tick( app );
					app.SettingsFile.Tick( app );
					app.AdminBoxx.Tick( app );
					app.Debug.Tick( app );
					app.ChatQueue.Tick( app );
					app.MainWindow.Tick( app );
					app.MultimediaTimer.Tick( app );
					app.Simulator.Tick( app );
					app.Graph.Tick( app );
				} );
			}
		}
	}
}
