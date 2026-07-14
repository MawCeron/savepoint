namespace Savepoint.Data;

public enum ScheduleType
{
    Daily,
    Weekly,
    Interval,
    OneTime,
}

public enum InterruptionLevel
{
    Gentle,
    Standard,
    Critical,
}

public sealed class SavepointEntry
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public InterruptionLevel InterruptionLevel { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SnoozeCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Daily / Weekly: time of day the savepoint is due.
    public TimeOnly? TimeOfDay { get; set; }

    // Weekly: which day it's due on.
    public DayOfWeek? DayOfWeek { get; set; }

    // Interval: how often it repeats.
    public TimeSpan? Interval { get; set; }

    // OneTime: the single instant it's due.
    public DateTime? OneTimeAt { get; set; }
}
