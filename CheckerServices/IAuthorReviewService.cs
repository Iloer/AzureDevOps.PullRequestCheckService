using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.CheckerServices
{
    public interface IAuthorReviewService
    {
        public Task AuthorReviewCheck(string projectId, string repoId, int pullRequestId);
    }
}
