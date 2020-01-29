using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.CheckerServices
{
    public class DevOpsServerConfiguration
    {
        public string URL { get; set; }
        public string Collection { get; set; }
        public string AccessToken { get; set; }
    }
}
