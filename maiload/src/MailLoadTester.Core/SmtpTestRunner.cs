using System.Collections.Concurrent;
using System.Diagnostics;
using MailKit.Net.Smtp;
using MimeKit;

namespace MailLoadTester;

public sealed class SmtpTestRunner
{
    public async Task<MailTestResult> RunAsync(
        MailTestOptions options,
        IProgress<ProgressUpdate> progress,
        CancellationToken ct)
    {
        var fsm = new TestStateMachine();
        void Report(int s, int f, string status, double? eta, int phase, string current, string next,
            MessageStep? step = null, int? messageIndex = null, int? workerId = null) =>
            progress.Report(new ProgressUpdate(s, f, status, eta, phase, current, next,
                fsm.Phase, step, messageIndex, workerId, options.MaxConcurrency));

        fsm.Transition(TestPhase.Validating, "Validating options");
        try { Validation.Validate(options); }
        catch { fsm.ForceFailure("Validation failed"); throw; }
        var sw = Stopwatch.StartNew();
        long activeTicks = 0;
        int sent = 0, failed = 0;
        int retries = 0, smtp4xx = 0, smtp5xx = 0, timeouts = 0;
        string lastError = "";
        var latencies = new ConcurrentBag<double>();
        var startTime = DateTime.UtcNow;
        var rateLimiter = new RateLimiter(options.IntervalMs);
        bool cancelled = false;

        fsm.Transition(TestPhase.PreparingAttachments, "Attachment planner");
        Report(0, 0, "Příprava příloh…", null, 2,
            "Připravuji přílohy a plán paměti",
            options.DryRun ? "Simulace odesílání (dry-run)" : "Vytvoření SMTP spojení / poolu");

        var attachmentPlan = AttachmentPlanner.Prepare(options.Attachments, options.MaxConcurrency);
        if (attachmentPlan.Sources.Count > 0)
            Report(0, 0, $"Přílohy: {attachmentPlan.Description}", null, 1,
                "Přílohy připraveny: " + (attachmentPlan.Preloaded ? "v RAM" : "z disku"),
                options.DryRun ? "Simulace odesílání" : "SMTP spojení");

        fsm.Transition(options.DryRun ? TestPhase.Sending : TestPhase.ConnectingSmtp,
            options.DryRun ? "Dry-run" : "Create SMTP pool");
        Report(0, 0, options.DryRun ? "Dry-run (bez SMTP)" : "Připojuji SMTP pool…", null, 2,
            options.DryRun ? "Dry-run režim — bez reálného serveru" : "Navazuji SMTP spojení (pool)",
            "Odesílání zpráv" + (options.MaxConcurrency > 1 ? $" (paralelismus {options.MaxConcurrency})" : ""));

        await using var pool = options.DryRun ? null : new SmtpConnectionPool(options);

        try
        {
            for (int batchStart = 1; batchStart <= options.MessageCount; batchStart += options.BatchMode ? options.BatchSize : options.MessageCount)
            {
                ct.ThrowIfCancellationRequested();
                int batchEnd = options.BatchMode
                    ? Math.Min(options.MessageCount, batchStart + options.BatchSize - 1)
                    : options.MessageCount;

                var batchActiveSw = Stopwatch.StartNew();
                if (fsm.Phase == TestPhase.ConnectingSmtp)
                    fsm.Transition(TestPhase.Sending, "First batch");
                var tasks = Enumerable.Range(batchStart, batchEnd - batchStart + 1).Select(async i =>
                {
                    var workerId = ((i - batchStart) % Math.Max(1, options.MaxConcurrency)) + 1;
                    Report(Volatile.Read(ref sent), Volatile.Read(ref failed), $"QUEUE #{i} · W{workerId}", null, 3,
                        $"Zpráva #{i} ve frontě · W{workerId}", "Rate limit",
                        MessageStep.Queued, i, workerId);
                    await rateLimiter.WaitAsync(ct);
                    var recipient = options.Recipients[(i - 1) % options.Recipients.Count];
                    var data = options.RandomTestData
                        ? RandomTestData.Create(i)
                        : (options.DisplayName, options.Subject, options.Body);
                    var msgSw = Stopwatch.StartNew();
                    Exception? lastEx = null;
                    bool success = false;
                    bool retryable = false;

                    for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
                    {
                        ct.ThrowIfCancellationRequested();
                        SmtpClient? client = null;
                        try
                        {
                            if (options.DryRun)
                            {
                                await Task.Delay(Random.Shared.Next(15, 80), ct);
                            }
                            else
                            {
                                Report(Volatile.Read(ref sent), Volatile.Read(ref failed), $"CONNECT #{i} · W{workerId}", null, 3,
                                    $"SMTP spojení · zpráva #{i} · W{workerId}", "MIME zprávy",
                                    MessageStep.RentingConnection, i, workerId);
                                client = await pool!.RentAsync(ct);
                                Report(Volatile.Read(ref sent), Volatile.Read(ref failed), $"MIME #{i} · W{workerId}", null, 3,
                                    $"Stavím MIME · zpráva #{i} · W{workerId}", "SMTP SEND",
                                    MessageStep.BuildingMime, i, workerId);
                                using var message = BuildMessage(options, data, recipient, attachmentPlan.Sources, i);
                                Report(Volatile.Read(ref sent), Volatile.Read(ref failed), $"SEND #{i} · W{workerId}", null, 3,
                                    $"SMTP SEND · zpráva #{i} · W{workerId}", "OK nebo retry",
                                    MessageStep.SmtpSend, i, workerId);
                                await client.SendAsync(message, ct);
                                pool.Return(client);
                                client = null;
                            }
                            success = true;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            if (client is not null) pool?.Discard(client);
                            throw;
                        }
                        catch (Exception ex) when (IsTransient(ex) && attempt < options.MaxRetries)
                        {
                            if (client is not null)
                            {
                                // Po SMTP/protocol/network chybě klienta už nepovažujeme za zdravého.
                                // Retry dostane nové spojení místo vracení poškozeného klienta do poolu.
                                pool!.Discard(client);
                                client = null;
                            }
                            lastEx = ex;
                            retryable = true;
                            Interlocked.Increment(ref retries);
                            if (ex is SmtpCommandException sce)
                            {
                                var code = (int)sce.StatusCode;
                                if (code >= 400 && code < 500) Interlocked.Increment(ref smtp4xx);
                            }
                            else if (ex is TimeoutException)
                            {
                                Interlocked.Increment(ref timeouts);
                            }
                            Report(Volatile.Read(ref sent), Volatile.Read(ref failed),
                                $"RETRY #{i} · W{workerId}", null, 3,
                                $"Dočasná chyba · zpráva #{i} · W{workerId}",
                                $"Retry {attempt + 1}/{options.MaxRetries}",
                                MessageStep.FailedTransient, i, workerId);
                            await Task.Delay(GetRetryDelay(attempt), ct);
                        }
                        catch (Exception ex)
                        {
                            if (client is not null)
                            {
                                // I u netriviální chyby je bezpečnější po SendAsync klienta zahodit;
                                // pool nemá recyklovat stav, jehož protokolový stav neznáme.
                                pool!.Discard(client);
                                client = null;
                            }
                            lastEx = ex;
                            if (ex is SmtpCommandException sce)
                            {
                                var code = (int)sce.StatusCode;
                                if (code >= 400 && code < 500) Interlocked.Increment(ref smtp4xx);
                                else if (code >= 500) Interlocked.Increment(ref smtp5xx);
                            }
                            else if (ex is TimeoutException)
                            {
                                Interlocked.Increment(ref timeouts);
                            }
                            break;
                        }
                    }

                    msgSw.Stop();
                    var explained = lastEx != null ? Validation.ExplainSmtpError(lastEx) : "";
                    if (success)
                    {
                        var s = Interlocked.Increment(ref sent);
                        latencies.Add(msgSw.Elapsed.TotalMilliseconds);
                        var done = s + Volatile.Read(ref failed);
                        var eta = EstimateEta(done, options.MessageCount, startTime);
                        var label = options.DryRun ? "DRY-RUN OK" : "OK";
                        var remain = options.MessageCount - done;
                        Report(s, Volatile.Read(ref failed),
                            $"{label} #{i} → {recipient} ({msgSw.ElapsedMilliseconds} ms)", eta, 3,
                            $"Hotovo #{i} → {recipient} ({msgSw.ElapsedMilliseconds} ms) · celkem OK {s}",
                            remain > 0
                                ? $"Zbývá odeslat ~{remain} zpráv" + (options.MaxConcurrency > 1 ? $" (až {options.MaxConcurrency} najednou)" : "")
                                : "Souhrn a statistiky",
                            MessageStep.Succeeded, i, workerId);
                    }
                    else
                    {
                        var f = Interlocked.Increment(ref failed);
                        Interlocked.Exchange(ref lastError, explained);
                        var done = Volatile.Read(ref sent) + f;
                        var eta = EstimateEta(done, options.MessageCount, startTime);
                        var retryInfo = retryable ? " (po retry)" : "";
                        Report(Volatile.Read(ref sent), f,
                            $"FAIL #{i}{retryInfo}: {explained}", eta, 3,
                            $"Zastaveno na zprávě #{i}: {explained}",
                            done < options.MessageCount ? "Pokračuji dalšími zprávami (nebo STOP)" : "Souhrn chyb",
                            MessageStep.FailedFinal, i, workerId);
                    }
                }).ToArray();

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                if (cancelled)
                {
                    fsm.Transition(TestPhase.Cancelled, "Cancellation during batch");
                    foreach (var t in tasks)
                    {
                        try { await t; }
                        catch { }
                    }
                    batchActiveSw.Stop();
                    activeTicks += batchActiveSw.ElapsedTicks;
                    break;
                }

                batchActiveSw.Stop();
                activeTicks += batchActiveSw.ElapsedTicks;

                if (options.BatchMode && batchEnd < options.MessageCount)
                {
                    fsm.Transition(TestPhase.BatchPause, "Batch completed");
                    Report(sent, failed,
                        $"PAUZA — dávka {batchEnd - batchStart + 1} dokončena, čekám {options.BatchPauseSeconds}s…", null, 3,
                        $"Dávka dokončena ({batchEnd}/{options.MessageCount})",
                        $"Za {options.BatchPauseSeconds}s začne další dávka");
                    await Task.Delay(TimeSpan.FromSeconds(options.BatchPauseSeconds), ct);
                    fsm.Transition(TestPhase.Sending, "Next batch");
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            fsm.Transition(TestPhase.Cancelled, "User cancellation");
        }
        finally
        {
            sw.Stop();
        }

        if (!cancelled && fsm.Phase == TestPhase.Sending)
            fsm.Transition(TestPhase.Completed, "All batches completed");

        var latencyArr = latencies.OrderBy(x => x).ToArray();
        double avg = latencyArr.Length > 0 ? latencyArr.Average() : 0;
        double min = latencyArr.Length > 0 ? latencyArr[0] : 0;
        double max = latencyArr.Length > 0 ? latencyArr[^1] : 0;
        double p50 = Percentile(latencyArr, 0.50);
        double p95 = Percentile(latencyArr, 0.95);
        double p99 = Percentile(latencyArr, 0.99);
        double throughput = sw.Elapsed.TotalSeconds > 0 ? sent / sw.Elapsed.TotalSeconds : 0;
        double activeSeconds = activeTicks / (double)Stopwatch.Frequency;
        double activeThroughput = activeSeconds > 0 ? sent / activeSeconds : 0;

        if (cancelled && string.IsNullOrEmpty(lastError))
            lastError = "Zastaveno uživatelem";

        var poolConnections = pool?.CreatedCount ?? 0;
        return new MailTestResult(options.MessageCount, sent, failed, sw.Elapsed, lastError,
            avg, min, max, p50, p95, p99, throughput, cancelled, activeThroughput, retries, smtp4xx, smtp5xx, timeouts, poolConnections);
    }

    static MimeMessage BuildMessage(
        MailTestOptions options,
        (string Name, string Subject, string Body) data,
        string recipient,
        IReadOnlyList<AttachmentSource> attachments,
        int testId)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(data.Name, options.From));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = data.Subject;
        message.Headers["X-MailLoadTester-Test-ID"] = testId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var builder = new BodyBuilder();
        if (options.HtmlBody)
            builder.HtmlBody = data.Body;
        else
            builder.TextBody = data.Body;

        foreach (var attachment in attachments)
        {
            if (attachment.PreloadedContent is not null)
                builder.Attachments.Add(attachment.FileName, attachment.PreloadedContent);
            else
                builder.Attachments.Add(attachment.FullPath);
        }

        message.Body = builder.ToMessageBody();

        foreach (var kv in options.CustomHeaders)
        {
            if (kv.Key.Equals("From", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("To", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Subject", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Message-Id", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("MIME-Version", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;
            message.Headers[kv.Key] = kv.Value;
        }

        return message;
    }

    static bool IsTransient(Exception ex)
    {
        if (ex is SmtpCommandException sce)
            return (int)sce.StatusCode is >= 400 and < 500;
        if (ex is SmtpProtocolException) return true;
        if (ex is IOException or TimeoutException) return true;
        return false;
    }

    static TimeSpan GetRetryDelay(int attempt)
    {
        // Exponential backoff with bounded jitter: 500 ms, 1 s, 2 s, 4 s, ...
        var baseMs = Math.Min(8_000, 500 * Math.Pow(2, attempt));
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
        return TimeSpan.FromMilliseconds(Math.Max(100, baseMs * (1 + jitter)));
    }

    static double? EstimateEta(int done, int total, DateTime start)
    {
        if (done <= 0) return null;
        var elapsed = (DateTime.UtcNow - start).TotalSeconds;
        if (elapsed < 0.5) return null;
        var rate = done / elapsed;
        if (rate <= 0) return null;
        return (total - done) / rate;
    }

    static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];
        var idx = p * (sorted.Length - 1);
        var lo = (int)Math.Floor(idx);
        var hi = (int)Math.Ceiling(idx);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
    }
}
