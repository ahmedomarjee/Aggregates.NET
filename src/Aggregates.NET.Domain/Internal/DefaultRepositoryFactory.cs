using Aggregates.Contracts;
using NServiceBus.ObjectBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Internal
{
    public class DefaultRepositoryFactory : IRepositoryFactory
    {
        // Where the magic happens
        // If the aggregate or entity has mementos, we need to pull out the memento type and create a repository capable of reading
        // snapshots


        public IRepository<T> ForAggregate<T>(IBuilder builder) where T : class, IAggregate
        {
            if (typeof(T).IsAssignableFrom(typeof(AggregateWithMemento<,>)))
            {
                Type withMemento = typeof(T);
                while (withMemento != typeof(AggregateWithMemento<,>) && withMemento.BaseType != null)
                    withMemento = withMemento.BaseType;

                var mementoType = withMemento.GetGenericArguments().Last();

                var repoType = typeof(RepositoryWithMemento<,>).MakeGenericType(typeof(T), mementoType);
                return (IRepository<T>)Activator.CreateInstance(repoType, builder);
            }
            else
            {

                var repoType = typeof(Repository<>).MakeGenericType(typeof(T));
                return (IRepository<T>)Activator.CreateInstance(repoType, builder);
            }
        }

        public IEntityRepository<TAggregateId, T> ForEntity<TAggregateId, T>(TAggregateId aggregateId, IEventStream aggregateStream, IBuilder builder) where T : class, IEntity
        {
            if (typeof(T).IsAssignableFrom(typeof(EntityWithMemento<,>)))
            {
                Type withMemento = typeof(T);
                while (withMemento != typeof(EntityWithMemento<,>) && withMemento.BaseType != null)
                    withMemento = withMemento.BaseType;

                var mementoType = withMemento.GetGenericArguments().Last();

                var repoType = typeof(EntityRepositoryWithMemento<,,>).MakeGenericType(typeof(TAggregateId), typeof(T), mementoType);
                return (IEntityRepository<TAggregateId, T>)Activator.CreateInstance(repoType, aggregateId, aggregateStream, builder);
            }
            else
            {
                var repoType = typeof(EntityRepository<,>).MakeGenericType(typeof(TAggregateId), typeof(T));
                return (IEntityRepository<TAggregateId, T>)Activator.CreateInstance(repoType, aggregateId, aggregateStream, builder);
            }
        }
    }
}