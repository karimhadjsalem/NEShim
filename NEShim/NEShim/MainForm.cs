using System.Runtime.InteropServices;
using System.Windows.Forms;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.GameLoop;
using NEShim.Input;
using NEShim.Rendering;
using NEShim.Saves;
using NEShim.Steam;
using NEShim.UI;

namespace NEShim;

public partial class MainForm : Form
{
    // ---- Win32 for WM_ACTIVATEAPP ----
    private const int WM_ACTIVATEAPP = 0x001C;

    // ---- Core components ----
    private AppConfig?        _config;
    private EmulatorHost?     _host;
    private InputManager?     _input;
    private AudioPlayer?      _audio;
    private SaveStateManager? _saveStates;
    private SaveRamManager?   _saveRam;
    private FrameBuffer?      _frameBuffer;
    private GamePanel?        _gamePanel;
    private InGameMenu?       _menu;
    private EmulationThread?  _emulationThread;

    private bool _isFullscreen = true;

    public MainForm()
    {
        InitializeComponent();
        Load += OnFormLoad;
        FormClosing += OnFormClosing;
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            InitializeEmulator();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start emulator:\n\n{ex.Message}",
                "NEShim — Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private void InitializeEmulator()
    {
        // 1. Config
        _config = ConfigLoader.Load();

        // 2. Load ROM
        string romPath = Path.IsPathRooted(_config.RomPath)
            ? _config.RomPath
            : Path.Combine(AppContext.BaseDirectory, _config.RomPath);

        if (!File.Exists(romPath))
            throw new FileNotFoundException($"ROM not found: {romPath}");

        // 3. Emulator core
        _host = EmulatorHost.Load(romPath, _config);

        // 4. Save RAM (load before first frame)
        _saveRam = new SaveRamManager((BizHawk.Emulation.Common.ISaveRam)_host.SaveRam,
            Path.IsPathRooted(_config.SaveRamPath)
                ? _config.SaveRamPath
                : Path.Combine(AppContext.BaseDirectory, _config.SaveRamPath));
        _saveRam.LoadFromDisk();

        // 5. Save states
        string stateDir = Path.IsPathRooted(_config.SaveStateDirectory)
            ? _config.SaveStateDirectory
            : Path.Combine(AppContext.BaseDirectory, _config.SaveStateDirectory);
        _saveStates = new SaveStateManager(_host.States, stateDir);
        _saveStates.ActiveSlot = _config.ActiveSlot;

        // 6. Rendering
        _frameBuffer = new FrameBuffer();
        _gamePanel   = new GamePanel(_frameBuffer) { Dock = DockStyle.Fill };
        Controls.Add(_gamePanel);

        // 7. Input
        _input = new InputManager();
        _gamePanel.KeyDown += (_, e) => _input.OnKeyDown(e.KeyCode);
        _gamePanel.KeyUp   += (_, e) => _input.OnKeyUp(e.KeyCode);
        _gamePanel.KeyDown += OnGamePanelKeyDown;
        KeyDown += (_, e) => _input.OnKeyDown(e.KeyCode);
        KeyUp   += (_, e) => _input.OnKeyUp(e.KeyCode);
        KeyDown += OnFormKeyDown;

        // 8. Audio
        _audio = new AudioPlayer(_config.AudioBufferFrames);

        // 9. In-game menu
        _menu = new InGameMenu(
            saveStates:         _saveStates,
            config:             _config,
            onExitToDesktop:    () => BeginInvoke(Application.Exit),
            onResetGame:        () => _emulationThread?.ResetGame(),
            onWindowModeToggle: fullscreen => BeginInvoke(() => SetWindowMode(fullscreen)),
            onConfigSaved:      () => { /* config saved on exit; nothing extra needed here */ });
        _gamePanel.SetMenu(_menu);

        // 10. Emulation thread
        _emulationThread = new EmulationThread(
            _host, _config, _input, _audio, _frameBuffer, _gamePanel, _saveStates, _menu);

        // Wire Steam overlay → pause
        SteamManager.Initialize(overlayActive =>
            _emulationThread.SetPauseReason(EmulationThread.PauseReasons.Overlay, overlayActive));

        // 11. Apply window mode
        SetWindowMode(_config.WindowMode.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase));

        // 12. Start audio then emulation
        _audio.Start(_config.AudioDevice);
        _gamePanel.Focus();
        _emulationThread.Start();

        // Auto-load last session state (best-effort)
        _saveStates.AutoLoad();
    }

    private void SetWindowMode(bool fullscreen)
    {
        _isFullscreen = fullscreen;
        if (fullscreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState     = FormWindowState.Maximized;
        }
        else
        {
            WindowState     = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize      = new Size(768, 672); // ~3× NES with aspect correction
            CenterToScreen();
        }
        _config!.WindowMode = fullscreen ? "Fullscreen" : "Windowed";
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_menu is null || _emulationThread is null) return;

        // Pass navigation keys to menu when open
        if (_menu.IsOpen)
        {
            if (_menu.HandleKey(e.KeyCode))
            {
                e.Handled = true;
                _gamePanel?.Invalidate();
                return;
            }
        }

        // F11 window toggle — handled on UI thread
        if (e.KeyCode == Keys.F11)
        {
            SetWindowMode(!_isFullscreen);
            e.Handled = true;
        }
    }

    private void OnGamePanelKeyDown(object? sender, KeyEventArgs e)
    {
        OnFormKeyDown(sender, e);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Stop emulation thread first
        _emulationThread?.Stop();

        // Persist state
        try
        {
            _saveStates?.AutoSave();
            _saveRam?.SaveToDisk();

            if (_config is not null)
            {
                if (_saveStates is not null)
                    _config.ActiveSlot = _saveStates.ActiveSlot;
                ConfigLoader.Save(_config);
            }
        }
        catch { /* best-effort on shutdown */ }

        // Dispose resources
        _audio?.Dispose();
        _host?.Dispose();
        SteamManager.Shutdown();
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
