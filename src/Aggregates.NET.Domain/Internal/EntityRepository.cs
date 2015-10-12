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
    // Todo: Since entities no longer live 'in' the aggregate's stream, we can probably merge EntityRepository and Repository
    public class EntityRepository<TAggregateId, T> : IEntityRepository<TAggregateId, T> where T : class, IEntity
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityRepository<,>));
        protected readonly IStoreEvents _store;
        protected readonly IBuilder _builder;
        protected readonly TAggregateId _aggregateId;
        protected readonly IEventStream _aggregateStream;
        
        private readonly ConcurrentDictionary<String, IEventStream> _streams = new ConcurrentDictionary<String, IEventStream>();
        private Boolean _disposed;

        public EntityRepository(TAggregateId aggregateId, IEventStream aggregateStream, IBuilder builder)
        {
            _aggregateId = aggregateId;
            _aggregateStream = aggregateStream;
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

        public virtual T Get<TId>(TId id)
        {
            Logger.DebugFormat("Retreiving entity id '{0}' from aggregate '{1}' in store", id, _aggregateId);
            
            var stream = OpenStream(id);

            if (stream == null) return (T)null;
            // Get requires the stream exists
            if (stream.StreamVersion == -1) return (T)null;

            // Call the 'private' constructor
            var entity = Newup(stream, _builder);
            (entity as IEventSource<TId>).Id = id;
            (entity as IEntity<TId, TAggregateId>).AggregateId = _aggregateId;

            entity.Hydrate(stream.Events.Select(e => e.Event));

            this._aggregateStream.AddChild(stream);
            return entity;
        }

        public T New<TId>(TId id)
        {
            var stream = PrepareStream(id);
            var entity = Newup(stream, _builder);
            (entity as IEventSource<TId>).Id = id;

            this._aggregateStream.AddChild(stream);
            return entity;
        }

        protected T Newup(IEventStream stream, IBuilder builder)
        {
            // Call the 'private' constructor
            var tCtor = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);

            if (tCtor == null)
                throw new AggregateException("Entity needs a PRIVATE parameterless constructor");
            var entity = (T)tCtor.Invoke(null);

            // Todo: I bet there is a way to make a INeedBuilding<T> type interface
            //      and loop over each, calling builder.build for each T
            if (entity is INeedStream)
                (entity as INeedStream).Stream = stream;
            if (entity is INeedBuilder)
                (entity as INeedBuilder).Builder = builder;
            if (entity is INeedSnapshots)
                (entity as INeedSnapshots).Snapshots = builder.Build<IPersistSnapshots>();
            if (entity is INeedEventFactory)
                (entity as INeedEventFactory).EventFactory = builder.Build<IMessageCreator>();
            if (entity is INeedRouteResolver)
                (entity as INeedRouteResolver).Resolver = builder.Build<IRouteResolver>();
            if (entity is INeedRepositoryFactory)
                (entity as INeedRepositoryFactory).RepositoryFactory = builder.Build<IRepositoryFactory>();

            return entity;
        }

        protected IEventStream OpenStream<TId>(TId id, Int32? version = -1)
        {
            IEventStream stream;
            var streamId = String.Format("{0}-{1}-{2}", _aggregateStream.StreamId, typeof(T).FullName, id);
            if (_streams.TryGetValue(streamId, out stream))
                return stream;
            
            _streams[streamId] = stream = _store.GetStream<T>(_aggregateStream.Bucket, id.ToString(), version + 1);
            return stream;
        }

        protected IEventStream PrepareStream<TId>(TId id)
        {
            IEventStream stream;
            var streamId = String.Format("{0}-{1}-{2}", _aggregateStream.StreamId, typeof(T).FullName, id);
            if (!_streams.TryGetValue(streamId, out stream))
                _streams[streamId] = stream = _store.GetStream<T>(_aggregateStream.Bucket, id.ToString());

            return stream;
        }
    }
}