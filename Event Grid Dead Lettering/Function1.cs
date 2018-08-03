using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventGridDeadLettering
{
    public static class Function1
    {
        private static readonly HttpClient _client = new HttpClient();

        private static readonly Stopwatch _timer = new Stopwatch();

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

            if (_timer.IsRunning)
            {
                log.Info($@"Elapsed time: {_timer.Elapsed}");
            }
            else
            {
                _timer.Start();
                log.Info(@"Timer started.");
            }

            throw new System.Exception(@"Exception to crash function & force dead lettering");
        }

        [FunctionName(@"ResetTimer")]
        public static IActionResult ResetTimer([HttpTrigger]string req, TraceWriter log)
        {
            log.Verbose($@"Resetting timer... ({_timer.Elapsed})");
            ResetTimer(log);

            return new OkResult();
        }

        [FunctionName(@"DeadLettered")]
        public static async System.Threading.Tasks.Task DeadLetterReactionAsync([BlobTrigger(@"%DeadLetterContainerName%", Connection = @"DeadLetterBlobStorageConnectionString")]ICloudBlob blob, TraceWriter log)
        {
            log.Info($@"Blob {blob.Name} was dead-lettered by Event Grid! Content:{JToken.ReadFrom(new JsonTextReader(new StreamReader(await blob.OpenReadAsync(null, null, new Microsoft.WindowsAzure.Storage.OperationContext())))).ToString(Formatting.Indented)}");

            log.Info($@"Time to dead lettering: {_timer.Elapsed}");
            ResetTimer(log);
        }

        private static void ResetTimer(TraceWriter log)
        {
            _timer.Reset();
            log.Info(@"Timer reset.");
        }
    }
}
