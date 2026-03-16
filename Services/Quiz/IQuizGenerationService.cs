using Web_Project.Models;

namespace Web_Project.Services.Quiz
{
    public interface IQuizGenerationService
    {
        Task<GenerateQuizResponse> GenerateQuizAsync(
            GenerateQuizRequest request,
            int? userId,
            string requestIp,
            string userAgent,
            CancellationToken cancellationToken);

        Task<SubmitQuizResponse> SubmitQuizAsync(
            SubmitQuizRequest request,
            int? userId,
            CancellationToken cancellationToken);

        Task<GenerateQuizResponse?> GetQuizAsync(
            int quizId,
            int? userId,
            CancellationToken cancellationToken);
    }
}
