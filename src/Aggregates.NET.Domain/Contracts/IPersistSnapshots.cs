using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface IPersistSnapshots
    {
        ISnapshot<TMemento, TId> Load<TMemento, TId>(TId Id) where TMemento : class, IMemento;
        void Add<TMemento, TId>(ISnapshot<TMemento, TId> Snapshot) where TMemento : class, IMemento;

        IEnumerable<ISnapshot<TMemento, TId>> Query<TMemento, TId>(Func<TMemento, Boolean> Predicate) where TMemento : class, IMemento;

        void Commit();
    }
}
