using System.Net;
using Azure.Communication.CallAutomation;
using CallFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class SendSpeechTestFunction
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public SendSpeechTestFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<SendSpeechTestFunction>();
            _configuration = configuration;
        }

        [Function("SendSpeechTestFunction")]
        public async Task<HttpResponseData> SendSpeech([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "send/text")] HttpRequestData request)
        {
            var model = await request.ReadFromJsonAsync<SendSpeedRequestModel>();
            var callAutomationClient = new CallAutomationClient(_configuration["AcsConnectionString"]);
            var callConnection = callAutomationClient.GetCallConnection(model.CallConnectionId);
            var callMedia = callConnection.GetCallMedia();

            var playSource = new TextSource(model.Text)
            {
                SourceLocale = "en-US",
                CustomVoiceEndpointId = _configuration["CustomVoiceEndpointId"],
                VoiceName = _configuration["NeuralVoiceName"]
            };

            await callMedia.PlayToAllAsync(playSource);
            return request.CreateResponse(HttpStatusCode.Accepted);
        }
    }
}
