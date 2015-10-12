using Aggregates.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aggregates;

namespace Aggregates.Internal
{
    public class Snapshot<TMemento, TId> : ISnapshot<TMemento, TId> where TMemento : class, IMemento
    {
        public TId Id { get; set; }
        public Int32 Version { get; set; }
        public TMemento Memento { get; set; }
    }
}