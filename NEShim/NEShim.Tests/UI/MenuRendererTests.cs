using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BizHawk.Emulation.Common;
using NEShim.Config;
using NEShim.Saves;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI;

/// <summary>
/// Unit tests for MenuRenderer.HitTestItem.
/// Uses real InGameMenu instances (concrete class, not mockable) with a temp-dir
/// SaveStateManager and no-op callbacks — no file I/O beyond directory creation.
/// </summary>
[TestFixture]
internal class MenuRendererTests
{
    private string           _tempDir      = null!;
    private IStatable        _mockStatable = null!;
    private SaveStateManager _saveStates   = null!;
    private AppConfig        _config       = null!;

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
        _tempDir      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _mockStatable = Substitute.For<IStatable>();
        _saveStates   = new SaveStateManager(_mockStatable, _tempDir);
        _config       = new AppConfig();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private InGameMenu CreateOpenMenu()
    {
        var menu = new InGameMenu(
            _saveStates, _config,
            () => { }, () => { }, () => { }, _ => { }, () => { }, _ => { }, _ => { }, _ => { });
        menu.Open(new int[256 * 240]);
        return menu;
    }

    // ---- Rebinding guard ----

    [Test]
    public void HitTestItem_DuringKeyRebinding_ReturnsNegativeOne()
    {
        var menu = CreateOpenMenu();
        // Root → Settings (Down×4 skips disabled Load Game at index 4) → Keyboard Controls → "Up" action
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // → Settings screen
        menu.HandleKey(Keys.Return); // → KeyboardBindings screen
        menu.HandleKey(Keys.Return); // → starts rebinding "P1 Up"

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
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // → ConfirmMainMenu

        // With warningRowH=38, panelH=194, panelY=143:
        // Item 0 rect: (106, 237, 428, 36) → center y = 255
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 255), Bounds640x480, menu), Is.EqualTo(0));
    }

    [Test]
    public void HitTestItem_ConfirmScreen_PointAtNormalItemPosition_Misses()
    {
        var menu = CreateOpenMenu();
        // Same navigation as above
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        // y=122 was item 0 on Root (no warning row); on ConfirmMainMenu it falls before any item
        Assert.That(MenuRenderer.HitTestItem(new Point(320, 122), Bounds640x480, menu), Is.EqualTo(-1));
    }
}
