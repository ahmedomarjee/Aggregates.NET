﻿using Aggregates.Contracts;
using Aggregates.Specifications;
using NServiceBus;
using NServiceBus.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates
{
    public abstract class Entity<TId, TAggregateId> : IEntity<TId, TAggregateId>, INeedStream, INeedEventFactory, INeedRouteResolver
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Entity<,>));

        private IEventStream _eventStream { get { return (this as INeedStream).Stream; } }

        private IMessageCreator _eventFactory { get { return (this as INeedEventFactory).EventFactory; } }

        private IRouteResolver _resolver { get { return (this as INeedRouteResolver).Resolver; } }

        public TId Id { get { return (this as IEventSource<TId>).Id; } }

        public TAggregateId AggregateId { get { return (this as IEntity<TId, TAggregateId>).AggregateId; } }

        String IEventSource.Bucket { get { return this.Bucket; } }
        String IEventSource.StreamId { get { return this.StreamId; } }

        Int32 IEventSource.Version { get { return this.Version; } }

        public String Bucket { get { return _eventStream.Bucket; } }
        public String StreamId { get { return _eventStream.StreamId; } }

        public Int32 Version { get { return _eventStream.StreamVersion; } }

        public Int32 CommitVersion { get { return _eventStream.CommitVersion; } }

        IEventStream INeedStream.Stream { get; set; }

        IMessageCreator INeedEventFactory.EventFactory { get; set; }

        IRouteResolver INeedRouteResolver.Resolver { get; set; }

        TId IEventSource<TId>.Id { get; set; }

        TAggregateId IEntity<TId, TAggregateId>.AggregateId { get; set; }


        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        void IEventSource.Hydrate(IEnumerable<object> events)
        {
            foreach (var @event in events)
                Raise(@event);
        }

        void IEventSource.Apply<TEvent>(Action<TEvent> action)
        {
            Apply(action);
        }

        protected virtual void Apply<TEvent>(Action<TEvent> action)
        {
            var @event = _eventFactory.CreateInstance(action);

            Raise(@event);

            // Todo: Fill with user headers or something
            var headers = new Dictionary<String, Object>();
            headers.Keys.Where()
            _eventStream.Add(@event, headers);
        }

        private void Raise(object @event)
        {
            if (@event == null) return;

            RouteFor(@event.GetType(), @event);
        }

        void IEventRouter.RouteFor(Type @eventType, object @event)
        {
            RouteFor(eventType, @event);
        }

        protected virtual void RouteFor(Type eventType, object @event)
        {
            var route = _resolver.Resolve(this, eventType);
            if (route == null) return;

            route(@event);
        }
    }

    public abstract class EntityWithMemento<TId, TAggregateId, TMemento> : Entity<TId, TAggregateId>, ISnapshotting<TMemento, TId>, INeedSnapshots where TMemento : class, IMemento
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

    public abstract class Entity<TId> : Entity<TId, TId> { }

    public abstract class EntityWithMemento<TId, TMemento> : EntityWithMemento<TId, TId, TMemento> where TMemento : class, IMemento { }
}