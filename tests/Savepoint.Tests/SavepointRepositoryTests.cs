using Savepoint.Data;

namespace Savepoint.Tests;

public sealed class SavepointRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"savepoint-tests-{Guid.NewGuid():N}.db");
    private readonly SavepointRepository _repository;

    public SavepointRepositoryTests()
    {
        _repository = new SavepointRepository(_dbPath);
    }

    public void Dispose() => File.Delete(_dbPath);

    [Fact]
    public void RoundTrips_Daily() => AssertRoundTrip(new SavepointEntry
    {
        Name = "Drink water",
        Icon = "water",
        ScheduleType = ScheduleType.Daily,
        InterruptionLevel = InterruptionLevel.Gentle,
        CreatedAt = DateTime.UtcNow,
        TimeOfDay = new TimeOnly(14, 30),
    });

    [Fact]
    public void RoundTrips_Weekly() => AssertRoundTrip(new SavepointEntry
    {
        Name = "Water the plants",
        Icon = "plant",
        ScheduleType = ScheduleType.Weekly,
        InterruptionLevel = InterruptionLevel.Standard,
        CreatedAt = DateTime.UtcNow,
        TimeOfDay = new TimeOnly(9, 0),
        DayOfWeek = DayOfWeek.Monday,
    });

    [Fact]
    public void RoundTrips_Interval() => AssertRoundTrip(new SavepointEntry
    {
        Name = "Stretch",
        Icon = "stretch",
        ScheduleType = ScheduleType.Interval,
        InterruptionLevel = InterruptionLevel.Standard,
        CreatedAt = DateTime.UtcNow,
        Interval = TimeSpan.FromMinutes(45),
    });

    [Fact]
    public void RoundTrips_OneTime() => AssertRoundTrip(new SavepointEntry
    {
        Name = "Take medication",
        Icon = "pill",
        ScheduleType = ScheduleType.OneTime,
        InterruptionLevel = InterruptionLevel.Critical,
        CreatedAt = DateTime.UtcNow,
        OneTimeAt = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
    });

    [Fact]
    public void GetAll_ReturnsEveryStoredSavepoint()
    {
        _repository.Add(new SavepointEntry { Name = "A", Icon = "a", ScheduleType = ScheduleType.Daily, CreatedAt = DateTime.UtcNow });
        _repository.Add(new SavepointEntry { Name = "B", Icon = "b", ScheduleType = ScheduleType.Interval, CreatedAt = DateTime.UtcNow, Interval = TimeSpan.FromHours(1) });

        var all = _repository.GetAll();

        Assert.Equal(2, all.Count);
    }

    private void AssertRoundTrip(SavepointEntry entry)
    {
        var id = _repository.Add(entry);

        var stored = _repository.GetById(id);
        Assert.NotNull(stored);
        Assert.Equal(entry.Name, stored!.Name);
        Assert.Equal(entry.Icon, stored.Icon);
        Assert.Equal(entry.ScheduleType, stored.ScheduleType);
        Assert.Equal(entry.InterruptionLevel, stored.InterruptionLevel);
        Assert.Equal(entry.TimeOfDay, stored.TimeOfDay);
        Assert.Equal(entry.DayOfWeek, stored.DayOfWeek);
        Assert.Equal(entry.Interval, stored.Interval);
        Assert.Equal(entry.OneTimeAt, stored.OneTimeAt);

        stored.Name = "Renamed";
        stored.SnoozeCount = 2;
        _repository.Update(stored);

        var updated = _repository.GetById(id);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal(2, updated.SnoozeCount);

        _repository.Delete(id);
        Assert.Null(_repository.GetById(id));
    }
}
