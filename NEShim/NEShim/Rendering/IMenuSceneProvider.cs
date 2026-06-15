using System.Drawing;

namespace NEShim.Rendering;

/// <summary>
/// Pull interface for per-frame menu and logo scene rendering. Implemented by MainForm.
/// D3D11Renderer calls <see cref="GetActiveScenePainter"/> once per <c>DrawAndPresent</c>
/// to obtain a GDI+ paint callback that it composites into the overlay texture.
/// Lives in NEShim.Rendering to avoid a Rendering → UI dependency.
/// </summary>
internal interface IMenuSceneProvider
{
    /// <summary>
    /// Returns an action that paints the current menu or logo scene onto a GDI+ Graphics
    /// object sized to the viewport, or <see langword="null"/> when no overlay is active
    /// (pure gameplay). Called on the UI thread.
    /// </summary>
    Action<Graphics, Rectangle>? GetActiveScenePainter();
}
