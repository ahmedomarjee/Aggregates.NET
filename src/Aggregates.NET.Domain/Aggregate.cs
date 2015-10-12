using Aggregates.Contracts;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.ObjectBuilder;
using NServiceBus.ObjectBuilder.Common;
using System;
using System.Collections.Generic;

namespace Aggregates
{
    public abstract class Aggregate<TId> : Entity<TId, TId>, IAggregate<TId>, IHaveEntities<TId>, INeedBuilder, INeedStream, INeedRepositoryFactory
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Aggregate<>));

        private IDictionary<Type, IEntityRepository> _repositories = new Dictionary<Type, IEntityRepository>();

        private IBuilder _builder { get { return (this as INeedBuilder).Builder; } }

        private IEventStream _eventStream { get { return (this as INeedStream).Stream; } }

        private IRepositoryFactory _repoFactory { get { return (this as INeedRepositoryFactory).RepositoryFactory; } }

        IBuilder INeedBuilder.Builder { get; set; }

        IEventStream INeedStream.Stream { get; set; }

        IRepositoryFactory INeedRepositoryFactory.RepositoryFactory { get; set; }

        public IEntityRepository<TId, TEntity> E<TEntity>() where TEntity : class, IEntity
        {
            return Entity<TEntity>();
        }

        public IEntityRepository<TId, TEntity> Entity<TEntity>() where TEntity : class, IEntity
        {
            Logger.DebugFormat("Retreiving entity repository for type {0}", typeof(TEntity));
            var type = typeof(TEntity);

            IEntityRepository repository;
            if (_repositories.TryGetValue(type, out repository))
                return (IEntityRepository<TId, TEntity>)repository;

            return (IEntityRepository<TId, TEntity>)(_repositories[type] = (IEntityRepository)_repoFactory.ForEntity<TId, TEntity>(Id, _eventStream, _builder));
        }
    }

    public abstract class AggregateWithMemento<TId, TMemento> : Aggregate<TId>, ISnapshotting<TMemento, TId>, INeedSnapshots where TMemento : class, IMemento
    {
        private IEventStream _eventStream { get { return (this as INeedStream).Stream; } }
        private IPersistSnapshots _snapshots { get { return (this as INeedSnapshots).Snapshots; } }
        IPersistSnapshots INeedSnapshots.Snapshots { get; set; }

        void ISnapshotting<TMemento, TId>.RestoreSnapshot(ISnapshot<TMemento, TId> snapshot)
        {
            RestoreSnapshot(snapshot as TMemento);
        }

        ISnapshot<TMemento, TId> ISnapshotting<TMemento, TId>.TakeSnapshot()
        {
            return new Internal.Snapshot<TMemento, TId>
            {
                Id = this.Id,
                Version = this.Version,
                Memento = TakeSnapshot()
            };
        }

        Boolean ISnapshotting<TMemento, TId>.ShouldTakeSnapshot()
        {
            return ShouldTakeSnapshot();
        }

        protected abstract void RestoreSnapshot(TMemento memento);

        protected abstract TMemento TakeSnapshot();

        protected abstract Boolean ShouldTakeSnapshot();

        protected override void Apply<TEvent>(Action<TEvent> action)
        {
            base.Apply(action);

            if (this.ShouldTakeSnapshot())
                _snapshots.Add((this as ISnapshotting<TMemento, TId>).TakeSnapshot());
        }
    }
}