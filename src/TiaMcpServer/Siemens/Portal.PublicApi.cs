using Siemens.Engineering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Runtime;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        private sealed class ApiHandle
        {
            public object Target = null!;
            public object? Project;
            public TiaPortal Portal = null!;
            public string ProjectPath = "";
            public string Parent = "";
            public Func<object, object?>? Replay;
        }

        private readonly Dictionary<string, ApiHandle> _apiHandles = new Dictionary<string, ApiHandle>(StringComparer.Ordinal);
        private readonly Queue<string> _apiHandleOrder = new Queue<string>();
        private readonly object _apiHandleSync = new object();
        private const int MaxApiHandles = 1000;
        private const int MaxApiDepth = 20;
        private const int MaxApiCollection = 5000;

        internal static bool ApiTrustedType(Type type)
        {
            if (!type.IsVisible || type.FullName == null || type.FullName.StartsWith("Siemens.Engineering.Private.", StringComparison.Ordinal) ||
                !type.FullName.StartsWith("Siemens.Engineering.", StringComparison.Ordinal)) return false;
            var identity = type.Assembly.GetName();
            if (!SiemensEngineeringAssemblyPolicy.IsSupportedName(identity.Name)) return false;
            try { SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(identity); return true; }
            catch (Exception) { return false; }
        }

        internal static Type[] ApiContracts(object target)
        {
            var type = target as Type ?? target.GetType();
            var contracts = new List<Type>();
            for (var current = type; current != null; current = current.BaseType)
                if (ApiTrustedType(current)) contracts.Add(current);
            contracts.AddRange(type.GetInterfaces().Where(ApiTrustedType));
            return contracts.Distinct().ToArray();
        }

        private static bool ApiSensitive(string name)
        {
            return new[] { "password", "secret", "credential", "token", "privatekey" }
                .Any(s => name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static string ApiMemberId(MemberInfo member)
        {
            var signature = member.DeclaringType!.Assembly.FullName + "|" + member.DeclaringType.FullName + "|" + member;
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(signature))).Replace("-", "").ToLowerInvariant();
        }

        private static bool ApiAllowedMethod(MethodInfo method)
        {
            if (!method.IsPublic || method.IsSpecialName || (method.ContainsGenericParameters && !method.IsGenericMethodDefinition) || !ApiTrustedType(method.DeclaringType!)) return false;
            if (new[] { "GetType", "Equals", "GetHashCode", "ToString", "Dispose", "GetService", "Invoke" }.Contains(method.Name)) return false;
            return true;
        }

        private static MethodInfo[] ApiMethods(object target)
        {
            var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            return ApiContracts(target).SelectMany(t => t.GetMethods(flags))
                .Where(ApiAllowedMethod).GroupBy(ApiMemberId).Select(g => g.First()).OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(ApiMemberId).ToArray();
        }

        private static PropertyInfo[] ApiProperties(object target)
        {
            var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            return ApiContracts(target).SelectMany(t => t.GetProperties(flags))
                .Where(p => (p.GetMethod != null && p.GetMethod.IsPublic) || (p.SetMethod != null && p.SetMethod.IsPublic))
                .GroupBy(ApiMemberId).Select(g => g.First()).OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(ApiMemberId).ToArray();
        }

        private static FieldInfo[] ApiFields(object target)
        {
            var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            return ApiContracts(target).SelectMany(t => t.GetFields(flags)).Where(f => f.IsPublic)
                .GroupBy(ApiMemberId).Select(g => g.First()).OrderBy(f => f.Name, StringComparer.Ordinal).ThenBy(ApiMemberId).ToArray();
        }

        private static object ApiFieldSchema(FieldInfo field)
        {
            return new { kind = "field", id = ApiMemberId(field), name = field.Name, declaringType = field.DeclaringType!.FullName,
                type = PublicApiValueCodec.Schema(field.FieldType), readable = true, writable = !field.IsInitOnly && !field.IsLiteral, redacted = ApiSensitive(field.Name) };
        }

        private sealed class ApiAssemblyCatalogue
        {
            public readonly List<Assembly> Assemblies = new List<Assembly>();
            public readonly List<object> Issues = new List<object>();
            public bool Truncated;
        }

        private static ApiAssemblyCatalogue LoadApiAssemblies()
        {
            var result = new ApiAssemblyCatalogue();
            var pending = new Queue<AssemblyName>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seeds = AppDomain.CurrentDomain.GetAssemblies().Where(a => a == typeof(Portal).Assembly ||
                SiemensEngineeringAssemblyPolicy.IsSupportedName(a.GetName().Name)).ToArray();
            foreach (var assembly in seeds)
            {
                if (assembly != typeof(Portal).Assembly) pending.Enqueue(assembly.GetName());
                foreach (var reference in assembly.GetReferencedAssemblies().Where(r => SiemensEngineeringAssemblyPolicy.IsSupportedName(r.Name))) pending.Enqueue(reference);
            }
            while (pending.Count > 0 && seen.Count < 2048)
            {
                var identity = pending.Dequeue();
                var key = identity.FullName ?? identity.Name ?? "";
                if (!seen.Add(key)) continue;
                try
                {
                    SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(identity);
                    var assembly = Assembly.Load(identity);
                    if (!SiemensEngineeringAssemblyPolicy.MatchesRequestedIdentity(identity, assembly.GetName()))
                        throw new System.IO.FileLoadException("The loaded assembly identity differs from the requested reference.");
                    if (assemblies.Add(assembly.FullName)) result.Assemblies.Add(assembly);
                    foreach (var reference in assembly.GetReferencedAssemblies().Where(r => SiemensEngineeringAssemblyPolicy.IsSupportedName(r.Name))) pending.Enqueue(reference);
                }
                catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is System.IO.FileLoadException || ex is BadImageFormatException)
                {
                    result.Issues.Add(new { assembly = key, status = ex.GetType().Name });
                }
            }
            result.Truncated = pending.Count > 0;
            return result;
        }

        internal static Type ResolveApiType(string name)
        {
            var standard = new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["System.Boolean"] = typeof(bool), ["System.Byte"] = typeof(byte), ["System.SByte"] = typeof(sbyte),
                ["System.Int16"] = typeof(short), ["System.UInt16"] = typeof(ushort), ["System.Int32"] = typeof(int),
                ["System.UInt32"] = typeof(uint), ["System.Int64"] = typeof(long), ["System.UInt64"] = typeof(ulong),
                ["System.Single"] = typeof(float), ["System.Double"] = typeof(double), ["System.Decimal"] = typeof(decimal),
                ["System.Char"] = typeof(char), ["System.String"] = typeof(string), ["System.Guid"] = typeof(Guid),
                ["System.DateTime"] = typeof(DateTime), ["System.DateTimeOffset"] = typeof(DateTimeOffset), ["System.TimeSpan"] = typeof(TimeSpan),
                ["System.Globalization.CultureInfo"] = typeof(CultureInfo), ["System.Version"] = typeof(Version)
            };
            if (name != null && standard.TryGetValue(name, out var scalar)) return scalar;
            if (name == null || string.IsNullOrWhiteSpace(name) || name.Length > 512) throw new ArgumentException("Supply an exact public Siemens type name.");
            var matches = LoadApiAssemblies().Assemblies.Select(a => a.GetType(name!, false, false))
                .Where(t => t != null && ApiTrustedType(t)).ToArray();
            if (matches.Length != 1) throw new ArgumentException("The public Siemens type is unavailable or ambiguous in the trusted referenced assemblies; see ListApiTypes for assembly status.");
            return matches[0]!;
        }

        public object ListApiTypes(string filter, string namespacePrefix, int limit, string cursor)
        {
            if (filter == null || namespacePrefix == null || filter.Length > 256 || namespacePrefix.Length > 256)
                throw new ArgumentException("Type filter and namespace prefix must each contain at most 256 characters.");
            var context = ApiProjectScope();
            var catalogue = LoadApiAssemblies();
            var types = new List<Type>();
            foreach (var assembly in catalogue.Assemblies)
            {
                try { types.AddRange(assembly.GetTypes()); }
                catch (ReflectionTypeLoadException ex)
                {
                    types.AddRange(ex.Types.Where(t => t != null).Cast<Type>());
                    catalogue.Issues.Add(new { assembly = assembly.FullName, status = "ReflectionTypeLoadException", unavailableTypes = ex.Types.Count(t => t == null) });
                }
                catch (Exception ex) when (ex is System.IO.FileLoadException || ex is System.IO.FileNotFoundException || ex is BadImageFormatException)
                {
                    catalogue.Issues.Add(new { assembly = assembly.FullName, status = ex.GetType().Name });
                }
            }
            var rows = types.Where(ApiTrustedType).Where(t => (t.FullName ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 &&
                (t.Namespace ?? "").StartsWith(namespacePrefix, StringComparison.Ordinal))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Assembly.FullName, StringComparer.Ordinal).ToArray();
            var scope = "api-types:" + context + "\n" + filter + "\n" + namespacePrefix + "\n" + string.Join("\n", rows.Select(t => t.Assembly.FullName + ":" + t.FullName));
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > rows.Length) throw new ArgumentException("The cursor is outside the available types.");
            var items = rows.Skip(offset).Take(request.GetBoundedLimit()).Select(t => new {
                fullName = t.FullName, assembly = t.Assembly.FullName,
                kind = t.IsEnum ? "enum" : typeof(Delegate).IsAssignableFrom(t) ? "delegate" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class",
                isAbstract = t.IsAbstract, isStatic = t.IsAbstract && t.IsSealed, isGenericDefinition = t.IsGenericTypeDefinition,
                staticAnchor = t.ContainsGenericParameters ? null : "$type:" + t.FullName
            }).ToArray();
            var more = offset + items.Length < rows.Length;
            return new { items, page = new PageInfo { Returned = items.Length, HasMore = more, NextCursor = more ? PageRequest.CreateCursor(offset + items.Length, scope) : null },
                assembliesInspected = catalogue.Assemblies.Count, assemblyIssues = catalogue.Issues, referenceLimitReached = catalogue.Truncated,
                note = "Only loaded or exactly referenced Siemens Engineering assemblies are inspected. Generic type definitions cannot be used as closed type anchors." };
        }

        internal string ApiProjectScope()
        {
            if (_portal == null || !IsPortalConnectionHealthy()) throw new ArgumentException("No healthy TIA Portal session is attached.");
            RefreshSelectedProject();
            ExpireApiEventsForSelection();
            return _project?.Path?.FullName ?? "";
        }

        internal void RequireApiProjectScope(string expectedProjectPath)
        {
            ProfileAccessPolicy.DemandWrite("Public API mutation");
            if (expectedProjectPath == null) throw new ArgumentException("Supply the exact project path, or an empty string when no project is open.");
            var previous = _project;
            var path = ApiProjectScope();
            if (previous != null && !SameProject(previous, _project)) throw new ArgumentException("The selected project was closed or changed.");
            if (_project != null)
            {
                if (!PathsEqual(path, expectedProjectPath)) throw new ArgumentException("The selected project differs from the expected project path.");
            }
            else if (expectedProjectPath.Length != 0 || _portal!.Projects.Any() || _portal.LocalSessions.Any())
                throw new ArgumentException("No project is selected or open projects are ambiguous; select the exact project first.");
        }

        internal string ApiContextScope() => ApiProjectScope();
        internal void RequireApiContext(string expectedProjectPath) => RequireApiProjectScope(expectedProjectPath);

        internal object ResolveApiTarget(string entityRef) => ResolveApiReference(entityRef);
        internal object? ProjectApiValue(object? value, string parentRef) => ApiProject(value, parentRef);

        private object ResolveApiReference(string entityRef, int depth = 0)
        {
            if (entityRef == "$portal") { ApiProjectScope(); return _portal!; }
            if (entityRef.StartsWith("$type:", StringComparison.Ordinal)) { ApiProjectScope(); var type = ResolveApiType(entityRef.Substring(6)); if (!ApiTrustedType(type)) throw new ArgumentException("The type anchor requires a public Siemens type."); return type; }
            if (depth > MaxApiDepth) throw new ArgumentException("The API reference exceeds the maximum depth.");
            if (!entityRef.StartsWith("api:", StringComparison.Ordinal)) return ResolveNativeReference(entityRef, out _);
            var project = ApiProjectScope();
            ApiHandle handle;
            lock (_apiHandleSync)
                if (!_apiHandles.TryGetValue(entityRef, out handle!)) throw new ArgumentException("The API reference expired or was not issued by this session.");
            if (!ReferenceEquals(handle.Portal, _portal) || !(handle.Project == null ? _project == null : _project != null && SameNativeObject(handle.Project, _project)) ||
                !string.Equals(handle.ProjectPath, project, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The API reference belongs to another project or Portal session.");
            if (handle.Replay != null || handle.Parent.StartsWith("api:", StringComparison.Ordinal))
            {
                var parent = ResolveApiReference(handle.Parent, depth + 1);
                if (handle.Replay != null)
                {
                    var actual = handle.Replay(parent);
                    if (actual == null || !SameNativeObject(handle.Target, actual)) throw new ArgumentException("The API reference is stale; browse its parent again.");
                }
            }
            return handle.Target;
        }

        private string IssueApiReference(object target, string parent, Func<object, object?>? replay)
        {
            if (ApiContracts(target).Length == 0 && !(target is IEnumerable)) throw new ArgumentException("The result is not a public Siemens Engineering object or collection and cannot be referenced.");
            var projectPath = ApiProjectScope();
            var project = _project;
            var portal = _portal ?? throw new ArgumentException("No Portal session is connected.");
            var id = "api:" + Guid.NewGuid().ToString("N");
            lock (_apiHandleSync)
            {
                var protectedIds = new HashSet<string>(StringComparer.Ordinal);
                var ancestor = parent;
                while (ancestor.StartsWith("api:", StringComparison.Ordinal))
                {
                    if (protectedIds.Count >= MaxApiDepth || !_apiHandles.TryGetValue(ancestor, out var entry))
                        throw new ArgumentException("The API reference expired or exceeds the maximum depth.");
                    protectedIds.Add(ancestor);
                    ancestor = entry.Parent;
                }
                while (_apiHandles.Count >= MaxApiHandles)
                {
                    var oldest = _apiHandleOrder.Dequeue();
                    if (protectedIds.Contains(oldest)) _apiHandleOrder.Enqueue(oldest);
                    else _apiHandles.Remove(oldest);
                }
                _apiHandles.Add(id, new ApiHandle { Target = target, Project = project, Portal = portal, ProjectPath = projectPath, Parent = parent, Replay = replay });
                _apiHandleOrder.Enqueue(id);
            }
            return id;
        }

        private object? ApiProject(object? value, string parent, Func<object, object?>? replay = null, int depth = 0)
        {
            if (depth > 16) throw new ArgumentException("Projected value nesting exceeds the limit.");
            var type = value?.GetType();
            if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var key = type.GetProperty("Key")!;
                var item = type.GetProperty("Value")!;
                Func<object, object?>? replayKey = replay == null ? null : source => { var pair = replay(source); return pair == null ? null : key.GetValue(pair); };
                Func<object, object?>? replayValue = replay == null ? null : source => { var pair = replay(source); return pair == null ? null : item.GetValue(pair); };
                return new { key = ApiProject(key.GetValue(value), parent, replayKey, depth + 1), value = ApiProject(item.GetValue(value), parent, replayValue, depth + 1) };
            }
            return PublicApiValueCodec.Project(value, item => IssueApiReference(item, parent, ReferenceEquals(item, value) ? replay : null));
        }

        private static object ApiMethodSchema(MethodInfo m)
        {
            return new { kind = "method", id = ApiMemberId(m), name = m.Name, declaringType = m.DeclaringType!.FullName,
                genericParameters = m.IsGenericMethodDefinition ? m.GetGenericArguments().Select(t => t.Name).ToArray() : Array.Empty<string>(), returns = PublicApiValueCodec.Schema(m.ReturnType), parameters = m.GetParameters().Select(p => new { name = p.Name, type = PublicApiValueCodec.Schema(p.ParameterType), optional = p.IsOptional, isOut = p.IsOut }).ToArray(),
                effects = "unclassified native effects; invocation requires ReadWrite and explicit acceptance" };
        }

        private static object ApiPropertySchema(PropertyInfo p)
        {
            return new { kind = "property", id = ApiMemberId(p), name = p.Name, declaringType = p.DeclaringType!.FullName,
                type = PublicApiValueCodec.Schema(p.PropertyType), readable = p.GetMethod?.IsPublic == true, writable = p.SetMethod?.IsPublic == true,
                index = p.GetIndexParameters().Select(x => new { name = x.Name, type = PublicApiValueCodec.Schema(x.ParameterType) }).ToArray() };
        }

        public object DescribeApiObject(string entityRef, string filter, int limit, string cursor)
        {
            if (filter == null || filter.Length > 256) throw new ArgumentException("The member filter must contain at most 256 characters.");
            var target = ResolveApiReference(entityRef);
            var constructors = target is Type selectedType && selectedType != typeof(TiaPortal)
                ? selectedType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Select(c => (key: "C:" + ApiMemberId(c), schema: (object)new { kind = "constructor", id = ApiMemberId(c), name = ".ctor", declaringType = selectedType.FullName, parameters = c.GetParameters().Select(p => new { name = p.Name, type = PublicApiValueCodec.Schema(p.ParameterType), optional = p.IsOptional, isOut = p.IsOut }).ToArray(), effects = "unclassified native effects" }))
                : Enumerable.Empty<(string key, object schema)>();
            var fields = ApiFields(target).Where(f => f.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => (key: "F:" + f.Name + ":" + ApiMemberId(f), schema: ApiFieldSchema(f)));
            var services = target is IEngineeringServiceProvider provider
                ? provider.GetServiceInfos().Select(x => x.Type.FullName ?? x.Type.Name).Where(x => x.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(x => (key: "S:" + x, schema: (object)new { kind = "service", typeName = x }))
                : Enumerable.Empty<(string key, object schema)>();
            var members = ApiProperties(target).Where(p => p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(p => (key: "P:" + p.Name + ":" + ApiMemberId(p), schema: ApiPropertySchema(p)))
                .Concat(ApiMethods(target).Where(m => m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => (key: "M:" + m.Name + ":" + ApiMemberId(m), schema: ApiMethodSchema(m))))
                .Concat(fields).Concat(constructors).Concat(services).Concat(ApiEventSchemas(target).Where(x => x.key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(x => x.key, StringComparer.Ordinal).ToArray();
            var scope = "api-schema:" + ApiProjectScope() + "\n" + entityRef + "\n" + filter + "\n" + string.Join("\n", members.Select(x => x.key));
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > members.Length) throw new ArgumentException("The cursor is outside the member schema.");
            var rows = members.Skip(offset).Take(request.GetBoundedLimit()).Select(x => x.schema).ToArray();
            var more = offset + rows.Length < members.Length;
            return new { type = (target as Type ?? target.GetType()).FullName, contracts = ApiContracts(target).Select(t => t.FullName).ToArray(),
                items = rows, page = new PageInfo { Returned = rows.Length, HasMore = more, NextCursor = more ? PageRequest.CreateCursor(offset + rows.Length, scope) : null },
                note = "Member identifiers select an exact declaring contract and signature. Method effects are unclassified. References are bounded and session-scoped." };
        }

        private object?[] ApiIndices(PropertyInfo property, JsonElement indices)
        {
            var parameters = property.GetIndexParameters();
            if (parameters.Length == 0 && (indices.ValueKind == JsonValueKind.Undefined || indices.ValueKind == JsonValueKind.Null)) return Array.Empty<object>();
            if (indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() != parameters.Length) throw new ArgumentException("Supply one index per indexer parameter in schema order.");
            return indices.EnumerateArray().Select((value, i) => {
                var type = parameters[i].ParameterType;
                if (!(type.IsPrimitive || type.IsEnum || type == typeof(string))) throw new ArgumentException("Indexer inputs support only primitive, string and enum values.");
                return PublicApiValueCodec.Convert(value, type, r => ResolveApiReference(r), ResolveApiType);
            }).ToArray();
        }

        public object? ReadApiProperty(string entityRef, string propertyId, JsonElement indices)
        {
            var target = ResolveApiReference(entityRef);
            var p = ApiProperties(target).SingleOrDefault(x => ApiMemberId(x) == propertyId) ?? throw new ArgumentException("No matching public property schema exists on this target.");
            if (p.GetMethod?.IsPublic != true) throw new ArgumentException("The selected property needs a public getter.");
            if (ApiSensitive(p.Name)) return "<redacted>";
            var indexValues = ApiIndices(p, indices);
            return ApiProject(p.GetValue(target is Type ? null : target, indexValues), entityRef, parent => p.GetValue(parent is Type ? null : parent, indexValues));
        }

        public object? ReadApiField(string entityRef, string fieldId)
        {
            var target = ResolveApiReference(entityRef);
            var field = ApiFields(target).SingleOrDefault(f => ApiMemberId(f) == fieldId) ?? throw new ArgumentException("No matching public field schema exists on this target.");
            if (ApiSensitive(field.Name)) return "<redacted>";
            return ApiProject(field.GetValue(target is Type ? null : target), entityRef, parent => field.GetValue(parent is Type ? null : parent));
        }

        private static object? ApiCollectionAt(object target, int index)
        {
            if (!(target is IEnumerable items)) throw new ArgumentException("The target is not an enumerable Siemens collection.");
            var enumerator = items.GetEnumerator();
            try
            {
                for (var i = 0; i <= index; i++) if (!enumerator.MoveNext()) throw new ArgumentException("The collection changed; browse it again.");
                return enumerator.Current;
            }
            finally { (enumerator as IDisposable)?.Dispose(); }
        }

        public object BrowseApiCollection(string entityRef, int limit, string cursor)
        {
            var target = ResolveApiReference(entityRef);
            if (!(target is IEnumerable)) throw new ArgumentException("The reference is not an enumerable Siemens collection.");
            var scope = "api-collection:" + ApiProjectScope() + "\n" + entityRef;
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset >= MaxApiCollection) throw new ArgumentException("The collection exceeds the 5000-item navigation limit.");
            var rows = new List<object?>();
            var count = Math.Min(request.GetBoundedLimit(), MaxApiCollection - offset);
            var enumerator = ((IEnumerable)target).GetEnumerator();
            var hasMore = false;
            try
            {
                for (var i = 0; i < offset; i++) if (!enumerator.MoveNext()) throw new ArgumentException("The collection changed; browse it again.");
                for (var i = 0; i < count && enumerator.MoveNext(); i++)
                {
                    var index = offset + i;
                    rows.Add(ApiProject(enumerator.Current, entityRef, parent => ApiCollectionAt(parent, index)));
                }
                hasMore = enumerator.MoveNext();
            }
            finally { (enumerator as IDisposable)?.Dispose(); }
            var truncated = hasMore && offset + rows.Count >= MaxApiCollection;
            return new { items = rows, page = new PageInfo { Returned = rows.Count, HasMore = hasMore && !truncated,
                NextCursor = hasMore && !truncated ? PageRequest.CreateCursor(offset + rows.Count, scope) : null }, truncated,
                note = "Paging uses collection order; changes between pages can alter results. Child references are validated against their original index." };
        }

        public object? GetApiService(string entityRef, string serviceType)
        {
            var target = ResolveApiReference(entityRef);
            if (!(target is IEngineeringServiceProvider provider)) throw new ArgumentException("This target does not expose declared engineering services.");
            var matches = provider.GetServiceInfos().Where(s => string.Equals(s.Type.FullName, serviceType, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || !ApiTrustedType(matches[0].Type)) throw new ArgumentException("Select one exact declared public Siemens service type.");
            var method = typeof(IEngineeringServiceProvider).GetMethods().Single(m => m.Name == "GetService" && m.IsGenericMethodDefinition).MakeGenericMethod(matches[0].Type);
            return ApiProject(method.Invoke(provider, null), entityRef, parent => method.Invoke(parent, null));
        }

#if TIA_MCP_READ_WRITE
        private object? ApiProjectCompleted(object? value, string parent)
        {
            try { return ApiProject(value, parent); }
            catch (Exception) { return new { unavailable = true, type = value?.GetType().FullName, reason = "OutputProjectionFailed", nativeCallCompleted = true }; }
        }

        public object? InvokeApiMethod(string entityRef, string methodId, JsonElement arguments, string expectedProjectPath, bool acceptNativeEffects, string[] genericTypes)
        {
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified native effects.");
            var target = ResolveApiReference(entityRef);
            var method = ApiMethods(target).SingleOrDefault(m => ApiMemberId(m) == methodId) ?? throw new ArgumentException("No matching public method schema exists on this target.");
            if (target is Type selected && selected == typeof(TiaPortal)) throw new ArgumentException("Portal lifetime is managed by the connection tools.");
            if (method.IsGenericMethodDefinition)
            {
                if (genericTypes == null || genericTypes.Length != method.GetGenericArguments().Length) throw new ArgumentException("Supply one exact loaded public Siemens type for each generic parameter.");
                method = method.MakeGenericMethod(genericTypes.Select(ResolveApiType).ToArray());
            }
            else if (genericTypes != null && genericTypes.Length != 0) throw new ArgumentException("This method is not generic.");
            var parameters = method.GetParameters();
            if (arguments.ValueKind != JsonValueKind.Array || arguments.GetArrayLength() != parameters.Length) throw new ArgumentException("Supply one argument per parameter in schema order, including optional parameters.");
            var values = new object?[parameters.Length];
            var index = 0;
            foreach (var input in arguments.EnumerateArray())
            {
                var parameter = parameters[index];
                var valueType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
                values[index] = parameter.IsOut ? (valueType.IsValueType ? Activator.CreateInstance(valueType) : null)
                    : PublicApiValueCodec.Convert(input, valueType, reference => ResolveApiReference(reference), ResolveApiType);
                index++;
            }
            object? result;
            try { result = method.Invoke(target is Type ? null : target, values); }
            catch (Exception) { throw new InvalidOperationException("The native method failed and may have partially applied effects. Inspect the state before retrying."); }
            return new { nativeCallCompleted = true, result = ApiSensitive(method.Name) ? "<redacted>" : ApiProjectCompleted(result, entityRef), outputs = parameters.Select((p, i) => new { p, i }).Where(x => x.p.ParameterType.IsByRef).Select(x => new { name = x.p.Name, value = ApiSensitive(x.p.Name ?? "") ? "<redacted>" : ApiProjectCompleted(values[x.i], entityRef) }).ToArray() };
        }

        public object? CreateApiObject(string typeName, string constructorId, JsonElement arguments, string expectedProjectPath, bool acceptNativeEffects)
        {
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified native effects.");
            var type = ResolveApiType(typeName);
            if (!ApiTrustedType(type)) throw new ArgumentException("Constructors require a public Siemens Engineering type.");
            if (type == typeof(TiaPortal)) throw new ArgumentException("Portal lifetime is managed by the connection tools.");
            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SingleOrDefault(c => ApiMemberId(c) == constructorId)
                ?? throw new ArgumentException("No matching public constructor schema exists for this type.");
            var parameters = constructor.GetParameters();
            if (arguments.ValueKind != JsonValueKind.Array || arguments.GetArrayLength() != parameters.Length) throw new ArgumentException("Supply one argument per parameter in schema order.");
            var values = arguments.EnumerateArray().Select((input, i) => {
                var p = parameters[i];
                var valueType = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                return p.IsOut ? (valueType.IsValueType ? Activator.CreateInstance(valueType) : null)
                    : PublicApiValueCodec.Convert(input, valueType, r => ResolveApiReference(r), ResolveApiType);
            }).ToArray();
            object? result;
            try { result = constructor.Invoke(values); }
            catch (Exception) { throw new InvalidOperationException("The native constructor failed and may have partially applied effects. Inspect the state before retrying."); }
            return new { nativeCallCompleted = true, result = ApiProjectCompleted(result, "$portal"), outputs = parameters.Select((p, i) => new { p, i }).Where(x => x.p.ParameterType.IsByRef)
                .Select(x => new { name = x.p.Name, value = ApiSensitive(x.p.Name ?? "") ? "<redacted>" : ApiProjectCompleted(values[x.i], "$portal") }).ToArray() };
        }

        public object ReleaseApiObject(string entityRef, string expectedProjectPath, bool acceptNativeEffects)
        {
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified native effects.");
            if (entityRef == null || !entityRef.StartsWith("api:", StringComparison.Ordinal)) throw new ArgumentException("Only issued API references can be released.");
            var target = ResolveApiReference(entityRef);
            if (target is TiaPortal || ApiContracts(target).Length == 0 || !(target is IDisposable disposable))
                throw new ArgumentException("Release requires a disposable public Siemens object other than the Portal connection.");
            try { disposable.Dispose(); }
            catch (Exception ex) { throw new InvalidOperationException("Release failed and may have had partial native effects; the reference was invalidated.", ex); }
            finally
            {
                lock (_apiHandleSync)
                {
                    _apiHandles.Remove(entityRef);
                    var retained = _apiHandleOrder.Where(id => _apiHandles.ContainsKey(id)).ToArray();
                    _apiHandleOrder.Clear();
                    foreach (var id in retained) _apiHandleOrder.Enqueue(id);
                }
            }
            return new { released = true };
        }

        public object? WriteApiField(string entityRef, string fieldId, JsonElement expectedValue, JsonElement value, string expectedProjectPath, bool acceptNativeEffects)
        {
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified native effects.");
            var target = ResolveApiReference(entityRef);
            var field = ApiFields(target).SingleOrDefault(f => ApiMemberId(f) == fieldId) ?? throw new ArgumentException("No matching public field schema exists on this target.");
            if (field.IsInitOnly || field.IsLiteral) throw new ArgumentException("The selected field is read-only.");
            var expected = PublicApiValueCodec.Convert(expectedValue, field.FieldType, r => ResolveApiReference(r), ResolveApiType);
            var replacement = PublicApiValueCodec.Convert(value, field.FieldType, r => ResolveApiReference(r), ResolveApiType);
            var instance = target is Type ? null : target;
            if (!PublicApiValueCodec.ValueEquals(field.GetValue(instance), expected)) throw new ArgumentException("The current field value differs from expectedValue; read it again.");
            field.SetValue(instance, replacement);
            if (ApiSensitive(field.Name)) return new { written = true, value = "<redacted>" };
            try { return new { written = true, value = ApiProject(field.GetValue(instance), entityRef, parent => field.GetValue(parent is Type ? null : parent)) }; }
            catch (Exception) { return new { written = true, readbackUnavailable = true }; }
        }

        public object? WriteApiProperty(string entityRef, string propertyId, JsonElement expectedValue, JsonElement value, string expectedProjectPath, bool acceptNativeEffects, JsonElement indices)
        {
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified native effects.");
            var target = ResolveApiReference(entityRef);
            var p = ApiProperties(target).SingleOrDefault(x => ApiMemberId(x) == propertyId) ?? throw new ArgumentException("No matching public property schema exists on this target.");
            if (p.SetMethod?.IsPublic != true) throw new ArgumentException("A public writable property is required.");
            var indexValues = ApiIndices(p, indices);
            var replacement = PublicApiValueCodec.Convert(value, p.PropertyType, reference => ResolveApiReference(reference), ResolveApiType);
            if (p.GetMethod?.IsPublic == true)
            {
                var expected = PublicApiValueCodec.Convert(expectedValue, p.PropertyType, reference => ResolveApiReference(reference), ResolveApiType);
                if (!PublicApiValueCodec.ValueEquals(p.GetValue(target is Type ? null : target, indexValues), expected)) throw new ArgumentException("The current property value differs from expectedValue; read it again.");
            }
            else if (expectedValue.ValueKind != JsonValueKind.Null) throw new ArgumentException("For a write-only property expectedValue must be null; the prior value cannot be checked.");
            p.SetValue(target is Type ? null : target, replacement, indexValues);
            if (p.GetMethod?.IsPublic != true) return new { written = true, previousValueChecked = false };
            if (ApiSensitive(p.Name)) return new { written = true, previousValueChecked = true, value = "<redacted>" };
            try { return new { written = true, previousValueChecked = true, value = ApiProject(p.GetValue(target is Type ? null : target, indexValues), entityRef, parent => p.GetValue(parent is Type ? null : parent, indexValues)) }; }
            catch (Exception) { return new { written = true, previousValueChecked = true, readbackUnavailable = true }; }
        }
#endif
    }
}
