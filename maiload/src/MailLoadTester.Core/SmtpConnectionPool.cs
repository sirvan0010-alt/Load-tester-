using MailKit.Net.Smtp;
using MailKit.Security;
using System.Collections.Concurrent;

namespace MailLoadTester;

/// <summary>
/// Pool SMTP klientů s persistentními spojeními.
/// Při selhání Connect/Auth se nově vytvořený klient vždy uvolní (žádný únik socketů).
/// </summary>
public sealed class SmtpConnectionPool : IAsyncDisposable
{
    private readonly ConcurrentBag<SmtpClient> _idle = new();
    private readonly ConcurrentDictionary<SmtpClient, byte> _all = new();
    private readonly SemaphoreSlim _gate;
    private readonly MailTestOptions _options;
    private readonly SecureSocketOptions _socket;
    private int _created;
    private bool _disposed;

    public SmtpConnectionPool(MailTestOptions options)
    {
        _options = options;
        _socket = SmtpConnectivityTester.ToSocketOptions(options.Security);
        _gate = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
    }

    public async Task<SmtpClient> RentAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // 1) zkusit idle klienta
            if (_idle.TryTake(out var idleClient))
            {
                if (idleClient.IsConnected)
                    return idleClient;

                try
                {
                    await EnsureConnectedAsync(idleClient, ct);
                    return idleClient;
                }
                catch
                {
                    // Odstranit z _all, jinak by zůstal disposnutý klient v evidenci do konce testu
                    Forget(idleClient);
                    // spadneme na vytvoření nového
                }
            }

            // 2) vytvořit nový – při selhání Connect/Auth MUSÍME dispose
            var client = new SmtpClient { Timeout = 20_000 };
            if (_options.IgnoreCertificateErrors)
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            try
            {
                await EnsureConnectedAsync(client, ct);
            }
            catch
            {
                SafeDispose(client);
                throw;
            }

            _all.TryAdd(client, 0);
            Interlocked.Increment(ref _created);
            return client;
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    public void Return(SmtpClient client)
    {
        if (_disposed)
        {
            Forget(client);
            return;
        }

        if (!client.IsConnected)
        {
            Forget(client);
            _gate.Release();
            return;
        }
        _idle.Add(client);
        _gate.Release();
    }

    public void Discard(SmtpClient client)
    {
        Forget(client);
        // Release jen pokud semafor ještě žije (po DisposeAsync už ne)
        if (!_disposed)
        {
            try { _gate.Release(); } catch (ObjectDisposedException) { /* pool se právě uvolňuje */ }
        }
    }

    void Forget(SmtpClient client)
    {
        _all.TryRemove(client, out _);
        SafeDispose(client);
    }

    async Task EnsureConnectedAsync(SmtpClient client, CancellationToken ct)
    {
        if (!client.IsConnected)
        {
            await client.ConnectAsync(_options.SmtpHost, _options.Port, _socket, ct);
            if (_options.UseAuthentication)
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        }
        else if (_options.UseAuthentication && !client.IsAuthenticated)
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        }
    }

    static void SafeDispose(SmtpClient client)
    {
        try { if (client.IsConnected) client.Disconnect(false); } catch { /* ignore */ }
        try { client.Dispose(); } catch { /* ignore */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        while (_idle.TryTake(out var c))
        {
            _all.TryRemove(c, out _);
            SafeDispose(c);
        }
        foreach (var c in _all.Keys)
        {
            _all.TryRemove(c, out _);
            SafeDispose(c);
        }
        _gate.Dispose();
        await Task.CompletedTask;
    }

    public int CreatedCount => Volatile.Read(ref _created);
}
