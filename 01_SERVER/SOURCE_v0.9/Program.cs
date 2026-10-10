using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting.WindowsServices;

var webOptions = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : Directory.GetCurrentDirectory()
};

var builder = WebApplication.CreateBuilder(webOptions);
builder.Host.UseWindowsService(options => options.ServiceName = "ESP32 Timer Server");
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20 * 1024 * 1024);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var deviceApiKey = builder.Configuration["DeviceApiKey"] ?? "CHANGE_ME_DEVICE_KEY";
var defaultStationId = builder.Configuration["StationDefaults:StationId"] ?? "TIMER01";
var testEndpoints = builder.Configuration.GetValue("Features:EnableTestEndpoints", true);
var backupRetention = Math.Max(1, builder.Configuration.GetValue("Storage:BackupRetentionCount", 30));
var deviceStatusFreshSeconds = Math.Max(5, builder.Configuration.GetValue("DeviceStatusFreshSeconds", 15));
var adminPin = builder.Configuration["Admin:Pin"] ?? "2026";
var adminCookieName = "TimerAdmin";
var adminCookieValue = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"ESP32-TIMER|{deviceApiKey}|{adminPin}")));
var publicDisplayTitle = builder.Configuration["PublicDisplay:Title"] ?? "Dětská hasičská soutěž";
var publicDisplaySubtitle = builder.Configuration["PublicDisplay:Subtitle"] ?? "Výsledky a pořadí závodníků";
var publicRecentResults = Math.Clamp(builder.Configuration.GetValue("PublicDisplay:RecentResults", 3), 1, 8);
string NormalizeUiTheme(string? value) => string.Equals(value, "legacy", StringComparison.OrdinalIgnoreCase) ? "legacy" : "clean";

var dbPath = ResolvePath(builder.Configuration["Storage:DatabasePath"] ?? @"Data\timing.db");
var backupDir = ResolvePath(builder.Configuration["Storage:BackupDirectory"] ?? "Backups");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
Directory.CreateDirectory(backupDir);

var csb = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared,
    ForeignKeys = true,
    Pooling = true,
    DefaultTimeout = 10
};
var connectionString = csb.ToString();

await InitializeDatabaseAsync();

bool DeviceAuthorized(HttpRequest request) =>
    request.Headers.TryGetValue("X-Device-Key", out var key) &&
    string.Equals(key.ToString(), deviceApiKey, StringComparison.Ordinal);

bool AdminAuthorized(HttpRequest request) =>
    request.Cookies.TryGetValue(adminCookieName, out var cookie) &&
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(adminCookieValue));

bool IsUnauthenticatedApiAllowed(HttpContext context)
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)) return true;
    if (path.StartsWith("/api/public/", StringComparison.OrdinalIgnoreCase)) return true;
    if (context.Request.Method == "GET" && path.StartsWith("/api/device/config/", StringComparison.OrdinalIgnoreCase)) return true;
    if (context.Request.Method == "POST" && string.Equals(path, "/api/device/status", StringComparison.OrdinalIgnoreCase)) return true;
    if (context.Request.Method == "POST" && string.Equals(path, "/api/results", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
        !IsUnauthenticatedApiAllowed(context) && !AdminAuthorized(context.Request))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { error = "Admin PIN required." });
        return;
    }
    await next();
});

app.MapPost("/api/auth/login", (AdminLoginInput input, HttpResponse response) =>
{
    var supplied = input.Pin ?? "";
    var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(adminPin));
    if (!ok) return Results.Unauthorized();
    response.Cookies.Append(adminCookieName, adminCookieValue, new CookieOptions
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = false, IsEssential = true, Path = "/",
        Expires = DateTimeOffset.UtcNow.AddHours(12)
    });
    return Results.Ok(new { authenticated = true });
});

app.MapPost("/api/auth/logout", (HttpResponse response) =>
{
    response.Cookies.Delete(adminCookieName, new CookieOptions { Path = "/" });
    return Results.Ok(new { authenticated = false });
});

app.MapGet("/api/auth/status", (HttpRequest request) => Results.Ok(new { authenticated = AdminAuthorized(request) }));

app.MapGet("/api/public/config", () => Results.Ok(new
{
    defaultStationId, title = publicDisplayTitle, subtitle = publicDisplaySubtitle, recentResults = publicRecentResults
}));

app.MapGet("/api/public/display/{stationId}", async (string stationId) =>
{
    var active = await GetActiveSessionAsync(stationId.Trim());
    if (active is null)
        return Results.Ok(new { active = false, stationId, title = publicDisplayTitle, subtitle = publicDisplaySubtitle, uiTheme = "clean", utc = DateTimeOffset.UtcNow });
    var lanes = await GetPublicDisplayAsync(active.Id, publicRecentResults);
    return Results.Ok(new
    {
        active = true, stationId = active.StationId, sessionId = active.Id, sessionName = active.Name,
        title = publicDisplayTitle, subtitle = publicDisplaySubtitle, uiTheme = active.UiTheme, utc = DateTimeOffset.UtcNow, lanes
    });
});

app.MapGet("/api/public/top/{stationId}", async (string stationId, int? take) =>
{
    var active = await GetActiveSessionAsync(stationId.Trim());
    var limit = Math.Clamp(take ?? 5, 1, 20);
    if (active is null)
        return Results.Ok(new { active = false, stationId, title = publicDisplayTitle, subtitle = publicDisplaySubtitle, uiTheme = "clean", take = limit, utc = DateTimeOffset.UtcNow });

    var lanes = await GetTopTimesAsync(active.Id, limit);
    return Results.Ok(new
    {
        active = true, stationId = active.StationId, sessionId = active.Id, sessionName = active.Name,
        title = publicDisplayTitle, subtitle = publicDisplaySubtitle, uiTheme = active.UiTheme, take = limit, utc = DateTimeOffset.UtcNow, lanes
    });
});

app.MapGet("/api/status", async () =>
{
    var count = await ScalarLongAsync("SELECT COUNT(*) FROM TimingResults;");
    var pending = await ScalarLongAsync("SELECT COUNT(*) FROM TimingResults WHERE ValidationStatus='Pending';");
    return Results.Ok(new
    {
        ok = true,
        utc = DateTimeOffset.UtcNow,
        defaultStationId,
        database = Path.GetFileName(dbPath),
        resultCount = count,
        pendingCount = pending,
        testEndpoints,
        schemaVersion = 5
    });
});

// Device configuration is lane-specific. Every lane advances independently.
app.MapGet("/api/device/config/{stationId}", async (string stationId, HttpRequest request) =>
{
    if (!DeviceAuthorized(request)) return Results.Unauthorized();
    var active = await GetActiveSessionAsync(stationId);
    if (active is null)
    {
        return Results.Ok(new
        {
            activeRunSessionId = (string?)null,
            name = (string?)null,
            lanes = Enumerable.Range(1, 8).Select(l => new
            {
                lane = l,
                entryId = (string?)null,
                runnerName = (string?)null,
                sequence = (int?)null,
                status = "EMPTY",
                canStart = false
            })
        });
    }

    var laneStates = await GetLaneStatesAsync(active.Id);
    return Results.Ok(new
    {
        activeRunSessionId = active.Id,
        name = active.Name,
        lanes = laneStates.Select(x => new
        {
            lane = x.Lane,
            entryId = x.EntryId,
            runnerName = x.RunnerName,
            sequence = x.Sequence,
            status = x.Status,
            canStart = x.Status == "Scheduled"
        })
    });
});

app.MapPost("/api/results", async (TimingResultInput input, HttpRequest request) =>
{
    if (!DeviceAuthorized(request)) return Results.Unauthorized();
    return await StoreResultAsync(input);
});

// Lightweight station heartbeat. Besides battery state it carries RUNNING/WAITING
// lane masks so UNDO can be refused if a lane is currently active.
app.MapPost("/api/device/status", async (DeviceStatusInput input, HttpRequest request) =>
{
    if (!DeviceAuthorized(request)) return Results.Unauthorized();
    var station = (input.StationId ?? "").Trim();
    if (station.Length is < 1 or > 50) return Results.BadRequest(new { error = "Neplatné StationId." });
    if (input.RunningMask is < 0 or > 255 || input.WaitingMask is < 0 or > 255)
        return Results.BadRequest(new { error = "Neplatná maska drah." });
    if (input.BatteryVoltage is double bv && (bv < 0 || bv > 6.0))
        return Results.BadRequest(new { error = "Neplatné napětí baterie." });

    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
INSERT INTO DeviceStatus(StationId,FirmwareVersion,BatteryVoltage,BatteryPercent,RunningMask,WaitingMask,LastSeenUtc)
VALUES($station,$fw,$voltage,$percent,$running,$waiting,$seen)
ON CONFLICT(StationId) DO UPDATE SET
 FirmwareVersion=excluded.FirmwareVersion, BatteryVoltage=excluded.BatteryVoltage,
 BatteryPercent=excluded.BatteryPercent, RunningMask=excluded.RunningMask,
 WaitingMask=excluded.WaitingMask, LastSeenUtc=excluded.LastSeenUtc;";
    cmd.Parameters.AddWithValue("$station", station);
    cmd.Parameters.AddWithValue("$fw", string.IsNullOrWhiteSpace(input.FirmwareVersion) ? DBNull.Value : input.FirmwareVersion.Trim());
    cmd.Parameters.AddWithValue("$voltage", (object?)input.BatteryVoltage ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$percent", input.BatteryPercent is null ? DBNull.Value : Math.Clamp(input.BatteryPercent.Value, 0, 100));
    cmd.Parameters.AddWithValue("$running", input.RunningMask);
    cmd.Parameters.AddWithValue("$waiting", input.WaitingMask);
    cmd.Parameters.AddWithValue("$seen", UtcNowText());
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/device/status/{stationId}", async (string stationId) =>
{
    var d = await GetDeviceStatusAsync(stationId.Trim());
    if (d is null) return Results.Ok(new { online = false, stationId, lastSeenUtc = (string?)null });
    var online = DeviceStatusIsFresh(d);
    return Results.Ok(new
    {
        online,
        d.StationId,
        d.FirmwareVersion,
        d.BatteryVoltage,
        d.BatteryPercent,
        d.RunningMask,
        d.WaitingMask,
        d.LastSeenUtc
    });
});

app.MapGet("/api/sessions", async (int? take, string? stationId) =>
{
    var limit = Math.Clamp(take ?? 100, 1, 1000);
    var station = string.IsNullOrWhiteSpace(stationId) ? null : stationId.Trim();

    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT s.Id,s.StationId,s.Name,s.IsActive,s.CreatedAtUtc,s.ClosedAtUtc,
       COUNT(DISTINCT CASE WHEN r.ValidationStatus='Confirmed' THEN r.Id END) AS ResultCount,
       COUNT(DISTINCT e.Id) AS EntryCount
FROM RunSessions s
LEFT JOIN TimingResults r ON r.RunSessionId=s.Id
LEFT JOIN StartEntries e ON e.RunSessionId=s.Id
WHERE ($station IS NULL OR s.StationId=$station)
GROUP BY s.Id,s.StationId,s.Name,s.IsActive,s.CreatedAtUtc,s.ClosedAtUtc
ORDER BY s.CreatedAtUtc DESC
LIMIT $limit;";
    cmd.Parameters.AddWithValue("$station", (object?)station ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$limit", limit);

    var rows = new List<object>();
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
    {
        rows.Add(new
        {
            id = rd.GetString(0),
            stationId = rd.GetString(1),
            name = rd.GetString(2),
            isActive = rd.GetInt64(3) == 1,
            createdAtUtc = rd.GetString(4),
            closedAtUtc = rd.IsDBNull(5) ? null : rd.GetString(5),
            resultCount = rd.GetInt64(6),
            entryCount = rd.GetInt64(7)
        });
    }
    return Results.Ok(rows);
});

app.MapGet("/api/sessions/active/{stationId}", async (string stationId) =>
{
    var active = await GetActiveSessionAsync(stationId);
    if (active is null) return Results.Ok(new { active = false });

    var dashboard = await GetDashboardAsync(active.Id);
    return Results.Ok(new
    {
        active = true,
        id = active.Id,
        stationId = active.StationId,
        name = active.Name,
        createdAtUtc = active.CreatedAtUtc,
        autoConfirmResults = active.AutoConfirmResults,
        uiTheme = active.UiTheme,
        lanes = dashboard
    });
});

app.MapGet("/api/sessions/{id}/dashboard", async (string id) =>
{
    var session = await GetSessionAsync(id);
    if (session is null) return Results.NotFound(new { error = "Běh/série nenalezena." });
    var dashboard = await GetDashboardAsync(id);
    return Results.Ok(new { session, lanes = dashboard });
});

app.MapPost("/api/sessions", async (RunSessionInput input) =>
{
    var stationId = (input.StationId ?? "").Trim();
    if (stationId.Length is < 1 or > 50)
        return Results.BadRequest(new { error = "StationId musí mít 1 až 50 znaků." });

    var id = Guid.NewGuid().ToString("D");
    var now = UtcNowText();
    var name = string.IsNullOrWhiteSpace(input.Name)
        ? $"Běh {DateTime.Now:dd.MM.yyyy HH:mm:ss}"
        : input.Name.Trim();

    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();

    await using (var close = con.CreateCommand())
    {
        close.Transaction = tx;
        close.CommandText = @"
UPDATE RunSessions SET IsActive=0,ClosedAtUtc=$now
WHERE StationId=$station AND IsActive=1;";
        close.Parameters.AddWithValue("$now", now);
        close.Parameters.AddWithValue("$station", stationId);
        await close.ExecuteNonQueryAsync();
    }

    await using (var insert = con.CreateCommand())
    {
        insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO RunSessions(Id,StationId,Name,IsActive,CreatedAtUtc,AutoConfirmResults,UiTheme)
VALUES($id,$station,$name,1,$now,$autoConfirm,$uiTheme);";
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$station", stationId);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$now", now);
        insert.Parameters.AddWithValue("$autoConfirm", input.AutoConfirmResults ? 1 : 0);
        insert.Parameters.AddWithValue("$uiTheme", NormalizeUiTheme(input.UiTheme));
        await insert.ExecuteNonQueryAsync();
    }

    // Backward-compatible: old UI may still send one runner per lane.
    foreach (var assignment in input.Assignments ?? [])
    {
        if (assignment.Lane is < 1 or > 8 || string.IsNullOrWhiteSpace(assignment.RunnerName))
            continue;
        await InsertStartEntryAsync(con, tx, id, assignment.Lane, 1, assignment.RunnerName.Trim());
    }

    await tx.CommitAsync();
    return Results.Ok(new { id, stationId, name, autoConfirmResults = input.AutoConfirmResults, uiTheme = NormalizeUiTheme(input.UiTheme) });
});

app.MapPost("/api/sessions/{id}/settings", async (string id, SessionSettingsInput input) =>
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = "UPDATE RunSessions SET AutoConfirmResults=$auto WHERE Id=$id AND IsActive=1;";
    cmd.Parameters.AddWithValue("$auto", input.AutoConfirmResults ? 1 : 0);
    cmd.Parameters.AddWithValue("$id", id);
    var changed = await cmd.ExecuteNonQueryAsync();
    if (changed == 0) return Results.NotFound(new { error = "Aktivní série nenalezena." });
    return Results.Ok(new { updated = true, autoConfirmResults = input.AutoConfirmResults });
});

app.MapPost("/api/sessions/{id}/theme", async (string id, SessionThemeInput input) =>
{
    var uiTheme = NormalizeUiTheme(input.UiTheme);
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = "UPDATE RunSessions SET UiTheme=$theme WHERE Id=$id AND IsActive=1;";
    cmd.Parameters.AddWithValue("$theme", uiTheme);
    cmd.Parameters.AddWithValue("$id", id);
    var changed = await cmd.ExecuteNonQueryAsync();
    if (changed == 0) return Results.NotFound(new { error = "Aktivní série nenalezena." });
    return Results.Ok(new { updated = true, uiTheme });
});

app.MapPost("/api/sessions/{id}/close", async (string id) =>
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
UPDATE RunSessions SET IsActive=0,ClosedAtUtc=$now
WHERE Id=$id AND IsActive=1;";
    cmd.Parameters.AddWithValue("$now", UtcNowText());
    cmd.Parameters.AddWithValue("$id", id);
    var changed = await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { closed = changed > 0 });
});

app.MapGet("/api/sessions/{id}/startlist", async (string id) =>
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT Id,Lane,Sequence,RunnerName,Status,ResultId,CreatedAtUtc,UpdatedAtUtc
FROM StartEntries WHERE RunSessionId=$run
ORDER BY Lane,Sequence;";
    cmd.Parameters.AddWithValue("$run", id);
    var rows = new List<object>();
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
    {
        rows.Add(new
        {
            id = rd.GetString(0), lane = rd.GetInt32(1), sequence = rd.GetInt32(2),
            runnerName = rd.GetString(3), status = rd.GetString(4),
            resultId = rd.IsDBNull(5) ? (long?)null : rd.GetInt64(5),
            createdAtUtc = rd.GetString(6), updatedAtUtc = rd.GetString(7)
        });
    }
    return Results.Ok(rows);
});

app.MapPost("/api/sessions/{id}/startlist/import-text", async (string id, ImportTextInput input) =>
{
    var parsed = ParseDelimitedStartList(input.Text ?? "");
    if (parsed.Count == 0)
        return Results.BadRequest(new { error = "V textu nebyla nalezena žádná platná položka. Formát: Jméno;Dráha;Pořadí" });
    return await ReplaceStartListAsync(id, parsed);
});

app.MapPost("/api/sessions/{id}/startlist/import-file", async (string id, IFormFile file) =>
{
    if (file.Length <= 0) return Results.BadRequest(new { error = "Soubor je prázdný." });
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    List<StartListRow> rows;
    await using var stream = file.OpenReadStream();
    try
    {
        if (ext == ".xlsx") rows = ParseXlsxStartList(stream);
        else if (ext == ".csv" || ext == ".txt")
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true);
            rows = ParseDelimitedStartList(await reader.ReadToEndAsync());
        }
        else return Results.BadRequest(new { error = "Podporované soubory: .CSV, .TXT, .XLSX" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Soubor se nepodařilo načíst: " + ex.Message });
    }

    if (rows.Count == 0) return Results.BadRequest(new { error = "Soubor neobsahuje platné řádky." });
    return await ReplaceStartListAsync(id, rows);
}).DisableAntiforgery();

app.MapPost("/api/startlist/{entryId}/skip", async (string entryId) =>
{
    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();
    var entry = await GetEntryAsync(con, tx, entryId);
    if (entry is null) return Results.NotFound(new { error = "Položka nenalezena." });
    if (entry.Status != "Scheduled") return Results.Conflict(new { error = "Přeskočit lze pouze připraveného závodníka." });
    if (!await IsCurrentEntryAsync(con, tx, entry)) return Results.Conflict(new { error = "Položka není aktuální pro tuto dráhu." });

    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = "UPDATE StartEntries SET Status='Skipped',UpdatedAtUtc=$now WHERE Id=$id;";
    cmd.Parameters.AddWithValue("$now", UtcNowText());
    cmd.Parameters.AddWithValue("$id", entryId);
    await cmd.ExecuteNonQueryAsync();
    await tx.CommitAsync();
    return Results.Ok(new { skipped = true });
});

app.MapPost("/api/results/{id:long}/confirm", async (long id) =>
{
    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();
    var result = await GetResultForDecisionAsync(con, tx, id);
    if (result is null) return Results.NotFound(new { error = "Výsledek nenalezen." });
    if (result.ValidationStatus != "Pending") return Results.Conflict(new { error = "Výsledek už byl vyřízen." });

    await using (var cmd = con.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE TimingResults SET ValidationStatus='Confirmed' WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }
    if (!string.IsNullOrWhiteSpace(result.EntryId))
    {
        await using var e = con.CreateCommand();
        e.Transaction = tx;
        e.CommandText = @"
UPDATE StartEntries SET Status='Completed',ResultId=$result,UpdatedAtUtc=$now
WHERE Id=$entry AND Status='Pending';";
        e.Parameters.AddWithValue("$result", id);
        e.Parameters.AddWithValue("$now", UtcNowText());
        e.Parameters.AddWithValue("$entry", result.EntryId);
        await e.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
    return Results.Ok(new { confirmed = true });
});

app.MapPost("/api/results/{id:long}/reject", async (long id) =>
{
    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();
    var result = await GetResultForDecisionAsync(con, tx, id);
    if (result is null) return Results.NotFound(new { error = "Výsledek nenalezen." });
    if (result.ValidationStatus != "Pending") return Results.Conflict(new { error = "Výsledek už byl vyřízen." });

    await using (var cmd = con.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE TimingResults SET ValidationStatus='Rejected' WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }
    if (!string.IsNullOrWhiteSpace(result.EntryId))
    {
        await using var e = con.CreateCommand();
        e.Transaction = tx;
        e.CommandText = @"
UPDATE StartEntries SET Status='Scheduled',ResultId=NULL,UpdatedAtUtc=$now
WHERE Id=$entry AND Status='Pending';";
        e.Parameters.AddWithValue("$now", UtcNowText());
        e.Parameters.AddWithValue("$entry", result.EntryId);
        await e.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
    return Results.Ok(new { rejected = true });
});

app.MapPost("/api/sessions/{id}/lanes/{lane:int}/undo", async (string id, int lane) =>
{
    if (lane is < 1 or > 8) return Results.BadRequest(new { error = "Neplatná dráha." });

    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();

    string? stationId = null;
    await using (var q = con.CreateCommand())
    {
        q.Transaction = tx;
        q.CommandText = "SELECT StationId FROM RunSessions WHERE Id=$id;";
        q.Parameters.AddWithValue("$id", id);
        stationId = (await q.ExecuteScalarAsync()) as string;
    }
    if (stationId is null) return Results.NotFound(new { error = "Série nenalezena." });

    var device = await GetDeviceStatusAsync(stationId);
    if (device is null || !DeviceStatusIsFresh(device))
        return Results.Conflict(new { error = "UNDO je z bezpečnostních důvodů povoleno jen když je časomíra online. Obnov stav stanice a zkus to znovu." });

    var bit = 1 << (lane - 1);
    if ((device.RunningMask & bit) != 0)
        return Results.Conflict(new { error = $"Dráha {lane} právě běží. UNDO nelze provést." });
    if ((device.WaitingMask & bit) != 0)
        return Results.Conflict(new { error = $"Dráha {lane} čeká na potvrzení výsledku. Nejdřív jej vyřiď." });

    await using (var pending = con.CreateCommand())
    {
        pending.Transaction = tx;
        pending.CommandText = "SELECT COUNT(*) FROM TimingResults WHERE RunSessionId=$run AND Lane=$lane AND ValidationStatus='Pending';";
        pending.Parameters.AddWithValue("$run", id);
        pending.Parameters.AddWithValue("$lane", lane);
        if (Convert.ToInt64(await pending.ExecuteScalarAsync()) > 0)
            return Results.Conflict(new { error = "Na dráze je čekající výsledek. UNDO nelze provést." });
    }

    long resultId;
    string entryId;
    int sequence;
    await using (var last = con.CreateCommand())
    {
        last.Transaction = tx;
        last.CommandText = @"
SELECT r.Id,e.Id,e.Sequence
FROM TimingResults r
JOIN StartEntries e ON e.Id=r.EntryId
WHERE r.RunSessionId=$run AND r.Lane=$lane AND r.ValidationStatus='Confirmed' AND e.Status='Completed'
ORDER BY e.Sequence DESC LIMIT 1;";
        last.Parameters.AddWithValue("$run", id);
        last.Parameters.AddWithValue("$lane", lane);
        await using var rd = await last.ExecuteReaderAsync();
        if (!await rd.ReadAsync()) return Results.Conflict(new { error = "Na této dráze není potvrzený výsledek, který by šel vrátit." });
        resultId = rd.GetInt64(0);
        entryId = rd.GetString(1);
        sequence = rd.GetInt32(2);
    }

    // Undo is intentionally conservative: nothing after the returned runner may
    // already be skipped/pending/completed, otherwise order could become ambiguous.
    await using (var later = con.CreateCommand())
    {
        later.Transaction = tx;
        later.CommandText = @"
SELECT COUNT(*) FROM StartEntries
WHERE RunSessionId=$run AND Lane=$lane AND Sequence>$seq AND Status<>'Scheduled';";
        later.Parameters.AddWithValue("$run", id);
        later.Parameters.AddWithValue("$lane", lane);
        later.Parameters.AddWithValue("$seq", sequence);
        if (Convert.ToInt64(await later.ExecuteScalarAsync()) > 0)
            return Results.Conflict(new { error = "Po tomto výsledku už byla na dráze provedena další akce. Automatické UNDO je zablokováno, aby se nepromíchalo pořadí." });
    }

    await using (var r = con.CreateCommand())
    {
        r.Transaction = tx;
        r.CommandText = "UPDATE TimingResults SET ValidationStatus='Undone' WHERE Id=$id AND ValidationStatus='Confirmed';";
        r.Parameters.AddWithValue("$id", resultId);
        await r.ExecuteNonQueryAsync();
    }
    await using (var e = con.CreateCommand())
    {
        e.Transaction = tx;
        e.CommandText = "UPDATE StartEntries SET Status='Scheduled',ResultId=NULL,UpdatedAtUtc=$now WHERE Id=$entry AND Status='Completed';";
        e.Parameters.AddWithValue("$now", UtcNowText());
        e.Parameters.AddWithValue("$entry", entryId);
        await e.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    return Results.Ok(new { undone = true, resultId, entryId, lane, sequence });
});

app.MapGet("/api/results", async (int? take, string? sessionId, string? runner, string? stationId, string? status) =>
{
    var limit = Math.Clamp(take ?? 200, 1, 5000);
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT r.Id,r.StationId,r.RunSessionId,r.EntryId,r.Lane,r.RunnerNameSnapshot,
       r.StartedAtUtc,r.StoppedAtUtc,r.DurationUs,r.ReceivedAtUtc,r.ValidationStatus,
       s.Name,e.Sequence
FROM TimingResults r
LEFT JOIN RunSessions s ON s.Id=r.RunSessionId
LEFT JOIN StartEntries e ON e.Id=r.EntryId
WHERE ($session IS NULL OR r.RunSessionId=$session)
  AND ($runner IS NULL OR r.RunnerNameSnapshot LIKE '%' || $runner || '%')
  AND ($station IS NULL OR r.StationId=$station)
  AND ($status IS NULL OR r.ValidationStatus=$status)
ORDER BY r.StartedAtUtc DESC
LIMIT $limit;";
    cmd.Parameters.AddWithValue("$session", string.IsNullOrWhiteSpace(sessionId) ? DBNull.Value : sessionId.Trim());
    cmd.Parameters.AddWithValue("$runner", string.IsNullOrWhiteSpace(runner) ? DBNull.Value : runner.Trim());
    cmd.Parameters.AddWithValue("$station", string.IsNullOrWhiteSpace(stationId) ? DBNull.Value : stationId.Trim());
    cmd.Parameters.AddWithValue("$status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim());
    cmd.Parameters.AddWithValue("$limit", limit);

    var rows = new List<object>();
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
    {
        var durationUs = rd.GetInt64(8);
        rows.Add(new
        {
            id = rd.GetInt64(0),
            stationId = rd.GetString(1),
            runSessionId = rd.IsDBNull(2) ? null : rd.GetString(2),
            entryId = rd.IsDBNull(3) ? null : rd.GetString(3),
            lane = rd.GetInt32(4),
            runner = rd.IsDBNull(5) ? null : rd.GetString(5),
            startedAtUtc = rd.GetString(6), stoppedAtUtc = rd.GetString(7),
            durationUs, durationSeconds = durationUs / 1_000_000.0,
            receivedAtUtc = rd.GetString(9), validationStatus = rd.GetString(10),
            sessionName = rd.IsDBNull(11) ? null : rd.GetString(11),
            sequence = rd.IsDBNull(12) ? (int?)null : rd.GetInt32(12)
        });
    }
    return Results.Ok(rows);
});

app.MapGet("/api/export.xlsx", async (string? sessionId, string? runner, string? stationId, string? status) =>
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT r.StationId,r.Lane,e.Sequence,r.RunnerNameSnapshot,r.StartedAtUtc,r.StoppedAtUtc,
       r.DurationUs,r.ValidationStatus,s.Name,r.ReceivedAtUtc
FROM TimingResults r
LEFT JOIN RunSessions s ON s.Id=r.RunSessionId
LEFT JOIN StartEntries e ON e.Id=r.EntryId
WHERE ($session IS NULL OR r.RunSessionId=$session)
  AND ($runner IS NULL OR r.RunnerNameSnapshot LIKE '%' || $runner || '%')
  AND ($station IS NULL OR r.StationId=$station)
  AND ($status IS NULL OR r.ValidationStatus=$status)
ORDER BY r.StartedAtUtc;";
    cmd.Parameters.AddWithValue("$session", string.IsNullOrWhiteSpace(sessionId) ? DBNull.Value : sessionId.Trim());
    cmd.Parameters.AddWithValue("$runner", string.IsNullOrWhiteSpace(runner) ? DBNull.Value : runner.Trim());
    cmd.Parameters.AddWithValue("$station", string.IsNullOrWhiteSpace(stationId) ? DBNull.Value : stationId.Trim());
    cmd.Parameters.AddWithValue("$status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim());

    await using var rd = await cmd.ExecuteReaderAsync();
    using var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Výsledky");
    string[] headers = ["Stanice", "Dráha", "Pořadí", "Jméno", "Start UTC", "Stop UTC", "Čas [s]", "Stav", "Běh", "Přijato UTC"];
    for (var i = 0; i < headers.Length; i++)
    {
        ws.Cell(1, i + 1).Value = headers[i];
        ws.Cell(1, i + 1).Style.Font.Bold = true;
    }

    var row = 2;
    while (await rd.ReadAsync())
    {
        ws.Cell(row, 1).Value = rd.GetString(0);
        ws.Cell(row, 2).Value = rd.GetInt32(1);
        if (!rd.IsDBNull(2)) ws.Cell(row, 3).Value = rd.GetInt32(2);
        ws.Cell(row, 4).Value = rd.IsDBNull(3) ? "" : rd.GetString(3);
        ws.Cell(row, 5).Value = rd.GetString(4);
        ws.Cell(row, 6).Value = rd.GetString(5);
        ws.Cell(row, 7).Value = rd.GetInt64(6) / 1_000_000.0;
        ws.Cell(row, 7).Style.NumberFormat.Format = "0.000";
        ws.Cell(row, 8).Value = rd.GetString(7);
        ws.Cell(row, 9).Value = rd.IsDBNull(8) ? "" : rd.GetString(8);
        ws.Cell(row, 10).Value = rd.GetString(9);
        row++;
    }
    ws.SheetView.FreezeRows(1);
    ws.RangeUsed()?.SetAutoFilter();
    ws.Columns().AdjustToContents();

    using var ms = new MemoryStream();
    wb.SaveAs(ms);
    return Results.File(ms.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"casomira_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
});

app.MapPost("/api/admin/backup", async () =>
{
    var file = await CreateBackupAsync();
    return Results.Ok(new { ok = true, file = Path.GetFileName(file), utc = DateTimeOffset.UtcNow });
});

if (testEndpoints)
{
    app.MapPost("/api/test/result", async (TestResultInput input) =>
    {
        if (input.Lane is < 1 or > 8 || input.DurationSeconds <= 0 || input.DurationSeconds > 36000)
            return Results.BadRequest(new { error = "Neplatná dráha nebo čas." });

        var station = string.IsNullOrWhiteSpace(input.StationId) ? defaultStationId : input.StationId.Trim();
        var active = await GetActiveSessionAsync(station);
        if (active is null) return Results.Conflict(new { error = "Není aktivní série." });
        var laneState = (await GetLaneStatesAsync(active.Id)).First(x => x.Lane == input.Lane);
        if (laneState.Status != "Scheduled" || string.IsNullOrWhiteSpace(laneState.EntryId))
            return Results.Conflict(new { error = "Dráha nemá připraveného závodníka nebo čeká na potvrzení." });

        var stop = DateTimeOffset.UtcNow;
        var start = stop.AddSeconds(-input.DurationSeconds);
        var result = new TimingResultInput(
            Guid.NewGuid(), station, Guid.Parse(active.Id), Guid.Parse(laneState.EntryId),
            (byte)input.Lane, start, stop, (long)Math.Round(input.DurationSeconds * 1_000_000.0));
        return await StoreResultAsync(result);
    });
}

app.MapGet("/admin", () => Results.Redirect("/admin.html"));
app.MapGet("/display", () => Results.Redirect("/display.html"));
app.MapGet("/top", () => Results.Redirect("/top.html"));
app.MapFallbackToFile("index.html");
app.Run();

string ResolvePath(string configured)
{
    var normalized = configured.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    return Path.IsPathRooted(normalized) ? normalized : Path.Combine(AppContext.BaseDirectory, normalized);
}

string UtcNowText() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

async Task<SqliteConnection> OpenConnectionAsync()
{
    var con = new SqliteConnection(connectionString);
    await con.OpenAsync();
    await using var pragma = con.CreateCommand();
    pragma.CommandText = "PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;";
    await pragma.ExecuteNonQueryAsync();
    return con;
}

async Task InitializeDatabaseAsync()
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;

CREATE TABLE IF NOT EXISTS AppMeta(Key TEXT NOT NULL PRIMARY KEY,Value TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS RunSessions(
    Id TEXT NOT NULL PRIMARY KEY,
    StationId TEXT NOT NULL,
    Name TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    ClosedAtUtc TEXT NULL,
    AutoConfirmResults INTEGER NOT NULL DEFAULT 0,
    UiTheme TEXT NOT NULL DEFAULT 'clean'
);
CREATE INDEX IF NOT EXISTS IX_RunSessions_StationActive ON RunSessions(StationId,IsActive);

CREATE TABLE IF NOT EXISTS LaneAssignments(
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RunSessionId TEXT NOT NULL,
    Lane INTEGER NOT NULL CHECK(Lane BETWEEN 1 AND 8),
    RunnerName TEXT NOT NULL,
    FOREIGN KEY(RunSessionId) REFERENCES RunSessions(Id) ON DELETE CASCADE,
    UNIQUE(RunSessionId,Lane)
);

CREATE TABLE IF NOT EXISTS TimingResults(
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    DeviceEventId TEXT NOT NULL UNIQUE,
    StationId TEXT NOT NULL,
    RunSessionId TEXT NULL,
    Lane INTEGER NOT NULL CHECK(Lane BETWEEN 1 AND 8),
    RunnerNameSnapshot TEXT NULL,
    StartedAtUtc TEXT NOT NULL,
    StoppedAtUtc TEXT NOT NULL,
    DurationUs INTEGER NOT NULL CHECK(DurationUs > 0),
    ReceivedAtUtc TEXT NOT NULL,
    FOREIGN KEY(RunSessionId) REFERENCES RunSessions(Id)
);
CREATE INDEX IF NOT EXISTS IX_TimingResults_Start ON TimingResults(StartedAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TimingResults_Session ON TimingResults(RunSessionId);
CREATE INDEX IF NOT EXISTS IX_TimingResults_Runner ON TimingResults(RunnerNameSnapshot);

CREATE TABLE IF NOT EXISTS StartEntries(
    Id TEXT NOT NULL PRIMARY KEY,
    RunSessionId TEXT NOT NULL,
    Lane INTEGER NOT NULL CHECK(Lane BETWEEN 1 AND 8),
    Sequence INTEGER NOT NULL CHECK(Sequence > 0),
    RunnerName TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Scheduled',
    ResultId INTEGER NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY(RunSessionId) REFERENCES RunSessions(Id) ON DELETE CASCADE,
    UNIQUE(RunSessionId,Lane,Sequence)
);
CREATE INDEX IF NOT EXISTS IX_StartEntries_Current ON StartEntries(RunSessionId,Lane,Status,Sequence);

CREATE TABLE IF NOT EXISTS DeviceStatus(
    StationId TEXT NOT NULL PRIMARY KEY,
    FirmwareVersion TEXT NULL,
    BatteryVoltage REAL NULL,
    BatteryPercent INTEGER NULL,
    RunningMask INTEGER NOT NULL DEFAULT 0,
    WaitingMask INTEGER NOT NULL DEFAULT 0,
    LastSeenUtc TEXT NOT NULL
);

INSERT OR IGNORE INTO AppMeta(Key,Value) VALUES('SchemaVersion','1');
";
    await cmd.ExecuteNonQueryAsync();

    await EnsureColumnAsync(con, "RunSessions", "AutoConfirmResults", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(con, "RunSessions", "UiTheme", "TEXT NOT NULL DEFAULT 'clean'");
    await EnsureColumnAsync(con, "TimingResults", "EntryId", "TEXT NULL");
    await EnsureColumnAsync(con, "TimingResults", "ValidationStatus", "TEXT NOT NULL DEFAULT 'Confirmed'");
    await using (var idx = con.CreateCommand())
    {
        idx.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_TimingResults_Entry ON TimingResults(EntryId);
CREATE INDEX IF NOT EXISTS IX_TimingResults_Validation ON TimingResults(ValidationStatus);";
        await idx.ExecuteNonQueryAsync();
    }

    await using var meta = con.CreateCommand();
    meta.CommandText = "INSERT INTO AppMeta(Key,Value) VALUES('SchemaVersion','5') ON CONFLICT(Key) DO UPDATE SET Value='5';";
    await meta.ExecuteNonQueryAsync();
}

async Task EnsureColumnAsync(SqliteConnection con, string table, string column, string definition)
{
    await using var check = con.CreateCommand();
    check.CommandText = $"PRAGMA table_info({table});";
    await using var rd = await check.ExecuteReaderAsync();
    while (await rd.ReadAsync())
        if (string.Equals(rd.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
    await rd.DisposeAsync();
    await using var alter = con.CreateCommand();
    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
    await alter.ExecuteNonQueryAsync();
}

async Task<ActiveSession?> GetActiveSessionAsync(string stationId)
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT Id,StationId,Name,CreatedAtUtc,AutoConfirmResults,UiTheme
FROM RunSessions
WHERE StationId=$station AND IsActive=1
ORDER BY CreatedAtUtc DESC LIMIT 1;";
    cmd.Parameters.AddWithValue("$station", stationId);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return null;
    return new ActiveSession(rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetInt64(4) == 1, NormalizeUiTheme(rd.IsDBNull(5) ? null : rd.GetString(5)));
}

async Task<object?> GetSessionAsync(string id)
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT Id,StationId,Name,IsActive,CreatedAtUtc,ClosedAtUtc,AutoConfirmResults,UiTheme FROM RunSessions WHERE Id=$id;";
    cmd.Parameters.AddWithValue("$id", id);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return null;
    return new { id = rd.GetString(0), stationId = rd.GetString(1), name = rd.GetString(2), isActive = rd.GetInt64(3) == 1,
        createdAtUtc = rd.GetString(4), closedAtUtc = rd.IsDBNull(5) ? null : rd.GetString(5), autoConfirmResults = rd.GetInt64(6) == 1,
        uiTheme = NormalizeUiTheme(rd.IsDBNull(7) ? null : rd.GetString(7)) };
}

async Task<List<LaneStateDto>> GetLaneStatesAsync(string runId)
{
    var entries = new List<StartEntryData>();
    await using var con = await OpenConnectionAsync();
    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Id,Lane,Sequence,RunnerName,Status,ResultId
FROM StartEntries
WHERE RunSessionId=$run AND Status IN ('Scheduled','Pending')
ORDER BY Lane,Sequence;";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            entries.Add(new StartEntryData(rd.GetString(0), runId, rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetInt64(5)));
    }

    var result = new List<LaneStateDto>();
    for (var lane = 1; lane <= 8; lane++)
    {
        var current = entries.FirstOrDefault(x => x.Lane == lane);
        if (current is null) result.Add(new LaneStateDto(lane, null, null, null, "EMPTY", null));
        else result.Add(new LaneStateDto(lane, current.Id, current.RunnerName, current.Sequence, current.Status, current.ResultId));
    }
    return result;
}

async Task<List<object>> GetDashboardAsync(string runId)
{
    var all = new List<StartEntryData>();
    var pendingResults = new Dictionary<long, PendingResultData>();
    var confirmedResults = new Dictionary<long, ConfirmedResultData>();
    string stationId = "";

    await using var con = await OpenConnectionAsync();
    await using (var q = con.CreateCommand())
    {
        q.CommandText = "SELECT StationId FROM RunSessions WHERE Id=$run;";
        q.Parameters.AddWithValue("$run", runId);
        stationId = (await q.ExecuteScalarAsync()) as string ?? "";
    }
    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Id,Lane,Sequence,RunnerName,Status,ResultId
FROM StartEntries WHERE RunSessionId=$run ORDER BY Lane,Sequence;";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            all.Add(new StartEntryData(rd.GetString(0), runId, rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetInt64(5)));
    }
    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Id,EntryId,DurationUs,StartedAtUtc,StoppedAtUtc,ValidationStatus
FROM TimingResults WHERE RunSessionId=$run AND ValidationStatus IN ('Pending','Confirmed');";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var id = rd.GetInt64(0);
            var entryId = rd.IsDBNull(1) ? null : rd.GetString(1);
            var duration = rd.GetInt64(2);
            var startUtc = rd.GetString(3);
            var stopUtc = rd.GetString(4);
            var status = rd.GetString(5);
            if (status == "Pending") pendingResults[id] = new PendingResultData(id, entryId, duration, startUtc, stopUtc);
            else confirmedResults[id] = new ConfirmedResultData(id, entryId, duration, startUtc, stopUtc);
        }
    }

    var device = string.IsNullOrWhiteSpace(stationId) ? null : await GetDeviceStatusAsync(stationId);
    var deviceFresh = device is not null && DeviceStatusIsFresh(device);

    var lanesOut = new List<object>();
    for (var lane = 1; lane <= 8; lane++)
    {
        var laneRows = all.Where(x => x.Lane == lane).OrderBy(x => x.Sequence).ToList();
        var current = laneRows.FirstOrDefault(x => x.Status is "Scheduled" or "Pending");
        PendingResultData? pending = null;
        if (current?.ResultId is long rid && pendingResults.TryGetValue(rid, out var p)) pending = p;

        var next = current is null
            ? new List<object>()
            : laneRows.Where(x => x.Sequence > current.Sequence && x.Status == "Scheduled").Take(3)
                .Select(x => (object)new { x.Id, x.Sequence, x.RunnerName }).ToList();

        var lastCompleted = laneRows.Where(x => x.Status == "Completed" && x.ResultId is not null)
            .OrderByDescending(x => x.Sequence).FirstOrDefault();
        ConfirmedResultData? lastConfirmed = null;
        if (lastCompleted?.ResultId is long lastId && confirmedResults.TryGetValue(lastId, out var c)) lastConfirmed = c;

        var bit = 1 << (lane - 1);
        var deviceBusy = deviceFresh && device is not null && (((device.RunningMask | device.WaitingMask) & bit) != 0);
        var laterTouched = lastCompleted is not null && laneRows.Any(x => x.Sequence > lastCompleted.Sequence && x.Status != "Scheduled");
        var canUndo = lastCompleted is not null && lastConfirmed is not null && deviceFresh && !deviceBusy && !laterTouched && pending is null;
        string? undoReason = null;
        if (lastCompleted is not null && !canUndo)
        {
            if (!deviceFresh) undoReason = "Stanice není online / stav je zastaralý.";
            else if (deviceBusy) undoReason = "Dráha právě běží nebo čeká na rozhodnutí.";
            else if (laterTouched) undoReason = "Po výsledku už byla provedena další akce.";
            else if (pending is not null) undoReason = "Nejdřív vyřiď čekající výsledek.";
        }

        lanesOut.Add(new
        {
            lane,
            current = current is null ? null : new { id = current.Id, sequence = current.Sequence, runnerName = current.RunnerName, status = current.Status },
            pendingResult = pending is null ? null : new { id = pending.Id, durationUs = pending.DurationUs, durationSeconds = pending.DurationUs / 1_000_000.0, startedAtUtc = pending.StartedAtUtc, stoppedAtUtc = pending.StoppedAtUtc },
            next,
            completed = laneRows.Count(x => x.Status == "Completed"),
            skipped = laneRows.Count(x => x.Status == "Skipped"),
            total = laneRows.Count,
            lastConfirmed = lastCompleted is null || lastConfirmed is null ? null : new
            {
                id = lastConfirmed.Id,
                sequence = lastCompleted.Sequence,
                runnerName = lastCompleted.RunnerName,
                durationSeconds = lastConfirmed.DurationUs / 1_000_000.0
            },
            canUndo,
            undoReason
        });
    }
    return lanesOut;
}

async Task<List<object>> GetPublicDisplayAsync(string runId, int recentCount)
{
    var entries = new List<StartEntryData>();
    var confirmed = new List<PublicResultData>();
    string stationId = "";

    await using var con = await OpenConnectionAsync();
    await using (var q = con.CreateCommand())
    {
        q.CommandText = "SELECT StationId FROM RunSessions WHERE Id=$run;";
        q.Parameters.AddWithValue("$run", runId);
        stationId = (await q.ExecuteScalarAsync()) as string ?? "";
    }

    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Id,Lane,Sequence,RunnerName,Status,ResultId
FROM StartEntries WHERE RunSessionId=$run ORDER BY Lane,Sequence;";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            entries.Add(new StartEntryData(rd.GetString(0), runId, rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetInt64(5)));
    }

    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT r.Id,r.Lane,e.Sequence,e.RunnerName,r.DurationUs,r.StoppedAtUtc
FROM TimingResults r
JOIN StartEntries e ON e.Id=r.EntryId
WHERE r.RunSessionId=$run AND r.ValidationStatus='Confirmed' AND e.Status='Completed'
ORDER BY r.StoppedAtUtc DESC;";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            confirmed.Add(new PublicResultData(rd.GetInt64(0), rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3), rd.GetInt64(4), rd.GetString(5)));
    }

    var device = string.IsNullOrWhiteSpace(stationId) ? null : await GetDeviceStatusAsync(stationId);
    var deviceFresh = device is not null && DeviceStatusIsFresh(device);
    var output = new List<object>();

    for (var lane = 1; lane <= 8; lane++)
    {
        var laneRows = entries.Where(x => x.Lane == lane).OrderBy(x => x.Sequence).ToList();
        var pending = laneRows.FirstOrDefault(x => x.Status == "Pending");
        var scheduled = laneRows.Where(x => x.Status == "Scheduled").OrderBy(x => x.Sequence).ToList();
        var bit = 1 << (lane - 1);
        var running = deviceFresh && device is not null && ((device.RunningMask & bit) != 0);
        var waiting = deviceFresh && device is not null && ((device.WaitingMask & bit) != 0);

        object? current = null;
        IEnumerable<StartEntryData> nextRows;
        if (pending is not null)
        {
            current = new { pending.Id, pending.Sequence, pending.RunnerName, state = "PENDING" };
            nextRows = scheduled.Take(2);
        }
        else if (scheduled.Count > 0)
        {
            var first = scheduled[0];
            current = new { first.Id, first.Sequence, first.RunnerName, state = waiting ? "PENDING" : running ? "RUNNING" : "READY" };
            nextRows = scheduled.Skip(1).Take(2);
        }
        else
        {
            nextRows = Enumerable.Empty<StartEntryData>();
        }

        var recent = confirmed.Where(x => x.Lane == lane).Take(recentCount)
            .Select(x => (object)new
            {
                x.Id, x.Sequence, x.RunnerName, durationSeconds = x.DurationUs / 1_000_000.0, x.StoppedAtUtc
            }).ToList();

        var top = confirmed.Where(x => x.Lane == lane)
            .OrderBy(x => x.DurationUs)
            .ThenBy(x => x.StoppedAtUtc, StringComparer.Ordinal)
            .Take(5)
            .Select((x, index) => (object)new
            {
                rank = index + 1, x.Id, x.Sequence, x.RunnerName,
                durationSeconds = x.DurationUs / 1_000_000.0, x.StoppedAtUtc
            }).ToList();

        output.Add(new
        {
            lane, current,
            next = nextRows.Select(x => new { x.Sequence, x.RunnerName }).ToList(),
            recentResults = recent,
            topResults = top,
            completed = laneRows.Count(x => x.Status == "Completed"),
            skipped = laneRows.Count(x => x.Status == "Skipped"),
            total = laneRows.Count, deviceOnline = deviceFresh, deviceRunning = running, deviceWaiting = waiting
        });
    }
    return output;
}

async Task<List<object>> GetTopTimesAsync(string runId, int take)
{
    var rows = new List<PublicResultData>();
    await using var con = await OpenConnectionAsync();
    await using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT r.Id,r.Lane,e.Sequence,e.RunnerName,r.DurationUs,r.StoppedAtUtc
FROM TimingResults r
JOIN StartEntries e ON e.Id=r.EntryId
WHERE r.RunSessionId=$run
  AND r.ValidationStatus='Confirmed'
  AND e.Status='Completed'
ORDER BY r.Lane ASC,r.DurationUs ASC,r.StoppedAtUtc ASC;";
        cmd.Parameters.AddWithValue("$run", runId);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            rows.Add(new PublicResultData(rd.GetInt64(0), rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3), rd.GetInt64(4), rd.GetString(5)));
    }

    var output = new List<object>();
    for (var lane = 1; lane <= 8; lane++)
    {
        var top = rows.Where(x => x.Lane == lane)
            .OrderBy(x => x.DurationUs)
            .ThenBy(x => x.StoppedAtUtc, StringComparer.Ordinal)
            .Take(take)
            .Select((x, index) => (object)new
            {
                rank = index + 1,
                x.Id,
                x.Sequence,
                x.RunnerName,
                durationSeconds = x.DurationUs / 1_000_000.0,
                x.StoppedAtUtc
            }).ToList();

        output.Add(new { lane, top });
    }
    return output;
}

async Task InsertStartEntryAsync(SqliteConnection con, SqliteTransaction tx, string runId, int lane, int sequence, string name)
{
    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
INSERT INTO StartEntries(Id,RunSessionId,Lane,Sequence,RunnerName,Status,CreatedAtUtc,UpdatedAtUtc)
VALUES($id,$run,$lane,$seq,$name,'Scheduled',$now,$now);";
    cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
    cmd.Parameters.AddWithValue("$run", runId);
    cmd.Parameters.AddWithValue("$lane", lane);
    cmd.Parameters.AddWithValue("$seq", sequence);
    cmd.Parameters.AddWithValue("$name", name);
    cmd.Parameters.AddWithValue("$now", UtcNowText());
    await cmd.ExecuteNonQueryAsync();
}

async Task<IResult> ReplaceStartListAsync(string runId, List<StartListRow> rows)
{
    var session = await GetSessionAsync(runId);
    if (session is null) return Results.NotFound(new { error = "Běh/série nenalezena." });

    var normalized = rows
        .Where(x => x.Lane is >= 1 and <= 8 && x.Sequence > 0 && !string.IsNullOrWhiteSpace(x.RunnerName))
        .Select(x => new StartListRow(x.RunnerName.Trim(), x.Lane, x.Sequence))
        .ToList();
    var duplicate = normalized.GroupBy(x => new { x.Lane, x.Sequence }).FirstOrDefault(g => g.Count() > 1);
    if (duplicate is not null)
        return Results.BadRequest(new { error = $"Duplicitní pořadí: dráha {duplicate.Key.Lane}, pořadí {duplicate.Key.Sequence}." });

    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();
    await using (var check = con.CreateCommand())
    {
        check.Transaction = tx;
        check.CommandText = "SELECT COUNT(*) FROM TimingResults WHERE RunSessionId=$run;";
        check.Parameters.AddWithValue("$run", runId);
        if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0)
            return Results.Conflict(new { error = "Startovní listinu nelze přepsat po uložení prvního času. Vytvoř novou sérii." });
    }
    await using (var del = con.CreateCommand())
    {
        del.Transaction = tx;
        del.CommandText = "DELETE FROM StartEntries WHERE RunSessionId=$run;";
        del.Parameters.AddWithValue("$run", runId);
        await del.ExecuteNonQueryAsync();
    }
    foreach (var row in normalized.OrderBy(x => x.Lane).ThenBy(x => x.Sequence))
        await InsertStartEntryAsync(con, tx, runId, row.Lane, row.Sequence, row.RunnerName);
    await tx.CommitAsync();
    return Results.Ok(new { imported = normalized.Count, lanes = normalized.Select(x => x.Lane).Distinct().Count() });
}

List<StartListRow> ParseDelimitedStartList(string text)
{
    var rows = new List<StartListRow>();
    var autoSeq = new Dictionary<int, int>();
    foreach (var raw in text.Replace("\r", "").Split('\n'))
    {
        var line = raw.Trim();
        if (line.Length == 0) continue;
        char delim = line.Contains(';') ? ';' : line.Contains('\t') ? '\t' : ',';
        var parts = line.Split(delim).Select(x => x.Trim().Trim('"')).ToArray();
        if (parts.Length < 2) continue;

        // Supported layouts: Name;Lane;Sequence OR Lane;Sequence;Name OR Lane;Name
        string name;
        int lane;
        int seq;
        if (int.TryParse(parts[0], out var p0) && p0 is >= 1 and <= 8)
        {
            lane = p0;
            if (parts.Length >= 3 && int.TryParse(parts[1], out var p1)) { seq = p1; name = parts[2]; }
            else { name = parts[1]; seq = autoSeq.TryGetValue(lane, out var a) ? a + 1 : 1; }
        }
        else if (int.TryParse(parts[1], out var p1) && p1 is >= 1 and <= 8)
        {
            name = parts[0]; lane = p1;
            seq = parts.Length >= 3 && int.TryParse(parts[2], out var p2) ? p2 : (autoSeq.TryGetValue(lane, out var a) ? a + 1 : 1);
        }
        else continue; // also ignores header line
        if (seq <= 0 || string.IsNullOrWhiteSpace(name)) continue;
        autoSeq[lane] = Math.Max(autoSeq.TryGetValue(lane, out var old) ? old : 0, seq);
        rows.Add(new StartListRow(name, lane, seq));
    }
    return rows;
}

List<StartListRow> ParseXlsxStartList(Stream stream)
{
    using var wb = new XLWorkbook(stream);
    var ws = wb.Worksheets.First();
    var rows = new List<StartListRow>();
    var autoSeq = new Dictionary<int, int>();
    foreach (var row in ws.RowsUsed())
    {
        var a = row.Cell(1).GetString().Trim();
        var b = row.Cell(2).GetString().Trim();
        var c = row.Cell(3).GetString().Trim();
        string name; int lane; int seq;
        if (int.TryParse(a, out var la) && la is >= 1 and <= 8)
        {
            lane = la;
            if (int.TryParse(b, out var sb)) { seq = sb; name = c; }
            else { name = b; seq = autoSeq.TryGetValue(lane, out var x) ? x + 1 : 1; }
        }
        else if (int.TryParse(b, out var lb) && lb is >= 1 and <= 8)
        {
            name = a; lane = lb;
            seq = int.TryParse(c, out var sc) ? sc : (autoSeq.TryGetValue(lane, out var x) ? x + 1 : 1);
        }
        else continue;
        if (seq <= 0 || string.IsNullOrWhiteSpace(name)) continue;
        autoSeq[lane] = Math.Max(autoSeq.TryGetValue(lane, out var old) ? old : 0, seq);
        rows.Add(new StartListRow(name, lane, seq));
    }
    return rows;
}

async Task<StartEntryData?> GetEntryAsync(SqliteConnection con, SqliteTransaction tx, string entryId)
{
    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = "SELECT Id,RunSessionId,Lane,Sequence,RunnerName,Status,ResultId FROM StartEntries WHERE Id=$id;";
    cmd.Parameters.AddWithValue("$id", entryId);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return null;
    return new StartEntryData(rd.GetString(0), rd.GetString(1), rd.GetInt32(2), rd.GetInt32(3), rd.GetString(4), rd.GetString(5), rd.IsDBNull(6) ? null : rd.GetInt64(6));
}

async Task<bool> IsCurrentEntryAsync(SqliteConnection con, SqliteTransaction tx, StartEntryData entry)
{
    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
SELECT Id FROM StartEntries
WHERE RunSessionId=$run AND Lane=$lane AND Status IN ('Scheduled','Pending')
ORDER BY Sequence LIMIT 1;";
    cmd.Parameters.AddWithValue("$run", entry.RunSessionId);
    cmd.Parameters.AddWithValue("$lane", entry.Lane);
    return string.Equals((string?)await cmd.ExecuteScalarAsync(), entry.Id, StringComparison.OrdinalIgnoreCase);
}

async Task<IResult> StoreResultAsync(TimingResultInput input)
{
    if (string.IsNullOrWhiteSpace(input.StationId) || input.StationId.Length > 50 ||
        input.Lane is < 1 or > 8 || input.DurationUs <= 0 || input.StoppedAtUtc < input.StartedAtUtc)
        return Results.BadRequest(new { error = "Neplatná data výsledku." });

    var eventId = input.DeviceEventId.ToString("D");
    var runId = input.RunSessionId?.ToString("D");
    var entryId = input.EntryId?.ToString("D");
    string? runner = null;
    var validation = "Confirmed"; // legacy results without a start-list entry

    await using var con = await OpenConnectionAsync();
    await using var tx = (SqliteTransaction)await con.BeginTransactionAsync();

    // Idempotency: a queued result may be submitted repeatedly.
    await using (var dup = con.CreateCommand())
    {
        dup.Transaction = tx;
        dup.CommandText = "SELECT Id,ValidationStatus FROM TimingResults WHERE DeviceEventId=$event;";
        dup.Parameters.AddWithValue("$event", eventId);
        await using var rd = await dup.ExecuteReaderAsync();
        if (await rd.ReadAsync())
            return Results.Ok(new { stored = false, duplicate = true, id = rd.GetInt64(0), validationStatus = rd.GetString(1) });
    }

    StartEntryData? entry = null;
    if (!string.IsNullOrWhiteSpace(entryId))
    {
        entry = await GetEntryAsync(con, tx, entryId);
        if (entry is null) return Results.Conflict(new { error = "Startovní položka neexistuje." });
        if (entry.Lane != input.Lane || !string.Equals(entry.RunSessionId, runId, StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { error = "Položka neodpovídá dráze nebo sérii." });
        if (entry.Status != "Scheduled")
            return Results.Conflict(new { error = "Položka už čeká na potvrzení nebo je dokončená." });
        if (!await IsCurrentEntryAsync(con, tx, entry))
            return Results.Conflict(new { error = "Položka není aktuální v pořadí této dráhy." });
        runner = entry.RunnerName;

        await using var mode = con.CreateCommand();
        mode.Transaction = tx;
        mode.CommandText = "SELECT AutoConfirmResults FROM RunSessions WHERE Id=$run;";
        mode.Parameters.AddWithValue("$run", entry.RunSessionId);
        var autoConfirm = Convert.ToInt64(await mode.ExecuteScalarAsync() ?? 0L) == 1;
        validation = autoConfirm ? "Confirmed" : "Pending";
    }
    else if (!string.IsNullOrWhiteSpace(runId))
    {
        // Backward compatibility only. If a start list exists, EntryId is mandatory.
        await using var q = con.CreateCommand();
        q.Transaction = tx;
        q.CommandText = "SELECT COUNT(*) FROM StartEntries WHERE RunSessionId=$run;";
        q.Parameters.AddWithValue("$run", runId);
        if (Convert.ToInt64(await q.ExecuteScalarAsync()) > 0)
            return Results.Conflict(new { error = "Firmware neposlal EntryId. Nahraj firmware v1.1 nebo novější." });
    }

    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
INSERT INTO TimingResults
(DeviceEventId,StationId,RunSessionId,EntryId,Lane,RunnerNameSnapshot,StartedAtUtc,StoppedAtUtc,DurationUs,ReceivedAtUtc,ValidationStatus)
VALUES($event,$station,$run,$entry,$lane,$runner,$start,$stop,$duration,$received,$validation);
SELECT last_insert_rowid();";
    cmd.Parameters.AddWithValue("$event", eventId);
    cmd.Parameters.AddWithValue("$station", input.StationId.Trim());
    cmd.Parameters.AddWithValue("$run", (object?)runId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$entry", (object?)entryId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$lane", input.Lane);
    cmd.Parameters.AddWithValue("$runner", (object?)runner ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$start", input.StartedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    cmd.Parameters.AddWithValue("$stop", input.StoppedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    cmd.Parameters.AddWithValue("$duration", input.DurationUs);
    cmd.Parameters.AddWithValue("$received", UtcNowText());
    cmd.Parameters.AddWithValue("$validation", validation);
    var resultId = Convert.ToInt64(await cmd.ExecuteScalarAsync());

    if (entry is not null)
    {
        await using var e = con.CreateCommand();
        e.Transaction = tx;
        e.CommandText = validation == "Confirmed"
            ? "UPDATE StartEntries SET Status='Completed',ResultId=$result,UpdatedAtUtc=$now WHERE Id=$entry;"
            : "UPDATE StartEntries SET Status='Pending',ResultId=$result,UpdatedAtUtc=$now WHERE Id=$entry;";
        e.Parameters.AddWithValue("$result", resultId);
        e.Parameters.AddWithValue("$now", UtcNowText());
        e.Parameters.AddWithValue("$entry", entry.Id);
        await e.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    return Results.Ok(new { stored = true, duplicate = false, id = resultId, runner, validationStatus = validation });
}

async Task<ResultDecisionData?> GetResultForDecisionAsync(SqliteConnection con, SqliteTransaction tx, long id)
{
    await using var cmd = con.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = "SELECT Id,EntryId,ValidationStatus FROM TimingResults WHERE Id=$id;";
    cmd.Parameters.AddWithValue("$id", id);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return null;
    return new ResultDecisionData(rd.GetInt64(0), rd.IsDBNull(1) ? null : rd.GetString(1), rd.GetString(2));
}

async Task<DeviceStatusData?> GetDeviceStatusAsync(string stationId)
{
    if (string.IsNullOrWhiteSpace(stationId)) return null;
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT StationId,FirmwareVersion,BatteryVoltage,BatteryPercent,RunningMask,WaitingMask,LastSeenUtc
FROM DeviceStatus WHERE StationId=$station;";
    cmd.Parameters.AddWithValue("$station", stationId);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return null;
    return new DeviceStatusData(
        rd.GetString(0),
        rd.IsDBNull(1) ? null : rd.GetString(1),
        rd.IsDBNull(2) ? null : rd.GetDouble(2),
        rd.IsDBNull(3) ? null : rd.GetInt32(3),
        rd.GetInt32(4), rd.GetInt32(5), rd.GetString(6));
}

bool DeviceStatusIsFresh(DeviceStatusData d)
{
    return DateTimeOffset.TryParse(d.LastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var seen)
        && DateTimeOffset.UtcNow - seen <= TimeSpan.FromSeconds(deviceStatusFreshSeconds);
}

async Task<long> ScalarLongAsync(string sql)
{
    await using var con = await OpenConnectionAsync();
    await using var cmd = con.CreateCommand();
    cmd.CommandText = sql;
    return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

async Task<string> CreateBackupAsync()
{
    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
    var backupPath = Path.Combine(backupDir, $"timing_{stamp}.db");
    var destinationCsb = new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
    await using var source = await OpenConnectionAsync();
    await using var destination = new SqliteConnection(destinationCsb.ToString());
    await destination.OpenAsync();
    source.BackupDatabase(destination);

    foreach (var file in new DirectoryInfo(backupDir).GetFiles("timing_*.db").OrderByDescending(f => f.CreationTimeUtc).Skip(backupRetention))
    {
        try { file.Delete(); } catch { }
    }
    return backupPath;
}

record AdminLoginInput(string? Pin);
record PublicResultData(long Id, int Lane, int Sequence, string RunnerName, long DurationUs, string StoppedAtUtc);
record TimingResultInput(Guid DeviceEventId, string StationId, Guid? RunSessionId, Guid? EntryId, byte Lane,
    DateTimeOffset StartedAtUtc, DateTimeOffset StoppedAtUtc, long DurationUs);
record LaneAssignmentInput(byte Lane, string RunnerName);
record RunSessionInput(string? StationId, string? Name, List<LaneAssignmentInput>? Assignments, bool AutoConfirmResults = false, string? UiTheme = null);
record SessionSettingsInput(bool AutoConfirmResults);
record SessionThemeInput(string? UiTheme);
record TestResultInput(string? StationId, int Lane, double DurationSeconds);
record ImportTextInput(string? Text);
record ActiveSession(string Id, string StationId, string Name, string CreatedAtUtc, bool AutoConfirmResults, string UiTheme);
record StartListRow(string RunnerName, int Lane, int Sequence);
record StartEntryData(string Id, string RunSessionId, int Lane, int Sequence, string RunnerName, string Status, long? ResultId);
record LaneStateDto(int Lane, string? EntryId, string? RunnerName, int? Sequence, string Status, long? ResultId);
record PendingResultData(long Id, string? EntryId, long DurationUs, string StartedAtUtc, string StoppedAtUtc);
record ConfirmedResultData(long Id, string? EntryId, long DurationUs, string StartedAtUtc, string StoppedAtUtc);
record DeviceStatusInput(string? StationId, string? FirmwareVersion, double? BatteryVoltage, int? BatteryPercent, int RunningMask, int WaitingMask);
record DeviceStatusData(string StationId, string? FirmwareVersion, double? BatteryVoltage, int? BatteryPercent, int RunningMask, int WaitingMask, string LastSeenUtc);
record ResultDecisionData(long Id, string? EntryId, string ValidationStatus);
