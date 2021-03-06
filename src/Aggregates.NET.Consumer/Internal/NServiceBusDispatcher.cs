﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.ObjectBuilder;
using NServiceBus.Unicast;
using NServiceBus.Settings;
using NServiceBus.Logging;
using Aggregates.Exceptions;
using Aggregates.Attributes;
using NServiceBus.MessageInterfaces;
using System.Collections.Concurrent;
using Metrics;
using Aggregates.Contracts;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Threading;
using Aggregates.Extensions;

namespace Aggregates.Internal
{
    public class NServiceBusDispatcher : IDispatcher, IDisposable
    {
        private class Job
        {
            public Object Event { get; set; }
            public IEventDescriptor Descriptor { get; set; }
            public long? Position { get; set; }
        }
        private class DelayedJob
        {
            public Object Event { get; set; }
            public IEventDescriptor Descriptor { get; set; }
            public long? Position { get; set; }
            public int Retry { get; set; }
            public long FailedAt { get; set; }
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(NServiceBusDispatcher));
        private static HashSet<String> SlowEventTypes = new HashSet<String>();

        private readonly IBuilder _builder;
        private readonly IBus _bus;
        private readonly IMessageCreator _eventFactory;
        private readonly IMessageMapper _mapper;
        private readonly IMessageHandlerRegistry _handlerRegistry;
        private readonly IInvokeObjects _objectInvoker;

        private readonly ConcurrentDictionary<String, IList<Type>> _invokeCache;
        private readonly ParallelOptions _parallelOptions;

        private readonly Boolean _parallelHandlers;
        private readonly Int32 _maxRetries;
        private readonly Boolean _dropEventFatal;
        private readonly Int32 _slowAlert;

        private readonly TaskProcessor _processor;

        private Int32 _processingQueueSize;
        private readonly Int32 _maxQueueSize;
        private DateTime? _warned;
        private HashSet<String> _noHandlers;

        private static Meter _eventsMeter = Metric.Meter("Events", Unit.Events);
        private static Metrics.Timer _eventsTimer = Metric.Timer("Event Duration", Unit.Events);
        private static Metrics.Timer _handlerTimer = Metric.Timer("Event Handler Duration", Unit.Events);
        private static Counter _queueSize = Metric.Counter("Event Queue Size", Unit.Events);

        private static Meter _errorsMeter = Metric.Meter("Event Errors", Unit.Errors);

        private void QueueTask(Job x)
        {
            Interlocked.Increment(ref _processingQueueSize);
            _queueSize.Increment();

            if (_processingQueueSize % 10 == 0 || Logger.IsDebugEnabled)
            {
                var eventType = _mapper.GetMappedTypeFor(x.Event.GetType());
                if (_processingQueueSize > (_maxQueueSize / 2))
                {
                    if (!_warned.HasValue)
                    {
                        Logger.WriteFormat(LogLevel.Warn, "Processing queue size growing large - slowing down the rate which we read from event store...");
                        _warned = DateTime.UtcNow;
                    }
                    // Progressively wait longer and longer as the queue size grows
                    Thread.Sleep(TimeSpan.FromMilliseconds(_processingQueueSize / 2));
                }
                else if (_warned.HasValue && (DateTime.UtcNow - _warned.Value).TotalSeconds > 30)
                    _warned = null;
                
                if (_processingQueueSize % 1000 == 0)
                    Logger.Write(LogLevel.Warn, () => $"Queueing event {eventType.FullName} at position {x.Position}.  Size of queue: {_processingQueueSize}/{_maxQueueSize}");
                else if (_processingQueueSize % 100 == 0)
                    Logger.Write(LogLevel.Info, () => $"Queueing event {eventType.FullName} at position {x.Position}.  Size of queue: {_processingQueueSize}/{_maxQueueSize}");
                else
                    Logger.Write(LogLevel.Debug, () => $"Queueing event {eventType.FullName} at position {x.Position}.  Size of queue: {_processingQueueSize}/{_maxQueueSize}");
            }

            _processor.Queue(async () =>
            {
                await Process(x.Event, x.Descriptor, x.Position);
                _queueSize.Decrement();
                Interlocked.Decrement(ref _processingQueueSize);
            });

        }

        public NServiceBusDispatcher(IBuilder builder, ReadOnlySettings settings, JsonSerializerSettings jsonSettings)
        {
            _builder = builder;
            _bus = builder.Build<IBus>();
            _eventFactory = builder.Build<IMessageCreator>();
            _mapper = builder.Build<IMessageMapper>();
            _handlerRegistry = builder.Build<IMessageHandlerRegistry>();
            _objectInvoker = builder.Build<IInvokeObjects>();
            _parallelHandlers = settings.Get<Boolean>("ParallelHandlers");
            _maxRetries = settings.Get<Int32>("MaxRetries");
            _dropEventFatal = settings.Get<Boolean>("EventDropIsFatal");
            _maxQueueSize = settings.Get<Int32>("MaxQueueSize");
            _slowAlert = settings.Get<Int32>("SlowAlertThreshold");
            _noHandlers = new HashSet<string>();

            _invokeCache = new ConcurrentDictionary<String, IList<Type>>();

            var parallelism = settings.Get<Int32>("Parallelism");
            _parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism,
            };

            _processor = new TaskProcessor(parallelism);
        }

        public void Pause(Boolean paused)
        {
            _processor.Pause(paused);
        }

        public void Dispatch(Object @event, IEventDescriptor descriptor = null, long? position = null)
        {
            if (_processingQueueSize >= _maxQueueSize)
                throw new SubscriptionCanceled("Processing queue overflow, too many items waiting to be processed");


            QueueTask(new Job
            {
                Event = @event,
                Descriptor = descriptor,
                Position = position,
            });
        }

        // Todo: all the logging and timing can be moved into a "Debug Dispatcher" which can be registered as the IDispatcher if the user wants
        private async Task Process(Object @event, IEventDescriptor descriptor = null, long? position = null)
        {

            var eventType = _mapper.GetMappedTypeFor(@event.GetType());

            // No handlers contains event types which have no recorded handlers on this consumer.  
            if (_noHandlers.Contains(eventType.FullName))
                return;

            if (SlowEventTypes.Contains(eventType.FullName))
            {
                Logger.Write(LogLevel.Info, () => $"Event {eventType.FullName} was previously detected as slow, switching to more verbose logging (for this instance)\nPayload: {JsonConvert.SerializeObject(@event, Formatting.Indented).MaxLines(15)}");
                Defaults.MinimumLogging.Value = LogLevel.Info;
            }

            Stopwatch s = new Stopwatch();

            var handleContext = new HandleContext
            {
                Mapper = _mapper,
                Bus = _bus,
                EventDescriptor = descriptor
            };

            using (_eventsTimer.NewContext())
            {
                Logger.Write(LogLevel.Debug, () => $"Processing event {eventType.FullName} at position {position}.  Size of queue: {_processingQueueSize}/{_maxQueueSize}");

                var success = false;
                var retry = 0;
                do
                {

                    using (var childBuilder = _builder.CreateChildBuilder())
                    {
                        var handlerGenericType = typeof(IHandleMessagesAsync<>).MakeGenericType(eventType);
                        List<dynamic> handlers = childBuilder.BuildAll(handlerGenericType).ToList();

                        if (handlers.Count == 0)
                        {
                            // If no handlers for event type, store it and future events of this type will be discarded sooner
                            _noHandlers.Add(eventType.FullName);
                            return;
                        }

                        var uows = new ConcurrentStack<IEventUnitOfWork>();

                        foreach(var uow in childBuilder.BuildAll<IEventUnitOfWork>())
                        {
                            uows.Push(uow);
                            uow.Builder = childBuilder;
                            uow.Retries = retry;
                            await uow.Begin();
                        }

                        var mutators = childBuilder.BuildAll<IEventMutator>();
                        if (mutators != null && mutators.Any())
                            foreach (var mutator in mutators)
                            {
                                Logger.Write(LogLevel.Debug, () => $"Mutating incoming event {eventType.FullName} with mutator {mutator.GetType().FullName}");
                                @event = mutator.MutateIncoming(@event, descriptor, position);
                            }

                        try
                        {
                            s.Restart();

                            Func<dynamic, Task> processor = async (handler) =>
                             {
                                 using (_handlerTimer.NewContext())
                                 {
                                     var handlerRetries = 0;
                                     var handlerSuccess = false;
                                     do
                                     {
                                         try
                                         {
                                             Stopwatch handlerWatch = Stopwatch.StartNew();
                                             Logger.Write(LogLevel.Debug, () => $"Executing event {eventType.FullName} on handler {handler.GetType().FullName}");

                                             var lambda = _objectInvoker.Invoker(handler, eventType);

                                             await lambda(handler, @event, handleContext);

                                             handlerWatch.Stop();
                                             if (handlerWatch.ElapsedMilliseconds > _slowAlert)
                                                 Logger.Write(LogLevel.Warn, () => $" - SLOW ALERT - Executing event {eventType.FullName} on handler {handler.GetType().FullName} took {handlerWatch.ElapsedMilliseconds} ms");
                                             else
                                                 Logger.Write(LogLevel.Debug, () => $"Executing event {eventType.FullName} on handler {handler.GetType().FullName} took {handlerWatch.ElapsedMilliseconds} ms");

                                             handlerSuccess = true;
                                         }
                                         catch (RetryException e)
                                         {
                                             Logger.Write(LogLevel.Info, () => $"Received retry signal while dispatching event {eventType.FullName} to {handler.GetType().FullName}. Retry: {handlerRetries}/3\nException: {e}");
                                             handlerRetries++;
                                         }

                                     } while (!handlerSuccess && (_maxRetries == -1 || handlerRetries <= _maxRetries));

                                     if (!handlerSuccess)
                                     {
                                         Logger.Write(LogLevel.Error, () => $"Failed executing event {eventType.FullName} on handler {handler.GetType().FullName}");
                                         throw new RetryException($"Failed executing event {eventType.FullName} on handler {handler.GetType().FullName}");
                                     }
                                 }

                             };

                            await handlers.WhenAllAsync(async (handler) =>
                            {
                                await processor(handler);
                            });
                            

                            s.Stop();
                            if (s.ElapsedMilliseconds > _slowAlert)
                            {
                                Logger.Write(LogLevel.Warn, () => $" - SLOW ALERT - Processing event {eventType.FullName} took {s.ElapsedMilliseconds} ms\nPayload: {JsonConvert.SerializeObject(@event, Formatting.Indented).MaxLines(15)}");
                                if (!SlowEventTypes.Contains(eventType.FullName))
                                    SlowEventTypes.Add(eventType.FullName);
                            }
                            else
                                Logger.Write(LogLevel.Debug, () => $"Processing event {eventType.FullName} took {s.ElapsedMilliseconds} ms");


                            s.Restart();
                            foreach (var uow in uows.Generate())
                            {
                                try
                                {
                                    await uow.End();
                                }
                                catch
                                {
                                    // If it failed it needs to go back on the stack
                                    uows.Push(uow);
                                    throw;
                                }
                            }
                            s.Stop();
                            if (s.ElapsedMilliseconds > _slowAlert)
                                Logger.Write(LogLevel.Warn, () => $" - SLOW ALERT - UOW.End for event {eventType.FullName} took {s.ElapsedMilliseconds} ms");
                            else
                                Logger.Write(LogLevel.Debug, () => $"UOW.End for event {eventType.FullName} took {s.ElapsedMilliseconds} ms");

                        }
                        catch (Exception e)
                        {
                            var trailingExceptions = new ConcurrentBag<Exception>();
                            foreach (var uow in uows.Generate())
                            {
                                try
                                {
                                    await uow.End(e);
                                }
                                catch (Exception endException)
                                {
                                    trailingExceptions.Add(endException);
                                }
                            }
                            if (trailingExceptions.Any())
                            {
                                var exceptions = trailingExceptions.ToList();
                                exceptions.Insert(0, e);
                                e = new System.AggregateException(exceptions);
                            }

                            // Only log if the event has failed more than half max retries indicating a non-transient error
                            if ((_maxRetries != -1 && retry > (_maxRetries / 2)) || (_maxRetries == -1 && (retry % 3) == 0))
                                Logger.Write(LogLevel.Warn, () => $"Encountered an error while processing {eventType.FullName}. Retry {retry}/{_maxRetries}\nPayload: {JsonConvert.SerializeObject(@event, Formatting.Indented).MaxLines(15)}\nException details:\n{e}");
                            else
                                Logger.Write(LogLevel.Debug, () => $"Encountered an error while processing {eventType.FullName}. Retry {retry}/{_maxRetries}\nPayload: {JsonConvert.SerializeObject(@event, Formatting.Indented).MaxLines(15)}\nException details:\n{e}");

                            _errorsMeter.Mark();
                            retry++;
                            Thread.Sleep(75 * (retry / 2));
                            continue;
                        }
                    }
                    success = true;
                } while (!success && (_maxRetries == -1 || retry < _maxRetries));

                if (!success)
                {
                    var message = String.Format("Encountered an error while processing {0}.  Ran out of retries, dropping event.\nPayload: {1}", eventType.FullName, JsonConvert.SerializeObject(@event).MaxLines(15));
                    if (_dropEventFatal)
                    {
                        Logger.Fatal(message);
                        throw new SubscriptionCanceled(message);
                    }

                    Logger.Error(message);
                }


                if (SlowEventTypes.Contains(eventType.FullName) && Defaults.MinimumLogging.Value.HasValue)
                {
                    Logger.Write(LogLevel.Info, () => $"Finished processing event {eventType.FullName} verbosely - resetting log level");
                    Defaults.MinimumLogging.Value = null;
                    SlowEventTypes.Remove(eventType.FullName);
                }
            }
            _eventsMeter.Mark();
        }


        public void Dispatch<TEvent>(Action<TEvent> action)
        {
            var @event = _eventFactory.CreateInstance(action);
            this.Dispatch(@event);

        }

        public void Dispose()
        {
            _processor.Dispose();
        }
    }
}