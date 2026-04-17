using System.Drawing;

namespace NEShim.Rendering;

/// <summary>
/// Strategy for configuring GDI+ interpolation when scaling the NES frame to the display.
/// </summary>
internal interface IGraphicsScaler
{
    void Configure(Graphics g);
}
