using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Text.Json;
using TiaMcpServer.Runtime;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    internal static class PublicApiValueCodec
    {
        private const int MaxItems = 1024;
        private const int MaxString = 32768;
        private const int MaxDepth = 16;

        internal static object? Convert(JsonElement value, Type targetType, Func<string, object> resolve, Func<string, Type>? resolveType = null)
        {
            return ConvertValue(value, targetType, resolve, resolveType, 0);
        }

        private static object? ConvertValue(JsonElement value, Type type, Func<string, object> resolve, Func<string, Type>? resolveType, int depth)
        {
            if (depth > MaxDepth) throw new ArgumentException("Value nesting exceeds the limit.");
            if (type.IsByRef) type = type.GetElementType()!;
            var nullable = Nullable.GetUnderlyingType(type);
            if (value.ValueKind == JsonValueKind.Null)
            {
                if (!type.IsValueType || nullable != null) return null;
                throw new ArgumentException("Null is not valid for " + type.FullName + ".");
            }
            if (nullable != null) return ConvertValue(value, nullable, resolve, resolveType, depth + 1);
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$ref", out var reference))
            {
                DemandOnly(value, "$ref", "type");
                var result = resolve(ReadString(reference));
                if (result == null || !type.IsInstanceOfType(result)) throw new ArgumentException("Reference is not assignable to " + type.FullName + ".");
                if (value.TryGetProperty("type", out var description) && !string.Equals(ReadString(description), result.GetType().FullName ?? result.GetType().Name, StringComparison.Ordinal))
                    throw new ArgumentException("Reference type description does not match the resolved object.");
                return result;
            }
            if (type == typeof(object))
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.String: return ReadString(value);
                    case JsonValueKind.True: return true;
                    case JsonValueKind.False: return false;
                    case JsonValueKind.Number:
                        if (value.TryGetInt64(out var integer)) return integer;
                        if (value.TryGetUInt64(out var unsignedInteger)) return unsignedInteger;
                        if (value.TryGetDecimal(out var decimalNumber)) return decimalNumber;
                        var number = value.GetDouble();
                        if (double.IsNaN(number) || double.IsInfinity(number)) throw new ArgumentException("Floating point values must be finite.");
                        return number;
                    case JsonValueKind.Object:
                        DemandOnly(value, "$type", "value");
                        if (resolveType == null) throw new ArgumentException("Typed object values require the bridge's allowed Siemens type resolver.");
                        var explicitType = resolveType(ReadString(value.GetProperty("$type")));
                        if (!IsSiemensPublic(explicitType) && !IsScalarType(explicitType)) throw new ArgumentException("Typed object values require an allowed public Siemens type or a supported scalar type.");
                        return ConvertValue(value.GetProperty("value"), explicitType, resolve, resolveType, depth + 1);
                    default: throw new ArgumentException("Object inputs require a scalar, an issued reference, or an explicit allowed Siemens type and value.");
                }
            }
            if (type == typeof(string)) return ReadString(value);
            if (type == typeof(bool)) return value.GetBoolean();
            if (type == typeof(char))
            {
                var text = ReadString(value);
                if (text.Length != 1) throw new ArgumentException("A character must contain exactly one UTF-16 code unit.");
                return text[0];
            }
            if (type == typeof(byte)) return checked((byte)value.GetUInt32());
            if (type == typeof(sbyte)) return checked((sbyte)value.GetInt32());
            if (type == typeof(short)) return checked((short)value.GetInt32());
            if (type == typeof(ushort)) return checked((ushort)value.GetUInt32());
            if (type == typeof(int)) return value.GetInt32();
            if (type == typeof(uint)) return value.GetUInt32();
            if (type == typeof(long)) return value.GetInt64();
            if (type == typeof(ulong)) return value.GetUInt64();
            if (type == typeof(decimal)) return value.GetDecimal();
            if (type == typeof(double))
            {
                var number = value.GetDouble();
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new ArgumentException("Floating point values must be finite.");
                return number;
            }
            if (type == typeof(float))
            {
                var number = value.GetSingle();
                if (float.IsNaN(number) || float.IsInfinity(number)) throw new ArgumentException("Floating point values must be finite.");
                return number;
            }
            if (type.IsEnum)
            {
                var name = ReadString(value);
                var names = name.Split(',').Select(n => n.Trim()).ToArray();
                if (names.Length == 0 || names.Any(n => !Enum.GetNames(type).Contains(n, StringComparer.Ordinal)) || (names.Length > 1 && !type.IsDefined(typeof(FlagsAttribute), false)))
                    throw new ArgumentException("An enum value must use declared names of " + type.FullName + "; combinations require FlagsAttribute.");
                return Enum.Parse(type, string.Join(", ", names), false);
            }
            if (type == typeof(Guid)) return Guid.Parse(ReadString(value));
            if (type == typeof(CultureInfo))
            {
                var name = ReadString(value);
                if (name.Length > 128) throw new ArgumentException("Culture name exceeds the length limit.");
                return CultureInfo.GetCultureInfo(name);
            }
            if (type == typeof(Version)) return Version.Parse(ReadString(value));
            if (type == typeof(DateTime)) return DateTime.Parse(ReadString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(ReadString(value), CultureInfo.InvariantCulture, DateTimeStyles.None);
            if (type == typeof(TimeSpan)) return TimeSpan.Parse(ReadString(value), CultureInfo.InvariantCulture);
            if (type == typeof(FileInfo)) return new FileInfo(OutputPathPolicy.ResolveApiPath(ReadString(value)));
            if (type == typeof(DirectoryInfo)) return new DirectoryInfo(OutputPathPolicy.ResolveApiPath(ReadString(value)));
            if (type == typeof(SecureString))
            {
                var secret = new SecureString();
                foreach (var c in ReadString(value)) secret.AppendChar(c);
                secret.MakeReadOnly();
                return secret;
            }
            if (type == typeof(Type))
            {
                if (resolveType == null) throw new ArgumentException("Type values require the bridge's allowed Siemens type resolver.");
                var resolved = resolveType(ReadString(value));
                if (!IsSiemensPublic(resolved)) throw new ArgumentException("Type value is not an allowed public Siemens type.");
                return resolved;
            }
            if (typeof(Delegate).IsAssignableFrom(type)) return MakeCallback(value, type, resolve, resolveType, depth + 1);
            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                if (rank > 8) throw new ArgumentException("Array rank exceeds the limit of eight.");
                var lengths = Enumerable.Repeat(-1, rank).ToArray();
                var total = 0;
                InspectArray(value, lengths, 0, ref total);
                for (var i = 0; i < lengths.Length; i++) if (lengths[i] < 0) lengths[i] = 0;
                var array = Array.CreateInstance(type.GetElementType()!, lengths);
                FillArray(value, array, new int[rank], 0, type.GetElementType()!, resolve, resolveType, depth + 1);
                return array;
            }
            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();
                if (definition == typeof(KeyValuePair<,>))
                {
                    DemandObject(value);
                    DemandOnly(value, "key", "value");
                    return Activator.CreateInstance(type, ConvertValue(value.GetProperty("key"), args[0], resolve, resolveType, depth + 1), ConvertValue(value.GetProperty("value"), args[1], resolve, resolveType, depth + 1));
                }
                if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                {
                    var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
                    foreach (var entry in ReadArray(value))
                    {
                        DemandObject(entry);
                        DemandOnly(entry, "key", "value");
                        dictionary.Add(ConvertValue(entry.GetProperty("key"), args[0], resolve, resolveType, depth + 1)!, ConvertValue(entry.GetProperty("value"), args[1], resolve, resolveType, depth + 1));
                    }
                    return dictionary;
                }
                if (definition == typeof(List<>) || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IList<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>))
                {
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args))!;
                    foreach (var item in ReadArray(value)) list.Add(ConvertValue(item, args[0], resolve, resolveType, depth + 1));
                    return list;
                }
            }
            if (!IsSiemensPublic(type) || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                throw new ArgumentException("Unsupported input type " + type.FullName + "; use an assignable issued reference where available.");
            return Construct(value, type, resolve, resolveType, depth + 1);
        }

        private static object Construct(JsonElement value, Type type, Func<string, object> resolve, Func<string, Type>? resolveType, int depth)
        {
            if (type == typeof(global::Siemens.Engineering.TiaPortal)) throw new ArgumentException("Portal lifetime is managed by the connection tools.");
            DemandObject(value);
            var fields = value.EnumerateObject().ToArray();
            if (fields.Length > MaxItems || fields.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw new ArgumentException("Too many or duplicate object fields.");
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.SetMethod != null && p.SetMethod.IsPublic).ToDictionary(p => p.Name, StringComparer.Ordinal);
            var candidates = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Where(c => c.GetParameters().All(p => p.Name != null && fields.Any(f => string.Equals(f.Name, p.Name, StringComparison.OrdinalIgnoreCase)))).ToArray();
            var matching = candidates.Where(c => fields.All(f => properties.ContainsKey(f.Name) || c.GetParameters().Any(p => string.Equals(f.Name, p.Name, StringComparison.OrdinalIgnoreCase)))).OrderByDescending(c => c.GetParameters().Length).ToArray();
            if (matching.Length == 0 && type.IsValueType && fields.All(f => properties.ContainsKey(f.Name)))
            {
                var boxed = Activator.CreateInstance(type)!;
                foreach (var f in fields) properties[f.Name].SetValue(boxed, ConvertValue(f.Value, properties[f.Name].PropertyType, resolve, resolveType, depth + 1));
                return boxed;
            }
            if (matching.Length == 0 || (matching.Length > 1 && matching[0].GetParameters().Length == matching[1].GetParameters().Length)) throw new ArgumentException("No unique public constructor matches fields for " + type.FullName + ".");
            var ctor = matching[0];
            var parameters = ctor.GetParameters();
            var args = parameters.Select(p => ConvertValue(fields.Single(f => string.Equals(f.Name, p.Name, StringComparison.OrdinalIgnoreCase)).Value, p.ParameterType, resolve, resolveType, depth + 1)).ToArray();
            var result = ctor.Invoke(args);
            foreach (var field in fields.Where(f => !parameters.Any(p => string.Equals(f.Name, p.Name, StringComparison.OrdinalIgnoreCase))))
                properties[field.Name].SetValue(result, ConvertValue(field.Value, properties[field.Name].PropertyType, resolve, resolveType, depth + 1));
            return result;
        }

        private static object MakeCallback(JsonElement value, Type type, Func<string, object> resolve, Func<string, Type>? resolveType, int depth)
        {
            DemandObject(value);
            DemandOnly(value, "$callback");
            var rule = value.GetProperty("$callback").Clone();
            DemandObject(rule);
            var invoke = type.GetMethod("Invoke") ?? throw new ArgumentException("Invalid delegate type.");
            var signature = invoke.GetParameters();
            if (rule.TryGetProperty("cases", out var cases)) return MakeDispatchCallback(rule, cases, type, signature, resolve, resolveType, depth);
            DemandOnly(rule, "return", "set", "argument", "out");
            if (rule.TryGetProperty("out", out var outputRules))
            {
                DemandObject(outputRules);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in outputRules.EnumerateObject())
                {
                    if (!names.Add(item.Name) || !signature.Any(p => p.Name == item.Name && p.ParameterType.IsByRef && !(p.IsIn && !p.IsOut))) throw new ArgumentException("Callback out rule must name a unique ref or out parameter: " + item.Name);
                }
            }
            foreach (var parameter in signature.Where(p => p.IsOut))
                if (!rule.TryGetProperty("out", out var outputs) || parameter.Name == null || !outputs.TryGetProperty(parameter.Name, out _)) throw new ArgumentException("A callback out rule is required for " + parameter.Name + ".");
            var taskReturn = typeof(System.Threading.Tasks.Task).IsAssignableFrom(invoke.ReturnType);
            var taskResult = invoke.ReturnType.IsGenericType && invoke.ReturnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>) ? invoke.ReturnType.GetGenericArguments()[0] : null;
            if (taskReturn && invoke.ReturnType != typeof(System.Threading.Tasks.Task) && taskResult == null) throw new ArgumentException("This callback Task subtype is unsupported.");
            var valueReturn = taskReturn ? taskResult : invoke.ReturnType;
            var index = rule.TryGetProperty("argument", out var argument) ? argument.GetInt32() : 0;
            if (rule.TryGetProperty("set", out var setters))
            {
                if (index < 0 || index >= signature.Length || signature[index].IsOut) throw new ArgumentException("Callback assignment requires an available argument.");
                DemandObject(setters);
                if (setters.EnumerateObject().Count() > MaxItems) throw new ArgumentException("Too many callback assignments.");
                foreach (var setter in setters.EnumerateObject())
                {
                    var callbackType = signature[index].ParameterType.IsByRef ? signature[index].ParameterType.GetElementType()! : signature[index].ParameterType;
                    var prop = callbackType.GetProperty(setter.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        if (prop.SetMethod == null || !prop.SetMethod.IsPublic || prop.GetIndexParameters().Length != 0) throw new ArgumentException("Callback property is not publicly writable: " + setter.Name);
                    }
                }
            }
            object? returnValue = null;
            if (valueReturn != null && valueReturn != typeof(void))
            {
                if (!rule.TryGetProperty("return", out var returnRule)) throw new ArgumentException("A callback return value is required.");
                if (returnRule.ValueKind == JsonValueKind.Object && returnRule.TryGetProperty("$default", out var defaultRule))
                {
                    DemandOnly(returnRule, "$default");
                    if (defaultRule.ValueKind != JsonValueKind.True) throw new ArgumentException("$default must be true.");
                    returnValue = valueReturn.IsValueType ? Activator.CreateInstance(valueReturn) : null;
                }
                else returnValue = ConvertValue(returnRule, valueReturn, resolve, resolveType, depth + 1);
            }
            else if (rule.TryGetProperty("return", out _)) throw new ArgumentException("A void or non-generic Task callback cannot have a return value.");
            if (taskReturn)
            {
                returnValue = taskResult == null
                    ? (object)System.Threading.Tasks.Task.CompletedTask
                    : typeof(System.Threading.Tasks.Task).GetMethod("FromResult")!.MakeGenericMethod(taskResult).Invoke(null, new[] { returnValue });
            }
            var handler = new CallbackRule(rule, index, signature, resolve, resolveType, depth, returnValue);
            return BuildCallbackDelegate(type, signature, invoke.ReturnType, handler);
        }

        private static object MakeDispatchCallback(JsonElement rule, JsonElement cases, Type type, ParameterInfo[] signature, Func<string, object> resolve, Func<string, Type>? resolveType, int depth)
        {
            DemandOnly(rule, "argument", "cases", "default");
            if (resolveType == null) throw new ArgumentException("Callback cases require the bridge's allowed Siemens type resolver.");
            var index = rule.TryGetProperty("argument", out var argument) ? argument.GetInt32() : 0;
            if (index < 0 || index >= signature.Length || signature[index].IsOut) throw new ArgumentException("Callback dispatch requires an available argument.");
            var items = ReadArray(cases);
            if (items.Length == 0 || items.Length > 32) throw new ArgumentException("Callback cases must contain between one and 32 entries.");
            var policies = new Dictionary<Type, Delegate>();
            var declared = signature[index].ParameterType.IsByRef ? signature[index].ParameterType.GetElementType()! : signature[index].ParameterType;
            foreach (var item in items)
            {
                DemandObject(item);
                DemandOnly(item, "type", "policy");
                var caseType = resolveType(ReadString(item.GetProperty("type")));
                if (!IsSiemensPublic(caseType) || !declared.IsAssignableFrom(caseType)) throw new ArgumentException("Callback case type must be an assignable public Siemens type.");
                if (policies.ContainsKey(caseType)) throw new ArgumentException("Duplicate callback case type.");
                policies.Add(caseType, MakeCasePolicy(item.GetProperty("policy"), index, type, resolve, resolveType, depth));
            }
            var fallback = rule.TryGetProperty("default", out var defaultPolicy) ? MakeCasePolicy(defaultPolicy, index, type, resolve, resolveType, depth) : null;
            var handler = new CallbackDispatch(index, policies, fallback);
            return BuildCallbackDelegate(type, signature, type.GetMethod("Invoke")!.ReturnType, handler);
        }

        private static Delegate MakeCasePolicy(JsonElement policy, int index, Type type, Func<string, object> resolve, Func<string, Type> resolveType, int depth)
        {
            DemandObject(policy);
            if (policy.TryGetProperty("argument", out _) || policy.TryGetProperty("cases", out _)) throw new ArgumentException("A case policy cannot override argument or contain nested cases.");
            var raw = policy.GetRawText();
            var separator = policy.EnumerateObject().Any() ? "," : "";
            using (var document = JsonDocument.Parse("{\"$callback\":{\"argument\":" + index.ToString(CultureInfo.InvariantCulture) + separator + raw.Substring(1) + "}"))
                return (Delegate)MakeCallback(document.RootElement, type, resolve, resolveType, depth + 1);
        }

        private static object BuildCallbackDelegate(Type type, ParameterInfo[] signature, Type returnType, object handler)
        {
            var parameters = signature.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var arguments = Expression.Variable(typeof(object[]), "arguments");
            var result = Expression.Variable(typeof(object), "result");
            var initialValues = parameters.Select((p, i) => signature[i].IsOut ? (Expression)Expression.Constant(null, typeof(object)) : Expression.Convert(p, typeof(object)));
            var expressions = new List<Expression>
            {
                Expression.Assign(arguments, Expression.NewArrayInit(typeof(object), initialValues)),
                Expression.Assign(result, Expression.Call(Expression.Constant(handler), handler.GetType().GetMethod("Invoke")!, arguments))
            };
            for (var i = 0; i < signature.Length; i++)
            {
                if (signature[i].ParameterType.IsByRef && !(signature[i].IsIn && !signature[i].IsOut))
                    expressions.Add(Expression.Assign(parameters[i], Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(i)), signature[i].ParameterType.GetElementType()!)));
            }
            expressions.Add(returnType == typeof(void) ? (Expression)Expression.Empty() : Expression.Convert(result, returnType));
            return Expression.Lambda(type, Expression.Block(new[] { arguments, result }, expressions), parameters).Compile();
        }

        private sealed class CallbackDispatch
        {
            private readonly int index;
            private readonly Dictionary<Type, Delegate> policies;
            private readonly Delegate? fallback;
            internal CallbackDispatch(int index, Dictionary<Type, Delegate> policies, Delegate? fallback)
            { this.index = index; this.policies = policies; this.fallback = fallback; }
            public object? Invoke(object[] args)
            {
                var target = args[index];
                Delegate? policy = null;
                if (target != null) policies.TryGetValue(target.GetType(), out policy);
                policy = policy ?? fallback;
                if (policy == null) throw new ArgumentException("No callback policy matches runtime type " + (target?.GetType().FullName ?? "null") + ".");
                try { return policy.DynamicInvoke(args); }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }
        }

        private sealed class CallbackRule
        {
            private readonly JsonElement rule;
            private readonly int index;
            private readonly ParameterInfo[] signature;
            private readonly Func<string, object> resolve;
            private readonly Func<string, Type>? resolveType;
            private readonly int depth;
            private readonly object? result;
            internal CallbackRule(JsonElement rule, int index, ParameterInfo[] signature, Func<string, object> resolve, Func<string, Type>? resolveType, int depth, object? result)
            { this.rule = rule; this.index = index; this.signature = signature; this.resolve = resolve; this.resolveType = resolveType; this.depth = depth; this.result = result; }
            public object? Invoke(object[] args)
            {
                if (rule.TryGetProperty("set", out var setters))
                {
                    var target = args[index] ?? throw new ArgumentException("Callback argument is null.");
                    foreach (var setter in setters.EnumerateObject())
                    {
                        if (!IsSiemensPublic(target.GetType())) throw new ArgumentException("Callback runtime argument is not a public Siemens type.");
                        var property = target.GetType().GetProperty(setter.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (property == null || property.SetMethod == null || !property.SetMethod.IsPublic || property.GetIndexParameters().Length != 0) throw new ArgumentException("Callback property is not publicly writable: " + setter.Name);
                        property.SetValue(target, ConvertValue(setter.Value, property.PropertyType, resolve, resolveType, depth + 1));
                    }
                }
                if (rule.TryGetProperty("out", out var outputs))
                {
                    foreach (var output in outputs.EnumerateObject())
                    {
                        var position = Array.FindIndex(signature, p => p.Name == output.Name);
                        var elementType = signature[position].ParameterType.GetElementType()!;
                        args[position] = ConvertValue(output.Value, elementType, resolve, resolveType, depth + 1)!;
                    }
                }
                return result;
            }
        }

        internal static object? Project(object? value, Func<object, string> register)
        {
            return ProjectValue(value, register, 0);
        }

        private static object? ProjectValue(object? value, Func<object, string> register, int depth)
        {
            if (depth > MaxDepth) throw new ArgumentException("Projected value nesting exceeds the limit.");
            if (value == null) return null;
            var type = value.GetType();
            if (value is SecureString) return new { type = "SecureString", redacted = true };
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                return new { key = ProjectValue(type.GetProperty("Key")!.GetValue(value), register, depth + 1), value = ProjectValue(type.GetProperty("Value")!.GetValue(value), register, depth + 1) };
            if (type.IsEnum) return new { type = type.FullName, name = value.ToString() };
            if (value is string text) return text.Length <= MaxString ? text : throw new ArgumentException("String output exceeds the limit.");
            if (value is char) return value.ToString();
            if (value is bool || value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is decimal) return value;
            if (value is double d) return double.IsNaN(d) || double.IsInfinity(d) ? throw new ArgumentException("Non-finite output is unsupported.") : value;
            if (value is float f) return float.IsNaN(f) || float.IsInfinity(f) ? throw new ArgumentException("Non-finite output is unsupported.") : value;
            if (value is DateTime dateTime) return dateTime.ToString("O", CultureInfo.InvariantCulture);
            if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            if (value is TimeSpan timeSpan) return timeSpan.ToString("c", CultureInfo.InvariantCulture);
            if (value is Guid guid) return guid.ToString("D");
            if (value is CultureInfo culture) return culture.Name;
            if (value is Version version) return version.ToString();
            if (value is Type namedType) return new { type = "System.Type", name = namedType.FullName };
            if (value is FileInfo file) return new { type = type.FullName, path = file.FullName };
            if (value is DirectoryInfo directory) return new { type = type.FullName, path = directory.FullName };
            return new Dictionary<string, object> { ["$ref"] = register(value), ["type"] = type.FullName ?? type.Name };
        }

        internal static bool ValueEquals(object? current, object? expected)
        {
            var budget = 2 * MaxItems * MaxItems + 1;
            return ValueEqualsCore(current, expected, 0, ref budget);
        }

        private static bool ValueEqualsCore(object? current, object? expected, int depth, ref int budget)
        {
            if (depth > MaxDepth || --budget < 0) return false;
            if (ReferenceEquals(current, expected)) return true;
            if (current == null || expected == null) return false;
            if (expected is Array expectedArray)
            {
                if (!(current is Array currentArray) || expectedArray.Rank != currentArray.Rank) return false;
                for (var i = 0; i < expectedArray.Rank; i++)
                    if (expectedArray.GetLength(i) != currentArray.GetLength(i) || expectedArray.GetLowerBound(i) != currentArray.GetLowerBound(i)) return false;
                var left = currentArray.GetEnumerator();
                var right = expectedArray.GetEnumerator();
                while (right.MoveNext())
                    if (!left.MoveNext() || !ValueEqualsCore(left.Current, right.Current, depth + 1, ref budget)) return false;
                return !left.MoveNext();
            }
            if (expected is IDictionary expectedDictionary && IsFrameworkCollection(expected.GetType()))
            {
                if (!(current is IDictionary currentDictionary) || !IsFrameworkCollection(current.GetType()) || currentDictionary.Count != expectedDictionary.Count || expectedDictionary.Count > MaxItems) return false;
                var matched = new HashSet<object>();
                foreach (DictionaryEntry entry in expectedDictionary)
                {
                    var found = false;
                    foreach (DictionaryEntry candidate in currentDictionary)
                    {
                        if (matched.Contains(candidate.Key)) continue;
                        if (!ValueEqualsCore(candidate.Key, entry.Key, depth + 1, ref budget))
                        {
                            if (budget < 0) return false;
                            continue;
                        }
                        if (!ValueEqualsCore(candidate.Value, entry.Value, depth + 1, ref budget)) return false;
                        matched.Add(candidate.Key);
                        found = true;
                        break;
                    }
                    if (!found) return false;
                }
                return true;
            }
            if (expected is IList expectedList && IsFrameworkCollection(expected.GetType()))
            {
                if (!(current is IList currentList) || !IsFrameworkCollection(current.GetType()) || currentList.Count != expectedList.Count || expectedList.Count > MaxItems) return false;
                for (var i = 0; i < expectedList.Count; i++)
                    if (!ValueEqualsCore(currentList[i], expectedList[i], depth + 1, ref budget)) return false;
                return true;
            }
            var expectedType = expected.GetType();
            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                if (current.GetType() != expectedType) return false;
                var key = expectedType.GetProperty("Key")!;
                var value = expectedType.GetProperty("Value")!;
                return ValueEqualsCore(key.GetValue(current), key.GetValue(expected), depth + 1, ref budget) &&
                    ValueEqualsCore(value.GetValue(current), value.GetValue(expected), depth + 1, ref budget);
            }
            return current.Equals(expected);
        }

        private static bool IsFrameworkCollection(Type type)
        {
            return type.Assembly == typeof(List<>).Assembly && (type.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) ?? false);
        }

        internal static object Schema(Type type)
        {
            if (type.IsByRef) type = type.GetElementType()!;
            var enumNames = type.IsEnum ? Enum.GetNames(type) : null;
            var generic = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            var knownGeneric = generic == typeof(Nullable<>) || generic == typeof(KeyValuePair<,>) || generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>) || generic == typeof(IReadOnlyDictionary<,>) || generic == typeof(List<>) || generic == typeof(IEnumerable<>) || generic == typeof(ICollection<>) || generic == typeof(IList<>) || generic == typeof(IReadOnlyCollection<>) || generic == typeof(IReadOnlyList<>);
            var supported = (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)) || type == typeof(object) || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(CultureInfo) || type == typeof(Version) || type == typeof(Type) || type == typeof(SecureString) || type == typeof(FileInfo) || type == typeof(DirectoryInfo) || (type.IsArray && type.GetArrayRank() <= 8) || knownGeneric || typeof(Delegate).IsAssignableFrom(type) || IsSiemensPublic(type);
            var constructors = IsSiemensPublic(type) && !type.IsAbstract && !type.IsInterface
                ? type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Take(MaxItems).Select(c => c.GetParameters().Select(p => new { name = p.Name, type = p.ParameterType.FullName }).ToArray()).ToArray()
                : null;
            var properties = IsSiemensPublic(type) ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.SetMethod != null && p.SetMethod.IsPublic).Take(MaxItems).Select(p => new { name = p.Name, type = p.PropertyType.FullName }).ToArray() : null;
            var invoke = typeof(Delegate).IsAssignableFrom(type) ? type.GetMethod("Invoke") : null;
            var callback = invoke == null ? null : new
            {
                parameters = invoke.GetParameters().Take(MaxItems).Select(p => new { name = p.Name, type = p.ParameterType.FullName, isOut = p.IsOut, isIn = p.IsIn }).ToArray(),
                returns = invoke.ReturnType.FullName,
                keys = new[] { "return", "set", "argument", "out", "cases", "default" },
                guidance = "Use $callback with constant return and named ref/out values. Cases dispatch by exact public Siemens runtime type and require a policy; unmatched types fail unless default is specified."
            };
            return new { type = type.FullName ?? type.Name, enumNames, nullable = !type.IsValueType || Nullable.GetUnderlyingType(type) != null, supported, arrayRank = type.IsArray ? (int?)type.GetArrayRank() : null, elementType = type.IsArray ? type.GetElementType()?.FullName : null, genericArguments = type.IsGenericType ? type.GetGenericArguments().Select(t => t.FullName ?? t.Name).Take(MaxItems).ToArray() : null, callback, constructors, properties, note = supported ? "Input support depends on the concrete type and members. Complex values may require an issued reference; typed file and directory paths are limited to configured roots." : "Unsupported type; an assignable issued reference may be accepted." };
        }

        private static bool IsScalarType(Type type)
        {
            return (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)) || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(CultureInfo) || type == typeof(Version);
        }
        private static bool IsSiemensPublic(Type type)
        {
            return type.IsVisible && Portal.ApiTrustedType(type);
        }
        private static void InspectArray(JsonElement value, int[] lengths, int level, ref int total)
        {
            var items = ReadArray(value);
            if (lengths[level] < 0) lengths[level] = items.Length;
            else if (lengths[level] != items.Length) throw new ArgumentException("Multidimensional arrays must be rectangular.");
            total = checked(total + items.Length);
            if (total > MaxItems) throw new ArgumentException("Array exceeds the total item limit.");
            if (level < lengths.Length - 1)
                foreach (var item in items) InspectArray(item, lengths, level + 1, ref total);
        }
        private static void FillArray(JsonElement value, Array array, int[] indices, int level, Type element, Func<string, object> resolve, Func<string, Type>? resolveType, int depth)
        {
            var items = ReadArray(value);
            for (var i = 0; i < items.Length; i++)
            {
                indices[level] = i;
                if (level == indices.Length - 1) array.SetValue(ConvertValue(items[i], element, resolve, resolveType, depth + 1), indices);
                else FillArray(items[i], array, indices, level + 1, element, resolve, resolveType, depth + 1);
            }
        }
        private static string ReadString(JsonElement value)
        {
            var text = value.GetString() ?? throw new ArgumentException("A string is required.");
            if (text.Length > MaxString) throw new ArgumentException("String exceeds the length limit.");
            return text;
        }
        private static JsonElement[] ReadArray(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) throw new ArgumentException("An array is required.");
            if (value.GetArrayLength() > MaxItems) throw new ArgumentException("Collection exceeds the item limit.");
            return value.EnumerateArray().ToArray();
        }
        private static void DemandObject(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("An object is required.");
        }
        private static void DemandOnly(JsonElement value, params string[] names)
        {
            var fields = value.EnumerateObject().ToArray();
            if (fields.Length > names.Length || fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length || fields.Any(f => !names.Contains(f.Name, StringComparer.Ordinal))) throw new ArgumentException("Object contains unknown or duplicate fields.");
        }
    }
}
