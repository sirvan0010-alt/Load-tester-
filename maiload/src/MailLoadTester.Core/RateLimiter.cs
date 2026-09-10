namespace MailLoadTester;

/// <summary>
/// Jednoduchý token-bucket / interval rate limiter.
/// Zajišťuje minimální rozestup mezi starty odesílání i při paralelismu.
/// </summary>
public sealed class RateLimiter
{
    private readonly int _intervalMs;
    private readonly object _lock = new();
    private DateTime _nextAllowed = DateTime.MinValue;

    public RateLimiter(int intervalMs)
    {
        _intervalMs = Math.Max(0, intervalMs);
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        if (_intervalMs <= 0) return;

        TimeSpan delay;
        DateTime reservedNext;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now < _nextAllowed)
            {
                delay = _nextAllowed - now;
                reservedNext = _nextAllowed.AddMilliseconds(_intervalMs);
                _nextAllowed = reservedNext;
            }
            else
            {
                delay = TimeSpan.Zero;
                reservedNext = now.AddMilliseconds(_intervalMs);
                _nextAllowed = reservedNext;
            }
        }

        try
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // If this reservation is still the last one, give the slot back.
            // This prevents a cancelled waiter from creating a phantom delay.
            lock (_lock)
            {
                if (_nextAllowed == reservedNext)
                    _nextAllowed = DateTime.UtcNow;
            }
            throw;
        }
    }
}
