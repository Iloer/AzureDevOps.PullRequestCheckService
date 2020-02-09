using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevOps.PullRequestCheckService.Controllers
{
    public static class helpers
    {
        public static Dictionary<String, String> toDictionary(this IQueryCollection query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (var item in query)
            {
                result.Add(item.Key, item.Value);
            }
            return result;
        }
    }
}
