using Savepoint.Data;
using Savepoint.Engine.StateMachine;

namespace Savepoint.Tests;

public sealed class SavepointStateMachineTests
{
    private static readonly DateTime Start = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkDue_FromIdle_TransitionsToPending()
    {
        var machine = new SavepointStateMachine();

        machine.MarkDue();

        Assert.Equal(SavepointState.Pending, machine.State);
    }

    [Fact]
    public void MarkDue_FromNonIdle_Throws()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();

        Assert.Throws<InvalidOperationException>(machine.MarkDue);
    }

    [Fact]
    public void Tick_WhileInputActive_StaysPending()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();

        machine.Tick(Start, TimeSpan.FromSeconds(1));

        Assert.Equal(SavepointState.Pending, machine.State);
    }

    [Fact]
    public void Tick_AfterTwoSecondsIdle_EntersRecovery()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();

        machine.Tick(Start, TimeSpan.FromSeconds(2));

        Assert.Equal(SavepointState.Recovery, machine.State);
    }

    [Fact]
    public void Tick_WithinRecoveryWindow_StaysInRecovery()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();
        machine.Tick(Start, TimeSpan.FromSeconds(2));

        machine.Tick(Start + TimeSpan.FromSeconds(7), TimeSpan.Zero);

        Assert.Equal(SavepointState.Recovery, machine.State);
    }

    [Fact]
    public void Tick_AfterEightSecondRecovery_Unlocks()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();
        machine.Tick(Start, TimeSpan.FromSeconds(2));

        machine.Tick(Start + TimeSpan.FromSeconds(8), TimeSpan.Zero);

        Assert.Equal(SavepointState.Unlocked, machine.State);
    }

    [Theory]
    [InlineData(InterruptionLevel.Gentle)]
    [InlineData(InterruptionLevel.Critical)]
    public void CanSnooze_GentleOrCritical_IsAlwaysFalse(InterruptionLevel level)
    {
        var machine = UnlockedMachine();

        Assert.False(machine.CanSnooze(level));
    }

    [Fact]
    public void Snooze_GentleOrCritical_Throws()
    {
        var machine = UnlockedMachine();

        Assert.Throws<InvalidOperationException>(() => machine.Snooze(InterruptionLevel.Gentle));
    }

    [Fact]
    public void Standard_CanSnoozeUpToThreeTimes_ThenMustConfirm()
    {
        var machine = UnlockedMachine();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(machine.CanSnooze(InterruptionLevel.Standard));
            machine.Snooze(InterruptionLevel.Standard);
            Assert.Equal(SavepointState.Pending, machine.State);
            Assert.Equal(i + 1, machine.SnoozeCount);

            // Re-arm to Unlocked for the next snooze attempt.
            machine.Tick(Start, TimeSpan.FromSeconds(2));
            machine.Tick(Start + TimeSpan.FromSeconds(8), TimeSpan.Zero);
        }

        Assert.False(machine.CanSnooze(InterruptionLevel.Standard));
        Assert.Throws<InvalidOperationException>(() => machine.Snooze(InterruptionLevel.Standard));
    }

    [Fact]
    public void Confirm_FromUnlocked_ReturnsToIdleAndResetsSnoozeCount()
    {
        var machine = UnlockedMachine();
        machine.Snooze(InterruptionLevel.Standard);
        machine.Tick(Start, TimeSpan.FromSeconds(2));
        machine.Tick(Start + TimeSpan.FromSeconds(8), TimeSpan.Zero);

        machine.Confirm();

        Assert.Equal(SavepointState.Idle, machine.State);
        Assert.Equal(0, machine.SnoozeCount);
    }

    [Fact]
    public void Confirm_FromNonUnlocked_Throws()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();

        Assert.Throws<InvalidOperationException>(machine.Confirm);
    }

    private static SavepointStateMachine UnlockedMachine()
    {
        var machine = new SavepointStateMachine();
        machine.MarkDue();
        machine.Tick(Start, TimeSpan.FromSeconds(2));
        machine.Tick(Start + TimeSpan.FromSeconds(8), TimeSpan.Zero);
        return machine;
    }
}
