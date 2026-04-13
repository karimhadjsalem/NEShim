using System.IO;
using NEShim.UI;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for MainMenuScreen.ResolveAssetPath — require actual file system access.
/// </summary>
[TestFixture]
internal class MainMenuScreenPathTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void ResolveAssetPath_AbsolutePathExists_ReturnsSamePath()
    {
        string file = Path.Combine(_tempDir, "image.png");
        File.WriteAllBytes(file, Array.Empty<byte>());

        string? result = MainMenuScreen.ResolveAssetPath(file);
        Assert.That(result, Is.EqualTo(file));
    }

    [Test]
    public void ResolveAssetPath_AbsolutePathDoesNotExist_ReturnsNull()
    {
        string file   = Path.Combine(_tempDir, "missing.png");
        string? result = MainMenuScreen.ResolveAssetPath(file);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveAssetPath_RelativePath_NotFoundAnywhere_ReturnsNull()
    {
        // A filename that definitely doesn't exist next to the exe or in the cwd
        string? result = MainMenuScreen.ResolveAssetPath("__no_such_file_8f3a9b__.png");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveAssetPath_RelativePath_FoundInWorkingDirectory_ReturnsAbsolutePath()
    {
        // Create a file in the current working directory
        string filename = $"test_asset_{Guid.NewGuid():N}.png";
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), filename);
        try
        {
            File.WriteAllBytes(fullPath, Array.Empty<byte>());
            string? result = MainMenuScreen.ResolveAssetPath(filename);
            Assert.That(result, Is.Not.Null);
            Assert.That(File.Exists(result!), Is.True);
        }
        finally
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }
}
