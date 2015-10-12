using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface ISnapshotting<TMemento, TId> where TMemento : class, IMemento
    {
        void RestoreSnapshot(ISnapshot<TMemento, TId> snapshot);
        ISnapshot<TMemento, TId> TakeSnapshot();
        Boolean ShouldTakeSnapshot();
    }
}