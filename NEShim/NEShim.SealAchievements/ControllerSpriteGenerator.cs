using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NEShim.SealAchievements;

/// <summary>
/// Renders the NES controller diagram to PNG files at a fixed resolution.
/// These PNGs are checked in as embedded resources in the main NEShim project
/// and blitted at runtime, replacing the runtime GDI+ drawing code.
/// </summary>
internal static class ControllerSpriteGenerator
{
    // Canvas size — 2.43:1 aspect to match the controller's Aspect constant.
    // With these exact dimensions the controller fills the canvas with no label-gap space above it.
    private const int CanvasW = 720;
    private const int CanvasH = 296;

    private static readonly (string? highlight, string filename)[] Variants =
    [
        (null,         "controller_none.png"),
        ("P1 Up",      "controller_p1_up.png"),
        ("P1 Down",    "controller_p1_down.png"),
        ("P1 Left",    "controller_p1_left.png"),
        ("P1 Right",   "controller_p1_right.png"),
        ("P1 A",       "controller_p1_a.png"),
        ("P1 B",       "controller_p1_b.png"),
        ("P1 Start",   "controller_p1_start.png"),
        ("P1 Select",  "controller_p1_select.png"),
    ];

    public static void Generate(string outDir)
    {
        Directory.CreateDirectory(outDir);

        foreach (var (highlight, filename) in Variants)
        {
            using var bmp = new Bitmap(CanvasW, CanvasH, PixelFormat.Format32bppArgb);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            DrawController(g, new RectangleF(0, 0, CanvasW, CanvasH), highlight);

            string path = Path.Combine(outDir, filename);
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine($"  Wrote {path}");
        }
    }

    // ── Drawing code ─────────────────────────────────────────────────────────
    // Source: NEShim/UI/NesControllerDiagram.cs (deleted after sprites are committed).

    private static readonly Color BodyColor     = Color.FromArgb(255,  20,  20,  24);
    private static readonly Color BandEdge      = Color.FromArgb(255,  60,  60,  66);
    private static readonly Color DpadColor     = Color.FromArgb(255,  26,  26,  32);
    private static readonly Color WhiteBg       = Color.FromArgb(255, 210, 210, 216);
    private static readonly Color ButtonRed     = Color.FromArgb(255, 165,  20,  20);
    private static readonly Color ButtonRedEdge = Color.FromArgb(255, 115,  10,  10);
    private static readonly Color Pill          = Color.FromArgb(255,  30,  30,  36);
    private static readonly Color PillLabel     = Color.FromArgb(255, 165,  20,  20);
    private static readonly Color HighlightFill = Color.FromArgb(190,  55, 110, 195);
    private static readonly Color ArrowColor    = Color.FromArgb(150,  88,  88,  94);

    private static void DrawController(Graphics g, RectangleF area, string? highlight)
    {
        if (area.Width < 40 || area.Height < 18) return;

        const float Aspect = 2.43f;
        float ctrlW = area.Width;
        float ctrlH = ctrlW / Aspect;
        if (ctrlH > area.Height) { ctrlH = area.Height; ctrlW = ctrlH * Aspect; }
        float ox = area.X + (area.Width  - ctrlW) * 0.5f;
        float oy = area.Y + (area.Height - ctrlH) * 0.5f;
        float aw = ctrlW, ah = ctrlH;

        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Controller body
        using (var bodyBrush = new SolidBrush(BodyColor))
        using (var bodyPen   = new Pen(WhiteBg, 5.5f))
            DrawRoundedRect(g, bodyBrush, bodyPen,
                new RectangleF(ox + 0.005f * aw, oy + 0.040f * ah, 0.990f * aw, 0.920f * ah),
                0.068f * aw, 0.160f * ah, 0.5f);

        float cpX   = ox + 0.356f * aw;
        float cpW   = 0.258f * aw;
        float cpRx  = 0.014f * aw;
        float cpRy  = 0.050f * ah;
        float bandH = 0.188f * ah;
        float baseY = oy + 0.622f * ah;
        float startY = baseY - 0.035f * ah;
        RectangleF startBand = new(cpX, startY, cpW, bandH);

        using (var bandBrush = new SolidBrush(WhiteBg))
        using (var bandPen   = new Pen(BandEdge, 0.8f))
            DrawRoundedRect(g, bandBrush, bandPen, startBand, cpRx, cpRy);

        float gSlotH  = 0.1f * ah;
        float gSlotRx = gSlotH * 0.5f;
        float yOff1 = baseY - 0.55f * ah;
        float yOff2 = baseY - 0.39f * ah;
        float yOff3 = baseY - 0.23f * ah;
        float yOff4 = baseY + 0.21f * ah;
        RectangleF startLabelBand = startBand with { Y = yOff3, Height = gSlotH };

        using var grilleBrush = new SolidBrush(BandEdge);
        DrawRoundedRect(g, grilleBrush, null, startBand with { Y = yOff1, Height = gSlotH }, gSlotRx, gSlotRx);
        DrawRoundedRect(g, grilleBrush, null, startBand with { Y = yOff2, Height = gSlotH }, gSlotRx, gSlotRx);
        DrawRoundedRect(g, grilleBrush, null, startLabelBand, gSlotRx, gSlotRx);
        DrawRoundedRect(g, grilleBrush, null, startBand with { Y = yOff4, Height = gSlotH }, gSlotRx, gSlotRx);

        float pillY  = startBand.Y + startBand.Height * 0.33f;
        float selCx  = startBand.X + startBand.Width * 0.25f;
        float staCx  = startBand.X + startBand.Width * 0.75f;
        float pillW  = 0.090f * aw;
        float pillH  = 0.070f * ah;
        float pillRx = pillW * 0.42f;
        float pillRy = pillH * 0.50f;
        RectangleF selRect = new(selCx - pillW * 0.5f, pillY, pillW, pillH);
        RectangleF staRect = new(staCx - pillW * 0.5f, pillY, pillW, pillH);

        using (var pillBrush = new SolidBrush(Pill))
        using (var pillPen   = new Pen(Pill, 0.8f))
        {
            DrawRoundedRect(g, pillBrush, pillPen, selRect, pillRx, pillRy);
            DrawRoundedRect(g, pillBrush, pillPen, staRect, pillRx, pillRy);
        }

        float pillLabelEm = Math.Max(5f, 5.0f * ah / 100f);
        float labelY      = startLabelBand.Y + startLabelBand.Height * 0.25f;
        DrawLabel(g, "SELECT", new RectangleF(startLabelBand.X, labelY, pillW * 1.5f, pillH), pillLabelEm, PillLabel);
        DrawLabel(g, "START",  new RectangleF(startLabelBand.X + startLabelBand.Width * 0.50f, labelY, pillW * 1.5f, pillH), pillLabelEm, PillLabel);

        float dcx    = ox  + 0.173f * aw;
        float dcy    = oy  + 0.500f * ah;
        float armT   = 0.058f * aw;
        float armL   = 0.090f * aw;
        float outset = 0.008f * aw;

        using (var dpadOutBrush = new SolidBrush(WhiteBg))
        {
            g.FillRectangle(dpadOutBrush,
                dcx - armL - outset,        dcy - armT * 0.5f - outset,
                2f * (armL + outset),       armT + 2f * outset);
            g.FillRectangle(dpadOutBrush,
                dcx - armT * 0.5f - outset, dcy - armL - outset,
                armT + 2f * outset,         2f * (armL + outset));
        }
        using (var dpadBrush = new SolidBrush(DpadColor))
        {
            g.FillRectangle(dpadBrush, dcx - armL,        dcy - armT * 0.5f, 2f * armL, armT);
            g.FillRectangle(dpadBrush, dcx - armT * 0.5f, dcy - armL,        armT,      2f * armL);
        }

        float arrowS    = 0.015f * aw;
        float arrowDist = armL   * 0.62f;
        using var arrowBrush = new SolidBrush(ArrowColor);
        DrawArrow(g, arrowBrush, dcx,              dcy - arrowDist,  0, -1, arrowS);
        DrawArrow(g, arrowBrush, dcx,              dcy + arrowDist,  0,  1, arrowS);
        DrawArrow(g, arrowBrush, dcx - arrowDist,  dcy,             -1,  0, arrowS);
        DrawArrow(g, arrowBrush, dcx + arrowDist,  dcy,              1,  0, arrowS);

        float btnR = 0.048f * aw;
        float bgH  = btnR * 1.26f;
        float bgRx = bgH  * 0.42f;
        float bcx  = ox   + 0.722f * aw, bcy = oy + 0.636f * ah;
        float acx  = ox   + 0.862f * aw, acy = oy + 0.636f * ah;

        using (var bgBrush = new SolidBrush(WhiteBg))
        {
            DrawRoundedRect(g, bgBrush, null,
                new RectangleF(bcx - bgH, bcy - bgH, 2f * bgH, 2f * bgH), bgRx, bgRx, 0.5f);
            DrawRoundedRect(g, bgBrush, null,
                new RectangleF(acx - bgH, acy - bgH, 2f * bgH, 2f * bgH), bgRx, bgRx, 0.5f);
        }
        using (var abBrush = new SolidBrush(ButtonRed))
        using (var abPen   = new Pen(ButtonRedEdge, 1f))
        {
            g.FillEllipse(abBrush, bcx - btnR, bcy - btnR, btnR * 2f, btnR * 2f);
            g.DrawEllipse(abPen,   bcx - btnR, bcy - btnR, btnR * 2f, btnR * 2f);
            g.FillEllipse(abBrush, acx - btnR, acy - btnR, btnR * 2f, btnR * 2f);
            g.DrawEllipse(abPen,   acx - btnR, acy - btnR, btnR * 2f, btnR * 2f);
        }
        float abLabelEm = Math.Max(5f, 9f * ah / 100f);
        DrawLabel(g, "B", new RectangleF(bcx - btnR, bcy - btnR, btnR * 2f, btnR * 2f), abLabelEm, Color.White);
        DrawLabel(g, "A", new RectangleF(acx - btnR, acy - btnR, btnR * 2f, btnR * 2f), abLabelEm, Color.White);

        if (highlight != null)
            DrawHighlight(g, highlight,
                dcx, dcy, armL, armT,
                selCx, staCx, pillY, pillW, pillH, pillRx, pillRy,
                bcx, bcy, acx, acy, btnR, aw);

        g.SmoothingMode = prev;
    }

    private static void DrawHighlight(
        Graphics g, string highlight,
        float dcx, float dcy, float armL, float armT,
        float selCx, float staCx, float pillY,
        float pillW, float pillH, float pillRx, float pillRy,
        float bcx, float bcy, float acx, float acy,
        float btnR, float aw)
    {
        using var hBrush = new SolidBrush(HighlightFill);
        float pad  = 0.010f * aw;
        float halfT = armT * 0.5f;

        switch (highlight)
        {
            case "P1 Up":
                g.FillRectangle(hBrush,
                    dcx - halfT - pad, dcy - armL - pad,
                    armT + 2f * pad,   armL - halfT + 2f * pad);
                break;
            case "P1 Down":
                g.FillRectangle(hBrush,
                    dcx - halfT - pad, dcy + halfT - pad,
                    armT + 2f * pad,   armL - halfT + 2f * pad);
                break;
            case "P1 Left":
                g.FillRectangle(hBrush,
                    dcx - armL - pad,        dcy - halfT - pad,
                    armL - halfT + 2f * pad, armT + 2f * pad);
                break;
            case "P1 Right":
                g.FillRectangle(hBrush,
                    dcx + halfT - pad,       dcy - halfT - pad,
                    armL - halfT + 2f * pad, armT + 2f * pad);
                break;
            case "P1 Select":
                DrawRoundedRect(g, hBrush, null,
                    new RectangleF(selCx - pillW * 0.5f - pad, pillY - pillH * 0.33f,
                                   pillW + 2f * pad, pillH + 2f * pad),
                    pillRx + pad, pillRy + pad);
                break;
            case "P1 Start":
                DrawRoundedRect(g, hBrush, null,
                    new RectangleF(staCx - pillW * 0.5f - pad, pillY - pillH * 0.33f,
                                   pillW + 2f * pad, pillH + 2f * pad),
                    pillRx + pad, pillRy + pad);
                break;
            case "P1 B":
                g.FillEllipse(hBrush,
                    bcx - btnR - pad, bcy - btnR - pad,
                    (btnR + pad) * 2f, (btnR + pad) * 2f);
                break;
            case "P1 A":
                g.FillEllipse(hBrush,
                    acx - btnR - pad, acy - btnR - pad,
                    (btnR + pad) * 2f, (btnR + pad) * 2f);
                break;
        }
    }

    private static void DrawRoundedRect(Graphics g, Brush brush, Pen? pen, RectangleF rect, float rx, float ry, float cornerCurve = 2f)
    {
        rx = Math.Min(rx, rect.Width  / cornerCurve);
        ry = Math.Min(ry, rect.Height / cornerCurve);
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, rx * cornerCurve, ry * cornerCurve, 180, 90);
        path.AddArc(rect.X + rect.Width - rx * cornerCurve, rect.Y, rx * cornerCurve, ry * cornerCurve, 270, 90);
        path.AddArc(rect.X + rect.Width - rx * cornerCurve, rect.Y + rect.Height - ry * cornerCurve, rx * cornerCurve, ry * cornerCurve, 0, 90);
        path.AddArc(rect.X, rect.Y + rect.Height - ry * cornerCurve, rx * cornerCurve, ry * cornerCurve, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
        if (pen != null) g.DrawPath(pen, path);
    }

    private static void DrawLabel(Graphics g, string text, RectangleF rect, float emSize, Color color)
    {
        if (emSize < 3f) return;
        using var font  = new Font("Arial", emSize, FontStyle.Bold, GraphicsUnit.Point);
        using var brush = new SolidBrush(color);
        using var fmt   = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(text, font, brush, rect, fmt);
    }

    private static void DrawArrow(Graphics g, Brush brush, float cx, float cy, float dx, float dy, float s)
    {
        PointF[] pts = (dx == 0)
            ? [new(cx - s * 0.7f, cy - dy * s * 0.3f), new(cx + s * 0.7f, cy - dy * s * 0.3f), new(cx, cy + dy * s)]
            : [new(cx - dx * s * 0.3f, cy - s * 0.7f), new(cx - dx * s * 0.3f, cy + s * 0.7f), new(cx + dx * s, cy)];
        g.FillPolygon(brush, pts);
    }
}
