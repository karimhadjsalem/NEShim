using System.Drawing;
using System.Drawing.Imaging;
using SDL3;
using NEShim.Config;
using NEShim.Saves;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI;

/// <summary>
/// Unit tests for MenuRenderer.HitTestItem and Draw smoke paths.
/// Uses real InGameMenu instances with a mocked ISaveManager — no file I/O.
/// </summary>
[TestFixture]
internal class MenuRendererTests
{
    private ISaveManager _saves  = null!;
    private AppConfig    _config = null!;

    // A 640×480 bounds used for all panel-geometry calculations below.
    // Root screen, 8 items, no warning row:
    //   panelW = min(440, 640-60) = 440
    //   panelH = 64 + 0 + 8*38 + 16 = 384
    //   panelX = max(8, (640-440)/2) = 100
    //   panelY = max(8, (480-384)/2) = 48
    //   item i rect: (106, 104 + i*38, 428, 36)
    private static readonly Rectangle Bounds640x480 = new(0, 0, 640, 480);

    [SetUp]
    public void SetUp()
    {
        _saves  = Substitute.For<ISaveManager>();
        _saves.SlotCount.Returns(8);
        _config = new AppConfig();
    }

    private InGameMenu CreateOpenMenu()
    {
        var menu = new InGameMenu(
            _saves, _config,
            new LocalizationData(),
            () => { }, () => { }, () => { }, _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        menu.Open();
        return menu;
    }

    // ---- Rebinding guard ----

    [Test]
    public void HitTestItem_DuringKeyRebinding_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // Root → Settings (Down×4 skips disabled Load Game at index 4) → Keyboard Controls (index 2) → rebind
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // → Settings screen
        menu.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        menu.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        menu.HandleKey(SDL.Keycode.Return); // → KeyboardBindings screen
        menu.HandleKey(SDL.Keycode.Return); // → starts rebinding "P1 Up"

        Assert.That(menu.RebindingAction, Is.Not.Null);
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 200), Bounds640x480, menu), Is.EqualTo(-1));
    }

    // ---- Geometric misses ----

    [Test]
    public void HitTestItem_PointAboveAllItems_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // First item starts at y=104; y=50 is above it
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 50), Bounds640x480, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_PointBelowAllItems_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // Last item (7) ends at y=406; y=450 is below it
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 450), Bounds640x480, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_PointLeftOfPanel_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // Item rects start at x=106; x=50 is to the left of the panel
        Assert.That(MenuRenderer.HitTestItem(new Point(50, 122), Bounds640x480, menu), Is.EqualTo(-1));
    }

    [Test]
    public void HitTestItem_PointBetweenItems_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // Item 0 ends at y=140 (exclusive); item 1 starts at y=142 — gap at y=140,141
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 140), Bounds640x480, menu), Is.EqualTo(-1));
    }

    // ---- Geometric hits ----

    [Test]
    public void HitTestItem_PointOnFirstItem_ReturnsZero()
    {
        var menu = CreateOpenMenu();
        // Item 0 center: (320, 122) — inside rect (106, 104, 428, 36)
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 122), Bounds640x480, menu), Is.EqualTo(0));
    }

    [Test]
    public void HitTestItem_PointOnSecondItem_ReturnsOne()
    {
        var menu = CreateOpenMenu();
        // Item 1 center: (320, 160) — inside rect (106, 142, 428, 36)
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 160), Bounds640x480, menu), Is.EqualTo(1));
    }

    [Test]
    public void HitTestItem_PointOnLastItem_ReturnsSevenForRootScreen()
    {
        var menu = CreateOpenMenu();
        // Item 7 center: (320, 388) — inside rect (106, 370, 428, 36)
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 388), Bounds640x480, menu), Is.EqualTo(7));
    }

    // ---- Confirm screen: warning row shifts item positions ----

    [Test]
    public void HitTestItem_ConfirmScreen_WarningRowShiftsItemsDown()
    {
        var menu = CreateOpenMenu();
        // Navigate to ConfirmMainMenu (warningRowH = ItemH = 38)
        // Down×5 from Root (0→1→2→3→(4skip)→5→6): lands on "Return to Main Menu"
        for (int i = 0; i < 5; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // → ConfirmMainMenu

        // With warningRowH=38, panelH=194, panelY=143:
        // Item 0 rect: (106, 237, 428, 36) → center y = 255
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 255), Bounds640x480, menu), Is.EqualTo(0));
    }

    [Test]
    public void HitTestItem_ConfirmScreen_PointAtNormalItemPosition_Misses()
    {
        var menu = CreateOpenMenu();
        // Same navigation as above
        for (int i = 0; i < 5; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return);

        // y=122 was item 0 on Root (no warning row); on ConfirmMainMenu it falls before any item
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 122), Bounds640x480, menu), Is.EqualTo(-1));
    }

    // ---- Draw smoke tests (verify no exception on each code path) ----

    private static Bitmap MakeCanvas() =>
        new(640, 480, PixelFormat.Format32bppArgb);

    [Test]
    public void Draw_RootScreen_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_RootScreen_WithSlotSave_DoesNotThrow()
    {
        // Slot 0 exists → Load Game is enabled and renders differently
        _saves.SlotExists(0).Returns(true);
        var menu = CreateOpenMenu();
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_ConfirmMainMenu_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 5; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // → ConfirmMainMenu (has warning row)
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_ConfirmExit_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 6; i++) menu.HandleKey(SDL.Keycode.Down); // → Exit (index 7)
        menu.HandleKey(SDL.Keycode.Return); // → ConfirmExit
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_KeyboardRebindingMode_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // Settings
        menu.HandleKey(SDL.Keycode.Return); // KeyboardBindings
        menu.HandleKey(SDL.Keycode.Return); // → RebindingAction = "P1 Up"
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_SettingsScreen_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // → Settings
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_VideoScreen_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // Settings
        menu.HandleKey(SDL.Keycode.Down);   // skip Keyboard Controls
        menu.HandleKey(SDL.Keycode.Down);   // select Video (index 2)
        menu.HandleKey(SDL.Keycode.Return); // → Video
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_SoundScreen_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // Settings
        for (int i = 0; i < 3; i++) menu.HandleKey(SDL.Keycode.Down); // → Sound (index 3)
        menu.HandleKey(SDL.Keycode.Return); // → Sound
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_SaveSlotSelectScreen_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // → SaveSlotSelect
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_ConfirmLoadScreen_DoesNotThrow()
    {
        _saves.SlotExists(0).Returns(true);
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down); // Load Game (enabled at index 4)
        menu.HandleKey(SDL.Keycode.Return); // → ConfirmLoad
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_GamepadRebindingMode_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down); // Settings
        menu.HandleKey(SDL.Keycode.Return);
        menu.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        menu.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        menu.HandleKey(SDL.Keycode.Down);   // Gamepad Controls (index 3)
        menu.HandleKey(SDL.Keycode.Return); // GamepadBindings
        menu.HandleKey(SDL.Keycode.Return); // start rebind for P1 Up (index 0)
        Assert.That(menu.IsGamepadRebinding, Is.True);
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void HitTestItem_DuringGamepadRebinding_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return);
        menu.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        menu.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        menu.HandleKey(SDL.Keycode.Down);   // Gamepad Controls (index 3)
        menu.HandleKey(SDL.Keycode.Return); // GamepadBindings
        menu.HandleKey(SDL.Keycode.Return); // start rebind
        Assert.That(menu.IsGamepadRebinding, Is.True);
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 200), Bounds640x480, menu), Is.EqualTo(-1));
    }

    [Test]
    public void Draw_ControllerDisconnected_DoesNotThrow()
    {
        var menu = new InGameMenu(
            _saves, _config,
            new LocalizationData(),
            () => { }, () => { }, () => { }, _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        menu.Open(InGameMenu.Screen.ControllerDisconnected);
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_LanguageScreen_WithIcons_DoesNotThrow()
    {
        var menu = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down); // → Settings
        menu.HandleKey(SDL.Keycode.Return);
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down); // → Language (index 4)
        menu.HandleKey(SDL.Keycode.Return); // Language screen (has flag icons)
        using var canvas = MakeCanvas();
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, Bounds640x480, menu), Throws.Nothing);
    }

    [Test]
    public void Draw_GamepadBindings_WithWideCanvas_ShowsController()
    {
        // Width > MinWidthForCtrl triggers the controller diagram column
        var wideBounds = new Rectangle(0, 0, 1280, 720);
        var menu       = CreateOpenMenu();
        for (int i = 0; i < 4; i++) menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return);
        menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Down);
        menu.HandleKey(SDL.Keycode.Return); // GamepadBindings
        using var canvas = new Bitmap(1280, 720, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g      = Graphics.FromImage(canvas);
        Assert.That(() => MenuRenderer.Draw(g, wideBounds, menu), Throws.Nothing);
    }
}
