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
        _window = SDL3WindowBuilder.Build(title, width, height);

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

    // SDL_ShowWindow only maps the window — it does not guarantee the window manager also
    // grants it input focus. Several X11/Wayland WMs deliberately withhold focus from a newly
    // mapped window (anti-focus-stealing heuristics, or "focus follows mouse" policies) unless
    // something explicitly requests it, which SDL_RaiseWindow does. Without this, the app can
    // start already in a WindowFocusLost state (see NEShimApp's FocusChanged subscription →
    // EmulationThread.PauseReasons.FocusLost), paused from the very first frame, until the
    // player happens to interact with the window in a way the WM treats as a focus request
    // (e.g. moving the mouse over it under a focus-follows-mouse policy) — indistinguishable
    // from a hang without checking neshim.log for a "Paused — active reasons: FocusLost" line
    // that's never followed by "Resumed".
    public void Show()       { SDL.ShowWindow(_window); SDL.RaiseWindow(_window); }
    public void Hide()       => SDL.HideWindow(_window);
    public void HideCursor() => SDL.HideCursor();

    public void RequestQuit() => _quit = true;

    public void RunLoop(Action onIdle)
    {
        while (!_quit)
        {
            while (SDL.PollEvent(out var sdlEvent))
                HandleSdlEvent(in sdlEvent);

            // Bounded to what was already queued when this iteration started — EmulationThread
            // paces itself independently on its own thread and keeps enqueueing regardless of
            // whether the UI thread has drained the previous action, so an unbounded "while
            // (TryDequeue) action()" can livelock this outer loop forever: if per-action work
            // (UploadFrame + a multi-pass GPU Tick) takes long enough to stay neck-and-neck with
            // the emulation thread's production rate, TryDequeue keeps finding a fresh item the
            // instant the last one finishes, so SDL_PollEvent above (and everything downstream of
            // it — gameplay input, the pause-menu hotkey) never runs again even though each
            // individual action keeps completing normally. Reproduced with custom video filters
            // active (enough extra per-frame GPU work to tip the race) on both WSL2 and Steam Deck.
            int pendingActions = _marshalQueue.Count;
            for (int i = 0; i < pendingActions && _marshalQueue.TryDequeue(out var action); i++)
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
