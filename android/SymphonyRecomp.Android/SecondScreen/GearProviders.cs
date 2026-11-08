namespace SymphonyRecomp.Android.SecondScreen;

public sealed record GearField(string Name, string Value);
public sealed record GearCard(string Title, IReadOnlyList<GearField> Fields);

/// <summary>Version 1 push-only companion API. No provider code runs on the UI thread.</summary>
public static class GearProviders
{
    public const int ApiVersion = 1;
    static readonly object Gate = new();
    static readonly Dictionary<string, Registration> Providers = new(StringComparer.OrdinalIgnoreCase);

    public static Registration Register(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) throw new ArgumentException("Invalid provider id", nameof(id));
        lock (Gate)
        {
            if (Providers.ContainsKey(id)) throw new InvalidOperationException("Provider already registered: " + id);
            if (Providers.Count >= 16) throw new InvalidOperationException("Too many Gear providers");
            var registration = new Registration(id);
            Providers.Add(id, registration);
            return registration;
        }
    }

    public static IReadOnlyList<GearCard> Snapshot()
    {
        lock (Gate) return Array.AsReadOnly(Providers.OrderBy(p => p.Key, StringComparer.Ordinal)
            .SelectMany(p => p.Value.Cards).ToArray());
    }

    static string Clean(string? text, int limit) => new((text ?? "").Where(c => !char.IsControl(c)).Take(limit).ToArray());

    public sealed class Registration : IDisposable
    {
        readonly string _id;
        bool _disposed;
        internal GearCard[] Cards = [];
        internal Registration(string id) => _id = id;

        // Copy and bound all caller-owned collections before publication. One atomic replacement.
        public void Publish(params GearCard[] cards)
        {
            ArgumentNullException.ThrowIfNull(cards);
            var safe = cards.Take(8).Where(c => c != null).Select(c => new GearCard(Clean(c.Title, 48),
                Array.AsReadOnly((c.Fields ?? Array.Empty<GearField>()).Take(64).Where(f => f != null)
                    .Select(f => new GearField(Clean(f.Name, 40), Clean(f.Value, 96))).ToArray()))).ToArray();
            lock (Gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                Cards = safe;
            }
        }

        public void Dispose()
        {
            lock (Gate)
            {
                if (_disposed) return;
                _disposed = true;
                Providers.Remove(_id);
                Cards = [];
            }
        }
    }
}
