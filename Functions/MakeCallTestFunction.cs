using System.Net;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallFunctions
{
    public class MakeCallTest
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public MakeCallTest(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<MakeCallTest>();
            _configuration = configuration;
        }

        [Function("MakeCallTest")]
        public async Task<HttpResponseData> MakeCall([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "make/call")] HttpRequestData request)
        {
            var callAutomationClient = new CallAutomationClient(_configuration["AcsConnectionString"]);
            var callInvite = new CallInvite(
                new PhoneNumberIdentifier(_configuration["DestinationPhoneNumber"]),
                new PhoneNumberIdentifier(_configuration["SourcePhoneNumber"])
            );

            var createCallOptions = new CreateCallOptions(callInvite, new Uri($"{_configuration["CallbackBaseUrl"]}/api/handle/event"))
            {
                CallIntelligenceOptions = new CallIntelligenceOptions
                {
                    CognitiveServicesEndpoint = new Uri(_configuration["CognitiveServicesEndpoint"])
                }
            };

            await callAutomationClient.CreateCallAsync(createCallOptions);
            
            return request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
