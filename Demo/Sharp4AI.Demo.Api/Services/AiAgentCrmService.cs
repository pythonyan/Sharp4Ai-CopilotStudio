using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Sharp4AI.Demo.Api.Interfaces;
using Sharp4AI.Demo.Api.Plugins;
using Sharp4AI.Demo.Api.DTO;
using Sharp4AI.Demo.Api.Settings;

namespace Sharp4AI.Demo.Api.Services;

public class AiAgentCrmService(
    Kernel kernel,
    ICrmProxyService crmProxyService,
    IOptions<AiAgentCrmSettings> settings,
    ILoggerFactory loggerFactory) : IAiAgentCrmService
{
    private readonly AiAgentCrmSettings _settings = settings.Value;
    private readonly ILogger<AiAgentCrmService> _logger = loggerFactory.CreateLogger<AiAgentCrmService>();

    /// <summary>
    /// Cerca nel CRM i ticket correlati alle descrizioni di segnalazioni simili fornite.
    /// Restituisce un risultato aggregato con le segnalazioni arricchite con ticket e soluzione CRM.
    /// Se la fase 2 è disabilitata o l'elenco è vuoto, restituisce un risultato vuoto senza errori.
    /// In caso di eccezione esegue un fallback graceful restituendo un risultato vuoto.
    /// </summary>
    /// <param name="descrizioniSimili">Elenco di descrizioni testuali di segnalazioni simili da ricercare nel CRM.</param>
    /// <param name="codiceEnte">Codice identificativo dell'ente, usato esclusivamente per il logging.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>
    /// Un <see cref="RisultatoSegnalazioniSimili"/> con il flag di successo e la lista delle segnalazioni arricchite.
    /// </returns>
    public async Task<RisultatoSegnalazioniSimili> TrovaSegnalazioniSimiliCrmAsync(
        IReadOnlyList<string> descrizioniSimili,
        string codiceEnte,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Fase2Abilitata)
        {
            _logger.LogDebug("AiAgentCrmService: Fase 2 non abilitata — skip");
            return new RisultatoSegnalazioniSimili(false, []);
        }

        if (descrizioniSimili.Count == 0)
        {
            _logger.LogDebug("AiAgentCrmService: nessuna descrizione simile — skip");
            return new RisultatoSegnalazioniSimili(false, []);
        }

        try
        {
            var risultato = await RicercaTicketCrmConAgenteAsync(descrizioniSimili, cancellationToken);
            _logger.LogInformation(
                "AiAgentCrmService: {Count} segnalazioni arricchite con CRM per {CodiceEnte}",
                risultato.Count, codiceEnte);
            return new RisultatoSegnalazioniSimili(risultato.Count > 0, risultato);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiAgentCrmService: errore ricerca CRM per {CodiceEnte} — fallback graceful", codiceEnte);
            return new RisultatoSegnalazioniSimili(false, []);
        }
    }

    /// <summary>
    /// Invoca l'agente AI (Semantic Kernel + tool calling) per ricercare i ticket CRM
    /// corrispondenti a ciascuna descrizione fornita, entro il limite configurato in <see cref="AiAgentCrmSettings.MaxEmailSegnalazioniSimili"/>.
    /// Al termine deduplicà i risultati raggruppando per soluzione CRM.
    /// </summary>
    /// <param name="descrizioni">Elenco di descrizioni testuali da elaborare.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>Lista di <see cref="SegnalazioneSimileInfo"/> deduplicata per soluzione CRM.</returns>
    private async Task<List<SegnalazioneSimileInfo>> RicercaTicketCrmConAgenteAsync(
        IReadOnlyList<string> descrizioni,
        CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.AddFromObject(
            new CrmTicketPlugin(crmProxyService, loggerFactory.CreateLogger<CrmTicketPlugin>()),
            "CrmTicketPlugin");

        var chatService = agentKernel.GetRequiredService<IChatCompletionService>();
        var risultato = new List<SegnalazioneSimileInfo>();

        foreach (var descrizione in descrizioni.Take(_settings.MaxEmailSegnalazioniSimili))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? ticketId = null;
            string? soluzione = null;

            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(_settings.SystemPromptCercaTicket);
                chatHistory.AddUserMessage($"Descrizione del problema: {descrizione}");

#pragma warning disable SKEXP0001
                var execSettings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    MaxTokens = 500
                };
#pragma warning restore SKEXP0001

                var risposta = await chatService.GetChatMessageContentAsync(
                    chatHistory, execSettings, agentKernel, cancellationToken);

                (ticketId, soluzione) = EstraiTicketESoluzione(risposta.Content ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AiAgentCrmService: errore per una descrizione — includo senza ticket");
            }

            risultato.Add(new SegnalazioneSimileInfo(descrizione, ticketId, soluzione));
        }

        // Deduplicazione per soluzione
        return risultato
            .GroupBy(s => string.IsNullOrWhiteSpace(s.SoluzioneCrm)
                ? "__no_solution__"
                : s.SoluzioneCrm.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Analizza il testo libero restituito dall'agente AI ed estrae il numero di ticket CRM e la soluzione.
    /// Si aspetta righe nel formato "Ticket XXXXX: ..." e "Soluzione: ...".
    /// </summary>
    /// <param name="testo">Testo grezzo prodotto dall'agente AI.</param>
    /// <returns>Tupla con <c>ticketId</c> e <c>soluzione</c>; entrambi <c>null</c> se non trovati o il testo è vuoto.</returns>
    private static (string? ticketId, string? soluzione) EstraiTicketESoluzione(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return (null, null);

        string? ticketId = null;
        string? soluzione = null;

        foreach (var riga in testo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var r = riga.Trim();
            if (ticketId == null && r.StartsWith("Ticket ", StringComparison.OrdinalIgnoreCase))
            {
                var idx = r.IndexOf(':');
                if (idx > 7) ticketId = r[7..idx].Trim();
            }
            if (soluzione == null && r.StartsWith("Soluzione:", StringComparison.OrdinalIgnoreCase))
            {
                var s = r[10..].Trim();
                if (!string.IsNullOrWhiteSpace(s)) soluzione = s;
            }
        }

        return (ticketId, soluzione);
    }
}
