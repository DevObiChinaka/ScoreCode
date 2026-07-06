using System.IO;
using Microsoft.Data.Sqlite;

namespace ScoreCode.Client.Services;

/// <summary>
/// SQLite-backed queue for crash-safe exam submission persistence.
/// Writes student answers to disk BEFORE attempting the API call,
/// so code is never lost if the app crashes or the network fails.
/// </summary>
public sealed class SubmissionQueueService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;

    public SubmissionQueueService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScoreCode");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "submission_queue.db");
    }

    // ── Connection ────────────────────────────────────────────────────────

    private SqliteConnection GetConnection()
    {
        if (_conn is not null) return _conn;
        _conn = new SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();
        EnsureTable();
        return _conn;
    }

    private void EnsureTable()
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS PendingSubmissions (
                SessionId   TEXT PRIMARY KEY,
                ExamId      TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                QueuedAt    TEXT NOT NULL DEFAULT (datetime('now')),
                Status      TEXT NOT NULL DEFAULT 'Pending'
            )
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Queue a submission locally. Call this BEFORE attempting the API call.
    /// If a submission for this session already exists, it is updated.
    /// </summary>
    public void QueueSubmission(Guid sessionId, Guid examId, string payloadJson)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PendingSubmissions (SessionId, ExamId, PayloadJson, Status)
            VALUES ($sid, $eid, $payload, 'Pending')
            ON CONFLICT(SessionId) DO UPDATE SET
                PayloadJson = $payload,
                QueuedAt    = datetime('now'),
                Status      = 'Pending'
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        cmd.Parameters.AddWithValue("$eid", examId.ToString());
        cmd.Parameters.AddWithValue("$payload", payloadJson);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Mark a submission as successfully sent.</summary>
    public void MarkSubmitted(Guid sessionId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE PendingSubmissions SET Status = 'Submitted' WHERE SessionId = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        cmd.ExecuteNonQuery();
    }

    /// <summary>Remove a submitted entry from the queue.</summary>
    public void Remove(Guid sessionId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PendingSubmissions WHERE SessionId = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId.ToString());
        cmd.ExecuteNonQuery();
    }

    /// <summary>Get the first pending (unsubmitted) entry, or null.</summary>
    public PendingSubmission? GetPending()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT SessionId, ExamId, PayloadJson, QueuedAt
            FROM PendingSubmissions
            WHERE Status = 'Pending'
            ORDER BY QueuedAt ASC
            LIMIT 1
            """;

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new PendingSubmission(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            DateTime.Parse(reader.GetString(3))
        );
    }

    /// <summary>Check if any pending submissions exist.</summary>
    public bool HasPending()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PendingSubmissions WHERE Status = 'Pending'";
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}

/// <summary>Represents a pending submission from the local queue.</summary>
public record PendingSubmission(
    Guid SessionId,
    Guid ExamId,
    string PayloadJson,
    DateTime QueuedAt
);
