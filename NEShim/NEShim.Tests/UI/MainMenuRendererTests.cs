using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using BizHawk.Emulation.Common;
using NEShim.Config;
using NEShim.Saves;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI;

/// <summary>
/// Tests for MainMenuRenderer.GetMainPanelRect (panel positioning) and HitTestItem.
/// Uses real MainMenuScreen instances with a temp-dir SaveStateManager and no-op callbacks.
/// </summary>
[TestFixture]
internal class MainMenuRendererTests
{
    private string           _tempDir      = null!;
    private IStatable        _mockStatable = null!;
    private SaveStateManager _saveStates   = null!;
    private AppConfig        _config       = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _mockStatable = Substitute.For<IStatable>();
        _saveStates   = new SaveStateManager(_mockStatable, _tempDir);
        _config       = new AppConfig(); // default MainMenuPosition = "BottomCenter"
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private MainMenuScreen CreateMenu() =>
        new(_saveStates, _config, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { });

    // ---- GetMainPanelRect — position variants ----
    //
    // Margin = 40 (private const in MainMenuRenderer)
    // All tests use bounds (0,0,800,600), panelW=300, panelH=200.

    private static readonly Rectangle Bounds800x600 = new(0, 0, 800, 600);
    private const int PanelW  = 300;
    private const int PanelH  = 200;
    private const int Margin  = 40;

    [Test]
    public void GetMainPanelRect_BottomCenter_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_BottomCenter_Y_IsNearBottom()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        int expectedY = 600 - PanelH - 600 / 6;
        Assert.That(rect.Y, Is.EqualTo(expectedY));
    }

    [Test]
    public void GetMainPanelRect_Center_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Center");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_Center_Y_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Center");
        Assert.That(rect.Y, Is.EqualTo((600 - PanelH) / 2));
    }

    [Test]
    public void GetMainPanelRect_BottomLeft_X_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomLeft");
        Assert.That(rect.X, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_BottomLeft_Y_IsNearBottom()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomLeft");
        int expectedY = 600 - PanelH - 600 / 6;
        Assert.That(rect.Y, Is.EqualTo(expectedY));
    }

    [Test]
    public void GetMainPanelRect_BottomRight_X_IsNearRightEdge()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomRight");
        Assert.That(rect.X, Is.EqualTo(800 - PanelW - Margin));
    }

    [Test]
    public void GetMainPanelRect_TopLeft_X_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopLeft");
        Assert.That(rect.X, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopLeft_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopLeft");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopCenter_X_IsCentered()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopCenter");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
    }

    [Test]
    public void GetMainPanelRect_TopCenter_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopCenter");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_TopRight_X_IsNearRightEdge()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopRight");
        Assert.That(rect.X, Is.EqualTo(800 - PanelW - Margin));
    }

    [Test]
    public void GetMainPanelRect_TopRight_Y_IsMargin()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "TopRight");
        Assert.That(rect.Y, Is.EqualTo(Margin));
    }

    [Test]
    public void GetMainPanelRect_AlwaysPreservesSuppliedDimensions()
    {
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "BottomCenter");
        Assert.That(rect.Width,  Is.EqualTo(PanelW));
        Assert.That(rect.Height, Is.EqualTo(PanelH));
    }

    [Test]
    public void GetMainPanelRect_NarrowBounds_X_ClampedToEight()
    {
        // panelW > bounds.Width → centered X is negative → clamped to 8
        var tinyBounds = new Rectangle(0, 0, 50, 600);
        var rect = MainMenuRenderer.GetMainPanelRect(tinyBounds, PanelW, PanelH, "Center");
        Assert.That(rect.X, Is.EqualTo(8));
    }

    [Test]
    public void GetMainPanelRect_ShortBounds_Y_ClampedToEight()
    {
        // panelH > bounds.Height → centered Y is negative → clamped to 8
        var tinyBounds = new Rectangle(0, 0, 800, 50);
        var rect = MainMenuRenderer.GetMainPanelRect(tinyBounds, PanelW, PanelH, "Center");
        Assert.That(rect.Y, Is.EqualTo(8));
    }

    [Test]
    public void GetMainPanelRect_UnknownPosition_DefaultsToCentered()
    {
        // Unrecognised position string falls through to the default (Center) branch
        var rect = MainMenuRenderer.GetMainPanelRect(Bounds800x600, PanelW, PanelH, "Mystery");
        Assert.That(rect.X, Is.EqualTo((800 - PanelW) / 2));
        Assert.That(rect.Y, Is.EqualTo((600 - PanelH) / 2));
    }

    // ---- HitTestItem ----
    //
    // Main screen: 4 items, BottomCenter, bounds 800×600:
    //   panelW = min(360, 800-60) = 360
    //   panelH = 52 + 4*42 + 14 = 234
    //   panelX = (800-360)/2 = 220, panelY = 600-234-100 = 266
    //   item i rect: (226, 316 + i*42, 348, 40)

    private static readonly Rectangle Bounds800x600HT = new(0, 0, 800, 600);

    [Test]
    public void HitTestItem_DuringRebinding_ReturnsNegativeOne()
    {
        using var menu = CreateMenu();
        // Main → Down once skips disabled Resume → lands on Settings (index 2) → enter → enter Keyboard → enter Up
        menu.HandleKey(Keys.Down);    // 0→(1 disabled)→2 (Settings)
        menu.HandleKey(Keys.Return);  // → Settings screen
        menu.HandleKey(Keys.Return);  // → KeyboardBindings (item 0 = Keyboard Controls)
        menu.HandleKey(Keys.Return);  // → starts rebinding "P1 Up"

        Assert.That(menu.RebindingAction, Is.Not.Null);
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 300), Bounds800x600HT, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_EnabledItem_ReturnsCorrectIndex()
    {
        using var menu = CreateMenu();
        // Item 0 (New Game) center: (400, 336) — inside rect (226, 316, 348, 40)
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 336), Bounds800x600HT, menu), Is.EqualTo(0));
    }

    [Test]
    public void HitTestItem_DisabledItem_ReturnsNegativeOne()
    {
        using var menu = CreateMenu();
        // Item 1 (Resume Game) is disabled when no saves exist.
        // Item 1 center: (400, 378) — inside rect (226, 358, 348, 40)
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 378), Bounds800x600HT, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_PointAbovePanel_ReturnsNegativeOne()
    {
        using var menu = CreateMenu();
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 10), Bounds800x600HT, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_PointOutsidePanelHorizontally_ReturnsNegativeOne()
    {
        using var menu = CreateMenu();
        // Item rects start at x=226; x=50 is outside
        Assert.That(MainMenuRenderer.HitTestItem(new Point(50, 336), Bounds800x600HT, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_SubScreen_CenteredPanel_ReturnsFirstItem()
    {
        using var menu = CreateMenu();
        // Navigate to Settings sub-screen (one Down skips disabled Resume, lands on Settings)
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        Assert.That(menu.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        // Settings sub-screen (5 items), centered 800×600:
        //   panelW = min(440, 800-60) = 440
        //   panelH = 52 + 5*42 + 14 = 276
        //   panelX = max(8, (800-440)/2) = 180
        //   panelY = max(8, (600-276)/2) = 162
        //   item 0 rect: (186, 212, 428, 40) → center y = 232
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 232), Bounds800x600HT, menu), Is.EqualTo(0));
    }

    [Test]
    public void HitTestItem_DisabledResumeBecomesEnabled_WhenSlotExists()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "slot0.state"), Array.Empty<byte>());
        using var menu = CreateMenu();

        // Resume Game (item 1) is now enabled
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 378), Bounds800x600HT, menu), Is.EqualTo(1));
    }

    // ---- Draw smoke tests ----

    private static Bitmap MakeCanvas() =>
        new(800, 600, PixelFormat.Format32bppArgb);

    [Test]
    public void Draw_MainScreen_NoBackground_DoesNotThrow()
    {
        using var menu   = CreateMenu();
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_MainScreen_WithResumeEnabled_DoesNotThrow()
    {
        // Make Resume Game enabled so the selected item can reach it
        File.WriteAllBytes(Path.Combine(_tempDir, "slot0.state"), Array.Empty<byte>());
        using var menu   = CreateMenu();
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_SettingsSubScreen_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings (skips disabled Resume)
        menu.HandleKey(Keys.Return);  // → Settings
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_VideoSubScreen_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Down);    // skip Keyboard Controls
        menu.HandleKey(Keys.Down);    // Video (index 2)
        menu.HandleKey(Keys.Return);  // → Video
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_SoundSubScreen_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings
        menu.HandleKey(Keys.Return);
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down); // Sound (index 3)
        menu.HandleKey(Keys.Return);  // → Sound
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_KeyboardBindingsSubScreen_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Return);  // → KeyboardBindings (index 0)
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_RebindingMode_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Return);  // KeyboardBindings
        menu.HandleKey(Keys.Return);  // → starts rebinding "P1 Up"
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_ResumeSlots_DoesNotThrow()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "slot0.state"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(_tempDir, "autosave.state"), Array.Empty<byte>());
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Resume (now enabled)
        menu.HandleKey(Keys.Return);  // → ResumeSlots
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_GamepadRebindingMode_DoesNotThrow()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings (skips disabled Resume)
        menu.HandleKey(Keys.Return);  // → Settings
        menu.HandleKey(Keys.Down);    // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return);  // → GamepadBindings
        menu.HandleKey(Keys.Return);  // start rebind for P1 Up (index 0)
        Assert.That(menu.IsGamepadRebinding, Is.True);
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void HitTestItem_DuringGamepadRebinding_ReturnsNegativeOne()
    {
        using var menu = CreateMenu();
        menu.HandleKey(Keys.Down);    // Settings
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Down);    // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return);  // → GamepadBindings
        menu.HandleKey(Keys.Return);  // start rebind
        Assert.That(menu.IsGamepadRebinding, Is.True);
        Assert.That(MainMenuRenderer.HitTestItem(new Point(400, 300), Bounds800x600HT, menu), Is.EqualTo(-1));
    }

    [Test]
    public void Draw_MainScreen_WithWideBackground_DoesNotThrow()
    {
        // Wide image (200×100): imgAspect=2.0 > bounds aspect 1.333 → else branch in DrawBackground
        string imgPath = Path.Combine(_tempDir, "bg_wide.bmp");
        using (var bmp = new Bitmap(200, 100, PixelFormat.Format32bppArgb))
            bmp.Save(imgPath, System.Drawing.Imaging.ImageFormat.Bmp);

        using var menu   = new MainMenuScreen(_saveStates, _config, imgPath,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { });
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_MainScreen_WithTallBackground_DoesNotThrow()
    {
        // Tall image (100×200): imgAspect=0.5, bounds aspect 1.333 > imgAspect → if branch in DrawBackground
        string imgPath = Path.Combine(_tempDir, "bg_tall.bmp");
        using (var bmp = new Bitmap(100, 200, PixelFormat.Format32bppArgb))
            bmp.Save(imgPath, System.Drawing.Imaging.ImageFormat.Bmp);

        using var menu   = new MainMenuScreen(_saveStates, _config, imgPath,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { });
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MainMenuRenderer.Draw(g, Bounds800x600HT, menu), Throws.Nothing);
    }
}
