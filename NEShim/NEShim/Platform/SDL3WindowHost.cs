using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SDL3;

namespace NEShim.Platform;

[ExcludeFromCodeCoverage]
internal sealed class SDL3WindowHost : IWindowHost, IDisposable
{
    private IntPtr _window;
    private bool _quit;
    private int _clientWidth;
    private int _clientHeight;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly IntPtr _hwnd;
    private readonly ConcurrentQueue<Action> _marshalQueue = new();
    private readonly Action<Action> _marshalDelegate;

    public event Action<int, int>? Resized;
    public event Action<bool>?     FocusChanged;

    internal event Action<SDL.Keycode>? KeyDown;
    internal event Action<SDL.Keycode>? KeyUp;

    public SDL3WindowHost(string title, int width, int height)
    {
        PlatformDetector.ConfigureVideoDriverForSteamOverlay();
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");

        // Audio is initialized separately and non-fatally: some Linux environments have no
        // working audio backend at all (e.g. missing libjack.so.0 with no PulseAudio/ALSA/
        // PipeWire fallback available either), which must not prevent the app from starting.
        // AudioPlayer.Start() already tolerates SDL_OpenAudioDeviceStream failing (logs and
        // runs muted) — this applies the same tolerance one level up, at subsystem init.
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            Logger.Log($"[Audio] SDL audio subsystem init failed, running muted: {SDL.GetError()}");

        _window = SDL.CreateWindow(title, width, height, SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden);
        if (_window == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");

        SDL.GetWindowSizeInPixels(_window, out _clientWidth, out _clientHeight);
        _initialWidth  = width;
        _initialHeight = height;

        uint props = SDL.GetWindowProperties(_window);
        _hwnd = SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer, IntPtr.Zero);

        _marshalDelegate = action => _marshalQueue.Enqueue(action);
    }

    public IntPtr Handle    => _hwnd;
    public IntPtr SdlWindow => _window;

    public int ClientWidth  => _clientWidth;
    public int ClientHeight => _clientHeight;

    public Action<Action> MarshalToMainThread => _marshalDelegate;

    public void SetTitle(string title) => SDL.SetWindowTitle(_window, title);

    public void SetFullscreen(bool fullscreen)
    {
        SDL.SetWindowFullscreen(_window, fullscreen);
        if (!fullscreen)
        {
            SDL.SetWindowSize(_window, _initialWidth, _initialHeight);
            SDL.SetWindowPosition(_window, (int)SDL.WindowPosCentered(), (int)SDL.WindowPosCentered());
        }
    }

    public void Show()       => SDL.ShowWindow(_window);
    public void Hide()       => SDL.HideWindow(_window);
    public void HideCursor() => SDL.HideCursor();

    public void RequestQuit() => _quit = true;

    public void RunLoop(Action onIdle)
    {
        while (!_quit)
        {
            while (SDL.PollEvent(out var sdlEvent))
                HandleSdlEvent(in sdlEvent);
            while (_marshalQueue.TryDequeue(out var action))
                action();
            onIdle();
        }
    }

    private void HandleSdlEvent(in SDL.Event sdlEvent)
    {
        switch ((SDL.EventType)sdlEvent.Type)
        {
            case SDL.EventType.Quit:
            case SDL.EventType.WindowCloseRequested:
                _quit = true;
                break;

            case SDL.EventType.WindowResized:
                _clientWidth  = sdlEvent.Window.Data1;
                _clientHeight = sdlEvent.Window.Data2;
                Resized?.Invoke(_clientWidth, _clientHeight);
                break;

            case SDL.EventType.WindowFocusGained:
                FocusChanged?.Invoke(true);
                break;

            case SDL.EventType.WindowFocusLost:
                FocusChanged?.Invoke(false);
                break;

            case SDL.EventType.KeyDown when !sdlEvent.Key.Repeat:
                KeyDown?.Invoke(sdlEvent.Key.Key);
                break;

            case SDL.EventType.KeyUp:
                KeyUp?.Invoke(sdlEvent.Key.Key);
                break;
        }
    }

    public void Dispose()
    {
        if (_window != IntPtr.Zero)
        {
            SDL.DestroyWindow(_window);
            _window = IntPtr.Zero;
        }
        SDL.Quit();
    }
}
