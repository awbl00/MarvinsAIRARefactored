﻿
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using MarvinsAIRARefactored.Classes;
using MarvinsAIRARefactored.Enums;

namespace MarvinsAIRARefactored.DataContext;

public class Settings : INotifyPropertyChanged
{
	#region INotifyProperty stuff

	public event PropertyChangedEventHandler? PropertyChanged;

	public void OnPropertyChanged( [CallerMemberName] string? propertyName = null )
	{
		var app = App.Instance;

		if ( app != null )
		{
			if ( ( propertyName != null ) && !propertyName.EndsWith( "String" ) )
			{
				var property = GetType().GetProperty( propertyName );

				if ( property != null )
				{
					app.Logger.WriteLine( $"[Settings] {propertyName} = {property.GetValue( this )}" );

					var contextSwitchesPropertyName = $"{propertyName}ContextSwitches";

					var contextSwitchesProperty = GetType().GetProperty( contextSwitchesPropertyName );

					if ( contextSwitchesProperty != null )
					{
						var contextSwitches = (ContextSwitches?) contextSwitchesProperty.GetValue( this );

						if ( contextSwitches != null )
						{
							var context = new Context( contextSwitches );

							var contextSettings = FindContextSettings( context );

							UpdateToContextSettings( contextSettings );
						}
					}
				}
			}

			app.SettingsFile.QueueForSerialization = true;
		}

		PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
	}

	#endregion

	#region Context settings

	public SerializableDictionary<Context, ContextSettings> ContextSettingsDictionary { get; set; } = [];

	public void UpdateFromContextSettings()
	{
		var app = App.Instance;

		if ( app != null )
		{
			var destinationProperties = typeof( Settings ).GetProperties( BindingFlags.Public | BindingFlags.Instance );

			foreach ( var destinationProperty in destinationProperties )
			{
				if ( destinationProperty.CanWrite && !destinationProperty.Name.EndsWith( "String" ) )
				{
					var contextSwitchesPropertyName = $"{destinationProperty.Name}ContextSwitches";

					var contextSwitchesProperty = GetType().GetProperty( contextSwitchesPropertyName );

					if ( contextSwitchesProperty != null )
					{
						var contextSwitches = (ContextSwitches?) contextSwitchesProperty.GetValue( this );

						if ( contextSwitches != null )
						{
							var context = new Context( contextSwitches );

							var contextSettings = FindContextSettings( context );

							var sourceProperty = typeof( ContextSettings ).GetProperty( destinationProperty.Name );

							if ( sourceProperty != null )
							{
								var value = sourceProperty.GetValue( contextSettings );

								app.Logger.WriteLine( $"[Settings] Setting {destinationProperty.Name} = {value} ({context.WheelbaseGuid}|{context.CarName}|{context.TrackName}|{context.TrackConfigurationName}|{context.WetDryName})" );

								destinationProperty.SetValue( this, value );
							}
						}
					}
				}
			}
		}
	}

	private ContextSettings FindContextSettings( Context context )
	{
		if ( !ContextSettingsDictionary.TryGetValue( context, out var contextSettings ) )
		{
			contextSettings = new ContextSettings();

			UpdateToContextSettings( contextSettings );

			ContextSettingsDictionary.Add( context, contextSettings );
		}

		return contextSettings;
	}

	private void UpdateToContextSettings( ContextSettings contextSettings )
	{
		var sourceProperties = typeof( Settings ).GetProperties( BindingFlags.Public | BindingFlags.Instance );

		var destinationProperties = typeof( ContextSettings ).GetProperties( BindingFlags.Public | BindingFlags.Instance );

		foreach ( var sourceProperty in sourceProperties )
		{
			if ( sourceProperty.CanRead )
			{
				var destinationProperty = Array.Find( destinationProperties, p => p.Name == sourceProperty.Name && p.CanWrite && p.PropertyType.IsAssignableFrom( sourceProperty.PropertyType ) );

				if ( destinationProperty != null )
				{
					var value = sourceProperty.GetValue( this );

					destinationProperty.SetValue( contextSettings, value );
				}
			}
		}
	}

	#endregion

	#region Racing wheel - Device

	private Guid _racingWheelDeviceGuid = Guid.Empty;

	public Guid RacingWheelDeviceGuid
	{
		get => _racingWheelDeviceGuid;

		set
		{
			if ( value != _racingWheelDeviceGuid )
			{
				_racingWheelDeviceGuid = value;

				OnPropertyChanged();

				var app = App.Instance;

				if ( app != null )
				{
					app.RacingWheel.NextRacingWheelGuid = _racingWheelDeviceGuid;
				}
			}
		}
	}

	#endregion

	#region Racing wheel - Enable force feedback

	private bool _racingWheelEnableForceFeedback = true;

	public bool RacingWheelEnableForceFeedback
	{
		get => _racingWheelEnableForceFeedback;

		set
		{
			if ( value != _racingWheelEnableForceFeedback )
			{
				_racingWheelEnableForceFeedback = value;

				OnPropertyChanged();
			}

			var app = App.Instance;

			app?.MainWindow.UpdateRacingWheelPowerButton();
		}
	}

	public ContextSwitches RacingWheelEnableForceFeedbackContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelEnableForceFeedbackMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Test

	public ButtonMappings RacingWheelTestButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Reset

	public ButtonMappings RacingWheelResetButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Max force

	private float _racingWheelMaxForce = 50f;

	public float RacingWheelMaxForce
	{
		get => _racingWheelMaxForce;

		set
		{
			value = Math.Clamp( value, 5f, 99.9f );

			if ( value != _racingWheelMaxForce )
			{
				_racingWheelMaxForce = value;

				OnPropertyChanged();
			}

			RacingWheelMaxForceString = $"{_racingWheelMaxForce:F1}{DataContext.Instance.Localization[ "TorqueUnits" ]}";

			// Force the display values to update for the zeAlanLeTwist algorithm
			DataContext.Instance.Settings.RacingWheelTorqueAccelThreshold = DataContext.Instance.Settings.RacingWheelTorqueAccelThreshold;
			DataContext.Instance.Settings.RacingWheelTotalTorqueThreshold = DataContext.Instance.Settings.RacingWheelTotalTorqueThreshold;
		}
	}

	private string _racingWheelMaxForceString = string.Empty;

	[XmlIgnore]
	public string RacingWheelMaxForceString
	{
		get => _racingWheelMaxForceString;

		set
		{
			if ( value != _racingWheelMaxForceString )
			{
				_racingWheelMaxForceString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelMaxForceContextSwitches { get; set; } = new( true, true, true, false, false );
	public ButtonMappings RacingWheelMaxForcePlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelMaxForceMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Auto margin

	private float _racingWheelAutoMargin = 0f;

	public float RacingWheelAutoMargin
	{
		get => _racingWheelAutoMargin;

		set
		{
			value = Math.Clamp( value, -1f, 1f );

			if ( value != _racingWheelAutoMargin )
			{
				_racingWheelAutoMargin = value;

				OnPropertyChanged();
			}

			RacingWheelAutoMarginString = $"{_racingWheelAutoMargin * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _racingWheelAutoMarginString = string.Empty;

	[XmlIgnore]
	public string RacingWheelAutoMarginString
	{
		get => _racingWheelAutoMarginString;

		set
		{
			if ( value != _racingWheelAutoMarginString )
			{
				_racingWheelAutoMarginString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelAutoMarginContextSwitches { get; set; } = new( true, false, false, false, false );
	public ButtonMappings RacingWheelAutoMarginPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelAutoMarginMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Auto

	public ButtonMappings RacingWheelAutoButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Clear

	public ButtonMappings RacingWheelClearButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Algorithm

	private RacingWheelAlgorithmEnum _racingWheelAlgorithm = RacingWheelAlgorithmEnum.DetailBooster;

	public RacingWheelAlgorithmEnum RacingWheelAlgorithm
	{
		get => _racingWheelAlgorithm;

		set
		{
			if ( value != _racingWheelAlgorithm )
			{
				_racingWheelAlgorithm = value;

				OnPropertyChanged();
			}

			var app = App.Instance;

			app?.MainWindow.UpdateRacingWheelAlgorithmControls();
		}
	}

	public ContextSwitches RacingWheelAlgorithmContextSwitches { get; set; } = new( false, false, false, false, false );

	#endregion

	#region Racing wheel - Detail boost

	private float _racingWheelDetailBoost = 0f;

	public float RacingWheelDetailBoost
	{
		get => _racingWheelDetailBoost;

		set
		{
			value = Math.Clamp( value, 0f, 9.99f );

			if ( value != _racingWheelDetailBoost )
			{
				_racingWheelDetailBoost = value;

				OnPropertyChanged();
			}

			RacingWheelDetailBoostString = $"{_racingWheelDetailBoost * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _racingWheelDetailBoostString = string.Empty;

	[XmlIgnore]
	public string RacingWheelDetailBoostString
	{
		get => _racingWheelDetailBoostString;

		set
		{
			if ( value != _racingWheelDetailBoostString )
			{
				_racingWheelDetailBoostString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelDetailBoostContextSwitches { get; set; } = new( true, true, false, false, false );
	public ButtonMappings RacingWheelDetailBoostPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelDetailBoostMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Delta limit

	private float _racingWheelDeltaLimit = 1f;

	public float RacingWheelDeltaLimit
	{
		get => _racingWheelDeltaLimit;

		set
		{
			value = Math.Clamp( value, 0f, 99.99f );

			if ( value != _racingWheelDeltaLimit )
			{
				_racingWheelDeltaLimit = value;

				OnPropertyChanged();
			}

			RacingWheelDeltaLimitString = $"{_racingWheelDeltaLimit:F2}{DataContext.Instance.Localization[ "DeltaLimitUnits" ]}";
		}
	}

	private string _racingWheelDeltaLimitString = string.Empty;

	[XmlIgnore]
	public string RacingWheelDeltaLimitString
	{
		get => _racingWheelDeltaLimitString;

		set
		{
			if ( value != _racingWheelDeltaLimitString )
			{
				_racingWheelDeltaLimitString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelDeltaLimitContextSwitches { get; set; } = new( true, true, false, false, false );
	public ButtonMappings RacingWheelDeltaLimitPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelDeltaLimitMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Bias

	private float _racingWheelBias = 0.1f;

	public float RacingWheelBias
	{
		get => _racingWheelBias;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelBias )
			{
				_racingWheelBias = value;

				OnPropertyChanged();
			}

			RacingWheelBiasString = $"{_racingWheelBias * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _racingWheelBiasString = string.Empty;

	[XmlIgnore]
	public string RacingWheelBiasString
	{
		get => _racingWheelBiasString;

		set
		{
			if ( value != _racingWheelBiasString )
			{
				_racingWheelBiasString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelBiasContextSwitches { get; set; } = new( true, true, false, false, false );
	public ButtonMappings RacingWheelBiasPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelBiasMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Torque Acceleration Threshold

	private float _racingWheelTorqueAccelThreshold = 200f;
	public float RacingWheelTorqueAccelThreshold
	{
		get => _racingWheelTorqueAccelThreshold;

		set
		{
			value = Math.Clamp(value, 100f, 30000f);

			if (value != _racingWheelTorqueAccelThreshold)
			{
				_racingWheelTorqueAccelThreshold = value;

				OnPropertyChanged();
			}

			RacingWheelTorqueAccelThresholdString = $"{_racingWheelTorqueAccelThreshold / 100f * DataContext.Instance.Settings.RacingWheelMaxForce:F1} {DataContext.Instance.Localization["TorqueAccelUnits"]}";
		}
	}

	private string _racingWheelTorqueAccelThresholdString = string.Empty;

	[XmlIgnore]
	public string RacingWheelTorqueAccelThresholdString
	{
		get => _racingWheelTorqueAccelThresholdString;

		set
		{
			if (value != _racingWheelTorqueAccelThresholdString)
			{
				_racingWheelTorqueAccelThresholdString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelTorqueAccelThresholdContextSwitches { get; set; } = new(true, true, false, false, false);
	public ButtonMappings RacingWheelTorqueAccelThresholdPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelTorqueAccelThresholdMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Torque Acceleration Scale

	private float _racingWheelTorqueAccelScale = 35f;

	public float RacingWheelTorqueAccelScale
	{
		get => _racingWheelTorqueAccelScale;

		set
		{
			value = Math.Clamp(value, 0f, 100f);

			if (value != _racingWheelTorqueAccelScale)
			{
				_racingWheelTorqueAccelScale = value;

				OnPropertyChanged();
			}

			RacingWheelTorqueAccelScaleString = $"{_racingWheelTorqueAccelScale:F0}%";
		}
	}

	private string _racingWheelTorqueAccelScaleString = string.Empty;

	[XmlIgnore]
	public string RacingWheelTorqueAccelScaleString
	{
		get => _racingWheelTorqueAccelScaleString;

		set
		{
			if (value != _racingWheelTorqueAccelScaleString)
			{
				_racingWheelTorqueAccelScaleString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelTorqueAccelScaleContextSwitches { get; set; } = new(true, true, false, false, false);
	public ButtonMappings RacingWheelTorqueAccelScalePlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelTorqueAccelScaleMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Total Torque Threshold

	private float _racingWheelTotalTorqueThreshold = 65f;

	public float RacingWheelTotalTorqueThreshold
	{
		get => _racingWheelTotalTorqueThreshold;

		set
		{
			value = Math.Clamp(value, 0f, 100f);

			if (value != _racingWheelTotalTorqueThreshold)
			{
				_racingWheelTotalTorqueThreshold = value;

				OnPropertyChanged();
			}

			RacingWheelTotalTorqueThresholdString = $"{_racingWheelTotalTorqueThreshold / 100f * DataContext.Instance.Settings.RacingWheelMaxForce:F1} {DataContext.Instance.Localization["TorqueUnits"]}";
		}
	}

	private string _racingWheelTotalTorqueThresholdString = string.Empty;

	[XmlIgnore]
	public string RacingWheelTotalTorqueThresholdString
	{
		get => _racingWheelTotalTorqueThresholdString;

		set
		{
			if (value != _racingWheelTotalTorqueThresholdString)
			{
				_racingWheelTotalTorqueThresholdString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelTotalTorqueThresholdContextSwitches { get; set; } = new(true, true, false, false, false);
	public ButtonMappings RacingWheelTotalTorqueThresholdPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelTotalTorqueThresholdMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Total Torque Scale

	private float _racingWheelTotalTorqueScale = 75f;

	public float RacingWheelTotalTorqueScale
	{
		get => _racingWheelTotalTorqueScale;

		set
		{
			value = Math.Clamp( value, 0f, 100f );

			if ( value != _racingWheelTotalTorqueScale )
			{
				_racingWheelTotalTorqueScale = value;

				OnPropertyChanged();
			}

			RacingWheelTotalTorqueScaleString = $"{_racingWheelTotalTorqueScale:F0}%";
		}
	}

	private string _racingWheelTotalTorqueScaleString = string.Empty;

	[XmlIgnore]
	public string RacingWheelTotalTorqueScaleString
	{
		get => _racingWheelTotalTorqueScaleString;

		set
		{
			if ( value != _racingWheelTotalTorqueScaleString )
			{
				_racingWheelTotalTorqueScaleString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelTotalTorqueScaleContextSwitches { get; set; } = new( true, true, false, false, false );
	public ButtonMappings RacingWheelTotalTorqueScalePlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelTotalTorqueScaleMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Crash protection g-force

	private float _racingWheelCrashProtectionGForce = 8f;

	public float RacingWheelCrashProtectionGForce
	{
		get => _racingWheelCrashProtectionGForce;

		set
		{
			value = Math.Clamp( value, 2f, 20f );

			if ( value != _racingWheelCrashProtectionGForce )
			{
				_racingWheelCrashProtectionGForce = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCrashProtectionGForce == 0f )
			{
				RacingWheelCrashProtectionGForceString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCrashProtectionGForceString = $"{_racingWheelCrashProtectionGForce:F1}{DataContext.Instance.Localization[ "GForceUnits" ]}";
			}
		}
	}

	private string _racingWheelCrashProtectionGForceString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCrashProtectionGForceString
	{
		get => _racingWheelCrashProtectionGForceString;

		set
		{
			if ( value != _racingWheelCrashProtectionGForceString )
			{
				_racingWheelCrashProtectionGForceString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCrashProtectionGForceContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCrashProtectionGForcePlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCrashProtectionGForceMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Crash protection duration

	private float _racingWheelCrashProtectionDuration = 1f;

	public float RacingWheelCrashProtectionDuration
	{
		get => _racingWheelCrashProtectionDuration;

		set
		{
			value = Math.Clamp( value, 0f, 10f );

			if ( value != _racingWheelCrashProtectionDuration )
			{
				_racingWheelCrashProtectionDuration = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCrashProtectionDuration == 0f )
			{
				RacingWheelCrashProtectionDurationString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCrashProtectionDurationString = $"{_racingWheelCrashProtectionDuration:F1}{DataContext.Instance.Localization[ "SecondsUnits" ]}";
			}
		}
	}

	private string _racingWheelCrashProtectionDurationString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCrashProtectionDurationString
	{
		get => _racingWheelCrashProtectionDurationString;

		set
		{
			if ( value != _racingWheelCrashProtectionDurationString )
			{
				_racingWheelCrashProtectionDurationString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCrashProtectionDurationContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCrashProtectionDurationPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCrashProtectionDurationMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Crash protection force reduction

	private float _racingWheelCrashProtectionForceReduction = 0.95f;

	public float RacingWheelCrashProtectionForceReduction
	{
		get => _racingWheelCrashProtectionForceReduction;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelCrashProtectionForceReduction )
			{
				_racingWheelCrashProtectionForceReduction = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCrashProtectionForceReduction == 0f )
			{
				RacingWheelCrashProtectionForceReductionString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCrashProtectionForceReductionString = $"{_racingWheelCrashProtectionForceReduction * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
			}
		}
	}

	private string _racingWheelCrashProtectionForceReductionString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCrashProtectionForceReductionString
	{
		get => _racingWheelCrashProtectionForceReductionString;

		set
		{
			if ( value != _racingWheelCrashProtectionForceReductionString )
			{
				_racingWheelCrashProtectionForceReductionString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCrashProtectionForceReductionContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCrashProtectionForceReductionPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCrashProtectionForceReductionMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Curb protection shock velocity

	private float _racingWheelCurbProtectionShockVelocity = 0.5f;

	public float RacingWheelCurbProtectionShockVelocity
	{
		get => _racingWheelCurbProtectionShockVelocity;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelCurbProtectionShockVelocity )
			{
				_racingWheelCurbProtectionShockVelocity = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCurbProtectionShockVelocity == 0f )
			{
				RacingWheelCurbProtectionShockVelocityString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCurbProtectionShockVelocityString = $"{_racingWheelCurbProtectionShockVelocity:F2}{DataContext.Instance.Localization[ "ShockVelocityUnits" ]}";
			}
		}
	}

	private string _racingWheelCurbProtectionShockVelocityString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCurbProtectionShockVelocityString
	{
		get => _racingWheelCurbProtectionShockVelocityString;

		set
		{
			if ( value != _racingWheelCurbProtectionShockVelocityString )
			{
				_racingWheelCurbProtectionShockVelocityString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCurbProtectionShockVelocityContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCurbProtectionShockVelocityPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCurbProtectionShockVelocityMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Curb protection duration

	private float _racingWheelCurbProtectionDuration = 0.1f;

	public float RacingWheelCurbProtectionDuration
	{
		get => _racingWheelCurbProtectionDuration;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelCurbProtectionDuration )
			{
				_racingWheelCurbProtectionDuration = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCurbProtectionDuration == 0f )
			{
				RacingWheelCurbProtectionDurationString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCurbProtectionDurationString = $"{_racingWheelCurbProtectionDuration:F2}{DataContext.Instance.Localization[ "SecondsUnits" ]}";
			}
		}
	}

	private string _racingWheelCurbProtectionDurationString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCurbProtectionDurationString
	{
		get => _racingWheelCurbProtectionDurationString;

		set
		{
			if ( value != _racingWheelCurbProtectionDurationString )
			{
				_racingWheelCurbProtectionDurationString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCurbProtectionDurationContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCurbProtectionDurationPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCurbProtectionDurationMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Curb protection force reduction

	private float _racingWheelCurbProtectionForceReduction = 0.75f;

	public float RacingWheelCurbProtectionForceReduction
	{
		get => _racingWheelCurbProtectionForceReduction;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelCurbProtectionForceReduction )
			{
				_racingWheelCurbProtectionForceReduction = value;

				OnPropertyChanged();
			}

			if ( _racingWheelCurbProtectionForceReduction == 0f )
			{
				RacingWheelCurbProtectionForceReductionString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelCurbProtectionForceReductionString = $"{_racingWheelCurbProtectionForceReduction * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
			}
		}
	}

	private string _racingWheelCurbProtectionForceReductionString = string.Empty;

	[XmlIgnore]
	public string RacingWheelCurbProtectionForceReductionString
	{
		get => _racingWheelCurbProtectionForceReductionString;

		set
		{
			if ( value != _racingWheelCurbProtectionForceReductionString )
			{
				_racingWheelCurbProtectionForceReductionString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelCurbProtectionForceReductionContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings RacingWheelCurbProtectionForceReductionPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelCurbProtectionForceReductionMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Parked strength

	private float _racingWheelParkedStrength = 0.25f;

	public float RacingWheelParkedStrength
	{
		get => _racingWheelParkedStrength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelParkedStrength )
			{
				_racingWheelParkedStrength = value;

				OnPropertyChanged();
			}

			if ( _racingWheelParkedStrength == 1f )
			{
				RacingWheelParkedStrengthString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelParkedStrengthString = $"{_racingWheelParkedStrength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
			}
		}
	}

	private string _racingWheelParkedStrengthString = string.Empty;

	[XmlIgnore]
	public string RacingWheelParkedStrengthString
	{
		get => _racingWheelParkedStrengthString;

		set
		{
			if ( value != _racingWheelParkedStrengthString )
			{
				_racingWheelParkedStrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelParkedStrengthContextSwitches { get; set; } = new( true, true, false, false, false );
	public ButtonMappings RacingWheelParkedStrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelParkedStrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Soft lock strength

	private float _racingWheelSoftLockStrength = 0.25f;

	public float RacingWheelSoftLockStrength
	{
		get => _racingWheelSoftLockStrength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelSoftLockStrength )
			{
				_racingWheelSoftLockStrength = value;

				OnPropertyChanged();
			}

			if ( _racingWheelSoftLockStrength == 0f )
			{
				RacingWheelSoftLockStrengthString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelSoftLockStrengthString = $"{_racingWheelSoftLockStrength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
			}
		}
	}

	private string _racingWheelSoftLockStrengthString = string.Empty;

	[XmlIgnore]
	public string RacingWheelSoftLockStrengthString
	{
		get => _racingWheelSoftLockStrengthString;

		set
		{
			if ( value != _racingWheelSoftLockStrengthString )
			{
				_racingWheelSoftLockStrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelSoftLockStrengthContextSwitches { get; set; } = new( true, false, false, false, false );
	public ButtonMappings RacingWheelSoftLockStrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelSoftLockStrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Friction

	private float _racingWheelFriction = 0f;

	public float RacingWheelFriction
	{
		get => _racingWheelFriction;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _racingWheelFriction )
			{
				_racingWheelFriction = value;

				OnPropertyChanged();
			}

			if ( _racingWheelFriction == 0f )
			{
				RacingWheelFrictionString = DataContext.Instance.Localization[ "OFF" ];
			}
			else
			{
				RacingWheelFrictionString = $"{_racingWheelFriction * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
			}
		}
	}

	private string _racingWheelFrictionString = string.Empty;

	[XmlIgnore]
	public string RacingWheelFrictionString
	{
		get => _racingWheelFrictionString;

		set
		{
			if ( value != _racingWheelFrictionString )
			{
				_racingWheelFrictionString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelFrictionContextSwitches { get; set; } = new( true, false, false, false, false );
	public ButtonMappings RacingWheelFrictionPlusButtonMappings { get; set; } = new();
	public ButtonMappings RacingWheelFrictionMinusButtonMappings { get; set; } = new();

	#endregion

	#region Racing wheel - Fade enabled

	private bool _racingWheelFadeEnabled = true;

	public bool RacingWheelFadeEnabled
	{
		get => _racingWheelFadeEnabled;

		set
		{
			if ( value != _racingWheelFadeEnabled )
			{
				_racingWheelFadeEnabled = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches RacingWheelFadeEnabledContextSwitches { get; set; } = new( false, false, false, false, false );

	#endregion

	#region Pedals - Minimum frequency

	private float _pedalsMinimumFrequency = 15f;

	public float PedalsMinimumFrequency
	{
		get => _pedalsMinimumFrequency;

		set
		{
			value = Math.Clamp( value, 0f, 50f );

			if ( value != _pedalsMinimumFrequency )
			{
				_pedalsMinimumFrequency = value;

				OnPropertyChanged();

				PedalsMaximumFrequency = MathF.Max( PedalsMaximumFrequency, _pedalsMinimumFrequency );
			}

			PedalsMinimumFrequencyString = $"{_pedalsMinimumFrequency:F0}{DataContext.Instance.Localization[ "HertzUnits" ]}";
		}
	}

	private string _pedalsMinimumFrequencyString = string.Empty;

	[XmlIgnore]
	public string PedalsMinimumFrequencyString
	{
		get => _pedalsMinimumFrequencyString;

		set
		{
			if ( value != _pedalsMinimumFrequencyString )
			{
				_pedalsMinimumFrequencyString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsMinimumFrequencyContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsMinimumFrequencyPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsMinimumFrequencyMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Maximum frequency

	private float _pedalsMaximumFrequency = 35f;

	public float PedalsMaximumFrequency
	{
		get => _pedalsMaximumFrequency;

		set
		{
			value = Math.Clamp( value, 0f, 50f );

			if ( value != _pedalsMaximumFrequency )
			{
				_pedalsMaximumFrequency = value;

				OnPropertyChanged();

				PedalsMinimumFrequency = MathF.Min( PedalsMinimumFrequency, _pedalsMaximumFrequency );
			}

			PedalsMaximumFrequencyString = $"{_pedalsMaximumFrequency:F0}{DataContext.Instance.Localization[ "HertzUnits" ]}";
		}
	}

	private string _pedalsMaximumFrequencyString = string.Empty;

	[XmlIgnore]
	public string PedalsMaximumFrequencyString
	{
		get => _pedalsMaximumFrequencyString;

		set
		{
			if ( value != _pedalsMaximumFrequencyString )
			{
				_pedalsMaximumFrequencyString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsMaximumFrequencyContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsMaximumFrequencyPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsMaximumFrequencyMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Frequency curve

	private float _pedalsFrequencyCurve = 0f;

	public float PedalsFrequencyCurve
	{
		get => _pedalsFrequencyCurve;

		set
		{
			value = Math.Clamp( value, -1f, 1f );

			if ( value != _pedalsFrequencyCurve )
			{
				_pedalsFrequencyCurve = value;

				OnPropertyChanged();
			}

			PedalsFrequencyCurveString = $"{_pedalsFrequencyCurve * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsFrequencyCurveString = string.Empty;

	[XmlIgnore]
	public string PedalsFrequencyCurveString
	{
		get => _pedalsFrequencyCurveString;

		set
		{
			if ( value != _pedalsFrequencyCurveString )
			{
				_pedalsFrequencyCurveString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsFrequencyCurveContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsFrequencyCurvePlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsFrequencyCurveMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Minimum Amplitude

	private float _pedalsMinimumAmplitude = 0.18f;

	public float PedalsMinimumAmplitude
	{
		get => _pedalsMinimumAmplitude;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsMinimumAmplitude )
			{
				_pedalsMinimumAmplitude = value;

				OnPropertyChanged();

				PedalsMaximumAmplitude = MathF.Max( PedalsMaximumAmplitude, _pedalsMinimumAmplitude );
			}

			PedalsMinimumAmplitudeString = $"{_pedalsMinimumAmplitude * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsMinimumAmplitudeString = string.Empty;

	[XmlIgnore]
	public string PedalsMinimumAmplitudeString
	{
		get => _pedalsMinimumAmplitudeString;

		set
		{
			if ( value != _pedalsMinimumAmplitudeString )
			{
				_pedalsMinimumAmplitudeString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsMinimumAmplitudeContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsMinimumAmplitudePlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsMinimumAmplitudeMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Maximum Amplitude

	private float _pedalsMaximumAmplitude = 0.6f;

	public float PedalsMaximumAmplitude
	{
		get => _pedalsMaximumAmplitude;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsMaximumAmplitude )
			{
				_pedalsMaximumAmplitude = value;

				OnPropertyChanged();

				PedalsMinimumAmplitude = MathF.Min( PedalsMinimumAmplitude, _pedalsMaximumAmplitude );
			}

			PedalsMaximumAmplitudeString = $"{_pedalsMaximumAmplitude * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsMaximumAmplitudeString = string.Empty;

	[XmlIgnore]
	public string PedalsMaximumAmplitudeString
	{
		get => _pedalsMaximumAmplitudeString;

		set
		{
			if ( value != _pedalsMaximumAmplitudeString )
			{
				_pedalsMaximumAmplitudeString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsMaximumAmplitudeContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsMaximumAmplitudePlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsMaximumAmplitudeMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Amplitude curve

	private float _pedalsAmplitudeCurve = 0f;

	public float PedalsAmplitudeCurve
	{
		get => _pedalsAmplitudeCurve;

		set
		{
			value = Math.Clamp( value, -1f, 1f );

			if ( value != _pedalsAmplitudeCurve )
			{
				_pedalsAmplitudeCurve = value;

				OnPropertyChanged();
			}

			PedalsAmplitudeCurveString = $"{_pedalsAmplitudeCurve * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsAmplitudeCurveString = string.Empty;

	[XmlIgnore]
	public string PedalsAmplitudeCurveString
	{
		get => _pedalsAmplitudeCurveString;

		set
		{
			if ( value != _pedalsAmplitudeCurveString )
			{
				_pedalsAmplitudeCurveString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsAmplitudeCurveContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsAmplitudeCurvePlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsAmplitudeCurveMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Clutch effect 1

	private PedalEffectEnum _pedalsClutchEffect1 = PedalEffectEnum.GearChange;

	public PedalEffectEnum PedalsClutchEffect1
	{
		get => _pedalsClutchEffect1;

		set
		{
			if ( value != _pedalsClutchEffect1 )
			{
				_pedalsClutchEffect1 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect1ContextSwitches { get; set; } = new( false, false, false, false, false );

	#endregion

	#region Pedals - Clutch effect 1 strength

	private float _pedalsClutchEffect1Strength = 1f;

	public float PedalsClutchEffect1Strength
	{
		get => _pedalsClutchEffect1Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsClutchEffect1Strength )
			{
				_pedalsClutchEffect1Strength = value;

				OnPropertyChanged();
			}

			PedalsClutchEffect1StrengthString = $"{_pedalsClutchEffect1Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsClutchEffect1StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsClutchEffect1StrengthString
	{
		get => _pedalsClutchEffect1StrengthString;

		set
		{
			if ( value != _pedalsClutchEffect1StrengthString )
			{
				_pedalsClutchEffect1StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect1StrengthContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsClutchEffect1StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsClutchEffect1StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Clutch effect 2

	private PedalEffectEnum _pedalsClutchEffect2 = PedalEffectEnum.ClutchSlip;

	public PedalEffectEnum PedalsClutchEffect2
	{
		get => _pedalsClutchEffect2;

		set
		{
			if ( value != _pedalsClutchEffect2 )
			{
				_pedalsClutchEffect2 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect2ContextSwitches { get => PedalsClutchEffect1ContextSwitches; set => PedalsClutchEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Clutch effect 2 strength

	private float _pedalsClutchEffect2Strength = 1f;

	public float PedalsClutchEffect2Strength
	{
		get => _pedalsClutchEffect2Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsClutchEffect2Strength )
			{
				_pedalsClutchEffect2Strength = value;

				OnPropertyChanged();
			}

			PedalsClutchEffect2StrengthString = $"{_pedalsClutchEffect2Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsClutchEffect2StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsClutchEffect2StrengthString
	{
		get => _pedalsClutchEffect2StrengthString;

		set
		{
			if ( value != _pedalsClutchEffect2StrengthString )
			{
				_pedalsClutchEffect2StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect2StrengthContextSwitches { get => PedalsClutchEffect1StrengthContextSwitches; set => PedalsClutchEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsClutchEffect2StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsClutchEffect2StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Clutch effect 3

	private PedalEffectEnum _pedalsClutchEffect3 = PedalEffectEnum.SteeringEffects;

	public PedalEffectEnum PedalsClutchEffect3
	{
		get => _pedalsClutchEffect3;

		set
		{
			if ( value != _pedalsClutchEffect3 )
			{
				_pedalsClutchEffect3 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect3ContextSwitches { get => PedalsClutchEffect1ContextSwitches; set => PedalsClutchEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Clutch effect 3 strength

	private float _pedalsClutchEffect3Strength = 1f;

	public float PedalsClutchEffect3Strength
	{
		get => _pedalsClutchEffect3Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsClutchEffect3Strength )
			{
				_pedalsClutchEffect3Strength = value;

				OnPropertyChanged();
			}

			PedalsClutchEffect3StrengthString = $"{_pedalsClutchEffect3Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsClutchEffect3StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsClutchEffect3StrengthString
	{
		get => _pedalsClutchEffect3StrengthString;

		set
		{
			if ( value != _pedalsClutchEffect3StrengthString )
			{
				_pedalsClutchEffect3StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsClutchEffect3StrengthContextSwitches { get => PedalsClutchEffect1StrengthContextSwitches; set => PedalsClutchEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsClutchEffect3StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsClutchEffect3StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Brake effect 1

	private PedalEffectEnum _pedalsBrakeEffect1 = PedalEffectEnum.ABSEngaged;

	public PedalEffectEnum PedalsBrakeEffect1
	{
		get => _pedalsBrakeEffect1;

		set
		{
			if ( value != _pedalsBrakeEffect1 )
			{
				_pedalsBrakeEffect1 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect1ContextSwitches { get; set; } = new( false, false, false, false, false );

	#endregion

	#region Pedals - Brake effect 1 strength

	private float _pedalsBrakeEffect1Strength = 1f;

	public float PedalsBrakeEffect1Strength
	{
		get => _pedalsBrakeEffect1Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsBrakeEffect1Strength )
			{
				_pedalsBrakeEffect1Strength = value;

				OnPropertyChanged();
			}

			PedalsBrakeEffect1StrengthString = $"{_pedalsBrakeEffect1Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsBrakeEffect1StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsBrakeEffect1StrengthString
	{
		get => _pedalsBrakeEffect1StrengthString;

		set
		{
			if ( value != _pedalsBrakeEffect1StrengthString )
			{
				_pedalsBrakeEffect1StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect1StrengthContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsBrakeEffect1StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsBrakeEffect1StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Brake effect 2

	private PedalEffectEnum _pedalsBrakeEffect2 = PedalEffectEnum.WheelLock;

	public PedalEffectEnum PedalsBrakeEffect2
	{
		get => _pedalsBrakeEffect2;

		set
		{
			if ( value != _pedalsBrakeEffect2 )
			{
				_pedalsBrakeEffect2 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect2ContextSwitches { get => PedalsBrakeEffect1ContextSwitches; set => PedalsBrakeEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Brake effect 2 strength

	private float _pedalsBrakeEffect2Strength = 1f;

	public float PedalsBrakeEffect2Strength
	{
		get => _pedalsBrakeEffect2Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsBrakeEffect2Strength )
			{
				_pedalsBrakeEffect2Strength = value;

				OnPropertyChanged();
			}

			PedalsBrakeEffect2StrengthString = $"{_pedalsBrakeEffect2Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsBrakeEffect2StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsBrakeEffect2StrengthString
	{
		get => _pedalsBrakeEffect2StrengthString;

		set
		{
			if ( value != _pedalsBrakeEffect2StrengthString )
			{
				_pedalsBrakeEffect2StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect2StrengthContextSwitches { get => PedalsBrakeEffect1StrengthContextSwitches; set => PedalsBrakeEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsBrakeEffect2StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsBrakeEffect2StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Brake effect 3

	private PedalEffectEnum _pedalsBrakeEffect3 = PedalEffectEnum.SteeringEffects;

	public PedalEffectEnum PedalsBrakeEffect3
	{
		get => _pedalsBrakeEffect3;

		set
		{
			if ( value != _pedalsBrakeEffect3 )
			{
				_pedalsBrakeEffect3 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect3ContextSwitches { get => PedalsBrakeEffect1ContextSwitches; set => PedalsBrakeEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Brake effect 3 strength

	private float _pedalsBrakeEffect3Strength = 1f;

	public float PedalsBrakeEffect3Strength
	{
		get => _pedalsBrakeEffect3Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsBrakeEffect3Strength )
			{
				_pedalsBrakeEffect3Strength = value;

				OnPropertyChanged();
			}

			PedalsBrakeEffect3StrengthString = $"{_pedalsBrakeEffect3Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsBrakeEffect3StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsBrakeEffect3StrengthString
	{
		get => _pedalsBrakeEffect3StrengthString;

		set
		{
			if ( value != _pedalsBrakeEffect3StrengthString )
			{
				_pedalsBrakeEffect3StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsBrakeEffect3StrengthContextSwitches { get => PedalsBrakeEffect1StrengthContextSwitches; set => PedalsBrakeEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsBrakeEffect3StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsBrakeEffect3StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Throttle effect 1

	private PedalEffectEnum _pedalsThrottleEffect1 = PedalEffectEnum.WheelSpin;

	public PedalEffectEnum PedalsThrottleEffect1
	{
		get => _pedalsThrottleEffect1;

		set
		{
			if ( value != _pedalsThrottleEffect1 )
			{
				_pedalsThrottleEffect1 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect1ContextSwitches { get; set; } = new( false, false, false, false, false );

	#endregion

	#region Pedals - Throttle effect 1 strength

	private float _pedalsThrottleEffect1Strength = 1f;

	public float PedalsThrottleEffect1Strength
	{
		get => _pedalsThrottleEffect1Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsThrottleEffect1Strength )
			{
				_pedalsThrottleEffect1Strength = value;

				OnPropertyChanged();
			}

			PedalsThrottleEffect1StrengthString = $"{_pedalsThrottleEffect1Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsThrottleEffect1StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsThrottleEffect1StrengthString
	{
		get => _pedalsThrottleEffect1StrengthString;

		set
		{
			if ( value != _pedalsThrottleEffect1StrengthString )
			{
				_pedalsThrottleEffect1StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect1StrengthContextSwitches { get; set; } = new( false, false, false, false, false );
	public ButtonMappings PedalsThrottleEffect1StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsThrottleEffect1StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Throttle effect 2

	private PedalEffectEnum _pedalsThrottleEffect2 = PedalEffectEnum.NarrowRPM;

	public PedalEffectEnum PedalsThrottleEffect2
	{
		get => _pedalsThrottleEffect2;

		set
		{
			if ( value != _pedalsThrottleEffect2 )
			{
				_pedalsThrottleEffect2 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect2ContextSwitches { get => PedalsThrottleEffect1ContextSwitches; set => PedalsThrottleEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Throttle effect 2 strength

	private float _pedalsThrottleEffect2Strength = 1f;

	public float PedalsThrottleEffect2Strength
	{
		get => _pedalsThrottleEffect2Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsThrottleEffect2Strength )
			{
				_pedalsThrottleEffect2Strength = value;

				OnPropertyChanged();
			}

			PedalsThrottleEffect2StrengthString = $"{_pedalsThrottleEffect2Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsThrottleEffect2StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsThrottleEffect2StrengthString
	{
		get => _pedalsThrottleEffect2StrengthString;

		set
		{
			if ( value != _pedalsThrottleEffect2StrengthString )
			{
				_pedalsThrottleEffect2StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect2StrengthContextSwitches { get => PedalsThrottleEffect1StrengthContextSwitches; set => PedalsThrottleEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsThrottleEffect2StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsThrottleEffect2StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region Pedals - Throttle effect 3

	private PedalEffectEnum _pedalsThrottleEffect3 = PedalEffectEnum.None;

	public PedalEffectEnum PedalsThrottleEffect3
	{
		get => _pedalsThrottleEffect3;

		set
		{
			if ( value != _pedalsThrottleEffect3 )
			{
				_pedalsThrottleEffect3 = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect3ContextSwitches { get => PedalsThrottleEffect1ContextSwitches; set => PedalsThrottleEffect1ContextSwitches = value; }

	#endregion

	#region Pedals - Throttle effect 3 strength

	private float _pedalsThrottleEffect3Strength = 1f;

	public float PedalsThrottleEffect3Strength
	{
		get => _pedalsThrottleEffect3Strength;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _pedalsThrottleEffect3Strength )
			{
				_pedalsThrottleEffect3Strength = value;

				OnPropertyChanged();
			}

			PedalsThrottleEffect3StrengthString = $"{_pedalsThrottleEffect3Strength * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _pedalsThrottleEffect3StrengthString = string.Empty;

	[XmlIgnore]
	public string PedalsThrottleEffect3StrengthString
	{
		get => _pedalsThrottleEffect3StrengthString;

		set
		{
			if ( value != _pedalsThrottleEffect3StrengthString )
			{
				_pedalsThrottleEffect3StrengthString = value;

				OnPropertyChanged();
			}
		}
	}

	public ContextSwitches PedalsThrottleEffect3StrengthContextSwitches { get => PedalsThrottleEffect1StrengthContextSwitches; set => PedalsThrottleEffect1StrengthContextSwitches = value; }
	public ButtonMappings PedalsThrottleEffect3StrengthPlusButtonMappings { get; set; } = new();
	public ButtonMappings PedalsThrottleEffect3StrengthMinusButtonMappings { get; set; } = new();

	#endregion

	#region AdminBoxx - Brightness

	private float _adminBoxxBrightness = 0.15f;

	public float AdminBoxxBrightness
	{
		get => _adminBoxxBrightness;

		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( value != _adminBoxxBrightness )
			{
				_adminBoxxBrightness = value;

				OnPropertyChanged();
			}

			AdminBoxxBrightnessString = $"{_adminBoxxBrightness * 100f:F0}{DataContext.Instance.Localization[ "Percent" ]}";
		}
	}

	private string _adminBoxxBrightnessString = string.Empty;

	[XmlIgnore]
	public string AdminBoxxBrightnessString
	{
		get => _adminBoxxBrightnessString;

		set
		{
			if ( value != _adminBoxxBrightnessString )
			{
				_adminBoxxBrightnessString = value;

				OnPropertyChanged();
			}
		}
	}

	public ButtonMappings AdminBoxxBrightnessPlusButtonMappings { get; set; } = new();
	public ButtonMappings AdminBoxxBrightnessMinusButtonMappings { get; set; } = new();

	#endregion

	#region App - Current language code

	private string _appCurrentLanguageCode = "default";

	public string AppCurrentLanguageCode
	{
		get => _appCurrentLanguageCode;

		set
		{
			if ( value != _appCurrentLanguageCode )
			{
				_appCurrentLanguageCode = value;

				DataContext.Instance.Localization.LoadLanguage( value );

				OnPropertyChanged();

				var app = App.Instance;

				app?.MainWindow.RefreshWindow();

				Misc.ForcePropertySetters( this );
			}
		}
	}

	#endregion

	#region App - Topmost window enabled

	private bool _appTopmostWindowEnabled = false;

	public bool AppTopmostWindowEnabled
	{
		get => _appTopmostWindowEnabled;

		set
		{
			if ( value != _appTopmostWindowEnabled )
			{
				_appTopmostWindowEnabled = value;

				OnPropertyChanged();
			}

			var app = App.Instance;

			if ( app != null )
			{
				app.MainWindow.Topmost = _appTopmostWindowEnabled;
			}
		}
	}

	#endregion

	#region App - Check for updates

	private bool _appCheckForUpdates = true;

	public bool AppCheckForUpdates
	{
		get => _appCheckForUpdates;

		set
		{
			if ( value != _appCheckForUpdates )
			{
				_appCheckForUpdates = value;

				OnPropertyChanged();
			}
		}
	}

	#endregion

	#region App - Automatically download updates

	private bool _appAutomaticallyDownloadUpdates = true;

	public bool AppAutomaticallyDownloadUpdates
	{
		get => _appAutomaticallyDownloadUpdates;

		set
		{
			if ( value != _appAutomaticallyDownloadUpdates )
			{
				_appAutomaticallyDownloadUpdates = value;

				OnPropertyChanged();
			}
		}
	}

	#endregion

	#region Debug (temporary)

	public ButtonMappings DebugAlanLeResetMappings { get; set; } = new();
	public ButtonMappings DebugAlanLeDumpMappings { get; set; } = new();

	#endregion
}
