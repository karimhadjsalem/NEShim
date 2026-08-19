using System.IO;
using NEShim.Emulation;
using NEShim.Saves;
using NSubstitute;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for SaveManager — the Facade over SaveStateManager and SaveRamManager.
/// Cross the file system boundary; kept separate from unit tests per project testing guidelines.
/// </summary>
[TestFixture]
internal class SaveManagerTests
{
    private string         _tempDir  = null!;
    private string         _srmPath  = null!;
    private IEmulationCore _mockCore = null!;
    private SaveManager    _manager  = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _srmPath  = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.srm");
        _mockCore = Substitute.For<IEmulationCore>();
        _mockCore.When(c => c.SaveState(Arg.Any<Stream>()))
                 .Do(ci => ci.Arg<Stream>().Write(new byte[] { 1, 2, 3 }));
        _manager  = new SaveManager(_mockCore, _tempDir, _srmPath, initialActiveSlot: 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        if (File.Exists(_srmPath))      File.Delete(_srmPath);
        _mockCore.Dispose();
    }

    // ---- Constructor ----

    [Test]
    public void Constructor_SetsActiveSlot()
    {
        var manager = new SaveManager(_mockCore, _tempDir, _srmPath, initialActiveSlot: 3);
        Assert.That(manager.ActiveSlot, Is.EqualTo(3));
    }

    [Test]
    public void SlotCount_ReturnsEight()
    {
        Assert.That(_manager.SlotCount, Is.EqualTo(8));
    }

    // ---- ActiveSlot round-trip ----

    [Test]
    public void ActiveSlot_SetAndGet_Roundtrips()
    {
        _manager.ActiveSlot = 5;
        Assert.That(_manager.ActiveSlot, Is.EqualTo(5));
    }

    // ---- Save / load delegation ----

    [Test]
    public void SaveSlot_ThenSlotExists_ReturnsTrue()
    {
        _manager.SaveSlot(1);
        Assert.That(_manager.SlotExists(1), Is.True);
    }

    [Test]
    public void LoadSlot_WhenSlotEmpty_ReturnsFalse()
    {
        Assert.That(_manager.LoadSlot(0), Is.False);
    }

    [Test]
    public void AutoSave_CreatesAutosaveFile()
    {
        _manager.AutoSave();
        Assert.That(_manager.HasAutoSave, Is.True);
    }

    [Test]
    public void SaveToActiveSlot_SavesToActiveSlotIndex()
    {
        _manager.ActiveSlot = 4;
        _manager.SaveToActiveSlot();
        Assert.That(_manager.SlotExists(4), Is.True);
    }

    [Test]
    public void LoadFromActiveSlot_AfterSave_CallsLoadState()
    {
        _manager.ActiveSlot = 2;
        _manager.SaveSlot(2);
        _manager.LoadFromActiveSlot();
        _mockCore.Received(1).LoadState(Arg.Any<Stream>());
    }

    [Test]
    public void AutoLoad_AfterAutoSave_CallsLoadState()
    {
        _manager.AutoSave();
        _manager.AutoLoad();
        _mockCore.Received(1).LoadState(Arg.Any<Stream>());
    }

    // ---- GetSlotMeta delegation ----

    [Test]
    public void GetSlotMeta_WhenNoSave_ReturnsNull()
    {
        Assert.That(_manager.GetSlotMeta(0), Is.Null);
    }

    [Test]
    public void GetSlotMeta_AfterSaveSlot_ReturnsNonNull()
    {
        _manager.SaveSlot(0);
        Assert.That(_manager.GetSlotMeta(0), Is.Not.Null);
    }

    // ---- Startup / Shutdown (SaveRam delegation) ----

    [Test]
    public void Startup_WhenSrmFileExists_CallsSetSaveRam()
    {
        byte[] data = { 0xDE, 0xAD, 0xBE, 0xEF };
        File.WriteAllBytes(_srmPath, data);
        _manager.Startup();
        _mockCore.Received(1).SetSaveRam(Arg.Is<byte[]>(b => b.SequenceEqual(data)));
    }

    [Test]
    public void Shutdown_WhenRamModified_WritesSrmFile()
    {
        byte[] data = { 0x01, 0x02 };
        _mockCore.SaveRamModified.Returns(true);
        _mockCore.GetSaveRam().Returns(data);
        _manager.Shutdown();
        Assert.That(File.Exists(_srmPath), Is.True);
        Assert.That(File.ReadAllBytes(_srmPath), Is.EqualTo(data));
    }

    [Test]
    public void Shutdown_WhenRamUnmodified_DoesNotWriteSrmFile()
    {
        _mockCore.SaveRamModified.Returns(false);
        _manager.Shutdown();
        Assert.That(File.Exists(_srmPath), Is.False);
    }
}
