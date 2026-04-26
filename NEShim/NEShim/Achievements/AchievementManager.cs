using BizHawk.Emulation.Common;

namespace NEShim.Achievements;

/// <summary>
/// Watches NES memory addresses each frame and fires Steam achievements when conditions are met.
///
/// Call <see cref="Tick"/> once per emulation frame (on the emulation thread, after RunFrame).
/// The <paramref name="statsReady"/> delegate must return true before any unlocks are attempted —
/// this guards against calling SetAchievement before Steam has delivered the initial stats snapshot via UserStatsReceived_t.
/// The <paramref name="unlock"/> delegate is invoked at most once per achievement per session;
/// subsequent frames that still satisfy the trigger condition are ignored.
/// </summary>
internal sealed class AchievementManager
{
    private readonly MemoryDomain?             _domain;
    private readonly IReadOnlyList<AchievementDef> _defs;
    private readonly Func<bool>               _statsReady;
    private readonly Action<string>           _unlock;
    private readonly HashSet<string>          _firedThisSession = new(StringComparer.Ordinal);

    private bool _statsWaitLogged;
    private bool _readyLogged;

    internal AchievementManager(
        IMemoryDomains        domains,
        GameAchievementConfig config,
        Func<bool>            statsReady,
        Action<string>        unlock)
    {
        _domain     = domains[config.MemoryDomain] ?? domains.MainMemory;
        _defs       = config.Achievements;
        _statsReady = statsReady;
        _unlock     = unlock;
    }

    /// <summary>
    /// Evaluates all configured achievement triggers. Call once per emulation frame.
    /// </summary>
    internal void Tick()
    {
        if (_domain is null) return;

        if (!_statsReady())
        {
            if (!_statsWaitLogged)
            {
                Logger.Log("[Achievements] Waiting for StatsReady — achievements are suppressed until Steam delivers the stats snapshot.");
                _statsWaitLogged = true;
            }
            return;
        }

        if (!_readyLogged)
        {
            Logger.Log($"[Achievements] StatsReady — evaluating {_defs.Count} trigger(s) per frame.");
            _readyLogged = true;
        }

        foreach (var def in _defs)
        {
            if (_firedThisSession.Contains(def.SteamId)) continue;

            long raw   = ReadRaw(_domain, def.Address, def.Bytes, def.BigEndian);
            long value = def.Encoding == "bcd" ? DecodeBcd(raw, def.Bytes) : raw;

            if (Matches(def.Comparison, value, def.Value))
            {
                _firedThisSession.Add(def.SteamId);
                _unlock(def.SteamId);
            }
        }
    }

    /// <summary>
    /// Reads <paramref name="byteCount"/> bytes from <paramref name="domain"/> starting at
    /// <paramref name="address"/> and assembles them into a long.
    /// When <paramref name="bigEndian"/> is true the first byte is the most significant.
    /// </summary>
    private static long ReadRaw(MemoryDomain domain, int address, int byteCount, bool bigEndian)
    {
        long value = 0;
        for (int i = 0; i < byteCount; i++)
        {
            byte b = domain.PeekByte(address + i);
            if (bigEndian)
                value = (value << 8) | b;
            else
                value |= (long)b << (i * 8);
        }
        return value;
    }

    /// <summary>
    /// Decodes a big-endian-assembled raw value as BCD.
    /// Each nibble represents one decimal digit; the most significant nibble of the highest byte
    /// is the most significant digit.
    /// Example: raw = 0x123456 (3 bytes) → 123456.
    /// </summary>
    private static long DecodeBcd(long bigEndianRaw, int byteCount)
    {
        long result     = 0;
        long multiplier = 1;
        // Iterate from the least-significant byte (low bits) to the most-significant byte (high bits).
        for (int i = 0; i < byteCount; i++)
        {
            byte b  = (byte)((bigEndianRaw >> (i * 8)) & 0xFF);
            result += (b & 0x0F) * multiplier              // units digit of this byte
                    + ((b >> 4) & 0x0F) * multiplier * 10; // tens digit of this byte
            multiplier *= 100;
        }
        return result;
    }

    private static bool Matches(string comparison, long actual, long threshold) =>
        comparison switch
        {
            "equals"         => actual == threshold,
            "greaterOrEqual" => actual >= threshold,
            "greaterThan"    => actual >  threshold,
            "lessOrEqual"    => actual <= threshold,
            "lessThan"       => actual <  threshold,
            _                => false,
        };
}
