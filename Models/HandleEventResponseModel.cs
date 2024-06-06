
using Azure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CallFunctions.Models
{
    public class HandleEventResponseModel
    {
        [HttpResult]
        public HttpResponseData Result { get; set; }

        [EventGridOutput(TopicEndpointUri = "EventGridTopicUri", TopicKeySetting = "EventGridTopicKey")]
        public CloudEvent Event { get; set; }
    }
}