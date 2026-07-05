using System.Text.Json;
using System.Text.Json.Serialization;
using NEShim.Achievements;

namespace NEShim.SealAchievementsUI;

internal sealed class MainForm : Form
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
    };

    // ── Seal controls ────────────────────────────────────────────────────────
    private readonly TextBox _keyBox              = new() { PlaceholderText = "Key file path, or paste base64 key…" };
    private readonly Button  _keyBrowseBtn        = new() { Text = "Browse…" };
    private readonly TextBox _achievementsBox     = new() { PlaceholderText = "Path to achievements.json…" };
    private readonly Button  _achievementsBrowseBtn = new() { Text = "Browse…" };
    private readonly Button  _sealBtn             = new() { Text = "Seal" };

    // ── Validate controls ────────────────────────────────────────────────────
    private readonly TextBox _packageDirBox       = new() { PlaceholderText = "Path to package directory…" };
    private readonly Button  _packageDirBrowseBtn = new() { Text = "Browse…" };
    private readonly TextBox _pubKeyBox           = new() { PlaceholderText = "Public key override (optional — leave blank to use embedded key or config.json)" };
    private readonly Button  _validateBtn         = new() { Text = "Validate" };

    // ── Output ───────────────────────────────────────────────────────────────
    private readonly RichTextBox _output = new() { ReadOnly = true, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None };

    internal MainForm()
    {
        Text            = "Achievement Sealer";
        ClientSize      = new Size(596, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        Controls.AddRange([BuildSealGroup(), BuildValidateGroup(), BuildOutputGroup()]);

        _keyBrowseBtn.Click          += (_, _) => BrowseFile(_keyBox,          "Key files|*.txt;*.key|All files|*.*");
        _achievementsBrowseBtn.Click += (_, _) => BrowseFile(_achievementsBox, "JSON files|*.json|All files|*.*");
        _packageDirBrowseBtn.Click   += (_, _) => BrowseFolder(_packageDirBox);
        _sealBtn.Click               += (_, _) => OnSealClicked();
        _validateBtn.Click           += (_, _) => OnValidateClicked();
    }

    // ── Layout builders ──────────────────────────────────────────────────────

    private GroupBox BuildSealGroup()
    {
        const int btnX = 480;
        const int btnW = 88;

        var group = Group("Seal", new Point(8, 8), new Size(580, 120));

        Position(_keyBox,               new Point(100, 22), btnX - 8 - 100);
        Position(_keyBrowseBtn,         new Point(btnX, 21), btnW);
        Position(_achievementsBox,      new Point(120, 51), btnX - 8 - 120);
        Position(_achievementsBrowseBtn, new Point(btnX, 50), btnW);
        _sealBtn.Location = new Point(btnX, 80); _sealBtn.Width = btnW;

        group.Controls.AddRange([
            MakeLabel("Private key",        new Point(8, 25)),
            _keyBox, _keyBrowseBtn,
            MakeLabel("achievements.json",  new Point(8, 54)),
            _achievementsBox, _achievementsBrowseBtn,
            _sealBtn,
        ]);
        return group;
    }

    private GroupBox BuildValidateGroup()
    {
        const int btnX = 480;
        const int btnW = 88;

        var group = Group("Validate", new Point(8, 136), new Size(580, 128));

        Position(_packageDirBox,       new Point(122, 22), btnX - 8 - 122);
        Position(_packageDirBrowseBtn, new Point(btnX, 21), btnW);
        Position(_pubKeyBox,           new Point(8, 70), 556);
        _validateBtn.Location = new Point(btnX, 96); _validateBtn.Width = btnW;

        group.Controls.AddRange([
            MakeLabel("Package directory",            new Point(8, 25)),
            _packageDirBox, _packageDirBrowseBtn,
            MakeLabel("Public key override (optional)", new Point(8, 52)),
            _pubKeyBox, _validateBtn,
        ]);
        return group;
    }

    private GroupBox BuildOutputGroup()
    {
        var group = Group("Output", new Point(8, 272), new Size(580, 260));
        _output.Location = new Point(8, 18);
        _output.Size     = new Size(564, 234);
        group.Controls.Add(_output);
        return group;
    }

    private static GroupBox Group(string title, Point location, Size size) =>
        new() { Text = title, Location = location, Size = size };

    private static Label MakeLabel(string text, Point location) =>
        new() { Text = text, Location = location, AutoSize = true };

    private static void Position(Control control, Point location, int width)
    {
        control.Location = location;
        control.Width    = width;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnSealClicked()
    {
        AppendSeparator("Seal");

        var key = ResolvePrivateKey(_keyBox.Text.Trim());
        if (key is null) return;

        var (configs, path) = LoadAchievements(_achievementsBox.Text.Trim());
        if (configs is null) return;

        foreach (var (hash, config) in configs)
            AppendLine($"ROM {hash[..Math.Min(12, hash.Length)]}…  ({config.Achievements.Count} achievement(s))");

        var result = SealingService.Seal(configs, key);

        foreach (var (_, config) in configs)
            foreach (var def in config.Achievements)
                AppendLine(string.IsNullOrWhiteSpace(def.SteamId)
                    ? "  [skip]   (no SteamId)"
                    : $"  [sealed] {def.SteamId}");

        try   { File.WriteAllText(path!, JsonSerializer.Serialize(configs, JsonOptions)); }
        catch (Exception ex) { AppendLine($"Failed to write {path}: {ex.Message}", Color.Red); return; }

        AppendLine($"\nDone. {result.Sealed} sealed, {result.Skipped} skipped → {path}", Color.DarkGreen);
    }

    private void OnValidateClicked()
    {
        AppendSeparator("Validate");

        var dir = _packageDirBox.Text.Trim();
        if (!Directory.Exists(dir)) { AppendLine($"Directory not found: {dir}", Color.Red); return; }

        var publicKey = ResolvePublicKey(_pubKeyBox.Text.Trim(), dir);
        if (publicKey is null) return;

        var (configs, _) = LoadAchievements(Path.Combine(dir, "achievements.json"));
        if (configs is null) return;

        int totalValid = 0, totalFailed = 0;
        foreach (var (hash, config) in configs)
        {
            AppendLine($"ROM {hash[..Math.Min(12, hash.Length)]}…  ({config.Achievements.Count} achievement(s))");
            var result = ValidationService.Validate(
                new Dictionary<string, GameAchievementConfig> { [hash] = config },
                publicKey,
                (steamId, ok) => AppendLine(
                    ok ? $"  [OK]   {steamId}" : $"  [FAIL] {steamId}",
                    ok ? Color.DarkGreen : Color.Red));
            totalValid  += result.Valid;
            totalFailed += result.Failed;
        }

        AppendLine(
            totalFailed == 0
                ? $"\nResult: {totalValid}/{totalValid + totalFailed} valid — All OK."
                : $"\nResult: {totalValid}/{totalValid + totalFailed} valid — {totalFailed} FAILED.",
            totalFailed == 0 ? Color.DarkGreen : Color.Red);
    }

    // ── Key resolution ───────────────────────────────────────────────────────

    private string? ResolvePrivateKey(string input)
    {
        if (string.IsNullOrEmpty(input)) { AppendLine("Private key is required.", Color.Red); return null; }
        if (File.Exists(input)) return File.ReadAllText(input).Trim();

        try   { Convert.FromBase64String(input); return input; }
        catch { AppendLine("Value is not a valid file path or base64 key.", Color.Red); return null; }
    }

    private string? ResolvePublicKey(string explicitKey, string packageDir)
    {
        if (!string.IsNullOrEmpty(explicitKey)) return explicitKey;

        if (!string.IsNullOrEmpty(AchievementSigner.EmbeddedPublicKeyBase64))
        {
            AppendLine("Using public key embedded in tool binary.", Color.Gray);
            return AchievementSigner.EmbeddedPublicKeyBase64;
        }

        var configPath = Path.Combine(packageDir, "config.json");
        if (!File.Exists(configPath)) { AppendLine($"config.json not found in {packageDir}", Color.Red); return null; }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var key = doc.RootElement.TryGetProperty("achievementPublicKey", out var el) ? el.GetString() : null;
            if (!string.IsNullOrEmpty(key)) return key;
        }
        catch (Exception ex) { AppendLine($"Failed to read config.json: {ex.Message}", Color.Red); return null; }

        AppendLine("achievementPublicKey not set in config.json. Use the public key override field.", Color.Red);
        return null;
    }

    // ── JSON loading ─────────────────────────────────────────────────────────

    private (Dictionary<string, GameAchievementConfig>? configs, string? path) LoadAchievements(string path)
    {
        if (!File.Exists(path)) { AppendLine($"File not found: {path}", Color.Red); return (null, null); }

        try
        {
            var configs = JsonSerializer.Deserialize<Dictionary<string, GameAchievementConfig>>(
                File.ReadAllText(path), JsonOptions);
            if (configs is null || configs.Count == 0) { AppendLine("No entries found in achievements.json.", Color.Red); return (null, null); }
            return (configs, path);
        }
        catch (Exception ex) { AppendLine($"Failed to parse achievements.json: {ex.Message}", Color.Red); return (null, null); }
    }

    // ── Output helpers ───────────────────────────────────────────────────────

    private void AppendSeparator(string title)
    {
        if (_output.TextLength > 0) _output.AppendText("\n");
        AppendLine($"── {title} ──────────────────────────────────────────", Color.Gray);
    }

    private void AppendLine(string text) => AppendLine(text, _output.ForeColor);

    private void AppendLine(string text, Color color)
    {
        _output.SelectionStart  = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor  = color;
        _output.AppendText(text + "\n");
        _output.SelectionColor  = _output.ForeColor;
        _output.ScrollToCaret();
    }

    // ── File/folder dialogs ──────────────────────────────────────────────────

    private static void BrowseFile(TextBox target, string filter)
    {
        using var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() == DialogResult.OK) target.Text = dlg.FileName;
    }

    private static void BrowseFolder(TextBox target)
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() == DialogResult.OK) target.Text = dlg.SelectedPath;
    }
}
