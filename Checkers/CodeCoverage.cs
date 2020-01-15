using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.Checkers
{
    public static class CodeCoverage
    {
        public static async void CheckPrCodeCoverageForBuild(string collectionUri, string pat, string projectId, string repoId, int pullRequestId)
        {
            const string evaluationConfigurationType = "Build";
            string artifactId(string projectId, int pullRequestId) => $"vstfs:///CodeReview/CodeReviewId/{projectId}/{pullRequestId}";
            
            VssCredentials creds = new VssBasicCredential(string.Empty, pat);
            using (VssConnection connection = new VssConnection(new Uri(collectionUri), creds))
            {
                GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
                PolicyHttpClient policyClient = connection.GetClient<PolicyHttpClient>();

                // получить политики для ПР
                var evaluations = await policyClient.GetPolicyEvaluationsAsync(projectId, artifactId(projectId, pullRequestId));
                var evaluation = evaluations.FirstOrDefault(x => x.Configuration.Type.DisplayName.Equals(evaluationConfigurationType, StringComparison.OrdinalIgnoreCase));

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
                            targetUrl = buildId != null ? $"{collectionUri}/{projectId}/_build/results?buildId={buildId}&view=results" : null;
                            break;
                        }
                    case PolicyEvaluationStatus.Approved:
                        {
                            var buildId = evaluation.Context.Value<int>("buildId");
                            var cover = await GetCodeCoverageForBuild(collectionUri, pat, buildId);
                            resState = GitStatusState.Succeeded;
                            description = cover != null ? $"CodeCoverage = {cover:F2}%" : "CodeCoverage = (не определен)";
                            targetUrl = $"{ collectionUri}/{ projectId}/ _build/results?buildId={buildId}&view=results";
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
                        Name = "CheckCodeCoverageService-checker",
                        Genre = "continuous-integration"
                    }
                };
                // set PR status
                gitClient.CreatePullRequestStatusAsync(status, repoId, pullRequestId);
            }
        }
        private static async Task<double?> GetCodeCoverageForBuild(string collectionUri, string pat, int buildId)
        {
            const string CoverageStatsLabel = "Lines";
            VssCredentials creds = new VssBasicCredential(string.Empty, pat);
            using (VssConnection connection = new VssConnection(new Uri(collectionUri), creds))
            {
                TestManagementHttpClient testClient = connection.GetClient<TestManagementHttpClient>();
                //Получить покрытие кода по id билда
                var codeCoverage = await testClient.GetCodeCoverageSummaryAsync("Asteros.Contact", buildId);

                CodeCoverageStatistics CoverageStats = null;
                if (codeCoverage.CoverageData.Count > 0)
                    // TODO: Переделать на случай если будет несколько CoverageData
                    CoverageStats = codeCoverage?.CoverageData[0].CoverageStats
                        .FirstOrDefault(x => x.Label.Equals(CoverageStatsLabel, StringComparison.OrdinalIgnoreCase));

                double? cover = null;
                if (CoverageStats != null)
                    cover = (CoverageStats.Covered * 100) / CoverageStats.Total;

                return cover;
            }
        }
    }
}
