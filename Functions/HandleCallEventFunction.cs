using System.Net;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using CallFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class HandleCallEventFunction
    {
        private readonly ILogger _logger;
        
        public HandleCallEventFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HandleCallEventFunction>();
        }

        [Function("HandleCallEventFunction")]
        public async Task<HandleEventResponseModel> HandleEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "handle/event")] HttpRequestData request)
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var cloudEvents = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true).ToList();
            CloudEvent outgoingCloudEvent = null;

            foreach (var cloudEvent in cloudEvents)
            {
                var parsedEvent = CallAutomationEventParser.Parse(cloudEvent);
                _logger.LogInformation($"Received event of type {cloudEvent.Type}");

                if (parsedEvent is CallConnected callConnected)
                {
                    // we are receiving a connection, we need to send our greeting and log the call
                    // into Cosmos for tracking
                    outgoingCloudEvent = new CloudEvent(nameof(HandleCallEventFunction), "CallEvent.SpeakText",
                        new SpeakTextEvent
                        {
                            CallConnectionId = callConnected.CallConnectionId,
                            Text = "Hello, I'm Katie. Welcome to Dashing Dish. Go ahead and ask a question and I'll do my best to help you."
                        });
                }
            }

            return new HandleEventResponseModel
            {
                Result = request.CreateResponse(HttpStatusCode.OK),
                Event = outgoingCloudEvent
            };
        }
    }
}
