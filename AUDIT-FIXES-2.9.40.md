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
4. retry pacing does not increment per-recipient `Reserved` twice.

No SMTP credentials or API keys are introduced by these changes.
