using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface IEntityRepository : IRepository { }

    public interface IQueryableEntityRepository<T, TMemento>
        where T : class, IEventSource
        where TMemento : class, IMemento
    {
        IEnumerable<T> Query(Func<TMemento, Boolean> predicate);
    }


    public interface IEntityRepository<TAggregateId, T> : IEntityRepository where T : class, IEventSource
    {
        T Get<TId>(TId id);

        T New<TId>(TId id);
    }
}