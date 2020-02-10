using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            _logger = logger;
            _config = config?.Value;

            if (_config == null)
                throw new ArgumentNullException("Config is null");
            
            if (string.IsNullOrWhiteSpace(_config.URL))
                throw new ArgumentNullException("DevOpsServerConfiguration.URL is null");
            
            if (string.IsNullOrWhiteSpace(_config.Collection))
                throw new ArgumentNullException("DevOpsServerConfiguration.Collection is null");
            if (string.IsNullOrWhiteSpace(_config.AccessToken))
                throw new ArgumentNullException("DevOpsServerConfiguration.AccessToken is null");

            VssCredentials creds = new VssBasicCredential(string.Empty, _config.AccessToken);
            _connection = new VssConnection(new Uri($"{_config.URL}/{_config.Collection}"), creds);
            _logger.LogInformation($"[{nameof(AuthorReviewService)}] CREATED.");

        }

        public async Task Check(string projectId, string repoId, int pullRequestId, Dictionary<string, string> args = null )
        {
            try
            {
                _logger.LogInformation($"[{nameof(Check)}] START {{pullRequestId:{pullRequestId}}}");
                GitHttpClient gitClient = _connection.GetClient<GitHttpClient>();
                var resState = GitStatusState.NotApplicable;
                string description;

                var pr = await gitClient.GetPullRequestByIdAsync(projectId, pullRequestId);

                if (pr == null)
                {
                    _logger.LogWarning($"[{nameof(Check)}] GetPullRequestByIdAsync: PullRequest not found");
                    return;
                }

                var author = pr.CreatedBy.UniqueName;
                _logger.LogInformation($"[{nameof(Check)}] GetPullRequestByIdAsync(project:{projectId}, pullRequestId:{pullRequestId}) success: {{author:{author}}}");

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
                        Name = "CheckAuthorReview",
                        Genre = "PullRequestCheckService"
                    }
                };
                _logger.LogInformation($"[{nameof(Check)}] created new status: " +
                    $"{{pullRequestId:{pullRequestId}," +
                    $"author:{author}," +
                    $"status:{{" +
                        $"state:{status.State}," +
                        $"description:{status.Description},context:{{name:{status.Context.Name},genre:{status.Context.Genre}}}" +
                        $"}}" +
                    $"}}");
                // set PR status
                var prStatus = await gitClient.CreatePullRequestStatusAsync(status, repoId, pullRequestId);
                _logger.LogInformation($"[{nameof(Check)}] CreatePullRequestStatusAsync(status:{status}, repositoryId:{repoId}, pullRequestId:{pullRequestId}) success: {JsonConvert.SerializeObject(prStatus)}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Check FAILED: {e.ToString()}");
            }
            finally
            {
                _logger.LogInformation($"[{nameof(Check)}] COMPLETED");
            }
        }
    }
}
