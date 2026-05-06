using System.IO;
using BizHawk.Emulation.Common;
using NEShim.Saves;
using NSubstitute;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for SaveStateManager — these cross the file system boundary and are
/// intentionally separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class SaveStateManagerTests
{
    private string           _tempDir      = null!;
    private IStatable        _mockStatable = null!;
    private SaveStateManager _manager      = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _mockStatable = Substitute.For<IStatable>();
        _manager      = new SaveStateManager(_mockStatable, _tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---- Constructor ----

    [Test]
    public void Constructor_CreatesDirectory()
    {
        Assert.That(Directory.Exists(_tempDir), Is.True);
    }

    [Test]
    public void SlotCount_IsEight()
    {
        Assert.That(SaveStateManager.SlotCount, Is.EqualTo(8));
    }

    // ---- Initial state ----

    [Test]
    public void SlotExists_WhenNothingWasSaved_ReturnsFalse()
    {
        Assert.That(_manager.SlotExists(0), Is.False);
    }

    [Test]
    public void HasAutoSave_WhenNothingWasSaved_ReturnsFalse()
    {
        Assert.That(_manager.HasAutoSave, Is.False);
    }

    // ---- SaveSlot ----

    [Test]
    public void SaveSlot_CallsSaveStateBinary()
    {
        _manager.SaveSlot(0);
        _mockStatable.Received(1).SaveStateBinary(Arg.Any<BinaryWriter>());
    }

    [Test]
    public void SaveSlot_CreatesStateFile()
    {
        _manager.SaveSlot(0);
        Assert.That(File.Exists(Path.Combine(_tempDir, "slot0.state")), Is.True);
    }

    [Test]
    public void SaveSlot_CreatesMetaFile()
    {
        _manager.SaveSlot(0);
        Assert.That(File.Exists(Path.Combine(_tempDir, "slot0.meta")), Is.True);
    }

    [Test]
    public void SlotExists_AfterSaveSlot_ReturnsTrue()
    {
        _manager.SaveSlot(0);
        Assert.That(_manager.SlotExists(0), Is.True);
    }

    [Test]
    public void SlotExists_ForDifferentSlot_ReturnsFalse()
    {
        _manager.SaveSlot(0);
        Assert.That(_manager.SlotExists(1), Is.False);
    }

    [Test]
    public void SaveSlot_LastSlotIndex_CreatesFile()
    {
        _manager.SaveSlot(SaveStateManager.SlotCount - 1);
        Assert.That(_manager.SlotExists(SaveStateManager.SlotCount - 1), Is.True);
    }

    [Test]
    public void SaveToActiveSlot_SavesToActiveSlotIndex()
    {
        _manager.ActiveSlot = 3;
        _manager.SaveToActiveSlot();
        Assert.That(_manager.SlotExists(3), Is.True);
    }

    // ---- LoadSlot ----

    [Test]
    public void LoadSlot_WhenNoFile_ReturnsFalse()
    {
        Assert.That(_manager.LoadSlot(0), Is.False);
    }

    [Test]
    public void LoadSlot_WhenNoFile_DoesNotCallLoadStateBinary()
    {
        _manager.LoadSlot(0);
        _mockStatable.DidNotReceive().LoadStateBinary(Arg.Any<BinaryReader>());
    }

    [Test]
    public void LoadSlot_WhenFileExists_ReturnsTrue()
    {
        _manager.SaveSlot(0);
        Assert.That(_manager.LoadSlot(0), Is.True);
    }

    [Test]
    public void LoadSlot_WhenFileExists_CallsLoadStateBinary()
    {
        _manager.SaveSlot(0);
        _manager.LoadSlot(0);
        _mockStatable.Received(1).LoadStateBinary(Arg.Any<BinaryReader>());
    }

    [Test]
    public void LoadFromActiveSlot_LoadsFromCurrentActiveSlot()
    {
        _manager.ActiveSlot = 2;
        _manager.SaveSlot(2);
        _manager.LoadFromActiveSlot();
        _mockStatable.Received(1).LoadStateBinary(Arg.Any<BinaryReader>());
    }

    // ---- AutoSave / AutoLoad ----

    [Test]
    public void AutoSave_CreatesAutoSaveFile()
    {
        _manager.AutoSave();
        Assert.That(File.Exists(Path.Combine(_tempDir, "autosave.state")), Is.True);
    }

    [Test]
    public void HasAutoSave_AfterAutoSave_ReturnsTrue()
    {
        _manager.AutoSave();
        Assert.That(_manager.HasAutoSave, Is.True);
    }

    [Test]
    public void AutoSave_CallsSaveStateBinary()
    {
        _manager.AutoSave();
        _mockStatable.Received(1).SaveStateBinary(Arg.Any<BinaryWriter>());
    }

    [Test]
    public void AutoLoad_WhenNoFile_ReturnsFalse()
    {
        Assert.That(_manager.AutoLoad(), Is.False);
    }

    [Test]
    public void AutoLoad_WhenNoFile_DoesNotCallLoadStateBinary()
    {
        _manager.AutoLoad();
        _mockStatable.DidNotReceive().LoadStateBinary(Arg.Any<BinaryReader>());
    }

    [Test]
    public void AutoLoad_WhenFileExists_ReturnsTrue()
    {
        _manager.AutoSave();
        Assert.That(_manager.AutoLoad(), Is.True);
    }

    [Test]
    public void AutoLoad_WhenFileExists_CallsLoadStateBinary()
    {
        _manager.AutoSave();
        _manager.AutoLoad();
        _mockStatable.Received(1).LoadStateBinary(Arg.Any<BinaryReader>());
    }

    [Test]
    public void AutoSave_WhenSaveStateBinaryThrows_DoesNotPropagateException()
    {
        var throwingStatable = Substitute.For<IStatable>();
        throwingStatable.When(s => s.SaveStateBinary(Arg.Any<BinaryWriter>()))
                        .Do(_ => throw new IOException("Disk full"));
        var manager = new SaveStateManager(throwingStatable, _tempDir);

        Assert.That(() => manager.AutoSave(), Throws.Nothing);
    }

    // ---- GetSlotMeta ----

    [Test]
    public void GetSlotMeta_WhenNoMetaFile_ReturnsNull()
    {
        Assert.That(_manager.GetSlotMeta(0), Is.Null);
    }

    [Test]
    public void GetSlotMeta_AfterSaveSlot_ReturnsNonNull()
    {
        _manager.SaveSlot(0);
        Assert.That(_manager.GetSlotMeta(0), Is.Not.Null);
    }

    [Test]
    public void GetSlotMeta_AfterSaveSlot_TimestampIsRecent()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        _manager.SaveSlot(0);
        var after = DateTime.UtcNow.AddSeconds(1);

        var meta = _manager.GetSlotMeta(0)!;
        Assert.That(meta.Timestamp, Is.GreaterThan(before));
        Assert.That(meta.Timestamp, Is.LessThan(after));
    }

    [Test]
    public void GetSlotMeta_CorruptMetaFile_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "slot0.meta"), "not-json{{{{");
        Assert.That(_manager.GetSlotMeta(0), Is.Null);
    }

}
