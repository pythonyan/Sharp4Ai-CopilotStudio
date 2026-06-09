using Sharp4AI.Demo.Api.DTO;

namespace Sharp4AI.Demo.Api.Interfaces;

public interface IBusinessConfigurationService
{
    Task<BusinessServiceConfiguration> GetBusinessServiceConfiguration(string codice, string urlCallbackClient);
}
