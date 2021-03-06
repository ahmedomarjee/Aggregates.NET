﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Logging;
using System.Threading;

namespace Aggregates
{
    public static class Defaults
    {
        public static Guid Domain = Guid.NewGuid();
        public static String Bucket = "default";
        public static String MessageIdHeader = "Originating.NServiceBus.MessageId";
        public static String CommitIdHeader = "CommitId";
        public static String DomainHeader = "Domain";
        
        public static AsyncLocal<LogLevel?> MinimumLogging = new AsyncLocal<LogLevel?>();

        // Header information to take from incoming messages
        public static IList<String> CarryOverHeaders = new List<String> {
                                                                          "NServiceBus.MessageId",
                                                                          "NServiceBus.CorrelationId",
                                                                          "NServiceBus.Version",
                                                                          "NServiceBus.TimeSent",
                                                                          "NServiceBus.ConversationId",
                                                                          "CorrId",
                                                                          "NServiceBus.OriginatingMachine",
                                                                          "NServiceBus.OriginatingEndpoint"
                                                                      };
    }
}
