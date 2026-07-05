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
    private readonly ConcurrentQueue<Action> _marshalQueue = new();

    public event Action<int, int>? Resized;
    public event Action<bool>? FocusChanged;

    internal event Action<Keys>? KeyDown;
    internal event Action<Keys>? KeyUp;

    private static readonly Dictionary<SDL.Keycode, Keys> s_KeyMap = BuildKeyMap();

    event Action<int, int>? IWindowHost.Resized
    {
        add    => Resized    += value;
        remove => Resized    -= value;
    }

    event Action<bool>? IWindowHost.FocusChanged
    {
        add    => FocusChanged += value;
        remove => FocusChanged -= value;
    }

    public SDL3WindowHost(string title, int width, int height)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");

        _window = SDL.CreateWindow(title, width, height, SDL.WindowFlags.Resizable);
        if (_window == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");

        SDL.GetWindowSizeInPixels(_window, out _clientWidth, out _clientHeight);
    }

    public IntPtr Handle
    {
        get
        {
            uint props = SDL.GetWindowProperties(_window);
            return SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer, IntPtr.Zero);
        }
    }

    public int ClientWidth  => _clientWidth;
    public int ClientHeight => _clientHeight;

    public Action<Action> MarshalToMainThread => action => _marshalQueue.Enqueue(action);

    public void SetTitle(string title) => SDL.SetWindowTitle(_window, title);

    public void SetFullscreen(bool fullscreen)
    {
        SDL.SetWindowFullscreen(_window, fullscreen);
        if (!fullscreen)
        {
            SDL.SetWindowSize(_window, 1024, 672);
            SDL.SetWindowPosition(_window, (int)SDL.WindowPosCentered(), (int)SDL.WindowPosCentered());
        }
    }

    public void HideCursor() => SDL.HideCursor();

    public void RequestQuit() => _quit = true;

    public void RunLoop(Action onIdle)
    {
        while (!_quit)
        {
            while (SDL.PollEvent(out var sdlEvent))
                HandleSdlEvent(ref sdlEvent);
            while (_marshalQueue.TryDequeue(out var action))
                action();
            onIdle();
        }
    }

    private void HandleSdlEvent(ref SDL.Event sdlEvent)
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
                if (TryMapKey(sdlEvent.Key.Key, out var downKey))
                    KeyDown?.Invoke(downKey);
                break;

            case SDL.EventType.KeyUp:
                if (TryMapKey(sdlEvent.Key.Key, out var upKey))
                    KeyUp?.Invoke(upKey);
                break;
        }
    }

    internal static bool TryMapKey(SDL.Keycode keycode, out Keys mappedKey) =>
        s_KeyMap.TryGetValue(keycode, out mappedKey);

    private static Dictionary<SDL.Keycode, Keys> BuildKeyMap() => new()
    {
        { SDL.Keycode.A, Keys.A }, { SDL.Keycode.B, Keys.B }, { SDL.Keycode.C, Keys.C },
        { SDL.Keycode.D, Keys.D }, { SDL.Keycode.E, Keys.E }, { SDL.Keycode.F, Keys.F },
        { SDL.Keycode.G, Keys.G }, { SDL.Keycode.H, Keys.H }, { SDL.Keycode.I, Keys.I },
        { SDL.Keycode.J, Keys.J }, { SDL.Keycode.K, Keys.K }, { SDL.Keycode.L, Keys.L },
        { SDL.Keycode.M, Keys.M }, { SDL.Keycode.N, Keys.N }, { SDL.Keycode.O, Keys.O },
        { SDL.Keycode.P, Keys.P }, { SDL.Keycode.Q, Keys.Q }, { SDL.Keycode.R, Keys.R },
        { SDL.Keycode.S, Keys.S }, { SDL.Keycode.T, Keys.T }, { SDL.Keycode.U, Keys.U },
        { SDL.Keycode.V, Keys.V }, { SDL.Keycode.W, Keys.W }, { SDL.Keycode.X, Keys.X },
        { SDL.Keycode.Y, Keys.Y }, { SDL.Keycode.Z, Keys.Z },

        { SDL.Keycode.Alpha0, Keys.D0 }, { SDL.Keycode.Alpha1, Keys.D1 },
        { SDL.Keycode.Alpha2, Keys.D2 }, { SDL.Keycode.Alpha3, Keys.D3 },
        { SDL.Keycode.Alpha4, Keys.D4 }, { SDL.Keycode.Alpha5, Keys.D5 },
        { SDL.Keycode.Alpha6, Keys.D6 }, { SDL.Keycode.Alpha7, Keys.D7 },
        { SDL.Keycode.Alpha8, Keys.D8 }, { SDL.Keycode.Alpha9, Keys.D9 },

        { SDL.Keycode.F1,  Keys.F1  }, { SDL.Keycode.F2,  Keys.F2  }, { SDL.Keycode.F3,  Keys.F3  },
        { SDL.Keycode.F4,  Keys.F4  }, { SDL.Keycode.F5,  Keys.F5  }, { SDL.Keycode.F6,  Keys.F6  },
        { SDL.Keycode.F7,  Keys.F7  }, { SDL.Keycode.F8,  Keys.F8  }, { SDL.Keycode.F9,  Keys.F9  },
        { SDL.Keycode.F10, Keys.F10 }, { SDL.Keycode.F11, Keys.F11 }, { SDL.Keycode.F12, Keys.F12 },

        { SDL.Keycode.Return,    Keys.Return },
        { SDL.Keycode.KpEnter,   Keys.Return },
        { SDL.Keycode.Escape,    Keys.Escape },
        { SDL.Keycode.Space,     Keys.Space  },
        { SDL.Keycode.Backspace, Keys.Back   },
        { SDL.Keycode.Tab,       Keys.Tab    },
        { SDL.Keycode.Delete,    Keys.Delete },
        { SDL.Keycode.Insert,    Keys.Insert },
        { SDL.Keycode.Home,      Keys.Home   },
        { SDL.Keycode.End,       Keys.End    },

        { SDL.Keycode.Up,    Keys.Up    }, { SDL.Keycode.Down,  Keys.Down  },
        { SDL.Keycode.Left,  Keys.Left  }, { SDL.Keycode.Right, Keys.Right },

        { SDL.Keycode.Kp0, Keys.NumPad0 }, { SDL.Keycode.Kp1, Keys.NumPad1 },
        { SDL.Keycode.Kp2, Keys.NumPad2 }, { SDL.Keycode.Kp3, Keys.NumPad3 },
        { SDL.Keycode.Kp4, Keys.NumPad4 }, { SDL.Keycode.Kp5, Keys.NumPad5 },
        { SDL.Keycode.Kp6, Keys.NumPad6 }, { SDL.Keycode.Kp7, Keys.NumPad7 },
        { SDL.Keycode.Kp8, Keys.NumPad8 }, { SDL.Keycode.Kp9, Keys.NumPad9 },

        { SDL.Keycode.LShift, Keys.LShiftKey   }, { SDL.Keycode.RShift, Keys.RShiftKey   },
        { SDL.Keycode.LCtrl,  Keys.LControlKey }, { SDL.Keycode.RCtrl,  Keys.RControlKey },
        { SDL.Keycode.LAlt,   Keys.LMenu        }, { SDL.Keycode.RAlt,   Keys.RMenu        },

        { SDL.Keycode.Period,       Keys.OemPeriod       },
        { SDL.Keycode.Comma,        Keys.Oemcomma        },
        { SDL.Keycode.Semicolon,    Keys.OemSemicolon    },
        { SDL.Keycode.Slash,        Keys.OemQuestion     },
        { SDL.Keycode.Backslash,    Keys.OemBackslash    },
        { SDL.Keycode.LeftBracket,  Keys.OemOpenBrackets },
        { SDL.Keycode.RightBracket, Keys.OemCloseBrackets },
        { SDL.Keycode.Grave,        Keys.Oemtilde        },
        { SDL.Keycode.Minus,        Keys.OemMinus        },
        { SDL.Keycode.Equals,       Keys.Oemplus         },
    };

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
