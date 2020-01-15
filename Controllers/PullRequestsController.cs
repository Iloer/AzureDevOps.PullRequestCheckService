using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.WebHooks.Payloads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AzureDevOps.PullRequestCheckService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PullRequestsController : ControllerBase
    {
        private IConfiguration _config;
        public PullRequestsController(IConfiguration configuration)
        {
            _config = configuration;
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
                Checkers.CodeCoverage.CheckPrCodeCoverageForBuild(collectionUri, pat, projectId, repoId, pullRequestId);

            return Ok("success");
        }

        [HttpPost("CheckAuthorReview")]
        public async Task<ActionResult<string>> CheckAuthorReview([FromBody] GitPullRequestUpdatedPayload data)
        {
            string pat = _config.GetSection("DevOpsServer").GetValue<string>("AccessToken");
            string collectionUri = $"{_config.GetSection("DevOpsServer").GetValue<string>("url")}/{_config.GetSection("DevOpsServer").GetValue<string>("Collection")}";
            var repoId = data.Resource.Repository.Id;
            var pullRequestId = data.Resource.PullRequestId;
            var projectId = data.Resource.Repository.Project.Id;

            if (data.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                Checkers.AuthorReview.CheckAuthorReview(collectionUri, pat, projectId, repoId, pullRequestId);

            return Ok("success");
        }
    }
}