using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aggregates.Contracts;
using Aggregates.Internal;

namespace Aggregates
{
    public class SnapshotStore : Contracts.IStoreSnapshots
    {
        private IList<WritableEvent> _pendingShots;

        public SnapshotStore(IStoreEvents store, String bucket, String streamId, Int32 streamVersion, IEnumerable<WritableEvent> events)
        {
        }


        public void Add<T, TId>(ISnapshot<TId> snapshot)
        {

            this._pendingShots.Add(new WritableEvent
            {
                Descriptor = new EventDescriptor
                {
                    EntityType = typeof(T).FullName,
                    Timestamp = DateTime.UtcNow,
                },
                Event = snapshot,
                EventId = Guid.NewGuid()
            });
        }

        public ISnapshot<TId> Load<T, TId>()
        {
            throw new NotImplementedException();
        }

        IEnumerable<ISnapshot<TId>> IStoreSnapshots.Query<T, TId, TMemento>(Func<TMemento, bool> predicate)
        {
            throw new NotImplementedException("Eventstore snapshots don't support querying");
        }
    }
}
