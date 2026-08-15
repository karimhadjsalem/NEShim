using System.Diagnostics;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.Input;
using NEShim.Saves;

namespace NEShim.GameLoop;

/// <summary>
/// Orchestrates one active emulation frame: core execution, achievement evaluation,
/// periodic auto-save, render submission, and audio enqueue.
/// </summary>
internal sealed class FramePipeline
{
    private readonly IEmulationCore _core;
    private readonly IAudioSink _audio;
    private readonly IAchievementProcessor _achievements;
    private readonly IRenderCoordinator _render;
    private readonly ISaveManager _saveStates;
    private readonly FpsTracker _fpsTracker = new(new StopwatchClock());

    private const int AutoSaveIntervalFrames = 18_000; // ~5 min at 60 fps
    private int _autoSaveFrameCounter;

    private static readonly long SlowFrameThresholdTicks = Stopwatch.Frequency * 14 / 1000;

    public float CurrentFps => _fpsTracker.CurrentFps;

    public FramePipeline(
        IEmulationCore core,
        IAudioSink audio,
        IAchievementProcessor achievements,
        IRenderCoordinator render,
        ISaveManager saveStates)
    {
        _core = core;
        _audio = audio;
        _achievements = achievements;
        _render = render;
        _saveStates = saveStates;
    }

    /// <summary>
    /// Executes one frame: RunFrame → achievements → auto-save → render submit → audio enqueue → FPS tick.
    /// </summary>
    public void RunFrame(InputSnapshot snapshot, AppConfig config, Action? afterPresented)
    {
        bool timingEnabled = Logger.IsEnabled;
        long t0 = timingEnabled ? Stopwatch.GetTimestamp() : 0;

        var frame = _core.RunFrame(snapshot);
        long tAfterRunFrame = timingEnabled ? Stopwatch.GetTimestamp() : 0;

        _achievements.Tick();

        if (++_autoSaveFrameCounter >= AutoSaveIntervalFrames)
        {
            _autoSaveFrameCounter = 0;
            _saveStates.AutoSave();
        }

        _render.SubmitFrame(frame, config.ShowFps, CurrentFps, afterPresented);
        _audio.Enqueue(frame.AudioSamples, frame.SampleCount);

        if (timingEnabled)
        {
            long tDone = Stopwatch.GetTimestamp();
            long workTicks = tDone - t0;
            if (workTicks > SlowFrameThresholdTicks)
            {
                double ms    = workTicks                           * 1000.0 / Stopwatch.Frequency;
                double runMs = (tAfterRunFrame - t0)              * 1000.0 / Stopwatch.Frequency;
                double postMs = (tDone - tAfterRunFrame)          * 1000.0 / Stopwatch.Frequency;
                Logger.Log($"[Timing] Slow frame {ms:F2}ms — runFrame={runMs:F2} post={postMs:F2}");
            }
        }

        _fpsTracker.Tick();
    }
}
