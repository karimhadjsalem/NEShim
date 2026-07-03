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
    private readonly IMemoryReader?                _memoryReader;
    private readonly IReadOnlyList<AchievementDef> _defs;
    private readonly Func<bool>                    _statsReady;
    private readonly Action<string>                _unlock;
    private readonly HashSet<string>               _firedThisSession = new(StringComparer.Ordinal);

    private bool _statsWaitLogged;
    private bool _readyLogged;

    internal AchievementManager(
        IMemoryDomains        domains,
        GameAchievementConfig config,
        Func<bool>            statsReady,
        Action<string>        unlock)
    {
        MemoryDomain? domain = domains[config.MemoryDomain] ?? domains.MainMemory;
        _memoryReader = domain is not null ? new MemoryDomainAdapter(domain) : null;
        _defs         = config.Achievements;
        _statsReady   = statsReady;
        _unlock       = unlock;
    }

    /// <summary>
    /// Evaluates all configured achievement triggers. Call once per emulation frame.
    /// </summary>
    internal void Tick()
    {
        if (_memoryReader is null) return;

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

            long raw   = AchievementEvaluator.ReadRaw(_memoryReader, def.Address, def.Bytes, def.BigEndian);
            long value = def.Encoding == "bcd" ? AchievementEvaluator.DecodeBcd(raw, def.Bytes) : raw;

            if (AchievementEvaluator.Matches(def.Comparison, value, def.Value))
            {
                _firedThisSession.Add(def.SteamId);
                _unlock(def.SteamId);
            }
        }
    }

    private sealed class MemoryDomainAdapter : IMemoryReader
    {
        private readonly MemoryDomain _domain;
        internal MemoryDomainAdapter(MemoryDomain domain) => _domain = domain;
        public byte PeekByte(long address) => _domain.PeekByte(address);
    }
}
