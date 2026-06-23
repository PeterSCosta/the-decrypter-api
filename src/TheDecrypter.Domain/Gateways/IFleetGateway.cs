namespace TheDecrypter.Domain.Gateways;

/// <summary>Frota em tempo real via Traccar (self-hosted). Vazio se não configurado.</summary>
public interface IFleetGateway
{
    Task<IReadOnlyList<FleetDevice>> GetFleetAsync(CancellationToken ct = default);
}
