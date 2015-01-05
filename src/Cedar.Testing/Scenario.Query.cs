﻿namespace Cedar.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Cedar.Handlers;

    public static partial class Scenario
    {
        public static Query.IGiven<TOutput> ForQuery<TOutput>(IHandlerResolver handlerResolver, [CallerMemberName] string scenarioName = null)
        {
            return new Query.ScenarioBuilder<TOutput>(handlerResolver, scenarioName);
        }

        public static class Query
        {
            public interface IGiven<TOutput> : IWhen<TOutput>
            {
                IWhen<TOutput> Given(params object[] events);
            }

            public interface IWhen<TOutput> : IThen<TOutput>
            {
                IThen<TOutput> When(Func<Task<TOutput>> performQuery);
            }

            public interface IThen<TOutput> : IScenario
            {
                IThen<TOutput> ThenShouldEqual(TOutput output);
            }

            internal class ScenarioBuilder<TOutput> : IGiven<TOutput>
            {
                private static readonly MethodInfo DispatchDomainEventMethod;

                private readonly IHandlerResolver _module;
                private readonly string _name;
                private bool _passed;
                private object[] _given;
                private Func<Task<TOutput>> _when;
                private TOutput _expect;
                private object _results;
                private readonly Func<Task> _runGiven;
                private readonly Func<Task> _runWhen;
                private readonly Action _runThen;
                private readonly Stopwatch _timer;

                static ScenarioBuilder()
                {
                    DispatchDomainEventMethod = typeof(HandlerModuleExtensions)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Single(method => method.Name == "Dispatch" && method.GetParameters().First().ParameterType == typeof(IHandlerResolver));
                }

                public ScenarioBuilder(IHandlerResolver module, string name)
                {
                    _module = module;
                    _name = name;
                    _given = new object[0];

                    _runGiven = async () =>
                    {
                        foreach(var @event in _given.Select(WrapInEnvelopeIfNecessary))
                        {
                            var task = (Task) DispatchDomainEventMethod.MakeGenericMethod(@event.GetType())
                                .Invoke(null, new object[] {_module, @event, new CancellationToken(),});

                            await task;
                        }
                    };
                    _runWhen = async () =>
                    {
                        _results = await _when();
                    };
                    _runThen = () =>
                    {
                        if(false == MessageEqualityComparer.Instance.Equals(_results, _expect))
                        {
                            throw new ScenarioException(string.Format("Expected {0}; got {1} instead.", _expect, _results));
                        }
                    };

                    _timer = new Stopwatch();
                }

                public IThen<TOutput> ThenShouldEqual(TOutput output)
                {
                    _expect = output;

                    return this;
                }

                public IThen<TOutput> When(Func<Task<TOutput>> performQuery)
                {
                    _when = performQuery;

                    return this;
                }

                public IWhen<TOutput> Given(params object[] events)
                {
                    _given = events;

                    return this;
                }

                public string Name
                {
                    get { return _name; }
                }

                async Task<ScenarioResult> IScenario.Run()
                {
                    try
                    {
                        _timer.Start();

                        try
                        {
                            await _runGiven();
                        }
                        catch(Exception ex)
                        {
                            _results = ex;

                            return this;
                        }

                        try
                        {
                            await _runWhen();
                        }
                        catch(Exception ex)
                        {
                            _results = ex;

                            return this;
                        }

                        try
                        {
                            _runThen();

                            _passed = true;
                        }
                        catch(Exception ex)
                        {
                            _results = ex;
                        }

                        return this;
                    }
                    finally 
                    {
                        _timer.Stop();
                    }
                }

                public TaskAwaiter<ScenarioResult> GetAwaiter()
                {
                    IScenario scenario = this;

                    return scenario.Run().GetAwaiter();
                }

                public static implicit operator ScenarioResult(ScenarioBuilder<TOutput> builder)
                {
                    return new ScenarioResult(builder._name, builder._passed, builder._given, builder._when, builder._expect, builder._results, builder._timer.Elapsed);
                }

                private static DomainEventMessage WrapInEnvelopeIfNecessary(object @event)
                {
                    return @event as DomainEventMessage
                           ?? (DomainEventMessage)Activator.CreateInstance(
                               typeof(DomainEventMessage<>).MakeGenericType(
                                   @event.GetType()),
                               new[] { "streamId", @event, 0, new Dictionary<string, object>(), null });
                }

            }
        }
    }
}