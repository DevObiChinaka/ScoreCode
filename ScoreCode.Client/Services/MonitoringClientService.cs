using ScoreCode.Shared.DTOs.Monitoring;

namespace ScoreCode.Client.Services;

/// <summary>Wraps the admin MonitoringController endpoints.</summary>
public class MonitoringClientService
{
    private readonly ApiService _api;

    public MonitoringClientService(ApiService api) => _api = api;

    public async Task<MonitoringOverviewDto?> GetOverviewAsync()
    {
        var resp = await _api.GetAsync<MonitoringOverviewDto>("monitoring/overview");
        return resp?.Data;
    }
}
