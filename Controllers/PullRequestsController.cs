using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureDevOps.PullRequestCheckService.CheckerServices;
using Microsoft.AspNet.WebHooks.Payloads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureDevOps.PullRequestCheckService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PullRequestsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly IAuthorReviewService _authorReviewService;
        public PullRequestsController(IConfiguration configuration, ILogger<PullRequestsController> logger, IAuthorReviewService authorReviewService)
        {
            _config = configuration;
            _logger = logger;
            _authorReviewService = authorReviewService;
        }

        [HttpPost("CheckCodeCoverage")]
        public async Task<ActionResult<string>> CheckCodeCoverage([FromBody] GitPullRequestUpdatedPayload data)
        {
            string pat = _config.GetSection("DevOpsServer").GetValue<string>("AccessToken");
            string collectionUri = $"{_config.GetSection("DevOpsServer").GetValue<string>("url")}/{_config.GetSection("DevOpsServer").GetValue<string>("Collection")}";
            var repoId = data.Resource.Repository.Id;
            var pullRequestId = data.Resource.PullRequestId;
            var projectId = data.Resource.Repository.Project.Id;

            if (data.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                CheckerServices.CodeCoverage.CheckPrCodeCoverageForBuild(collectionUri, pat, projectId, repoId, pullRequestId);

            return Ok("success");
        }

        [HttpPost("CheckAuthorReview")]
        public async Task<ActionResult<string>> CheckAuthorReview([FromBody] GitPullRequestUpdatedPayload data)
        {
            
            var repoId = data.Resource.Repository.Id;
            var pullRequestId = data.Resource.PullRequestId;
            var projectId = data.Resource.Repository.Project.Id;

            if (data.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                Task.Run(() => _authorReviewService.AuthorReviewCheck(projectId, repoId, pullRequestId));
            }
            else
            {
                _logger.LogInformation($"PullRequest-{pullRequestId} is inactive. Check skipped.");
            }

            return Ok("success");
        }
    }
}