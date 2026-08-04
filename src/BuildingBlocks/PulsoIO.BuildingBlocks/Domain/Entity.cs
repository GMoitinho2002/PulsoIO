namespace PulsoIO.BuildingBlocks.Domain;
public abstract class Entity { public Guid Id { get; protected init; } = Guid.NewGuid(); }
