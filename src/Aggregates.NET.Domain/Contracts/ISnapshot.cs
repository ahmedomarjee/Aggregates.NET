﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface ISnapshot<TId>
    {
        TId Id { get; }

        Int32 Version { get; }

        Object Payload { get; }
    }
}