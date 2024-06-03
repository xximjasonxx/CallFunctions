using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CallFunctions.Models
{
    public record SendSpeedRequestModel
    {
        public string CallConnectionId { get; init; }

        public string Text { get; init; }
    }
}