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
        private readonly ICodeCoverageService _codeCoverageService;
        public PullRequestsController(IConfiguration configuration, 
                                      ILogger<PullRequestsController> logger,
                                      IAuthorReviewService authorReviewService,
                                      ICodeCoverageService codeCoverageService)
        {
            _config = configuration;
            _logger = logger;
            _authorReviewService = authorReviewService;
            _codeCoverageService = codeCoverageService;
        }

        [HttpPost("CheckCodeCoverage")]
        public ActionResult<string> CheckCodeCoverage([FromBody] GitPullRequestUpdatedPayload data)
        {
            var repoId = data.Resource.Repository.Id;
            var pullRequestId = data.Resource.PullRequestId;
            var projectId = data.Resource.Repository.Project.Id;

            if (data.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                //TODO: "Забытые" таски не лучшее решение, но ресиверу хука незачем ожидать ее завершения.
                //в ддальнейшем переделать на складывание ресивером событий в очередь, а чекерам работать по очереди.
                Task.Run(() => _codeCoverageService.Check(projectId, repoId, pullRequestId));
            }
            else
            {
                _logger.LogInformation($"PullRequest-{pullRequestId} is inactive. Check skipped.");
            }

            return Ok("success");
        }

        [HttpPost("CheckAuthorReview")]
        public ActionResult<string> CheckAuthorReview([FromBody] GitPullRequestUpdatedPayload data)
        {

            var repoId = data.Resource.Repository.Id;
            var pullRequestId = data.Resource.PullRequestId;
            var projectId = data.Resource.Repository.Project.Id;

            if (data.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                //TODO: "Забытые" таски не лучшее решение, но ресиверу хука незачем ожидать ее завершения.
                //в ддальнейшем переделать на складывание ресивером событий в очередь, а чекерам работать по очереди.
                Task.Run(() => _authorReviewService.Check(projectId, repoId, pullRequestId));
            }
            else
            {
                _logger.LogInformation($"PullRequest-{pullRequestId} is inactive. Check skipped.");
            }

            return Ok("success");
        }
    }
}