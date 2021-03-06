﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates
{
    public interface IHandleMessagesAsync<TMessage>
    {
        Task Handle(TMessage message, IHandleContext context);
    }
}
