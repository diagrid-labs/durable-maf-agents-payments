using System.Globalization;

namespace InvoicePayments.ApiService.Demo;

public record LedgerEntry(DateTime TimestampUtc, string InvoiceId, string SupplierName);

// An append-only, human-readable record of every InvoiceReviewer agent call that actually
// executed. The workflow records each call through a checkpointed activity, so a completed
// call is replayed from durable history (never re-executed) after a crash. The file shows
// each invoice exactly once, with a visible time gap at the restart.
public sealed class AgentCallLedger(string directory)
{
    // The fan-out runs activities concurrently, so appends from parallel branches must not
    // interleave within a line. A process-wide lock serializes them.
    private static readonly object AppendLock = new();

    public string FilePath => Path.Combine(directory, "agent-calls.log");

    public void Append(string invoiceId, string supplierName, DateTime timestampUtc)
    {
        var line = string.Join('\t',
            timestampUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            Sanitize(invoiceId),
            Sanitize(supplierName));

        lock (AppendLock)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
    }

    public IReadOnlyList<LedgerEntry> ReadEntries()
    {
        lock (AppendLock)
        {
            if (!File.Exists(FilePath))
                return [];

            return File.ReadAllLines(FilePath)
                .Select(TryParse)
                .OfType<LedgerEntry>()
                .ToList();
        }
    }

    // Tolerant of a torn final line left by a hard crash mid-append.
    private static LedgerEntry? TryParse(string line)
    {
        var parts = line.Split('\t', 3);
        if (parts.Length < 2
            || !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
            return null;

        return new LedgerEntry(ts, parts[1], parts.Length > 2 ? parts[2] : "");
    }

    private static string Sanitize(string value) =>
        value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
