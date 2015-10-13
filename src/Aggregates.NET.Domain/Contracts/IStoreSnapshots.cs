using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface IStoreSnapshots
    {
        void Add<T, TId>(ISnapshot<TId> snapshot);
        ISnapshot<TId> Load<T, TId>();

        // Todo: I can write my own query provider to allow them to chain predicates like a boss
        IEnumerable<ISnapshot<TId>> Query<T, TId, TMemento>(Func<TMemento, Boolean> predicate) where TMemento : class, IMemento;
    }
}
