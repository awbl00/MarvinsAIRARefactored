using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MarvinsAIRARefactored.Components;

// SteeringDeviceChanged = one of the device paths that arrived or left carries the steering device's USB ids
public sealed class DeviceListChangedEventArgs( bool steeringDeviceChanged ) : EventArgs
{
	public bool SteeringDeviceChanged { get; } = steeringDeviceChanged;
}

public sealed partial class HidHotPlugMonitor : IDisposable
{
	private HwndSource? _hwndSource;
	private HDEVNOTIFY _deviceNotifyHandle;
	private readonly Guid _hidInterfaceGuid = new( "{4D1E55B2-F16F-11CF-88CB-001111000030}" ); // GUID_DEVINTERFACE_HID

	private System.Timers.Timer? _debounceTimer;

	// Device paths that arrived or left since the last time DeviceListMightHaveChanged was raised. A rescan
	// runs on the telemetry thread right before the FFB burst and stalls it, so while the player is racing
	// the rescan is held back unless one of these paths is the steering device (see OnDebounceElapsed).
	private readonly List<string> _pendingDevicePaths = [];
	private bool _rescanDeferred = false;

	// USB paths carry "VID_xxxx&PID_xxxx"; Bluetooth paths carry "VID&ssssxxxx_PID&xxxx", where the first
	// four digits of the vendor field are the id source and the last four are the vendor id
	[GeneratedRegex( @"VID[_&]([0-9A-F]{4,8})[_&]PID[_&]([0-9A-F]{4})", RegexOptions.IgnoreCase )]
	private static partial Regex VendorAndProductIdRegex();

	public event EventHandler<DeviceListChangedEventArgs>? DeviceListMightHaveChanged;

	public void Initialize()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[HidHotPlugMonitor] Initialize >>>" );

		app.MainWindow.SourceInitialized += ( _, __ ) =>
		{
			var hwnd = new WindowInteropHelper( app.MainWindow ).Handle;

			app.Logger.WriteLine( $"[HidHotPlugMonitor] SourceInitialized hwnd=0x{hwnd.ToInt64():X}" );

			SetupForHwnd( hwnd );
		};

		// In some startup scenarios (start with Windows + start minimized) the
		// window handle may never be created via the normal show path, so
		// SourceInitialized may not fire. Ensure the HWND exists and register
		// for device notifications immediately on the UI thread.
		app.Dispatcher.BeginInvoke( () =>
		{
			var hwnd = new WindowInteropHelper( app.MainWindow ).EnsureHandle();

			app.Logger.WriteLine( $"[HidHotPlugMonitor] EnsureHandle hwnd=0x{hwnd.ToInt64():X}" );

			SetupForHwnd( hwnd );
		} );

		app.MainWindow.Closed += ( _, __ ) => Dispose();

		app.Logger.WriteLine( "[HidHotPlugMonitor] <<< Initialize" );
	}

	public void Dispose()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[HidHotPlugMonitor] Dispose >>>" );

		if ( _deviceNotifyHandle != HDEVNOTIFY.Null )
		{
			var unregisterSucceeded = PInvoke.UnregisterDeviceNotification( _deviceNotifyHandle );

			if ( unregisterSucceeded )
			{
				app.Logger.WriteLine( "[HidHotPlugMonitor] UnregisterDeviceNotification succeeded." );
			}
			else
			{
				var lastError = Marshal.GetLastPInvokeError();
				var lastErrorMessage = new Win32Exception( lastError ).Message;

				app.Logger.WriteLine( $"[HidHotPlugMonitor] UnregisterDeviceNotification failed. Error={lastError} ({lastErrorMessage})" );
			}

			_deviceNotifyHandle = HDEVNOTIFY.Null;
		}
		else
		{
			app.Logger.WriteLine( "[HidHotPlugMonitor] No device notification handle to unregister." );
		}

		_hwndSource?.RemoveHook( WndProc );

		_hwndSource = null;

		_debounceTimer?.Dispose();
		_debounceTimer = null;

		app.Logger.WriteLine( "[HidHotPlugMonitor] <<< Dispose" );
	}

	private unsafe void RegisterForHidNotifications( IntPtr hwnd )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[HidHotPlugMonitor] RegisterForHidNotifications >>>" );
		app.Logger.WriteLine( $"[HidHotPlugMonitor] Registering HID notifications for hwnd=0x{hwnd.ToInt64():X}, interfaceGuid={_hidInterfaceGuid}" );

		var deviceBroadcastInterface = new DEV_BROADCAST_DEVICEINTERFACE_W
		{
			dbcc_size = (uint) sizeof( DEV_BROADCAST_DEVICEINTERFACE_W ),
			dbcc_classguid = _hidInterfaceGuid,
			dbcc_devicetype = (uint) DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE
		};

		_deviceNotifyHandle = PInvoke.RegisterDeviceNotification(
			new HANDLE( hwnd ),
			&deviceBroadcastInterface,
			REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE );

		if ( _deviceNotifyHandle == HDEVNOTIFY.Null )
		{
			var lastError = Marshal.GetLastPInvokeError();
			var lastErrorMessage = new Win32Exception( lastError ).Message;

			app.Logger.WriteLine( $"[HidHotPlugMonitor] RegisterDeviceNotification failed. Error={lastError} ({lastErrorMessage})" );
		}
		else
		{
			app.Logger.WriteLine( "[HidHotPlugMonitor] RegisterDeviceNotification succeeded." );
		}

		app.Logger.WriteLine( "[HidHotPlugMonitor] <<< RegisterForHidNotifications" );
	}

	private void SetupForHwnd( IntPtr hwnd )
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[HidHotPlugMonitor] SetupForHwnd >>>" );

		if ( hwnd == IntPtr.Zero )
		{
			app.Logger.WriteLine( "[HidHotPlugMonitor] SetupForHwnd skipped because hwnd is zero." );
			app.Logger.WriteLine( "[HidHotPlugMonitor] <<< SetupForHwnd" );

			return;
		}

		if ( _hwndSource is not null )
		{
			app.Logger.WriteLine( $"[HidHotPlugMonitor] SetupForHwnd skipped because HwndSource already exists. hwnd=0x{hwnd.ToInt64():X}" );
			app.Logger.WriteLine( "[HidHotPlugMonitor] <<< SetupForHwnd" );

			return;
		}

		_hwndSource = HwndSource.FromHwnd( hwnd );

		if ( _hwndSource is null )
		{
			app.Logger.WriteLine( $"[HidHotPlugMonitor] HwndSource.FromHwnd returned null. hwnd=0x{hwnd.ToInt64():X}" );
			app.Logger.WriteLine( "[HidHotPlugMonitor] <<< SetupForHwnd" );

			return;
		}

		_hwndSource.AddHook( WndProc );

		app.Logger.WriteLine( $"[HidHotPlugMonitor] WndProc hook added. hwnd=0x{hwnd.ToInt64():X}" );

		RegisterForHidNotifications( hwnd );

		if ( _debounceTimer is null )
		{
			_debounceTimer = new System.Timers.Timer( 2000 ) { AutoReset = false };

			_debounceTimer.Elapsed += ( _, __ ) => OnDebounceElapsed();

			app.Logger.WriteLine( "[HidHotPlugMonitor] Device change debounce timer created." );
		}

		app.Logger.WriteLine( "[HidHotPlugMonitor] <<< SetupForHwnd" );
	}

	private void OnDebounceElapsed()
	{
		var app = App.Instance!;

		bool steeringDeviceChanged;
		bool steeringDeviceIdsKnown;

		lock ( _pendingDevicePaths )
		{
			steeringDeviceChanged = PendingChangesIncludeSteeringDevice( app, out steeringDeviceIdsKnown );
		}

		// without the steering device's USB ids we cannot rule it out, so the rescan goes ahead
		if ( app.RacingWheel.IsDrivingWithForceFeedback && steeringDeviceIdsKnown && !steeringDeviceChanged )
		{
			if ( !_rescanDeferred )
			{
				_rescanDeferred = true;

				app.Logger.WriteLine( "[HidHotPlugMonitor] Device change does not involve the steering device and the player is driving with force feedback. Deferring the device rescan until the player is off the track." );
			}

			// check again after another debounce interval
			_debounceTimer?.Start();

			return;
		}

		lock ( _pendingDevicePaths )
		{
			_pendingDevicePaths.Clear();
		}

		_rescanDeferred = false;

		app.Logger.WriteLine( $"[HidHotPlugMonitor] Device change debounce elapsed (steering device changed = {steeringDeviceChanged}). Raising DeviceListMightHaveChanged." );

		DeviceListMightHaveChanged?.Invoke( this, new DeviceListChangedEventArgs( steeringDeviceChanged ) );
	}

	// caller holds the _pendingDevicePaths lock
	private bool PendingChangesIncludeSteeringDevice( App app, out bool steeringDeviceIdsKnown )
	{
		var steeringDeviceGuid = DataContext.DataContext.Instance.Settings.RacingWheelSteeringDeviceGuid;

		steeringDeviceIdsKnown = app.DirectInput.TryGetUsbIds( steeringDeviceGuid, out var steeringVendorId, out var steeringProductId );

		if ( !steeringDeviceIdsKnown )
		{
			// no USB ids to compare against (nothing selected yet, or a virtual device)
			return false;
		}

		foreach ( var devicePath in _pendingDevicePaths )
		{
			var match = VendorAndProductIdRegex().Match( devicePath );

			if ( match.Success )
			{
				var vendorDigits = match.Groups[ 1 ].Value;

				var vendorId = Convert.ToUInt16( vendorDigits[ ^4.. ], 16 );
				var productId = Convert.ToUInt16( match.Groups[ 2 ].Value, 16 );

				if ( ( vendorId == steeringVendorId ) && ( productId == steeringProductId ) )
				{
					return true;
				}
			}
		}

		return false;
	}

	private IntPtr WndProc( IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled )
	{
		if ( msg == PInvoke.WM_DEVICECHANGE )
		{
			var app = App.Instance!;
			var eventType = wParam.ToInt32();

			app.Logger.WriteLine( $"[HidHotPlugMonitor] WM_DEVICECHANGE eventType=0x{eventType:X}, lParam=0x{lParam.ToInt64():X}" );

			if ( eventType == PInvoke.DBT_DEVICEARRIVAL || eventType == PInvoke.DBT_DEVICEREMOVECOMPLETE )
			{
				_debounceTimer?.Stop();
				_debounceTimer?.Start();

				if ( lParam == IntPtr.Zero )
				{
					app.Logger.WriteLine( "[HidHotPlugMonitor] Device change lParam is zero; no device path available." );

					return IntPtr.Zero;
				}

				var broadcastHeader = Marshal.PtrToStructure<DEV_BROADCAST_HDR>( lParam );

				app.Logger.WriteLine( $"[HidHotPlugMonitor] Device change broadcast type={broadcastHeader.dbch_devicetype}" );

				if ( broadcastHeader.dbch_devicetype == DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE )
				{
					var nameOffset = Marshal.OffsetOf<DEV_BROADCAST_DEVICEINTERFACE_W>(
						nameof( DEV_BROADCAST_DEVICEINTERFACE_W.dbcc_name ) ).ToInt32();

					var namePtr = IntPtr.Add( lParam, nameOffset );
					var devicePath = Marshal.PtrToStringUni( namePtr ) ?? string.Empty;

					lock ( _pendingDevicePaths )
					{
						_pendingDevicePaths.Add( devicePath );
					}

					if ( eventType == PInvoke.DBT_DEVICEREMOVECOMPLETE )
					{
						app.Logger.WriteLine( $"[HidHotPlugMonitor] Device {devicePath} was removed!" );
					}
					else
					{
						app.Logger.WriteLine( $"[HidHotPlugMonitor] Device {devicePath} was added!" );
					}
				}
				else
				{
					app.Logger.WriteLine( $"[HidHotPlugMonitor] Ignoring non-device-interface broadcast type={broadcastHeader.dbch_devicetype}" );
				}
			}
		}

		return IntPtr.Zero;
	}
}