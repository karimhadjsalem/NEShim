using System.IO;
using BizHawk.Emulation.Common;
using Moq;
using NEShim.Saves;

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
    public void LoadFromDisk_WhenFileDoesNotExist_DoesNotCallStoreSaveRam()
    {
        var mockSaveRam = new Mock<ISaveRam>();
        var manager     = new SaveRamManager(mockSaveRam.Object, _tempFile);

        manager.LoadFromDisk();

        mockSaveRam.Verify(m => m.StoreSaveRam(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public void LoadFromDisk_WhenFileExists_PassesFileContentsToStoreSaveRam()
    {
        byte[] expected = { 0x01, 0x02, 0x03, 0xFF };
        File.WriteAllBytes(_tempFile, expected);

        var mockSaveRam = new Mock<ISaveRam>();
        var manager     = new SaveRamManager(mockSaveRam.Object, _tempFile);

        manager.LoadFromDisk();

        mockSaveRam.Verify(
            m => m.StoreSaveRam(It.Is<byte[]>(b => b.SequenceEqual(expected))),
            Times.Once);
    }

    [Test]
    public void SaveToDisk_WhenSaveRamNotModified_DoesNotCreateFile()
    {
        var mockSaveRam = new Mock<ISaveRam>();
        mockSaveRam.Setup(m => m.SaveRamModified).Returns(false);

        var manager = new SaveRamManager(mockSaveRam.Object, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenModifiedAndDataAvailable_WritesDataToDisk()
    {
        byte[] data     = { 0x0A, 0x0B, 0x0C };
        var mockSaveRam = new Mock<ISaveRam>();
        mockSaveRam.Setup(m => m.SaveRamModified).Returns(true);
        mockSaveRam.Setup(m => m.CloneSaveRam()).Returns(data);

        var manager = new SaveRamManager(mockSaveRam.Object, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.True);
        Assert.That(File.ReadAllBytes(_tempFile), Is.EqualTo(data));
    }

    [Test]
    public void SaveToDisk_WhenCloneSaveRamReturnsNull_DoesNotCreateFile()
    {
        var mockSaveRam = new Mock<ISaveRam>();
        mockSaveRam.Setup(m => m.SaveRamModified).Returns(true);
        mockSaveRam.Setup(m => m.CloneSaveRam()).Returns((byte[]?)null);

        var manager = new SaveRamManager(mockSaveRam.Object, _tempFile);
        manager.SaveToDisk();

        Assert.That(File.Exists(_tempFile), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenCloneSaveRamReturnsEmptyArray_DoesNotCreateFile()
    {
        var mockSaveRam = new Mock<ISaveRam>();
        mockSaveRam.Setup(m => m.SaveRamModified).Returns(true);
        mockSaveRam.Setup(m => m.CloneSaveRam()).Returns(Array.Empty<byte>());

        var manager = new SaveRamManager(mockSaveRam.Object, _tempFile);
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
            byte[] data     = { 1, 2 };
            var mockSaveRam = new Mock<ISaveRam>();
            mockSaveRam.Setup(m => m.SaveRamModified).Returns(true);
            mockSaveRam.Setup(m => m.CloneSaveRam()).Returns(data);

            var manager = new SaveRamManager(mockSaveRam.Object, srmPath);
            manager.SaveToDisk();

            Assert.That(File.Exists(srmPath), Is.True);
        }
        finally
        {
            if (Directory.Exists(subDir)) Directory.Delete(subDir, recursive: true);
        }
    }
}
