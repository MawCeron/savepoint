using Savepoint.Data;

namespace Savepoint.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"savepoint-tests-{Guid.NewGuid():N}.db");
    private readonly AppSettingsStore _store;

    public AppSettingsStoreTests()
    {
        _store = new AppSettingsStore(_dbPath);
    }

    public void Dispose() => File.Delete(_dbPath);

    [Fact]
    public void Flags_DefaultToFalse()
    {
        Assert.False(_store.AutostartOptOut);
        Assert.False(_store.FirstRunNoticeShown);
    }

    [Fact]
    public void Flags_PersistAcrossInstances()
    {
        _store.AutostartOptOut = true;
        _store.FirstRunNoticeShown = true;

        var reopened = new AppSettingsStore(_dbPath);

        Assert.True(reopened.AutostartOptOut);
        Assert.True(reopened.FirstRunNoticeShown);
    }
}
