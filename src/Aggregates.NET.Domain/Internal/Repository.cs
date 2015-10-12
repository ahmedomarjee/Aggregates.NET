using Aggregates.Contracts;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.ObjectBuilder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aggregates.Internal
{
    // inspired / taken from NEventStore.CommonDomain
    // https://github.com/NEventStore/NEventStore/blob/master/src/NEventStore/CommonDomain/Persistence/EventStore/EventStoreRepository.cs

    public class Repository<T> : IRepository<T> where T : class, IAggregate
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Repository<>));
        protected readonly IStoreEvents _store;
        protected readonly IBuilder _builder;
        
        private readonly ConcurrentDictionary<String, IEventStream> _streams = new ConcurrentDictionary<String, IEventStream>();
        private Boolean _disposed;

        public Repository(IBuilder builder)
        {
            _builder = builder;
            _store = _builder.Build<IStoreEvents>();
        }

        void IRepository.Commit(Guid commitId, IDictionary<String, Object> headers)
        {
            foreach (var stream in _streams)
                stream.Value.Commit(commitId, headers);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
                return;
            
            _streams.Clear();

            _disposed = true;
        }

        public T Get<TId>(TId id)
        {
            return Get<TId>(Bucket.Default, id);
        }

        public virtual T Get<TId>(String bucket, TId id)
        {
            Logger.DebugFormat("Retreiving aggregate id '{0}' from bucket '{1}' in store", id, bucket);
            
            var stream = OpenStream(bucket, id);

            if (stream == null) return (T)null;
            // Get requires the stream exists
            if (stream.StreamVersion == -1) return (T)null;

            // Call the 'private' constructor
            var root = Newup(stream, _builder);
            (root as IEventSource<TId>).Id = id;
            
            root.Hydrate(stream.Events.Select(e => e.Event));

            return root;
        }

        public T New<TId>(TId id)
        {
            return New<TId>(Bucket.Default, id);
        }

        public T New<TId>(String bucket, TId id)
        {
            var stream = PrepareStream(bucket, id);
            var root = Newup(stream, _builder);
            (root as IEventSource<TId>).Id = id;

            return root;
        }
        
        protected T Newup(IEventStream stream, IBuilder builder)
        {
            // Call the 'private' constructor
            var tCtor = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);

            if (tCtor == null)
                throw new AggregateException("Aggregate needs a PRIVATE parameterless constructor");
            var root = (T)tCtor.Invoke(null);

            // Todo: I bet there is a way to make a INeedBuilding<T> type interface
            //      and loop over each, calling builder.build for each T
            if (root is INeedStream)
                (root as INeedStream).Stream = stream;
            if (root is INeedBuilder)
                (root as INeedBuilder).Builder = builder;
            if (root is INeedSnapshots)
                (root as INeedSnapshots).Snapshots = builder.Build<IPersistSnapshots>();
            if (root is INeedEventFactory)
                (root as INeedEventFactory).EventFactory = builder.Build<IMessageCreator>();
            if (root is INeedRouteResolver)
                (root as INeedRouteResolver).Resolver = builder.Build<IRouteResolver>();
            if (root is INeedRepositoryFactory)
                (root as INeedRepositoryFactory).RepositoryFactory = builder.Build<IRepositoryFactory>();

            return root;
        }


        protected IEventStream OpenStream<TId>(String bucket, TId id, Int32 version = -1)
        {
            IEventStream stream;
            var streamId = String.Format("{1}-{0}-{2}v{3}", typeof(T).FullName, bucket, id, version);
            if (_streams.TryGetValue(streamId, out stream))
                return stream;
            
            _streams[streamId] = stream = _store.GetStream<T>(bucket, id.ToString(), version + 1);
            return stream;
        }

        protected IEventStream PrepareStream<TId>(String bucket, TId id)
        {
            IEventStream stream;
            var streamId = String.Format("{1}-{0}-{2}", typeof(T).FullName, bucket, id);
            if (!_streams.TryGetValue(streamId, out stream))
                _streams[streamId] = stream = _store.GetStream<T>(bucket, id.ToString());

            return stream;
        }
    }
}