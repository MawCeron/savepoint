using Savepoint.Data;

namespace Savepoint.Engine.Scheduler;

public static class SavepointScheduler
{
    public static bool IsDue(SavepointEntry entry, DateTime now)
    {
        if (!entry.IsEnabled)
        {
            return false;
        }

        return entry.ScheduleType switch
        {
            ScheduleType.Daily => IsDailyDue(entry, now),
            ScheduleType.Weekly => IsWeeklyDue(entry, now),
            ScheduleType.Interval => IsIntervalDue(entry, now),
            ScheduleType.OneTime => IsOneTimeDue(entry, now),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.ScheduleType, "Unknown schedule type."),
        };
    }

    private static bool IsDailyDue(SavepointEntry entry, DateTime now)
    {
        if (entry.TimeOfDay is not { } timeOfDay)
        {
            return false;
        }

        if (TimeOnly.FromDateTime(now) < timeOfDay)
        {
            return false;
        }

        return entry.LastTriggeredAt is not { } last || last.Date < now.Date;
    }

    private static bool IsWeeklyDue(SavepointEntry entry, DateTime now)
    {
        if (entry.TimeOfDay is not { } timeOfDay || entry.DayOfWeek is not { } dayOfWeek)
        {
            return false;
        }

        if (now.DayOfWeek != dayOfWeek)
        {
            return false;
        }

        if (TimeOnly.FromDateTime(now) < timeOfDay)
        {
            return false;
        }

        return entry.LastTriggeredAt is not { } last || last.Date < now.Date;
    }

    private static bool IsIntervalDue(SavepointEntry entry, DateTime now)
    {
        if (entry.Interval is not { } interval)
        {
            return false;
        }

        var last = entry.LastTriggeredAt ?? entry.CreatedAt;
        return now - last >= interval;
    }

    private static bool IsOneTimeDue(SavepointEntry entry, DateTime now)
    {
        if (entry.OneTimeAt is not { } oneTimeAt)
        {
            return false;
        }

        if (entry.LastTriggeredAt is not null)
        {
            return false;
        }

        return now >= oneTimeAt;
    }
}
