﻿using Aggregates.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Internal
{
    public class DefaultOOBPublisher : IOOBPublisher
    {
        private readonly IStoreEvents _store;

        public DefaultOOBPublisher(IStoreEvents store)
        {
            _store = store;
        }


        public async Task Publish<T>(String Bucket, String StreamId, IEnumerable<IWritableEvent> Events, IDictionary<String, String> commitHeaders) where T : class, IEventSource
        {
            await _store.AppendEvents<T>(Bucket + ".OOB", StreamId, Events, commitHeaders);
        }
    }
}
