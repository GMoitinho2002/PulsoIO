using PulsoIO.BuildingBlocks.Domain;

namespace PulsoIO.Modules.Administration.Domain;

public sealed class Client : Entity
{
    private readonly List<ClientEnvironment> _environments = [];

    private Client()
    {
    }

    public Client(string name, bool isActive, DateTimeOffset now)
    {
        SetName(name);
        IsActive = isActive;
        CreatedAtUtc = EnsureUtc(now);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public IReadOnlyCollection<ClientEnvironment> Environments => _environments;

    public void Update(string name, bool isActive, DateTimeOffset now)
    {
        SetName(name);
        IsActive = isActive;
        Touch(now);
    }

    private void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = EnsureUtc(now);
        ConcurrencyToken = Guid.NewGuid();
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value) => value.ToUniversalTime();
}
