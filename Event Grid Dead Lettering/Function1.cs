using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace EventGridDeadLettering
{
    public static class Function1
    {
        private static readonly HttpClient _client = new HttpClient();

        [FunctionName("Function1")]
        public static IActionResult Run([HttpTrigger]dynamic req, TraceWriter log)
        {
            log.Info(req.ToString());

            var first = req[0];
            if (first.eventType == @"Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                return new OkObjectResult(new
                {
                    validationResponse = first.data.validationCode
                });
            }

            return new OkResult();
        }
    }
}
