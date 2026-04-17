using System.Reflection;
using System.Resources;
using BizHawk.Bizware.Graphics;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES;

namespace NEShim;

public partial class MainForm : Form
{
	private Button btnStartGame;
	private Button btnOptions;
	private Button btnExit;
	private Panel pnlMenu;
	private Panel renderPanel;
	private Config initialConfig;


	private InputManager _inputManager;
	private DisplayManager _displayManager;
	private PresentationPanel _presentationPanel;
	private IEmulator _emulator;

	private bool _needsFullscreenOnLoad = true;
	private bool _cursorHidden = true;
	private Point _lastMouseAutoHidePos;
	private readonly Cursor _blankCursor;
	private bool _inFullscreen;
	private bool _framebufferResizedPending;
	private bool _exitRequestPending;
	private bool _windowClosedAndSafeToExitProcess;
	private Size _lastVideoSize;
	private Size _lastVirtualSize;
	private Point _windowedLocation;
	private IVideoProvider _currentVideoProvider = NullVideo.Instance;
	private ISoundProvider _currentSoundProvider = new NullSound(44100 / 60); // Reasonable default until we have a core instance

	public MainForm()
	{
		InitializeComponent();
		SetupForm();
		initialConfig = ConfigService.Load<Config>(".\\config.ini");
		Emulator = new NullEmulator();

		
		var assembly = typeof(MainForm).Assembly;
		_blankCursor =
			new Cursor(assembly.GetManifestResourceStream(assembly.GetName().Name + ".Resources.Cursor.ico"));

	}

	private void SetupForm()
	{
		// Set the form to full screen
		this.WindowState = FormWindowState.Maximized;
		this.FormBorderStyle = FormBorderStyle.None; // No window borders
		this.TopMost = true; // Keep the form on top of other applications

		// Optional: Set background image (or custom graphics for the game)
		// this.BackgroundImage = Image.FromFile("background.png"); // Your custom background image
		// this.BackgroundImageLayout = ImageLayout.Center;
		// this.BackColor = Color.Aqua;

		// Create and configure the on-screen menu
		pnlMenu = new Panel();
		pnlMenu.Dock = DockStyle.Fill;
		pnlMenu.BackgroundImage = Image.FromFile("background.png"); // Your custom background image
		pnlMenu.BackgroundImageLayout = ImageLayout.Zoom;
		pnlMenu.BackColor = Color.Aqua;

		// Create Start Game Button
		btnStartGame = new Button();
		btnStartGame.Text = "Start Game";
		btnStartGame.ForeColor = Color.White;
		btnStartGame.BackColor = Color.DarkBlue;
		btnStartGame.FlatStyle = FlatStyle.Flat;
		btnStartGame.Size = new Size(200, 50);
		btnStartGame.Location = new Point(100, 30);
		btnStartGame.Click += BtnStartGame_Click;

		// Create Options Button
		btnOptions = new Button();
		btnOptions.Text = "Options";
		btnOptions.ForeColor = Color.White;
		btnOptions.BackColor = Color.DarkGreen;
		btnOptions.FlatStyle = FlatStyle.Flat;
		btnOptions.Size = new Size(200, 50);
		btnOptions.Location = new Point(100, 90);
		btnOptions.Click += BtnOptions_Click;

		// Create Exit Button
		btnExit = new Button();
		btnExit.Text = "Exit";
		btnExit.ForeColor = Color.White;
		btnExit.BackColor = Color.DarkRed;
		btnExit.FlatStyle = FlatStyle.Flat;
		btnExit.Size = new Size(200, 50);
		btnExit.Location = new Point(100, 150);
		btnExit.Click += BtnExit_Click;

		// Add buttons to the panel
		pnlMenu.Controls.Add(btnStartGame);
		pnlMenu.Controls.Add(btnOptions);
		pnlMenu.Controls.Add(btnExit);
		this.Controls.Add(pnlMenu);

	}

	private MouseEventHandler emptyHandler = (sender, args) => { };

	private void BtnStartGame_Click(object sender, EventArgs e)
	{
		//TODO: replace with initialConfig
		var config = initialConfig;
		var glRenderer = GLRenderer.TryInitIGL(initialConfig.DispMethod, initialConfig);
		var presentationPanel = new PresentationPanel(config, glRenderer,
			(bool _) => { }, emptyHandler, emptyHandler, emptyHandler);

		var rom = File.ReadAllBytes("game.nes");
		var emulator = new QuickNES(rom, new QuickNES.QuickNESSettings(), new QuickNES.QuickNESSyncSettings());

		var inputManager = new InputManager();

		var displayManager =
			new DisplayManager(config, emulator, inputManager, glRenderer, presentationPanel, () => false);
		Controls.Add(presentationPanel);
		Controls.SetChildIndex(presentationPanel, -1);

		presentationPanel.Control.Paint += (o, e) =>
		{
			// I would like to trigger a repaint here, but this isn't done yet
		};

		_inputManager = inputManager;
		_displayManager = displayManager;
		_presentationPanel = presentationPanel;
		Emulator = emulator;
	}

	#region Bizhawk stuff to refactor

	private IEmulator Emulator
	{
		get => _emulator;

		set
		{
			_emulator = value;
			_currentVideoProvider = value.AsVideoProviderOrDefault();
			_currentSoundProvider = value.AsSoundProviderOrDefault();
		}
	}
	
	public int ProgramRunLoop()
	{
		// needs to be done late, after the log console snaps on top
		// fullscreen should snap on top even harder!
		if (_needsFullscreenOnLoad)
		{
			_needsFullscreenOnLoad = false;
			ToggleFullscreen();
		}

		LockMouse(initialConfig.CaptureMouse);

		// incantation required to get the program reliably on top of the console window
		// we might want it in ToggleFullscreen later, but here, it needs to happen regardless
		BringToFront();
		Activate();
		BringToFront();

		//TODO: need this?
		// InitializeFpsData();

		for (;;)
		{
			//TODO: need this?
			// Input.Instance.Update();

			// handle events and dispatch as a hotkey action, or a hotkey button, or an input button
			// ...but prepare haptics first, those get read in ProcessInput
			var finalHostController = _inputManager.ControllerInputCoalescer;
			_inputManager.ActiveController.PrepareHapticsForHost(finalHostController);
			ProcessInput(
				_hotkeyCoalescer,
				finalHostController,
				_inputManager.ClientControls.SearchBindings,
				_inputManager.ActiveController.HasBinding);
			_inputManager.ClientControls.LatchFromPhysical(_hotkeyCoalescer);

			_inputManager.ActiveController.LatchFromPhysical(finalHostController);

			if (initialConfig.N64UseCircularAnalogConstraint)
			{
				_inputManager.ActiveController.ApplyAxisConstraints("Natural Circle");
			}

			_inputManager.ActiveController.OR_FromLogical(_inputManager.ClickyVirtualPadController);
			_inputManager.AutoFireController.LatchFromPhysical(finalHostController);

			if (_inputManager.ClientControls["Autohold"])
			{
				_inputManager.ToggleStickies();
			}
			else if (_inputManager.ClientControls["Autofire"])
			{
				_inputManager.ToggleAutoStickies();
			}

			// autohold/autofire must not be affected by the following inputs
			_inputManager.ActiveController.Overrides(_inputManager.ButtonOverrideAdapter);

			// ext. tools don't yield per se, so just send them a GeneralUpdate
			Tools.GeneralUpdateActiveExtTools();

			StepRunLoop_Core();
			Render();
			StepRunLoop_Throttle();

			// HACK: RAIntegration might peek at memory during messages
			// we need this to allow memory access here, otherwise it will deadlock
			var raMemHack = (RA as RAIntegration)?.ThisIsTheRAMemHack();
			raMemHack?.Enter();

			CheckMessages();

			// RA == null possibly due MainForm Dispose disposing RA (which case Exit is not valid anymore)
			// RA != null possibly due to RA object being created (which case raMemHack is null, as RA was null before)
			if (RA is not null) raMemHack?.Exit();

			if (_exitRequestPending)
			{
				_exitRequestPending = false;
				Close();
			}

			if (IsDisposed || _windowClosedAndSafeToExitProcess)
			{
				break;
			}
		}

		Shutdown();
		return _exitCode;
	}

	public void Render()
	{
		if (Config.DispSpeedupFeatures == 0)
		{
			DisplayManager.DiscardApiHawkSurfaces();
			return;
		}

		var video = _currentVideoProvider;
		Size currVideoSize = new Size(video.BufferWidth, video.BufferHeight);
		Size currVirtualSize = new Size(video.VirtualWidth, video.VirtualHeight);


		bool resizeFramebuffer = currVideoSize != _lastVideoSize || currVirtualSize != _lastVirtualSize;

		bool isZero = currVideoSize.Width == 0 || currVideoSize.Height == 0 || currVirtualSize.Width == 0 ||
		              currVirtualSize.Height == 0;

		//don't resize if the new size is 0 somehow; we'll wait until we have a sensible size
		if (isZero)
		{
			resizeFramebuffer = false;
		}

		if (resizeFramebuffer)
		{
			_lastVideoSize = currVideoSize;
			_lastVirtualSize = currVirtualSize;
			FrameBufferResized();
		}

		//rendering flakes out egregiously if we have a zero size
		//can we fix it later not to?
		if (isZero)
			_displayManager.Blank();
		else
			_displayManager.UpdateSource(video);
	}

	public void ToggleFullscreen(bool allowSuppress = false)
	{
		AutohideCursor(hide: false);

		// prohibit this operation if the current controls include LMouse
		if (allowSuppress)
		{
			if (_inputManager.ActiveController.HasBinding("WMouse L"))
			{
				return;
			}
		}

		if (!_inFullscreen)
		{
			SuspendLayout();

			// Work around an AMD driver bug in >= vista:
			// It seems windows will activate opengl fullscreen mode when a GL control is occupying the exact space of a screen (0,0 and dimensions=screensize)
			// AMD cards manifest a problem under these circumstances, flickering other monitors.
			// It isn't clear whether nvidia cards are failing to employ this optimization, or just not flickering.
			// (this could be determined with more work; other side affects of the fullscreen mode include: corrupted TaskBar, no modal boxes on top of GL control, no screenshots)
			// At any rate, we can solve this by adding a 1px black border around the GL control
			// Please note: It is important to do this before resizing things, otherwise momentarily a GL control without WS_BORDER will be at the magic dimensions and cause the flakeout
			if (!OSTailoredCode.IsUnixHost
			    && initialConfig.DispFullscreenHacks
			    && initialConfig.DispMethod == EDispMethod.OpenGL)
			{
				// ATTENTION: this causes the StatusBar to not work well, since the backcolor is now set to black instead of SystemColors.Control.
				// It seems that some StatusBar elements composite with the backcolor.
				// Maybe we could add another control under the StatusBar. with a different backcolor
				Padding = new Padding(1);
				BackColor = Color.Black;

				// FUTURE WORK:
				// re-add this padding back into the display manager (so the image will get cut off a little but, but a few more resolutions will fully fit into the screen)
			}

			_windowedLocation = Location;

			_inFullscreen = true;
			// SynchChrome();
			WindowState =
				FormWindowState.Maximized; // be sure to do this after setting the chrome, otherwise it wont work fully
			ResumeLayout();

			_presentationPanel.Resized = true;
		}
		else
		{
			SuspendLayout();

			WindowState = FormWindowState.Normal;

			if (!OSTailoredCode.IsUnixHost)
			{
				// do this even if DispFullscreenHacks aren't enabled, to restore it in case it changed underneath us or something
				Padding = new Padding(0);

				// it's important that we set the form color back to this, because the StatusBar icons blend onto the mainform, not onto the StatusBar--
				// so we need the StatusBar and mainform backdrop color to match
				BackColor = SystemColors.Control;
			}

			_inFullscreen = false;

			// SynchChrome();
			Location = _windowedLocation;
			ResumeLayout();

			FrameBufferResized();
		}
	}

	public void FrameBufferResized(bool forceWindowResize = false)
	{
		if (WindowState is not FormWindowState.Normal)
		{
			// Wait until no longer maximized/minimized to get correct size/location values
			_framebufferResizedPending = true;
			return;
		}

		if (!initialConfig.ResizeWithFramebuffer && !forceWindowResize)
		{
			return;
		}

		// run this entire thing exactly twice, since the first resize may adjust the menu stacking
		void DoPresentationPanelResize()
		{
			int zoom = initialConfig.GetWindowScaleFor(Emulator.SystemId);
			var area = Screen.FromControl(this).WorkingArea;

			int borderWidth = Size.Width - _presentationPanel.Control.Size.Width;
			int borderHeight = Size.Height - _presentationPanel.Control.Size.Height;

			// start at target zoom and work way down until we find acceptable zoom
			Size lastComputedSize = new Size(1, 1);
			for (; zoom >= 1; zoom--)
			{
				lastComputedSize = _displayManager.CalculateClientSize(_currentVideoProvider, zoom);
				if (lastComputedSize.Width + borderWidth < area.Width
				    && lastComputedSize.Height + borderHeight < area.Height)
				{
					break;
				}
			}

//				Util.DebugWriteLine($"For emulator framebuffer {new Size(_currentVideoProvider.BufferWidth, _currentVideoProvider.BufferHeight)}:");
//				Util.DebugWriteLine($"  For virtual size {new Size(_currentVideoProvider.VirtualWidth, _currentVideoProvider.VirtualHeight)}:");
//				Util.DebugWriteLine($"  Selecting display size {lastComputedSize}");

			// Change size
			Size = new Size(lastComputedSize.Width + borderWidth, lastComputedSize.Height + borderHeight);
			PerformLayout();
			_presentationPanel.Resized = true;

			// Is window off the screen at this size?
			if (!area.Contains(Bounds))
			{
				// At large framebuffer sizes/low screen resolutions, the window may be too large to fit the screen even at 1x scale
				// Prioritize that the top-left of the window is on-screen so the title bar and menu stay accessible

				if (Bounds.Right > area.Right) // Window is off the right edge
				{
					Left = Math.Max(area.Right - Size.Width, area.Left);
				}

				if (Bounds.Bottom > area.Bottom) // Window is off the bottom edge
				{
					Top = Math.Max(area.Bottom - Size.Height, area.Top);
				}
			}
		}

		DoPresentationPanelResize();
		DoPresentationPanelResize();
	}

	/*private void SynchChrome()
	{
		if (_inFullscreen)
		{
			// TODO - maybe apply a hack tracked during fullscreen here to override it
			FormBorderStyle = FormBorderStyle.None;
			MainMenuStrip.Visible = initialConfig.DispChromeMenuFullscreen && !_argParser._chromeless;
			MainStatusBar.Visible = initialConfig.DispChromeStatusBarFullscreen && !_argParser._chromeless;
		}
		else
		{
			MainStatusBar.Visible = initialConfig.DispChromeStatusBarWindowed && !_argParser._chromeless;
			MainMenuStrip.Visible = initialConfig.DispChromeMenuWindowed && !_argParser._chromeless;
			MaximizeBox = MinimizeBox = initialConfig.DispChromeCaptionWindowed && !_argParser._chromeless;
			if (initialConfig.DispChromeFrameWindowed == 0 || _argParser._chromeless)
			{
				FormBorderStyle = FormBorderStyle.None;
			}
			else if (initialConfig.DispChromeFrameWindowed == 1)
			{
				FormBorderStyle = FormBorderStyle.SizableToolWindow;
			}
			else if (initialConfig.DispChromeFrameWindowed == 2)
			{
				FormBorderStyle = FormBorderStyle.Sizable;
			}
		}
	}*/

	private void AutohideCursor(bool hide, bool alwaysUpdate = true)
	{
		var mousePos = MousePosition;
		// avoid sensitive mice unhiding the mouse cursor
		var shouldUpdateCursor = alwaysUpdate
		                         || Math.Abs(_lastMouseAutoHidePos.X - mousePos.X) > 5
		                         || Math.Abs(_lastMouseAutoHidePos.Y - mousePos.Y) > 5;

		_lastMouseAutoHidePos = mousePos;
		if (hide && !_cursorHidden)
		{
			// this only works assuming the mouse is perfectly still
			// if the mouse is slightly moving, it will use the "moving" cursor rather
			_presentationPanel.Control.Cursor = _blankCursor;

			// This will actually fully hide the cursor
			// However, this is a no-op on Mono, so we need to do both ways
			Cursor.Hide();

			_cursorHidden = true;
		}
		// else if (!hide && _cursorHidden)
		// {
		//  _presentationPanel.Control.Cursor = Cursors.Default;
		//  Cursor.Show();
		//  timerMouseIdle.Stop();
		//  timerMouseIdle.Start();
		//  _cursorHidden = false;
		// }
	}

	// private IntPtr _x11Display;

	public void LockMouse(bool wantLock)
	{
		if (wantLock)
		{
			var fbLocation = Point.Subtract(Bounds.Location, new(PointToClient(Location)));
			fbLocation.Offset(_presentationPanel.Control.Location);
			Cursor.Clip = new(fbLocation, _presentationPanel.Control.Size);
			Cursor.Hide();
			_presentationPanel.Control.Cursor = _blankCursor;
			_cursorHidden = true;
		}
		else
		{
			Cursor.Clip = Rectangle.Empty;
			Cursor.Show();
			_presentationPanel.Control.Cursor = Cursors.Default;
			_cursorHidden = false;
		}

		//TODO: fix unix display
		// Cursor.Clip is a no-op on Linux, so we need this too
		/*if (OSTailoredCode.IsUnixHost)
		{
			if (_x11Display == IntPtr.Zero)
			{
				_x11Display = XlibImports.XOpenDisplay(null);
			}

			if (wantLock)
			{
				const XlibImports.EventMask eventMask = XlibImports.EventMask.ButtonPressMask | XlibImports.EventMask.ButtonMotionMask
					| XlibImports.EventMask.ButtonReleaseMask | XlibImports.EventMask.PointerMotionMask
					| XlibImports.EventMask.PointerMotionHintMask | XlibImports.EventMask.LeaveWindowMask;
				var grabResult = XlibImports.XGrabPointer(_x11Display, Handle, false, eventMask,
					XlibImports.GrabMode.Async, XlibImports.GrabMode.Async, Handle, IntPtr.Zero, XlibImports.CurrentTime);
				if (grabResult == XlibImports.GrabResult.AlreadyGrabbed)
				{
					// try to grab again after releasing whatever current active grab
					_ = XlibImports.XUngrabPointer(_x11Display, XlibImports.CurrentTime);
					_ = XlibImports.XGrabPointer(_x11Display, Handle, false, eventMask,
						XlibImports.GrabMode.Async, XlibImports.GrabMode.Async, Handle, IntPtr.Zero, XlibImports.CurrentTime);
				}
			}
			else
			{
				_ = XlibImports.XUngrabPointer(_x11Display, XlibImports.CurrentTime);
			}
		}*/
	}

	#endregion

	private void BtnOptions_Click(object sender, EventArgs e)
	{
		// Open options menu (this could open another form or show a panel)
		MessageBox.Show("Opening Options...");
	}

	private void BtnExit_Click(object sender, EventArgs e)
	{
		// Close the application
		Application.Exit();
	}

	// Override OnKeyDown to allow navigation via keyboard (e.g., pressing 'Esc' to quit)
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.Escape)
		{
			this.Close(); // Close the application when Escape is pressed
		}

		return base.ProcessCmdKey(ref msg, keyData);
	}
}

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ACTIVATEAPP && _emulationThread is not null)
        {
            bool active = m.WParam != IntPtr.Zero;
            _emulationThread.SetPauseReason(EmulationThread.PauseReasons.FocusLost, !active);
        }
        base.WndProc(ref m);
    }
}
