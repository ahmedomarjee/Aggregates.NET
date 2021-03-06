﻿using Aggregates.Contracts;
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
        public IRepository<T> ForAggregate<T>(IBuilder builder) where T : class, IAggregate
        {
            var repoType = typeof(Repository<>).MakeGenericType(typeof(T));
            return (IRepository<T>)Activator.CreateInstance(repoType, builder);
        }
        public IEntityRepository<TParent, TParentId, T> ForEntity<TParent, TParentId, T>(TParent parent, IBuilder builder) where T : class, IEntity where TParent : class, IBase<TParentId>
        {
            var repoType = typeof(EntityRepository<,,>).MakeGenericType(typeof(TParent), typeof(TParentId), typeof(T));
            return (IEntityRepository<TParent, TParentId, T>)Activator.CreateInstance(repoType, parent, builder);
        }
        public IPocoRepository<T> ForPoco<T>() where T : class, new()
        {
            var repoType = typeof(PocoRepository<>).MakeGenericType(typeof(T));
            return (IPocoRepository<T>)Activator.CreateInstance(repoType);
        }
        public IPocoRepository<TParent, TParentId, T> ForPoco<TParent, TParentId, T>(TParent parent) where T : class, new() where TParent : class, IBase<TParentId>
        {
            var repoType = typeof(PocoRepository<,,>).MakeGenericType(typeof(TParent), typeof(TParentId), typeof(T));
            return (IPocoRepository<TParent, TParentId, T>)Activator.CreateInstance(repoType);
        }
    }
}