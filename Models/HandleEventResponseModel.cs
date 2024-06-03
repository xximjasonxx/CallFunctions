using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CallFunctions.Models
{
    public class HandleEventResponseModel
    {
        [HttpResult]
        public HttpResponseData Result { get; set; }

        [CosmosDBOutput("CallTesting", "Calls", Connection = "CosmosDBConnection")]
        public dynamic Document { get; set; }
    }
}