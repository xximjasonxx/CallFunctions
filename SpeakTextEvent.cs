using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CallFunctions
{
    public record SpeakTextEvent
    {
        public string CallConnectionId { get; init; }
        public string Text { get; init; }
    }
}