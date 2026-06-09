using Sharp4AI.Demo.Api.DTO;

namespace Sharp4AI.Demo.Api.Interfaces;

public interface ITextAnalysisService
{
    Task<SentimentAnalysisResultDTO> AnalyzeSentimentAsync(List<string> documents);
}
