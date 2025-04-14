using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
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
    
    public MainForm()
    {
        InitializeComponent();
        SetupForm();
        initialConfig = ConfigService.Load<Config>(".\\config.ini");
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

        // Optional: Add other controls or animations to the form
    }

    private MouseEventHandler emptyHandler = (sender, args) => { };

    private void BtnStartGame_Click(object sender, EventArgs e)
    {
        //TODO: replace with initialConfig
        var config = new Config();
        var glRenderer = GLRenderer.TryInitIGL(initialConfig.DispMethod, initialConfig);
        var presentationPanel = new PresentationPanel(config, glRenderer,
            (bool _) => { }, emptyHandler, emptyHandler, emptyHandler);
        
        var rom = File.ReadAllBytes("game.nes");
        var emulator = new QuickNES(rom, new QuickNES.QuickNESSettings(), new QuickNES.QuickNESSyncSettings());

        var inputManager = new InputManager();

        var displayManager = new DisplayManager(config, emulator, inputManager, glRenderer, presentationPanel, () => false);
        Controls.Add(presentationPanel);
        Controls.SetChildIndex(presentationPanel, -1);

        presentationPanel.Control.Paint += (o, e) =>
        {
            // I would like to trigger a repaint here, but this isn't done yet
        };
    }

    /*
     public int ProgramRunLoop()
		{
			// needs to be done late, after the log console snaps on top
			// fullscreen should snap on top even harder!
			if (_needsFullscreenOnLoad)
			{
				_needsFullscreenOnLoad = false;
				ToggleFullscreen();
			}

			// Simply exit the program if the version is asked for
			if (_argParser.printVersion)
			{
				// Print the version
				Console.WriteLine(VersionInfo.GetEmuVersion());
				// Return and leave
				return _exitCode;
			}

			LockMouse(Config.CaptureMouse);

			// incantation required to get the program reliably on top of the console window
			// we might want it in ToggleFullscreen later, but here, it needs to happen regardless
			BringToFront();
			Activate();
			BringToFront();

			InitializeFpsData();

			for (; ; )
			{
				Input.Instance.Update();

				// handle events and dispatch as a hotkey action, or a hotkey button, or an input button
				// ...but prepare haptics first, those get read in ProcessInput
				var finalHostController = InputManager.ControllerInputCoalescer;
				InputManager.ActiveController.PrepareHapticsForHost(finalHostController);
				ProcessInput(
					_hotkeyCoalescer,
					finalHostController,
					InputManager.ClientControls.SearchBindings,
					InputManager.ActiveController.HasBinding);
				InputManager.ClientControls.LatchFromPhysical(_hotkeyCoalescer);

				InputManager.ActiveController.LatchFromPhysical(finalHostController);

				if (Config.N64UseCircularAnalogConstraint)
				{
					InputManager.ActiveController.ApplyAxisConstraints("Natural Circle");
				}

				InputManager.ActiveController.OR_FromLogical(InputManager.ClickyVirtualPadController);
				InputManager.AutoFireController.LatchFromPhysical(finalHostController);

				if (InputManager.ClientControls["Autohold"])
				{
					InputManager.ToggleStickies();
				}
				else if (InputManager.ClientControls["Autofire"])
				{
					InputManager.ToggleAutoStickies();
				}

				// autohold/autofire must not be affected by the following inputs
				InputManager.ActiveController.Overrides(InputManager.ButtonOverrideAdapter);

				// emu.yield()'ing scripts
				if (Tools.Has<LuaConsole>())
				{
					Tools.LuaConsole.ResumeScripts(false);
				}
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

        bool isZero = currVideoSize.Width == 0 || currVideoSize.Height == 0 || currVirtualSize.Width == 0 || currVirtualSize.Height == 0;

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
            DisplayManager.Blank();
        else
            DisplayManager.UpdateSource(video);
    }*/

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

