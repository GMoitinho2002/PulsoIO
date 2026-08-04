using PulsoIO.BuildingBlocks.Domain;

namespace PulsoIO.Modules.Administration.Domain;

public sealed class ClientEnvironment : Entity
{
    private readonly List<Integration> _integrations = [];

    private ClientEnvironment()
    {
    }

    public ClientEnvironment(
        Guid clientId,
        string name,
        EnvironmentKind kind,
        bool isActive,
        DateTimeOffset now)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("O cliente deve ser informado.", nameof(clientId));
        }

        ClientId = clientId;
        SetName(name);
        Kind = kind;
        IsActive = isActive;
        CreatedAtUtc = now.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid ClientId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public EnvironmentKind Kind { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public Client Client { get; private set; } = null!;

    public IReadOnlyCollection<Integration> Integrations => _integrations;

    public void Update(string name, EnvironmentKind kind, bool isActive, DateTimeOffset now)
    {
        SetName(name);
        Kind = kind;
        IsActive = isActive;
        UpdatedAtUtc = now.ToUniversalTime();
        ConcurrencyToken = Guid.NewGuid();
    }

    private void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }
}
