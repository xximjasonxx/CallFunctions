using System.Net;
using Azure;
using Azure.AI.OpenAI;
using Azure.Communication;
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
        private readonly IConfiguration _configuration;
        private readonly CallAutomationClient _callAutomationClient;
        
        public HandleCallEventFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<HandleCallEventFunction>();
            _configuration = configuration;

            _callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);
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
                    var callConnection = _callAutomationClient.GetCallConnection(callConnected.CallConnectionId);
                    var callMedia = callConnection.GetCallMedia();
                    var properties = await callConnection.GetCallConnectionPropertiesAsync();

                    var playSource = new TextSource("Hello, I'm Katie. Welcome to Dashing Dish. Go ahead and ask a question and I'll do my best to help you.")
                    {
                        SourceLocale = "en-US",
                        CustomVoiceEndpointId = _configuration["CustomVoiceEndpointId"],
                        VoiceName = _configuration["NeuralVoiceName"]
                    };

                    var recognizeOptions = new CallMediaRecognizeSpeechOptions(properties.Value.Source)
                    {
                        EndSilenceTimeout = TimeSpan.FromSeconds(1),
                        Prompt = playSource
                    };

                    await callMedia.StartRecognizingAsync(recognizeOptions);
                }

                if (parsedEvent is RecognizeCompleted recognizeCompleted)
                {
                    var callConnection = _callAutomationClient.GetCallConnection(recognizeCompleted.CallConnectionId);
                    var callMedia = callConnection.GetCallMedia();
                    var properties = await callConnection.GetCallConnectionPropertiesAsync();

                    var speechResult = (SpeechResult)recognizeCompleted.RecognizeResult;
                    var openAiEndpointBase = _configuration["OpenAiEndpoint"];
                    var openAiApiKey = _configuration["OpenAiApiKey"];
                    var deploymentId = "gpt-40-deployment";

                    var searchEndpoint = _configuration["SearchEndpoint"];
                    var searchApiKey = _configuration["SearchApiKey"];
                    var searchIndexName = _configuration["SearchIndexName"];

                    var client = new OpenAIClient(new Uri(openAiEndpointBase), new AzureKeyCredential(openAiApiKey));
                    var chatCompletionsOptions = new ChatCompletionsOptions()
                    {
                        DeploymentName = deploymentId,
                        Messages =
                        {
                            new ChatRequestSystemMessage("You are a helpful assistant working for the Dashing Dish company. You will summerize any response so as not to exceed 400 characters in length"),
                            new ChatRequestUserMessage(speechResult.Speech)
                        },
                        
                        AzureExtensionsOptions = new AzureChatExtensionsOptions()
                        {
                            Extensions =
                            {
                                new AzureSearchChatExtensionConfiguration
                                {
                                    SearchEndpoint = new Uri(searchEndpoint),
                                    Authentication = new OnYourDataApiKeyAuthenticationOptions(searchApiKey),
                                    IndexName = searchIndexName
                                }
                            }
                        }
                    };

                    var response = await client.GetChatCompletionsAsync(
                        chatCompletionsOptions);
                    
                    var responseContent = response.Value.Choices.First();
                    _logger.LogInformation($"Lenght of response from OpenAI: {responseContent.Message.Content.Length}");

                    var playSource = new TextSource(responseContent.Message.Content)
                    {
                        SourceLocale = "en-US",
                        CustomVoiceEndpointId = _configuration["CustomVoiceEndpointId"],
                        VoiceName = _configuration["NeuralVoiceName"]
                    };

                    await callMedia.PlayAsync(new PlayOptions(playSource, [properties.Value.Source]));
                }
            }

            return new HandleEventResponseModel
            {
                Result = request.CreateResponse(HttpStatusCode.OK)
            };
        }
    }
}
