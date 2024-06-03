using System.Net;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class ReceiveCallTestFunction
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public ReceiveCallTestFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<ReceiveCallTestFunction>();
            _configuration = configuration;
        }

        [Function("ReceiveCallOptionsEventHandler")]
        public HttpResponseData CallReceivedOptions([HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "receive/call")] HttpRequestData request)
        {
            _logger.LogInformation("Received OPTIONS request");
            var response = request.CreateResponse(HttpStatusCode.OK);
            string requestOriginAllowed = request.Headers.First(x => x.Key.ToLower() == "webhook-request-origin").Value.First();
        
            response.Headers.Add("Webhook-Allowed-Origin", requestOriginAllowed);
            return response;
        }

        [Function("ReceiveCallTestFunction")]
        public async Task<HttpResponseData> ReceiveCall([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "receive/call")] HttpRequestData request)
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var cloudEvents = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true).ToList();
            foreach (var cloudEvent in cloudEvents)
            {
                var callAutomationClient = new CallAutomationClient(_configuration["AcsConnectionString"]);
                if (cloudEvent.Type == SystemEventNames.AcsIncomingCall)
                {
                    _logger.LogInformation("Received AcsIncomingCall event");
                    var callData = cloudEvent.Data.ToObjectFromJson<AcsIncomingCallEventData>();
                    var answerOptions = new AnswerCallOptions(callData.IncomingCallContext, new Uri($"{_configuration["CallbackBaseUrl"]}/api/handle/event"))
                    {
                        CallIntelligenceOptions = new CallIntelligenceOptions()
                        {
                            CognitiveServicesEndpoint = new Uri(_configuration["CognitiveServicesEndpoint"])
                        }
                    };

                    await callAutomationClient.AnswerCallAsync(answerOptions);
                }

                _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
            }

            return request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
