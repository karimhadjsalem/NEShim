using System.IO;
using System.Text.RegularExpressions;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for Logger — these cross the file system boundary
/// and are kept separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class LoggerTests
{
    private string _logFile = null!;

    [SetUp]
    public void SetUp()
    {
        _logFile = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(_logFile);
    }

    [TearDown]
    public void TearDown()
    {
        Logger.Reset(_logFile); // ensure _enabled is false before next test
        if (File.Exists(_logFile)) File.Delete(_logFile);
    }

    // ---- Disabled-by-default behaviour ----

    [Test]
    public void Log_BeforeEnable_DoesNotCreateFile()
    {
        Logger.Log("should be suppressed");

        Assert.That(File.Exists(_logFile), Is.False);
    }

    [Test]
    public void Log_BeforeEnable_DoesNotWriteAnything_WhenFileAlreadyExists()
    {
        File.WriteAllText(_logFile, "pre-existing");

        Logger.Log("should be suppressed");

        Assert.That(File.ReadAllText(_logFile), Is.EqualTo("pre-existing"));
    }

    // ---- Enable behaviour ----

    [Test]
    public void Enable_CreatesLogFile()
    {
        Logger.Enable();

        Assert.That(File.Exists(_logFile), Is.True);
    }

    [Test]
    public void Enable_WritesSessionHeader()
    {
        Logger.Enable();

        string content = File.ReadAllText(_logFile);
        Assert.That(content, Does.Contain("NEShim session started"));
    }

    [Test]
    public void Enable_SessionHeaderContainsCurrentDate()
    {
        string expectedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        Logger.Enable();

        string content = File.ReadAllText(_logFile);
        Assert.That(content, Does.Contain(expectedDate));
    }

    // ---- Log-after-enable behaviour ----

    [Test]
    public void Log_AfterEnable_WritesLineToFile()
    {
        Logger.Enable();

        Logger.Log("hello world");

        string content = File.ReadAllText(_logFile);
        Assert.That(content, Does.Contain("hello world"));
    }

    [Test]
    public void Log_AfterEnable_PrefixesLineWithTimestamp()
    {
        Logger.Enable();

        Logger.Log("timestamped");

        string[] lines = File.ReadAllLines(_logFile);
        // Skip the header line written by Enable(); check the Log() line.
        string logLine = lines.First(l => l.Contains("timestamped"));
        // Expect HH:mm:ss.fff prefix
        Assert.That(logLine, Does.Match(@"^\d{2}:\d{2}:\d{2}\.\d{3} "));
    }

    [Test]
    public void Log_AfterEnable_AppendsMessageVerbatim()
    {
        const string message = "exact message content 123";
        Logger.Enable();

        Logger.Log(message);

        string content = File.ReadAllText(_logFile);
        Assert.That(content, Does.Contain(message));
    }

    [Test]
    public void Log_MultipleCalls_AllLinesAppendedInOrder()
    {
        Logger.Enable();

        Logger.Log("first");
        Logger.Log("second");
        Logger.Log("third");

        string content = File.ReadAllText(_logFile);
        int posFirst  = content.IndexOf("first",  StringComparison.Ordinal);
        int posSecond = content.IndexOf("second", StringComparison.Ordinal);
        int posThird  = content.IndexOf("third",  StringComparison.Ordinal);

        Assert.That(posFirst,  Is.GreaterThan(-1));
        Assert.That(posSecond, Is.GreaterThan(posFirst));
        Assert.That(posThird,  Is.GreaterThan(posSecond));
    }

    [Test]
    public void Log_EachLineTerminatedWithNewline()
    {
        Logger.Enable();
        Logger.Log("line");

        string content = File.ReadAllText(_logFile);
        // Every line (including the header) must end with a newline.
        foreach (string line in content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.That(content, Does.Contain(line + Environment.NewLine));
        }
    }

    // ---- Enable called twice ----

    [Test]
    public void Enable_CalledTwice_WritesTwoHeaders()
    {
        Logger.Enable();
        Logger.Enable();

        string content = File.ReadAllText(_logFile);
        int count = Regex.Matches(content, "NEShim session started").Count;
        Assert.That(count, Is.EqualTo(2));
    }

    // ---- Reset behaviour ----

    [Test]
    public void Reset_AfterEnable_SuppressesSubsequentLogs()
    {
        Logger.Enable();
        Logger.Log("before reset");

        string newPath = Path.Combine(Path.GetTempPath(), $"neshim_reset_{Guid.NewGuid()}.log");
        try
        {
            Logger.Reset(newPath);
            Logger.Log("after reset — should not appear");

            Assert.That(File.Exists(newPath), Is.False);
        }
        finally
        {
            if (File.Exists(newPath)) File.Delete(newPath);
        }
    }

    [Test]
    public void Reset_RedirectsOutputToNewPath()
    {
        Logger.Enable();
        Logger.Log("original path");

        string newPath = Path.Combine(Path.GetTempPath(), $"neshim_redirect_{Guid.NewGuid()}.log");
        try
        {
            Logger.Reset(newPath);
            Logger.Enable();
            Logger.Log("redirected");

            Assert.That(File.Exists(newPath), Is.True);
            Assert.That(File.ReadAllText(newPath), Does.Contain("redirected"));
        }
        finally
        {
            Logger.Reset(_logFile); // restore for TearDown
            if (File.Exists(newPath)) File.Delete(newPath);
        }
    }
}
