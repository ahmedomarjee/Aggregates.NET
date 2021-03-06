﻿using Aggregates.Contracts;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MessageInterfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Internal
{
    public class DefaultRouteResolver : IRouteResolver
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DefaultRouteResolver));

        private ConcurrentDictionary<Type, Action<IEventSource, Object>> _cache;
        private IMessageMapper _mapper;

        public DefaultRouteResolver(IMessageMapper mapper)
        {
            _mapper = mapper;
            _cache = new ConcurrentDictionary<Type, Action<IEventSource, Object>>();
        }

        public Action<IEventSource, Object> Resolve(IEventSource eventsource, Type eventType)
        {
            var mappedType = _mapper.GetMappedTypeFor(eventType);

            return _cache.GetOrAdd(mappedType, (key) =>
            {

                var handleMethod = eventsource.GetType()
                                     .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                                     .SingleOrDefault(
                                            m => m.Name == "Handle" &&
                                             m.GetParameters().Length == 1 &&
                                             m.GetParameters().Single().ParameterType == mappedType &&
                                             m.ReturnParameter.ParameterType == typeof(void));
                //.Select(m => new { Method = m, MessageType = m.GetParameters().Single().ParameterType });

                if (handleMethod == null)
                    return null;

                Action<IEventSource, Object> action = (es, m) => handleMethod.Invoke(es, new[] { m });
                return action;
            });
        }

    }
}
