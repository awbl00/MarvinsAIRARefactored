
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.Windows;

public partial class GraphWindow : Window
{
	private bool _isDraggable = false;

	private readonly OverlayWindowScaler _scaler;
	private readonly OverlayWindowMover _mover;

	public GraphWindow()
	{
		var app = App.Instance!;

		app.Logger.WriteLine( "[GraphWindow] Constructor >>>" );

		InitializeComponent();

		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		_scaler = new OverlayWindowScaler( this, ScaleIcon, () => settings.OverlaysGraphWindowScale, value => settings.OverlaysGraphWindowScale = value );
		_mover = new OverlayWindowMover( this, DragIcon );

		var rectangle = settings.OverlaysGraphWindowPosition;

		Left = rectangle.Location.X;
		Top = rectangle.Location.Y;

		WindowStartupLocation = WindowStartupLocation.Manual;

		// the graph component draws into its own bitmap and only records while this window is open
		app.Graph.Show( Image );

		MakeDraggable();

		Show();

		app.Logger.WriteLine( "[GraphWindow] <<< Constructor" );
	}

	protected override void OnClosed( EventArgs e )
	{
		App.Instance!.Graph.Hide();

		base.OnClosed( e );
	}

	private void Window_LocationChanged( object sender, EventArgs e )
	{
		if ( IsVisible && ( WindowState == WindowState.Normal ) )
		{
			var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

			var rectangle = settings.OverlaysGraphWindowPosition;

			rectangle.Location = new System.Drawing.Point( (int) RestoreBounds.Left, (int) RestoreBounds.Top );

			settings.OverlaysGraphWindowPosition = rectangle;
		}
	}

	public void ResetWindow()
	{
		Left = 0;
		Top = 0;
	}

	// Re-applies the window position from settings (used when the active overlay layout changes, e.g. on a car
	// change). Scale re-applies automatically through the XAML ScaleTransform binding, so only position is set here.
	public void ApplyPositionFromSettings()
	{
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		var rectangle = settings.OverlaysGraphWindowPosition;

		Left = rectangle.Location.X;
		Top = rectangle.Location.Y;
	}

	public void MakeDraggable()
	{
		_isDraggable = App.Instance!.OverlaysDraggable;

		ScaleIcon.Visibility = _isDraggable ? Visibility.Visible : Visibility.Collapsed;
		GearIcon.Visibility = _isDraggable ? Visibility.Visible : Visibility.Collapsed;
		DragIcon.Visibility = _isDraggable ? Visibility.Visible : Visibility.Collapsed;

		var hwnd = new WindowInteropHelper( this ).Handle;

		var exStyle = PInvoke.GetWindowLong( (HWND) hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE );

		if ( _isDraggable )
		{
			exStyle &= ~(int) WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
		}
		else
		{
			exStyle |= (int) WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
		}

		_ = PInvoke.SetWindowLong( (HWND) hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle );
	}

	private void Window_PreviewMouseLeftButtonDown( object sender, MouseButtonEventArgs e )
	{
		if ( ReferenceEquals( e.OriginalSource, ScaleIcon ) )
		{
			_scaler.Start();

			e.Handled = true;
		}
		else if ( ReferenceEquals( e.OriginalSource, DragIcon ) )
		{
			_mover.Start();

			e.Handled = true;
		}
		else if ( ReferenceEquals( e.OriginalSource, GearIcon ) )
		{
			e.Handled = true;

			Dispatcher.BeginInvoke( OpenOverlaySettings );
		}
	}

	private void Window_PreviewMouseLeftButtonUp( object sender, MouseButtonEventArgs e )
	{
		if ( _scaler.IsScaling )
		{
			_scaler.Stop();

			e.Handled = true;
		}
		else if ( _mover.IsMoving )
		{
			_mover.Stop();

			e.Handled = true;
		}
	}

	private void Window_MouseMove( object sender, System.Windows.Input.MouseEventArgs e )
	{
		_scaler.Update();
		_mover.Update();
	}

	private void OpenOverlaySettings()
	{
		var settings = MarvinsAIRARefactored.DataContext.DataContext.Instance.Settings;

		var overlaySettingsWindow = new OverlaySettingsWindow(
			colorEnabled: true,
			getColor: () => settings.OverlaysGraphWindowBackgroundColor,
			setColor: value => settings.OverlaysGraphWindowBackgroundColor = value,
			defaultColor: "#000000",
			getOpacity: () => settings.OverlaysGraphWindowBackgroundOpacity,
			setOpacity: value => settings.OverlaysGraphWindowBackgroundOpacity = value,
			defaultOpacity: 1f )
		{
			Owner = this
		};

		overlaySettingsWindow.ShowDialog();
	}
}
