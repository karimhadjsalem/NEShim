using System.IO;
using NEShim.Emulation;
using NEShim.Saves;
using NSubstitute;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for SaveRamManager — these cross the file system boundary
/// and are kept separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class SaveRamManagerTests
{
    private string _tempFile = null!;

    [SetUp]
    public void SetUp()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.srm");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Test]
    public void LoadFromDisk_WhenFileDoesNotExist_DoesNotCallSetSaveRam()
    {
        var mockCore = Substitute.For<IEmulationCore>();
        var manager  = new SaveRamManager(mockCore, _tempFile);

        manager.LoadFromDisk();

        mockCore.DidNotReceive().SetSaveRam(Arg.Any<byte[]>());
    }

    [Test]
    public void LoadFromDisk_WhenFileExists_PassesFileContentsToSetSaveRam()
    {
        byte[] expected = { 0x01, 0x02, 0x03, 0xFF };
        File.WriteAllBytes(_tempFile, expected);

        var mockCore = Substitute.For<IEmulationCore>();
        var manager  = new SaveRamManager(mockCore, _tempFile);

        manager.LoadFromDisk();

        mockCore.Received(1).SetSaveRam(Arg.Is<byte[]>(b => b.SequenceEqual(expected)));
    }

    [Test]
    public void SaveToDisk_WhenSaveRamNotModified_DoesNotCreateFile()
    {
        var mockCore = Substitute.For<IEmulationCore>();
        mockCore.SaveRamModified.Returns(false);

        var manager = new SaveRamManager(mockCore, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenModifiedAndDataAvailable_WritesDataToDisk()
    {
        byte[] data  = { 0x0A, 0x0B, 0x0C };
        var mockCore = Substitute.For<IEmulationCore>();
        mockCore.SaveRamModified.Returns(true);
        mockCore.GetSaveRam().Returns(data);

        var manager = new SaveRamManager(mockCore, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.True);
        Assert.That(File.ReadAllBytes(_tempFile), Is.EqualTo(data));
    }

    [Test]
    public void SaveToDisk_WhenGetSaveRamReturnsNull_DoesNotCreateFile()
    {
        var mockCore = Substitute.For<IEmulationCore>();
        mockCore.SaveRamModified.Returns(true);
        mockCore.GetSaveRam().Returns((byte[]?)null);

        var manager = new SaveRamManager(mockCore, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenGetSaveRamReturnsEmptyArray_DoesNotCreateFile()
    {
        var mockCore = Substitute.For<IEmulationCore>();
        mockCore.SaveRamModified.Returns(true);
        mockCore.GetSaveRam().Returns(Array.Empty<byte>());

        var manager = new SaveRamManager(mockCore, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.False);
    }

    [Test]
    public void SaveToDisk_CreatesParentDirectory_WhenItDoesNotExist()
    {
        string subDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string srmPath = Path.Combine(subDir, "save.srm");
        try
        {
            byte[] data  = { 1, 2 };
            var mockCore = Substitute.For<IEmulationCore>();
            mockCore.SaveRamModified.Returns(true);
            mockCore.GetSaveRam().Returns(data);

            var manager = new SaveRamManager(mockCore, srmPath);
            manager.SaveToDisk();

            Assert.That(File.Exists(srmPath), Is.True);
        }
        finally
        {
            if (Directory.Exists(subDir)) Directory.Delete(subDir, recursive: true);
        }
    }
}
