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
    public class EntityRepository<TAggregateId, T, TId> : IEntityRepository<TAggregateId, T> where T : class, IEntity
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityRepository<,,>));
        private readonly IStoreEvents _store;
        private readonly IStoreSnapshots _snapshots;
        private readonly IBuilder _builder;
        private readonly TAggregateId _aggregateId;
        private readonly IEventStream _aggregateStream;

        private struct EntityStream
        {
            public T Entity { get; set; }
            public IEventStream Stream { get; set; }
        }

        private readonly ConcurrentDictionary<String, EntityStream> _entities = new ConcurrentDictionary<String, EntityStream>();
        private Boolean _disposed;

        public EntityRepository(TAggregateId aggregateId, IEventStream aggregateStream, IBuilder builder)
        {
            _aggregateId = aggregateId;
            _aggregateStream = aggregateStream;
            _builder = builder;
            _store = _builder.Build<IStoreEvents>();
            _snapshots = _builder.Build<IStoreSnapshots>();
        }

        void IRepository.Commit(Guid commitId, IDictionary<String, Object> headers)
        {
            foreach (var ent in _entities.Values)
            {
                ent.Stream.Commit(commitId, headers);

                if (ent.Entity is ISnapshotting<TId> && (ent.Entity as ISnapshotting<TId>).ShouldTakeSnapshot())
                    _snapshots.Add<T, TId>((ent.Entity as ISnapshotting<TId>).TakeSnapshot());
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

            _entities.Clear();
            _disposed = true;
        }

        public T Get<TID>(TID id)
        {
            Logger.DebugFormat("Retreiving entity id '{0}' from aggregate '{1}' in store", id, _aggregateId);

            EntityStream cached;
            var streamId = String.Format("{0}-{1}-{2}", _aggregateStream.StreamId, typeof(T).FullName, id);
            if (_entities.TryGetValue(streamId, out cached))
                return cached.Entity;


            var snapshot = _snapshots.Load<T, TID>();
            var stream = OpenStream(id, snapshot);

            if (stream == null && snapshot == null) return (T)null;
            // Get requires the stream exists
            if (stream.StreamVersion == -1) return (T)null;

            // Call the 'private' constructor
            var entity = Newup(stream, _builder);
            (entity as IEventSource<TID>).Id = id;
            (entity as IEntity<TId, TAggregateId>).AggregateId = _aggregateId;

            if (snapshot != null && entity is ISnapshotting<TID>)
                ((ISnapshotting<TID>)entity).RestoreSnapshot(snapshot);

            entity.Hydrate(stream.Events.Select(e => e.Event));

            this._aggregateStream.AddChild(stream);
            return entity;
        }

        public T New<TID>(TID id)
        {
            var stream = OpenStream(id);
            var entity = Newup(stream, _builder);
            (entity as IEventSource<TID>).Id = id;

            this._aggregateStream.AddChild(stream);
            return entity;
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
                throw new AggregateException("Entity needs a PRIVATE parameterless constructor");
            var entity = (T)tCtor.Invoke(null);

            // Todo: I bet there is a way to make a INeedBuilding<T> type interface
            //      and loop over each, calling builder.build for each T
            if (entity is INeedStream)
                (entity as INeedStream).Stream = stream;
            if (entity is INeedBuilder)
                (entity as INeedBuilder).Builder = builder;
            if (entity is INeedEventFactory)
                (entity as INeedEventFactory).EventFactory = builder.Build<IMessageCreator>();
            if (entity is INeedRouteResolver)
                (entity as INeedRouteResolver).Resolver = builder.Build<IRouteResolver>();
            if (entity is INeedRepositoryFactory)
                (entity as INeedRepositoryFactory).RepositoryFactory = builder.Build<IRepositoryFactory>();

            return entity;
        }
        

        private IEventStream OpenStream<TID>(TID id, ISnapshot<TID> snapshot = null)
        {
            if (snapshot == null)
                return _store.GetStream<T>(_aggregateStream.Bucket, id.ToString());
            else
                return _store.GetStream<T>(_aggregateStream.Bucket, id.ToString(), snapshot.Version + 1);
        }
        
    }
}