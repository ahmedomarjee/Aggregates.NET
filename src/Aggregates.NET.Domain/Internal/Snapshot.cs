﻿using Aggregates.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aggregates;

namespace Aggregates.Internal
{
    public class Snapshot<TId> : ISnapshot<TId>
    {
        public TId Id { get; set; }
        public Int32 Version { get; set; }
        public Object Payload { get; set; }
    }
}