# MailLoadTester 2.9.40 — pacing/retry audit

## Fixed in working tree / prepared for push

### 1. First pacing slot
`SmartPaceController` previously scheduled the first global slot at `now + IntervalMs`. That caused the first message to wait one full interval before it could start.

The scheduler now uses `now` for the first reservation and `previousSlot + interval` for subsequent reservations.

Expected behavior with `IntervalMs = 500 ms`:
- message 1: immediate
- message 2: approximately +500 ms
- message 3: approximately +1000 ms

### 2. Retry pacing bypass
`SmtpTestRunner` previously applied `SmartPaceController.WaitBeforeSendAsync()` only once per logical message. A transient SMTP failure could therefore retry after the exponential retry delay without re-entering the global pacing schedule.

Retries now call `SmartPaceController.WaitBeforeRetryAsync()` after the retry backoff. This consumes a global pacing slot but does **not** create a second per-recipient reservation for the same logical message.

### 3. Cancellation / reservation safety
The retry path remains cancellation-aware. The existing `finally` continues to release the original per-recipient reservation when the logical message finishes unsuccessfully or is cancelled.

## Retry / SMTP error classification audit

### Result: classification is currently correct; no blind rewrite is justified

`SmtpTestRunner.IsTransient(Exception)` currently treats these as retryable:
- SMTP status 400–499 (`SmtpCommandException`)
- `SmtpProtocolException`
- `IOException`
- `TimeoutException`

SMTP status 500+ is **not** retryable. This prevents permanent SMTP rejections from consuming `MaxRetries`.

The result counters are also classified consistently in the catch paths:
- SMTP 4xx increments `Smtp4xx`
- SMTP 5xx increments `Smtp5xx`
- timeout increments `Timeouts`
- retry count increments only when an actual retry is scheduled

A transient SMTP command failure is recorded before retry; the final attempt is recorded in the terminal catch path. Therefore one SMTP failure event is not double-counted merely because it reaches the retry loop boundary.

`CircuitBreaker.RecordFailure()` is invoked once per failed SMTP attempt, including retry attempts. `RecordSuccess()` is invoked after a successful send. This is intentional: the breaker observes transport/server attempts rather than only logical messages.

### Important observation
`SmtpProtocolException` is intentionally retryable but is not an SMTP 4xx/5xx response, so it must not increment either SMTP status counter. Likewise, authentication failures are not classified as transient by `IsTransient` and therefore are not retried.

## Circuit breaker follow-up

The circuit breaker implementation is thread-safe and already protects its sliding-window ring with a lock. Cooldown reset uses conditional removal to avoid erasing a newer concurrent opening. `EverOpened` is maintained separately from live state for correct final reporting.

One behavior to keep under regression test: retry attempts count as individual breaker observations. If product requirements later define the breaker as logical-message based, this should be changed deliberately rather than accidentally.

## Architectural decision
The old `RateLimiter` remains unused by the runner (`new RateLimiter(0)`) because `SmartPaceController` is the single owner of global message spacing. This avoids two independent pacing queues.

## Next verification
Run on Windows/.NET 8:

```text
dotnet test
dotnet build -c Release
```

Regression coverage should include:
1. first pacing reservation has zero/near-zero delay;
2. subsequent reservations remain spaced by the configured interval;
3. cancelled retry pacing removes only its own global reservation;
4. retry pacing does not increment per-recipient `Reserved` twice;
5. SMTP 4xx is retryable;
6. SMTP 5xx is terminal and is not retried;
7. timeout/protocol/IO failures are retryable;
8. SMTP 4xx/5xx/timeout counters are incremented exactly once per failed attempt;
9. breaker observations remain consistent across retries.

No SMTP credentials or API keys are introduced by these changes.
