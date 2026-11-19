using System;
using System.Collections.Generic;

internal static class SheetsTelemetryOutboxTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static SheetsTelemetryRun Run(int n, DateTime ended) => new SheetsTelemetryRun
    { sessionId = n.ToString("x32"), endedAtUtc = ended.ToString("o") };
    private static int Main()
    {
        DateTime now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var queue = new SheetsTelemetryOutbox();
        var a = Run(1, now); var b = Run(2, now); var c = Run(3, now);
        Check(queue.Enqueue(a, now), "New run enqueued.");
        Check(!queue.Enqueue(a, now) && queue.runs.Count == 1, "Duplicate finalization does not duplicate the queue.");
        Check(!queue.Enqueue(new SheetsTelemetryRun { sessionId = "bad" }, now), "Invalid IDs rejected.");
        queue.Enqueue(b, now); queue.Enqueue(c, now);
        Check(queue.Acknowledge(new[] { a }, new[] { c.sessionId }, null) == 0, "Unsent IDs cannot be acknowledged.");
        Check(queue.Acknowledge(new[] { a }, null, null) == 0 && queue.runs.Count == 3, "Lost acknowledgement preserves data.");
        Check(queue.Acknowledge(new[] { a, b }, new[] { a.sessionId }, new[] { b.sessionId }) == 2,
            "Only explicitly accepted or invalid-rejected sent records leave the outbox.");
        Check(queue.runs.Count == 1 && queue.runs[0] == c, "Unrelated queued run survives.");
        Check(queue.Acknowledge(new[] { a }, new[] { a.sessionId }, null) == 0, "Duplicate acknowledgement is harmless.");
        queue.Enqueue(Run(4, now.AddDays(-31)), now); queue.Prune(now);
        Check(queue.runs.Count == 1, "Expired records are removed by retention.");
        queue.runs.Add(new SheetsTelemetryRun { sessionId = Run(5, now).sessionId, endedAtUtc = "broken" });
        queue.Prune(now); Check(queue.runs.Count == 1, "Malformed timestamps do not persist forever.");
        for (int i=10; i<130; i++) queue.Enqueue(Run(i, now), now);
        Check(queue.runs.Count == SheetsTelemetryOutbox.MaximumRuns, "Offline queue has a hard size bound.");
        Check(queue.runs[0].sessionId == Run(30, now).sessionId, "Overflow keeps the newest records.");
        queue.runs = null; queue.Prune(now); Check(queue.runs.Count == 0, "Null persisted list recovers safely.");
        Check(SheetsTelemetryOutbox.ValidId(Guid.NewGuid().ToString("N")), "Generated IDs satisfy protocol.");
        Check(!SheetsTelemetryOutbox.ValidId(null), "Null ID rejected.");
        Check(!SheetsTelemetryOutbox.ValidId("ABCDEF1234567890ABCDEF1234567890"), "Noncanonical IDs cannot block a receiver batch.");
        Console.WriteLine("Sheets outbox: " + checks + " checks passed."); return 0;
    }
}
