using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.CheckerServices
{
    public class AuthorReviewService : IAuthorReviewService
    {
        private readonly VssConnection _connection;
        private readonly ILogger<AuthorReviewService> _logger;
        private readonly DevOpsServerConfiguration _config;

        public AuthorReviewService(IOptions<DevOpsServerConfiguration> config, ILogger<AuthorReviewService> logger)
        {
            _config = config.Value;
            VssCredentials creds = new VssBasicCredential(string.Empty, _config.AccessToken);
            _connection = new VssConnection(new Uri($"{_config.URL}/{_config.Collection}"), creds);
            _logger = logger;
            _logger.LogInformation($"[{nameof(AuthorReviewService)}] CREATED.");
        }

        public async Task AuthorReviewCheck(string projectId, string repoId, int pullRequestId)
        {
            try
            {
                _logger.LogInformation($"[{nameof(AuthorReviewCheck)}] START {{pullRequestId:{pullRequestId}}}");
                GitHttpClient gitClient = _connection.GetClient<GitHttpClient>();
                var resState = GitStatusState.NotApplicable;
                string description;

                var pr = await gitClient.GetPullRequestByIdAsync(projectId, pullRequestId);

                if (pr == null)
                {
                    _logger.LogWarning($"[{nameof(AuthorReviewCheck)}] GetPullRequestByIdAsync: PullRequest not found");
                    return;
                }

                var author = pr.CreatedBy.UniqueName;
                _logger.LogInformation($"[{nameof(AuthorReviewCheck)}] GetPullRequestByIdAsync ok! {{author:{author}}}");

                var reviewer = pr.Reviewers.FirstOrDefault(v => v.UniqueName == author);

                switch (reviewer?.Vote)
                {
                    case 10: // Approved
                    case 5:  // Approved with suggestions
                        {
                            resState = GitStatusState.Succeeded;
                            description = "Готово для ревью";
                            break;
                        }
                    case 0:  // No vote
                        {
                            resState = GitStatusState.NotSet;
                            description = "Ожидает подтверждения автора";
                            break;
                        }
                    default:
                        {
                            resState = GitStatusState.NotSet;
                            description = "Готовность к ревью не подтверждена";
                            break;
                        }
                }

                // New status
                var status = new GitPullRequestStatus()
                {
                    State = resState,
                    Description = description,
                    Context = new GitStatusContext()
                    {
                        Name = "CheckAuthorReview-checker",
                        Genre = "continuous-integration"
                    }
                };
                // set PR status
                await gitClient.CreatePullRequestStatusAsync(status, repoId, pullRequestId);
                _logger.LogInformation($"[{nameof(AuthorReviewCheck)}] CreatePullRequestStatusAsync ok! " +
                    $"{{pullRequestId:{pullRequestId}," +
                    $"author:{author}," +
                    $"status:{{" +
                        $"state:{status.State}," +
                        $"description:{status.Description},context:{{name:{status.Context.Name},genre:{status.Context.Genre}}}" +
                        $"}}" +
                    $"}}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Не удалось выполнить проверку: {e.ToString()}");
            }
            finally
            {
                _logger.LogInformation($"[{nameof(AuthorReviewCheck)}] COMPLETED");
            }
        }
    }
}
