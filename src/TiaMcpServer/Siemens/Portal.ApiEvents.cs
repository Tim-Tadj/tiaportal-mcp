using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.Security;
using Microsoft.Extensions.Logging;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        private sealed class ApiSubscription
        {
            public string Id = "";
            public string EntityRef = "";
            public string Scope = "";
            public TiaPortal Portal = null!;
            public object? Project = null;
            public object Target = null!;
            public EventInfo Event = null!;
            public Delegate Handler = null!;
        }

        private sealed class ApiObservation
        {
            public long Sequence;
            public string Subscription = "";
            public DateTime Timestamp;
            public object?[] Arguments = Array.Empty<object?>();
        }

        private readonly object _apiEventSync = new object();
        private readonly Dictionary<string, ApiSubscription> _apiSubscriptions = new Dictionary<string, ApiSubscription>(StringComparer.Ordinal);
        private readonly Queue<ApiObservation> _apiObservations = new Queue<ApiObservation>();
        private long _apiEventSequence;
        private long _apiEventsDropped;

        private bool ApiSubscriptionActive(ApiSubscription subscription)
        {
            return ReferenceEquals(subscription.Portal, _portal) &&
                (subscription.Project == null ? _project == null : _project != null && SameNativeObject(subscription.Project, _project));
        }

        private void DemandActiveApiSubscription(string id)
        {
            lock (_apiEventSync)
                if (!_apiSubscriptions.TryGetValue(id, out var subscription) || !ApiSubscriptionActive(subscription))
                    throw new InvalidOperationException("The callback subscription expired after the Portal or project selection changed.");
        }

        private void ExpireApiEventsForSelection()
        {
            ApiSubscription[] stale;
            lock (_apiEventSync)
            {
                stale = _apiSubscriptions.Values.Where(subscription => !ApiSubscriptionActive(subscription)).ToArray();
                foreach (var subscription in stale) _apiSubscriptions.Remove(subscription.Id);
            }
            foreach (var subscription in stale)
            {
                try { subscription.Event.RemoveEventHandler(subscription.Target is Type ? null : subscription.Target, subscription.Handler); }
                catch (Exception error) { _logger?.LogWarning("An expired native event could not be detached ({ErrorType}); its callback remains disabled.", error.GetType().Name); }
            }
        }

        private static EventInfo[] ApiEvents(object target)
        {
            var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            return ApiContracts(target).SelectMany(t => t.GetEvents(flags))
                .Where(e => e.AddMethod?.IsPublic == true && e.RemoveMethod?.IsPublic == true && e.EventHandlerType != null)
                .GroupBy(ApiMemberId).Select(g => g.First()).OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(ApiMemberId).ToArray();
        }

        private static IEnumerable<(string key, object schema)> ApiEventSchemas(object target)
        {
            return ApiEvents(target).Select(e => ("E:" + e.Name + ":" + ApiMemberId(e), (object)new
            {
                id = ApiMemberId(e), name = e.Name, kind = "event", declaringType = e.DeclaringType!.FullName,
                handler = PublicApiValueCodec.Schema(e.EventHandlerType!),
                effects = "subscription requires ReadWrite; use a declarative callback for synchronous native decisions"
            }));
        }

        private void CaptureApiEvent(string subscription, object?[] arguments)
        {
            lock (_apiEventSync)
            {
                if (!_apiSubscriptions.TryGetValue(subscription, out var active) || !ApiSubscriptionActive(active)) return;
                if (_apiObservations.Count >= 512) { _apiObservations.Dequeue(); _apiEventsDropped++; }
                _apiObservations.Enqueue(new ApiObservation { Sequence = ++_apiEventSequence, Subscription = subscription,
                    Timestamp = DateTime.UtcNow, Arguments = arguments });
            }
        }

        public object ReadApiEvents(string subscriptionId, long afterSequence, int limit)
        {
            if (afterSequence < 0) throw new ArgumentException("afterSequence cannot be negative.");
            if (limit < 1 || limit > 200) throw new ArgumentException("limit must be from 1 to 200.");
            var scope = ApiContextScope();
            ApiSubscription subscription;
            ApiObservation[] observations;
            long dropped;
            long oldest;
            lock (_apiEventSync)
            {
                if (!_apiSubscriptions.TryGetValue(subscriptionId, out subscription!)) throw new ArgumentException("Unknown event subscription.");
                if (!ReferenceEquals(subscription.Portal, _portal) || !string.Equals(subscription.Scope, scope, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The subscription belongs to another Portal or project selection. Unsubscribe and subscribe again.");
                observations = _apiObservations.Where(x => x.Subscription == subscriptionId && x.Sequence > afterSequence).Take(limit + 1).ToArray();
                dropped = _apiEventsDropped;
                oldest = _apiObservations.Count == 0 ? _apiEventSequence + 1 : _apiObservations.Peek().Sequence;
            }
            var rows = observations.Take(limit).Select(item => new
            {
                sequence = item.Sequence, timestampUtc = item.Timestamp,
                arguments = item.Arguments.Select((value, index) =>
                {
                    var parameter = subscription.Event.EventHandlerType!.GetMethod("Invoke")!.GetParameters()[index];
                    if (ApiSensitive(parameter.Name ?? "")) return (object)"<redacted>";
                    try { return ProjectApiValue(value, subscription.EntityRef); }
                    catch (Exception) { return (object)new { type = value?.GetType().FullName, unavailable = true }; }
                }).ToArray()
            }).ToArray();
            return new { items = rows, returned = rows.Length, hasMore = observations.Length > limit,
                nextSequence = rows.Length == 0 ? afterSequence : rows[rows.Length - 1].sequence,
                oldestRetainedSequence = oldest, droppedEvents = dropped,
                note = "Events are retained in a shared 512-entry ring. Native arguments may expire before polling. Polling cannot answer synchronous native decisions; supply a callback policy when subscribing." };
        }

        private void ReleaseApiEvents()
        {
            ApiSubscription[] subscriptions;
            lock (_apiEventSync)
            {
                subscriptions = _apiSubscriptions.Values.ToArray();
                _apiSubscriptions.Clear();
                _apiObservations.Clear();
            }
            foreach (var subscription in subscriptions)
            {
                try { subscription.Event.RemoveEventHandler(subscription.Target is Type ? null : subscription.Target, subscription.Handler); }
                catch (Exception) { /* The native source may already have closed. */ }
            }
        }

#if TIA_MCP_READ_WRITE
        public object SubscribeApiEvent(string entityRef, string eventId, JsonElement callback, string expectedProjectPath, bool acceptNativeEffects)
        {
            ProfileAccessPolicy.DemandWrite(nameof(SubscribeApiEvent));
            RequireApiContext(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Explicitly accept native event and callback effects.");
            var target = ResolveApiTarget(entityRef);
            var info = ApiEvents(target).SingleOrDefault(e => ApiMemberId(e) == eventId) ?? throw new ArgumentException("Select an exact event schema id.");
            var handlerType = info.EventHandlerType!;
            var invoke = handlerType.GetMethod("Invoke")!;
            Delegate? policy = null;
            if (callback.ValueKind != JsonValueKind.Undefined && callback.ValueKind != JsonValueKind.Null)
                policy = (Delegate)PublicApiValueCodec.Convert(callback, handlerType, ResolveApiTarget, ResolveApiType)!;
            if (policy == null && (invoke.ReturnType != typeof(void) || invoke.GetParameters().Any(p => p.ParameterType.IsByRef)))
                throw new ArgumentException("This event needs a declarative callback policy for its return or ref/out parameters.");
            var id = "event:" + Guid.NewGuid().ToString("N");
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var capture = Expression.Call(Expression.Constant(this), typeof(Portal).GetMethod(nameof(CaptureApiEvent), BindingFlags.NonPublic | BindingFlags.Instance)!,
                Expression.Constant(id), Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object)))));
            Expression body;
            if (policy == null) body = capture;
            else if (invoke.ReturnType == typeof(void)) body = Expression.Block(Expression.Invoke(Expression.Constant(policy, handlerType), parameters), capture);
            else
            {
                var result = Expression.Variable(invoke.ReturnType, "result");
                body = Expression.Block(new[] { result }, Expression.Assign(result, Expression.Invoke(Expression.Constant(policy, handlerType), parameters)), capture, result);
            }
            if (policy != null)
                body = Expression.Block(Expression.Call(Expression.Constant(this), typeof(Portal).GetMethod(nameof(DemandActiveApiSubscription), BindingFlags.NonPublic | BindingFlags.Instance)!, Expression.Constant(id)), body);
            var subscription = new ApiSubscription { Id = id, EntityRef = entityRef, Scope = ApiContextScope(), Portal = _portal!,
                Project = _project, Target = target, Event = info, Handler = Expression.Lambda(handlerType, body, parameters).Compile() };
            lock (_apiEventSync)
            {
                if (_apiSubscriptions.Count >= 64) throw new ArgumentException("The session already has 64 subscriptions. Unsubscribe before adding more.");
                _apiSubscriptions.Add(id, subscription);
            }
            try { info.AddEventHandler(target is Type ? null : target, subscription.Handler); }
            catch (Exception)
            {
                lock (_apiEventSync) _apiSubscriptions.Remove(id);
                throw;
            }
            return new { subscriptionId = id, eventName = info.Name, callbackConfigured = policy != null, maximumRetainedEvents = 512 };
        }

        public object UnsubscribeApiEvent(string subscriptionId, string expectedProjectPath)
        {
            ProfileAccessPolicy.DemandWrite(nameof(UnsubscribeApiEvent));
            RequireApiContext(expectedProjectPath);
            ApiSubscription subscription;
            lock (_apiEventSync)
            {
                if (!_apiSubscriptions.TryGetValue(subscriptionId, out subscription!)) throw new ArgumentException("Unknown event subscription.");
                if (!ReferenceEquals(subscription.Portal, _portal)) throw new ArgumentException("The subscription belongs to another Portal session.");
            }
            subscription.Event.RemoveEventHandler(subscription.Target is Type ? null : subscription.Target, subscription.Handler);
            lock (_apiEventSync) _apiSubscriptions.Remove(subscriptionId);
            return new { subscriptionId, unsubscribed = true };
        }
#endif
    }
}
