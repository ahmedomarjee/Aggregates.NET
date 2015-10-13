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

    public class Repository<T, TId> : IRepository<T> where T : class, IAggregate<TId>
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Repository<,>));
        private readonly IStoreEvents _store;
        private readonly IStoreSnapshots _snapshots;
        private readonly IBuilder _builder;

        private struct AggregateStream
        {
            public T Aggregate { get; set; }
            public IEventStream Stream { get; set; }
        }
        
        private readonly ConcurrentDictionary<String, AggregateStream> _aggregates = new ConcurrentDictionary<String, AggregateStream>();
        private Boolean _disposed;

        public Repository(IBuilder builder)
        {
            _builder = builder;
            _store = _builder.Build<IStoreEvents>();
            _snapshots = _builder.Build<IStoreSnapshots>();
        }

        void IRepository.Commit(Guid commitId, IDictionary<String, Object> headers)
        {
            foreach (var agg in _aggregates.Values)
            {
                agg.Stream.Commit(commitId, headers);

                if (agg.Aggregate is ISnapshotting<TId> && (agg.Aggregate as ISnapshotting<TId>).ShouldTakeSnapshot())
                    _snapshots.Add<T, TId>((agg.Aggregate as ISnapshotting<TId>).TakeSnapshot());
            }
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

            _disposed = true;
        }

        public T Get<TID>(TID id)
        {
            return Get(Bucket.Default, id);
        }

        public T Get<TID>(String bucket, TID id)
        {
            Logger.DebugFormat("Retreiving aggregate id '{0}' from bucket '{1}' in store", id, bucket);

            AggregateStream cached;
            var streamId = String.Format("{1}-{0}-{2}", typeof(T).FullName, bucket, id);
            if (_aggregates.TryGetValue(streamId, out cached))
                return cached.Aggregate;

            var snapshot = _snapshots.Load<T, TID>();
            var stream = OpenStream(bucket, id, snapshot);

            if (stream == null && snapshot == null) return (T)null;
            // Get requires the stream exists
            if (stream.StreamVersion == -1) return (T)null;

            // Call the 'private' constructor
            var root = Newup(stream, _builder);
            (root as IEventSource<TID>).Id = id;

            if (snapshot != null && root is ISnapshotting<TID>)
                ((ISnapshotting<TID>)root).RestoreSnapshot(snapshot);

            root.Hydrate(stream.Events.Select(e => e.Event));

            return root;
        }

        public T New<TID>(TID id)
        {
            return New(Bucket.Default, id);
        }

        public T New<TID>(String bucket, TID id)
        {
            var stream = OpenStream(bucket, id);
            var root = Newup(stream, _builder);
            (root as IEventSource<TID>).Id = id;

            return root;
        }
        public IEnumerable<T> Query<TMemento>(Func<TMemento, Boolean> predicate) where TMemento : class, IMemento
        {
            var snapshots = _snapshots.Query<T, TId, TMemento>(predicate);
            return snapshots.Select(x =>
            {
                return Get(x.Id);
            });
        }

        private T Newup(IEventStream stream, IBuilder builder)
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
            if (root is INeedEventFactory)
                (root as INeedEventFactory).EventFactory = builder.Build<IMessageCreator>();
            if (root is INeedRouteResolver)
                (root as INeedRouteResolver).Resolver = builder.Build<IRouteResolver>();
            if (root is INeedRepositoryFactory)
                (root as INeedRepositoryFactory).RepositoryFactory = builder.Build<IRepositoryFactory>();

            return root;
        }
        

        private IEventStream OpenStream<TID>(String bucket, TID id, ISnapshot<TID> snapshot = null)
        {
            if (snapshot == null)
                return _store.GetStream<T>(bucket, id.ToString());
            else
                return _store.GetStream<T>(bucket, id.ToString(), snapshot.Version + 1);
        }
        
        
    }
}