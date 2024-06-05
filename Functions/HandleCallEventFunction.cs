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
        private readonly CallAutomationClient _callAutomationClient;
        private readonly IConfiguration _configuration;

        public HandleCallEventFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<HandleCallEventFunction>();
            _callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);
            _configuration = configuration;
        }

        [Function("HandleCallEventFunction")]
        public async Task<HttpResponseData> HandleEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "handle/event")] HttpRequestData request)
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var cloudEvents = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true).ToList();
            foreach (var cloudEvent in cloudEvents)
            {
                var parsedEvent = CallAutomationEventParser.Parse(cloudEvent);
                _logger.LogInformation($"Received event of type {cloudEvent.Type} with call connection id {parsedEvent.CallConnectionId}");

                var callConnection = _callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId);
                var callMedia = callConnection.GetCallMedia();

                if (parsedEvent is CallConnected callConnected)
                {
                    var playSource = new TextSource($"You are connected - Id: {callConnected.CallConnectionId}")
                    {
                        SourceLocale = "en-US",
                        CustomVoiceEndpointId = _configuration["CustomVoiceEndpointId"],
                        VoiceName = _configuration["NeuralVoiceName"]
                    };


                    await callMedia.PlayToAllAsync(playSource);
                }
            }

            return request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
