using Aggregates.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface IRepository : IDisposable
    {
        void Commit(Guid commitId, IDictionary<String, Object> headers);
    }

    public interface IQueryableRepository<T, TMemento>
        where T : class, IEventSource
        where TMemento : class, IMemento
    {
        IEnumerable<T> Query(Func<TMemento, Boolean> predicate);
    }

    public interface IRepository<T> : IRepository where T : class, IEventSource
    {
        T Get<TId>(TId id);

        T Get<TId>(String bucketId, TId id);

        T New<TId>(String bucketId, TId id);

        T New<TId>(TId id);

    }
}