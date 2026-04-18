using System.Collections.Concurrent;

namespace Kriteriom.Gateway.Auth;

public class RefreshTokenStore
{
    private record Entry(string Username, string Role, DateTime Expiry);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public string Generate(string username, string role, int expiryDays = 7)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _store[token] = new Entry(username, role, DateTime.UtcNow.AddDays(expiryDays));
        return token;
    }

    public (string Username, string Role)? Validate(string token)
    {
        if (!_store.TryGetValue(token, out var entry)) return null;
        if (entry.Expiry < DateTime.UtcNow) { _store.TryRemove(token, out _); return null; }
        return (entry.Username, entry.Role);
    }

    public void Revoke(string token) => _store.TryRemove(token, out _);
}
