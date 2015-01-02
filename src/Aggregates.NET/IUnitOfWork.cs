using Aggregates.Contracts;
using NEventStore.Dispatcher;
using NServiceBus.MessageMutator;
using NServiceBus.UnitOfWork;
using System;
using System.Collections.Generic;
namespace Aggregates
{
    public interface IUnitOfWork : IDisposable, IManageUnitsOfWork, IMutateTransportMessages
    {
        IRepository<T> R<T>() where T : class, IEventSource;
        IRepository<T> For<T>() where T : class, IEventSource;
        IRepository<T> Repository<T>() where T : class, IEventSource;

        void Commit();
    }
}