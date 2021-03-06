﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.CheckerServices
{
    public class CodeCoverageService : ICodeCoverageService
    {
        private readonly VssConnection _connection;
        private readonly ILogger<CodeCoverageService> _logger;
        private readonly DevOpsServerConfiguration _config;

        public CodeCoverageService(ILogger<CodeCoverageService> logger, IOptions<DevOpsServerConfiguration> config)
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
            _logger.LogInformation($"[{nameof(CodeCoverageService)}] CREATED.");
        }

        public async Task Check(string projectId, string repoId, int pullRequestId, Dictionary<string, string> args = null)
        {
            try
            {
                const string evaluationConfigurationType = "Build";
                string artifactId(string projectId, int pullRequestId) => $"vstfs:///CodeReview/CodeReviewId/{projectId}/{pullRequestId}";
                string buildUrl = $"{_config.URL}/{_config.Collection}/{projectId}/_build";
                string statusName = (args!=null) && args.TryGetValue("name", out string name) ? name : "CheckCodeCoverage";

                _logger.LogInformation($"[{nameof(Check)}] BEGIN {{pullRequestId:{pullRequestId}}}");

                GitHttpClient gitClient = _connection.GetClient<GitHttpClient>();
                PolicyHttpClient policyClient = _connection.GetClient<PolicyHttpClient>();

                // получить политики для ПР
                var evaluations = await policyClient.GetPolicyEvaluationsAsync(projectId, artifactId(projectId, pullRequestId));
                _logger.LogInformation($"[{nameof(Check)}] GetPolicyEvaluationsAsync(project:{projectId}, artifactId:{artifactId(projectId, pullRequestId)}) success: {{evaluations count:{evaluations.Count}}}");

                var policy = evaluations.FirstOrDefault(x => x.Configuration.Settings.TryGetValue("statusName", out JToken name) 
                                                        && name.ToString().Equals(statusName, StringComparison.OrdinalIgnoreCase));

                // Если у полученного PullRequest не найдена политика для правила с заданным именем, то не создаем статуса.
                if (policy == null)
                {
                    _logger.LogInformation($"[{nameof(Check)}] SKIPPED. Pullrequest({pullRequestId}) has no this policy");
                    return;
                }

                var evaluation = evaluations.FirstOrDefault(x => x.Configuration.Type.DisplayName.Equals(evaluationConfigurationType, StringComparison.OrdinalIgnoreCase));
                _logger.LogInformation($"[{nameof(Check)}] build evaluation: {JsonConvert.SerializeObject(evaluation)}");

                var resState = GitStatusState.NotApplicable;
                string description;
                string targetUrl;

                switch (evaluation?.Status)
                {
                    case PolicyEvaluationStatus.Running:
                    case PolicyEvaluationStatus.Queued:
                    case PolicyEvaluationStatus.Rejected:
                        {
                            var buildId = evaluation.Context?.Value<int>("buildId");
                            resState = GitStatusState.NotSet;
                            description = "Ожидаение успешной сборки";
                            targetUrl = buildId != null ? $"{buildUrl}/results?buildId={buildId}&view=results" : null;
                            break;
                        }
                    case PolicyEvaluationStatus.Approved:
                        {
                            var buildId = evaluation.Context.Value<int>("buildId");
                            var cover = await GetCodeCoverageForBuild(projectId, buildId);

                            int quality = 0;
                            if ( args != null && args.TryGetValue("quality", out string qualityStr) && (cover != null))
                            {
                                if (!int.TryParse(qualityStr, out quality))
                                {
                                    _logger.LogWarning(message: $"[{nameof(Check)}] param \"Quality\"({qualityStr}) is not parse to int");
                                }
                                resState = (quality <= cover)
                                    ? GitStatusState.Succeeded 
                                    : GitStatusState.Error;
                            }
                            else
                            {
                                resState = GitStatusState.Succeeded;
                            }

                            description = cover != null ? $"CodeCoverage = {cover:F2}%" : "CodeCoverage = (не определен)";
                            targetUrl = $"{buildUrl}/results?buildId={buildId}&view=results";
                            break; 
                        }
                    case PolicyEvaluationStatus.NotApplicable:
                    default:
                        resState = GitStatusState.Succeeded;
                        description = "CodeCoverage = (Build not used)";
                        targetUrl = null;
                        break;
                }
                // New status
                var status = new GitPullRequestStatus()
                {
                    State = resState,
                    Description = description,
                    TargetUrl = targetUrl,
                    Context = new GitStatusContext()
                    {
                        Name = statusName,
                        Genre = "PullRequestCheckService"
                    }
                };
                _logger.LogInformation($"[{nameof(Check)}] created new status: " +
                                       $"{{pullRequestId:{pullRequestId}," +
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
        private async Task<double?> GetCodeCoverageForBuild(String projectId, int buildId)
        {
            _logger.LogInformation($"[{nameof(GetCodeCoverageForBuild)}] BEGIN {{project:{projectId}, buildId:{buildId}}}");
            try
            {
                const string CoverageStatsLabel = "Lines";

                GitHttpClient gitClient = _connection.GetClient<GitHttpClient>();
                TestManagementHttpClient testClient = _connection.GetClient<TestManagementHttpClient>();
                //Получить покрытие кода по id билда
                var codeCoverage = await testClient.GetCodeCoverageSummaryAsync(projectId, buildId);
                _logger.LogInformation($"[{nameof(GetCodeCoverageForBuild)}] GetCodeCoverageSummaryAsync(project:{projectId}, buildId:{buildId}) success!");
                
                CodeCoverageStatistics CoverageStats = null;
                if (codeCoverage.CoverageData.Count > 0)
                    // TODO: Переделать на случай если будет несколько CoverageData
                    CoverageStats = codeCoverage?.CoverageData[0].CoverageStats
                        .FirstOrDefault(x => x.Label.Equals(CoverageStatsLabel, StringComparison.OrdinalIgnoreCase));
                
                return CoverageStats?.Covered * 100.00 / CoverageStats?.Total;
            }
            catch (Exception e)
            {
                _logger.LogError($"[{nameof(GetCodeCoverageForBuild)}] ERROR: {e.ToString()}");
                return null;
            }
            finally
            {
                _logger.LogInformation($"[{nameof(GetCodeCoverageForBuild)}] COMPLETED");
            }
        }
    }
}