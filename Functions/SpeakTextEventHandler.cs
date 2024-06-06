using System.Net;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class SpeakTextEventHandler
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly CallAutomationClient _callAutomationClient;

        public SpeakTextEventHandler(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<SpeakTextEventHandler>();
            _configuration = configuration;

            _callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);
        }

        [Function("SpeakTextEventOptionsHandler")]
        public HttpResponseData SpeakTextHandlerOptions([HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "handle/speak")] HttpRequestData request)
        {
            _logger.LogInformation("Received OPTIONS request");
            var response = request.CreateResponse(HttpStatusCode.OK);
            string requestOriginAllowed = request.Headers.First(x => x.Key.ToLower() == "webhook-request-origin").Value.First();
        
            response.Headers.Add("Webhook-Allowed-Origin", requestOriginAllowed);
            return response;
        }

        [Function("SpeakTextEventHandler")]
        public async Task<HttpResponseData> HandleSpeakText([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "handle/speak")] HttpRequestData request)
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var cloudEvent = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true)
                .FirstOrDefault(x => x.Type == "CallEvent.SpeakText");
            var eventData = cloudEvent.Data.ToObjectFromJson<SpeakTextEvent>();

            var callConnection = _callAutomationClient.GetCallConnection(eventData.CallConnectionId);
            var callMedia = callConnection.GetCallMedia();

            var playSource = new TextSource(eventData.Text)
            {
                SourceLocale = "en-US",
                CustomVoiceEndpointId = _configuration["CustomVoiceEndpointId"],
                VoiceName = _configuration["NeuralVoiceName"]
            };


            await callMedia.PlayToAllAsync(playSource);
            return request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
