using Savepoint.Data;
using Savepoint.Engine.Scheduler;

namespace Savepoint.Tests;

public sealed class SavepointSchedulerTests
{
    private static readonly DateTime Now = new(2026, 7, 14, 14, 30, 0, DateTimeKind.Utc); // Tuesday

    private static SavepointEntry Entry(ScheduleType type) => new()
    {
        Name = "Test",
        Icon = "icon",
        ScheduleType = type,
        CreatedAt = Now.AddDays(-7),
    };

    [Fact]
    public void Disabled_IsNeverDue()
    {
        var entry = Entry(ScheduleType.Daily);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.IsEnabled = false;

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Theory]
    [InlineData(10, 0, true)] // time passed, never triggered -> due
    [InlineData(15, 0, false)] // time not yet passed -> not due
    public void Daily(int hour, int minute, bool expectedDue)
    {
        var entry = Entry(ScheduleType.Daily);
        entry.TimeOfDay = new TimeOnly(hour, minute);

        Assert.Equal(expectedDue, SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Daily_AlreadyTriggeredToday_IsNotDue()
    {
        var entry = Entry(ScheduleType.Daily);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.LastTriggeredAt = Now.Date.AddHours(10);

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Daily_TriggeredYesterday_IsDueAgainToday()
    {
        var entry = Entry(ScheduleType.Daily);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.LastTriggeredAt = Now.Date.AddDays(-1).AddHours(10);

        Assert.True(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Weekly_WrongDayOfWeek_IsNotDue()
    {
        var entry = Entry(ScheduleType.Weekly);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.DayOfWeek = DayOfWeek.Monday; // Now is a Tuesday

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Weekly_CorrectDayAndTimePassed_IsDue()
    {
        var entry = Entry(ScheduleType.Weekly);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.DayOfWeek = Now.DayOfWeek;

        Assert.True(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Weekly_AlreadyTriggeredToday_IsNotDue()
    {
        var entry = Entry(ScheduleType.Weekly);
        entry.TimeOfDay = new TimeOnly(10, 0);
        entry.DayOfWeek = Now.DayOfWeek;
        entry.LastTriggeredAt = Now.Date.AddHours(10);

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Interval_NeverTriggered_DueAfterIntervalSinceCreation()
    {
        var entry = Entry(ScheduleType.Interval);
        entry.Interval = TimeSpan.FromHours(1);
        entry.CreatedAt = Now.AddHours(-2);

        Assert.True(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Interval_NeverTriggered_NotYetDue()
    {
        var entry = Entry(ScheduleType.Interval);
        entry.Interval = TimeSpan.FromHours(1);
        entry.CreatedAt = Now.AddMinutes(-30);

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void Interval_CrossingMidnight_IsDueOnceElapsed()
    {
        var entry = Entry(ScheduleType.Interval);
        entry.Interval = TimeSpan.FromMinutes(30);
        entry.LastTriggeredAt = new DateTime(2026, 7, 13, 23, 50, 0, DateTimeKind.Utc);

        var justBefore = new DateTime(2026, 7, 14, 0, 15, 0, DateTimeKind.Utc); // 25 min elapsed
        var justAfter = new DateTime(2026, 7, 14, 0, 25, 0, DateTimeKind.Utc); // 35 min elapsed

        Assert.False(SavepointScheduler.IsDue(entry, justBefore));
        Assert.True(SavepointScheduler.IsDue(entry, justAfter));
    }

    [Fact]
    public void OneTime_PastDue_IsDue()
    {
        var entry = Entry(ScheduleType.OneTime);
        entry.OneTimeAt = Now.AddMinutes(-1);

        Assert.True(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void OneTime_NotYetDue_IsNotDue()
    {
        var entry = Entry(ScheduleType.OneTime);
        entry.OneTimeAt = Now.AddMinutes(1);

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }

    [Fact]
    public void OneTime_AlreadyTriggered_NeverDueAgain()
    {
        var entry = Entry(ScheduleType.OneTime);
        entry.OneTimeAt = Now.AddMinutes(-1);
        entry.LastTriggeredAt = Now.AddSeconds(-30);

        Assert.False(SavepointScheduler.IsDue(entry, Now));
    }
}
