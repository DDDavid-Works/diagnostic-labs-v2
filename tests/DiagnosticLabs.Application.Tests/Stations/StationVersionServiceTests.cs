using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Stations;

namespace DiagnosticLabs.Application.Tests.Stations;

public class StationVersionServiceTests
{
    private sealed class FakeApp(string station, string version) : IAppInfo
    {
        public string StationName { get; } = station;

        public string Version { get; set; } = version;
    }

    private readonly TestEnvironment _env = new();

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    [Fact]
    public async Task A_station_gets_one_row_that_is_updated_each_time_it_reports()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var app = new FakeApp("FRONT-DESK", "1.0.0");
        var service = new StationVersionService(db, app, _env.Session, _env.Clock);

        await service.RecordSeenAsync();
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddHours(3);
        app.Version = "1.0.1";
        await service.RecordSeenAsync();

        var row = Assert.Single(db.StationVersions);
        Assert.Equal("1.0.1", row.AppVersion);
        Assert.Equal("Admin", row.LastUserName);
        Assert.Equal(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), row.FirstSeenUtc);
        Assert.Equal(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc), row.LastSeenUtc);
    }

    [Fact]
    public async Task Stations_behind_the_share_version_are_marked_and_recent_ones_are_open_now()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await new StationVersionService(db, new FakeApp("OLD-PC", "1.0.0"), _env.Session, _env.Clock).RecordSeenAsync();
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddHours(2);
        await new StationVersionService(db, new FakeApp("NEW-PC", "1.0.2"), _env.Session, _env.Clock).RecordSeenAsync();
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddMinutes(5);

        var result = await new StationVersionService(db, new FakeApp("X", "1"), _env.Session, _env.Clock).GetStationsAsync("1.0.2");

        Assert.True(result.IsSuccess);
        Assert.Equal(["NEW-PC", "OLD-PC"], result.Value.Select(r => r.StationName));
        Assert.Equal(StationState.UpToDate, result.Value[0].State);
        Assert.True(result.Value[0].IsOpenNow);
        Assert.Equal(StationState.Behind, result.Value[1].State);
        Assert.False(result.Value[1].IsOpenNow);
    }

    [Fact]
    public async Task Without_a_share_version_the_newest_reported_version_is_the_yardstick()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await new StationVersionService(db, new FakeApp("A", "1.0.9"), _env.Session, _env.Clock).RecordSeenAsync();
        await new StationVersionService(db, new FakeApp("B", "1.0.10"), _env.Session, _env.Clock).RecordSeenAsync();

        var result = await new StationVersionService(db, new FakeApp("X", "1"), _env.Session, _env.Clock).GetStationsAsync(null);

        // "1.0.10" is newer than "1.0.9" (numbers, not text).
        Assert.Equal(StationState.UpToDate, result.Value.Single(r => r.StationName == "B").State);
        Assert.Equal(StationState.Behind, result.Value.Single(r => r.StationName == "A").State);
    }

    [Fact]
    public async Task The_list_needs_the_Stations_permission()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(2, "clerk", "Clerk", false, false, []));

        var result = await new StationVersionService(db, new FakeApp("X", "1"), _env.Session, _env.Clock).GetStationsAsync("1");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Recording_works_before_anyone_has_signed_in_and_keeps_the_last_user()
    {
        await using var db = _env.CreateDb();
        var service = new StationVersionService(db, new FakeApp("LAB-2", "1.0.0"), _env.Session, _env.Clock);

        await service.RecordSeenAsync();
        Assert.Null(Assert.Single(db.StationVersions).LastUserName);

        SignInAsAdmin();
        await service.RecordSeenAsync();
        _env.Session.SignOut();
        await service.RecordSeenAsync();

        Assert.Equal("Admin", Assert.Single(db.StationVersions).LastUserName);
    }
}
