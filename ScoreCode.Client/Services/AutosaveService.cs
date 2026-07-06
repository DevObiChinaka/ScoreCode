using Microsoft.Data.Sqlite;

namespace ScoreCode.Client.Services;

/// <summary>
/// Local SQLite-based autosave for exam answers.
/// Stores per-question code in %LOCALAPPDATA%\ScoreCode\autosave.db
/// so the student can recover from crashes/restarts.
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly SqliteConnection _db;
    private static readonly string DbPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreCode", "autosave.db");

    public AutosaveService()
    {
        // Ensure directory exists
        var dir = System.IO.Path.GetDirectoryName(DbPath)!;
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        _db = new SqliteConnection($"Data Source={DbPath}");
        _db.Open();

        // Create table if not exists
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Autosave (
                SessionId   TEXT NOT NULL,
                QuestionId  TEXT NOT NULL,
                Code        TEXT NOT NULL DEFAULT '',
                SavedAt     TEXT NOT NULL,
                PRIMARY KEY (SessionId, QuestionId)
            )
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Upsert a single question's code.</summary>
    public void Save(Guid sessionId, Guid questionId, string code)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Autosave (SessionId, QuestionId, Code, SavedAt)
            VALUES ($sid, $qid, $code, $ts)
            ON CONFLICT(SessionId, QuestionId)
            DO UPDATE SET Code = $code, SavedAt = $ts
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        cmd.Parameters.AddWithValue("$qid", questionId.ToString());
        cmd.Parameters.AddWithValue("$code", code);
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Load all saved answers for a session.</summary>
    public Dictionary<Guid, string> LoadSession(Guid sessionId)
    {
        var result = new Dictionary<Guid, string>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT QuestionId, Code FROM Autosave WHERE SessionId = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var qId = Guid.Parse(reader.GetString(0));
            var code = reader.GetString(1);
            if (!string.IsNullOrEmpty(code))
                result[qId] = code;
        }

        return result;
    }

    /// <summary>Check if a session has any saved answers.</summary>
    public bool HasSavedSession(Guid sessionId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Autosave WHERE SessionId = $sid AND Code != ''";
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>Delete all saved data for a session (after submit).</summary>
    public void ClearSession(Guid sessionId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Autosave WHERE SessionId = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Close();
        _db.Dispose();
    }
}
