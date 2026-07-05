namespace NEShim.Platform;

/// <summary>
/// Abstracts what the host window must provide to the rendering and input subsystems.
/// Implemented by <see cref="SDL3WindowHost"/>.
/// </summary>
internal interface IWindowHost
{
    /// <summary>Native window handle (HWND). Required to create the D3D11 swap chain.</summary>
    IntPtr Handle { get; }

    /// <summary>Current client-area width in pixels.</summary>
    int ClientWidth { get; }

    /// <summary>Current client-area height in pixels.</summary>
    int ClientHeight { get; }

    /// <summary>
    /// Fired after the client area is resized.
    /// Parameters are the new client width and height in pixels.
    /// </summary>
    event Action<int, int>? Resized;

    /// <summary>
    /// Fired when the window gains or loses application focus.
    /// <c>true</c> = focus gained; <c>false</c> = focus lost.
    /// </summary>
    event Action<bool>? FocusChanged;
}
