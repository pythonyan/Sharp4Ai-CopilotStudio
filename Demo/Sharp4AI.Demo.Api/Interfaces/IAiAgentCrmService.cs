using Sharp4AI.Demo.Api.DTO;

namespace Sharp4AI.Demo.Api.Interfaces;

public interface IAiAgentCrmService
{
    /// <summary>
    /// Cerca nel CRM i ticket correlati alle descrizioni di segnalazioni simili e restituisce
    /// il risultato aggregato con ticket id e soluzione per ciascuna segnalazione.
    /// </summary>
    /// <param name="descrizioniSimili">Elenco di descrizioni testuali di segnalazioni simili.</param>
    /// <param name="codiceEnte">Codice identificativo dell'ente (usato per il logging).</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns><see cref="RisultatoSegnalazioniSimili"/> con l'esito e le segnalazioni arricchite.</returns>
    Task<RisultatoSegnalazioniSimili> TrovaSegnalazioniSimiliCrmAsync(
        IReadOnlyList<string> descrizioniSimili,
        string codiceEnte,
        CancellationToken cancellationToken = default);
}
