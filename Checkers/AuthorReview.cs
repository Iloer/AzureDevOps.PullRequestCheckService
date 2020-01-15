using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.Checkers
{
    public static class AuthorReview
    {
        public static async void CheckAuthorReview(string collectionUri, string pat, string projectId, string repoId, int pullRequestId)
        {
            VssCredentials creds = new VssBasicCredential(string.Empty, pat);
            using (VssConnection connection = new VssConnection(new Uri(collectionUri), creds))
            {
                GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
                var resState = GitStatusState.NotApplicable;
                string description;

                var pr = await gitClient.GetPullRequestByIdAsync(projectId, pullRequestId);

                var author = pr.CreatedBy.UniqueName;

                var reviewer = pr.Reviewers.FirstOrDefault(v => v.UniqueName == author);

                switch (reviewer.Vote)
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
                gitClient.CreatePullRequestStatusAsync(status, repoId, pullRequestId);
            }
        }
    }
}
