﻿using Aggregates.Contracts;
using NServiceBus;
using NServiceBus.MessageInterfaces;
using NServiceBus.Pipeline.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Internal
{
    public class HandleContext : IHandleContext
    {
        public IEventDescriptor EventDescriptor { get; set; }
        public IIncomingContextAccessor Context { get; set; }
        public IBus Bus { get; set; }
        public IMessageMapper Mapper { get; set; }
    }
}
