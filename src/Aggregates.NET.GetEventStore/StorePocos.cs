﻿using Aggregates.Contracts;
using Aggregates.Extensions;
using Aggregates.Internal;
using EventStore.ClientAPI;
using Metrics;
using Newtonsoft.Json;
using NServiceBus.Logging;
using NServiceBus.MessageInterfaces;
using NServiceBus.ObjectBuilder;
using NServiceBus.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates
{
    public class StorePocos : IStorePocos
    {
        private static Meter _hitMeter = Metric.Meter("Stream Cache Hits", Unit.Events);
        private static Meter _missMeter = Metric.Meter("Stream Cache Misses", Unit.Events);

        private static readonly ILog Logger = LogManager.GetLogger(typeof(StoreEvents));
        private readonly IEventStoreConnection _client;
        private readonly ReadOnlySettings _nsbSettings;
        private readonly IStreamCache _cache;
        private readonly Boolean _shouldCache;
        private readonly JsonSerializerSettings _settings;

        public IBuilder Builder { get; set; }

        public StorePocos(IEventStoreConnection client, ReadOnlySettings nsbSettings, IStreamCache cache, JsonSerializerSettings settings)
        {
            _client = client;
            _nsbSettings = nsbSettings;
            _settings = settings;
            _cache = cache;
            _shouldCache = _nsbSettings.Get<Boolean>("ShouldCacheEntities");
        }



        public async Task<T> Get<T>(String bucket, String stream) where T : class
        {
            Logger.Write(LogLevel.Debug, () => $"Getting stream [{stream}] in bucket [{bucket}]");

            var streamId = String.Format("{0}.POCO.{1}", bucket, stream);

            if (_shouldCache)
            {
                var cached = _cache.Retreive(streamId) as T;
                if (cached != null)
                {
                    _hitMeter.Mark();
                    Logger.Write(LogLevel.Debug, () => $"Found stream [{stream}] bucket [{bucket}] in cache");
                    // An easy way to make a deep copy
                    return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(cached));
                }
                _missMeter.Mark();
            }
            
            var read = await _client.ReadEventAsync(streamId, StreamPosition.End, false);
            if (read.Status != EventReadStatus.Success || !read.Event.HasValue)
                return null;

            var @event = read.Event.Value.Event;

            var descriptor = @event.Metadata.Deserialize(_settings);
            var data = @event.Data.Deserialize<T>(_settings);

            if (_shouldCache)
                _cache.Cache(streamId, data);
            return data;
        }
        public async Task Write<T>(T poco, String bucket, String stream, IDictionary<String, String> commitHeaders)
        {
            Logger.Write(LogLevel.Debug, () => $"Writing poco to stream id [{stream}] in bucket [{bucket}]");
            var streamId = String.Format("{0}.POCO.{1}", bucket, stream);

            if (_shouldCache)
                _cache.Evict(streamId);

            var descriptor = new EventDescriptor
            {
                EntityType = typeof(T).FullName,
                Timestamp = DateTime.UtcNow,
                Version = -1,
                Headers = commitHeaders
            };
            var translatedEvent = new EventData(
                    Guid.NewGuid(),
                    typeof(T).FullName,
                    true,
                    poco.Serialize(_settings).AsByteArray(),
                    descriptor.Serialize(_settings).AsByteArray()
                    );

            await _client.AppendToStreamAsync(streamId, ExpectedVersion.Any, translatedEvent);
        }
    }
}
