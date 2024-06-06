using System.Net;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CallFunctions.Models;
using Microsoft.Azure.Cosmos.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class ReceiveCallFunction
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly CallAutomationClient _callAutomationClient;

        public ReceiveCallFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<ReceiveCallFunction>();
            _configuration = configuration;

            _callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);;
        }

        [Function("ReceiveCallOptionsEventHandler")]
        public HttpResponseData ReceiveCallOptions([HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "receive/call")] HttpRequestData request)
        {
            _logger.LogInformation("Received OPTIONS request");
            var response = request.CreateResponse(HttpStatusCode.OK);
            string requestOriginAllowed = request.Headers.First(x => x.Key.ToLower() == "webhook-request-origin").Value.First();
        
            response.Headers.Add("Webhook-Allowed-Origin", requestOriginAllowed);
            return response;
        }

        [Function("ReceiveCallFunction")]
        public async Task<HttpResponseData> ReceiveCall([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "receive/call")] HttpRequestData request)
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var cloudEvent = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true)
                .FirstOrDefault(x => x.Type == SystemEventNames.AcsIncomingCall);

            if (cloudEvent != null)
            {
                var callData = cloudEvent.Data.ToObjectFromJson<AcsIncomingCallEventData>();
                var answerOptions = new AnswerCallOptions(callData.IncomingCallContext, new Uri($"{_configuration["CallbackBaseUrl"]}/api/handle/event"))
                {
                    CallIntelligenceOptions = new CallIntelligenceOptions()
                    {
                        CognitiveServicesEndpoint = new Uri(_configuration["CognitiveServicesEndpoint"])
                    }
                };

                await _callAutomationClient.AnswerCallAsync(answerOptions);
                return request.CreateResponse(HttpStatusCode.OK);
            }

            return request.CreateResponse(HttpStatusCode.NoContent);
        }
    }
}
