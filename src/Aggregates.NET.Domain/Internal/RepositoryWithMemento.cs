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
    public class RepositoryWithMemento<T, TMemento> : Repository<T>, IQueryableRepository<T, TMemento> where T : class, IAggregate where TMemento : class, IMemento
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(RepositoryWithMemento<,>));
        private readonly IPersistSnapshots _snapshots;


        public RepositoryWithMemento(IBuilder builder) : base(builder)
        {
            _snapshots = builder.Build<IPersistSnapshots>();
        }

        public override T Get<TId>(String bucket, TId id)
        {
            Logger.DebugFormat("Retreiving aggregate id '{0}' from bucket '{1}' in store", id, bucket);
            

            var snapshot = _snapshots.Load<TMemento, TId>(id);
            IEventStream stream;
            if (snapshot == null)
                stream = OpenStream(bucket, id);
            else
                stream = OpenStream(bucket, id, snapshot.Version);

            if (stream == null) return (T)null;
            // Get requires the stream exists
            if (stream.StreamVersion == -1) return (T)null;

            // Call the 'private' constructor
            var root = Newup(stream, _builder);
            (root as IEventSource<TId>).Id = id;

            if (snapshot != null)
                (root as ISnapshotting<TMemento, TId>).RestoreSnapshot(snapshot);

            root.Hydrate(stream.Events.Select(e => e.Event));

            return root;
        }
        
        public IEnumerable<T> Query(Func<TMemento, Boolean> predicate)
        {

        }
    }
}
