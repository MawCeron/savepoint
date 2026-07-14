using Savepoint.Data;

namespace Savepoint.Engine.StateMachine;

public enum SavepointState
{
    Idle,
    Pending,
    Visible,
    Recovery,
    Unlocked,
}

public sealed class SavepointStateMachine
{
    private static readonly TimeSpan InputIdleThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryDuration = TimeSpan.FromSeconds(8);
    private const int MaxStandardSnoozes = 3;

    private DateTime? _recoveryStartedAt;

    public SavepointState State { get; private set; } = SavepointState.Idle;
    public int SnoozeCount { get; private set; }

    /// <summary>Called by the scheduler when a savepoint becomes due.</summary>
    public void MarkDue()
    {
        if (State != SavepointState.Idle)
        {
            throw new InvalidOperationException($"Cannot mark due from state {State}.");
        }

        State = SavepointState.Pending;
    }

    /// <summary>
    /// Advances time-based transitions. Call periodically with the current time and
    /// how long the user has been idle (no keyboard/mouse activity).
    /// </summary>
    public void Tick(DateTime now, TimeSpan timeSinceLastInput)
    {
        if (State == SavepointState.Pending && timeSinceLastInput >= InputIdleThreshold)
        {
            // The overlay appears and its recovery window starts together (FR-3.2).
            State = SavepointState.Visible;
            State = SavepointState.Recovery;
            _recoveryStartedAt = now;
        }
        else if (State == SavepointState.Recovery && now - _recoveryStartedAt >= RecoveryDuration)
        {
            State = SavepointState.Unlocked;
        }
    }

    public bool CanSnooze(InterruptionLevel level) =>
        State == SavepointState.Unlocked
        && level == InterruptionLevel.Standard
        && SnoozeCount < MaxStandardSnoozes;

    /// <summary>Only available for Standard savepoints under their snooze limit (FR-4.3).</summary>
    public void Snooze(InterruptionLevel level)
    {
        if (!CanSnooze(level))
        {
            throw new InvalidOperationException("Snooze is not available in the current state/level.");
        }

        SnoozeCount++;
        State = SavepointState.Pending;
        _recoveryStartedAt = null;
    }

    /// <summary>
    /// Resolves the occurrence: Confirm for Standard/Critical, Dismiss for Gentle.
    /// Both are the same transition — only the button label differs in the UI.
    /// </summary>
    public void Confirm()
    {
        if (State != SavepointState.Unlocked)
        {
            throw new InvalidOperationException($"Cannot confirm from state {State}.");
        }

        State = SavepointState.Idle;
        SnoozeCount = 0;
        _recoveryStartedAt = null;
    }
}
