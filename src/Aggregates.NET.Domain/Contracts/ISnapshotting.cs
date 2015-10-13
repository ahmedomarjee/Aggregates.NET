using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface ISnapshotting<TId>
    {
        void RestoreSnapshot(ISnapshot<TId> snapshot);
        ISnapshot<TId> TakeSnapshot();
        Boolean ShouldTakeSnapshot();
    }
}