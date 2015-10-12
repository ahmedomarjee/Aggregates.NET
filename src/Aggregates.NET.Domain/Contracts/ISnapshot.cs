using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface ISnapshot<TMemento, TId> where TMemento : class, IMemento
    {
        TId Id { get; }

        Int32 Version { get; }

        TMemento Memento { get; }
    }
}