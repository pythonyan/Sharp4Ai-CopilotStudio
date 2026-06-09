using Microsoft.Extensions.Options;
using Sharp4AI.Demo.Api.DTO;
using Sharp4AI.Demo.Api.Interfaces;
using Sharp4AI.Demo.Api.Settings;

namespace Sharp4AI.Demo.Api.Stubs;

/// <summary>
/// Stub per IBusinessConfigurationService: legge la configurazione dell'ente da DemoSettings
/// invece di chiamare un servizio esterno (Dapr/HTTP).
/// </summary>
public class NullBusinessConfigurationService(IOptions<DemoSettings> settings) : IBusinessConfigurationService
{
    private readonly DemoSettings _s = settings.Value;

    public Task<BusinessServiceConfiguration> GetBusinessServiceConfiguration(string codiceEnte, string urlCallbackClient)
    {
        return Task.FromResult(new BusinessServiceConfiguration
        {
            Codice = _s.Codice,
            Intestazione = _s.Nome,
            EmailAssistenza = _s.EmailAssistenza,
            EmailDiContattoHelpDesk = _s.EmailDiContattoHelpDesk,
            ListaTipologiaErrore = _s.ListaTipologiaErrore,
        });
    }
}
