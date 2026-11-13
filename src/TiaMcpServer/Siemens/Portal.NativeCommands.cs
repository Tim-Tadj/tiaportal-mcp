using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        private sealed class NativeCreationParameter
        {
            public string Name { get; set; } = "";
            public bool IsMandatory { get; set; }
            public Type? KnownType { get; set; }
        }

        private sealed class NativeCreationSchema
        {
            public string Composition { get; set; } = "";
            public string TypeName { get; set; } = "";
            public NativeCreationParameter[] Parameters { get; set; } = Array.Empty<NativeCreationParameter>();
        }

        private static bool NativeSensitive(string name)
        {
            return new[] { "password", "secret", "credential", "token", "privatekey" }
                .Any(term => name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool NativeCodecSupports(Type type)
        {
            var schema = PublicApiValueCodec.Schema(type);
            var supported = schema.GetType().GetProperty("supported", BindingFlags.Public | BindingFlags.Instance);
            return supported != null && (bool)supported.GetValue(schema, null)!;
        }

        private static Type? NativeKnownParameterType(Type createdType, string name)
        {
            if (!Portal.ApiTrustedType(createdType)) return null;
            var property = Portal.ApiContracts(createdType)
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetIndexParameters().Length == 0 &&
                    ((candidate.GetMethod != null && candidate.GetMethod.IsPublic) || (candidate.SetMethod != null && candidate.SetMethod.IsPublic)));
            if (property != null) return property.PropertyType;
            var field = Portal.ApiContracts(createdType)
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .FirstOrDefault(candidate => candidate.Name == name && candidate.IsPublic);
            return field?.FieldType;
        }

        private static NativeCreationSchema ToNativeCreationSchema(string composition, EngineeringCreationInfo info)
        {
            var type = info.Type;
            if (!Portal.ApiTrustedType(type)) throw new ArgumentException("A creation schema contains a type outside the trusted public Siemens API.");
            return new NativeCreationSchema
            {
                Composition = composition,
                TypeName = type.FullName ?? type.Name,
                Parameters = info.ParameterInfos.Select(parameter => new NativeCreationParameter
                {
                    Name = parameter.Name,
                    IsMandatory = parameter.IsMandatory,
                    KnownType = NativeKnownParameterType(type, parameter.Name)
                }).ToArray()
            };
        }

        private static IEnumerable<string> NativeCreationCompositions(IEngineeringObject target)
        {
            return target.GetCompositionInfos().Select(info => info.Name).OrderBy(name => name, StringComparer.Ordinal);
        }

        private static IEnumerable<NativeCreationSchema> NativeCreationSchemas(object target, string composition)
        {
            if (target is IEngineeringComposition nativeComposition)
            {
                if (!string.IsNullOrEmpty(composition)) throw new ArgumentException("Do not supply a composition name when the reference is already a composition.");
                return nativeComposition.GetCreationInfos().Select(info => ToNativeCreationSchema("", info));
            }
            if (!(target is IEngineeringObject nativeObject))
                throw new ArgumentException("The selected reference does not expose native engineering creation metadata.");

            var names = string.IsNullOrEmpty(composition)
                ? NativeCreationCompositions(nativeObject).ToArray()
                : new[] { composition };
            var declared = new HashSet<string>(NativeCreationCompositions(nativeObject), StringComparer.Ordinal);
            if (names.Any(name => !declared.Contains(name)))
                throw new ArgumentException("The selected object does not declare the exact requested composition.");
            return names.SelectMany(name => nativeObject.GetCreationInfos(name).Select(info => ToNativeCreationSchema(name, info)));
        }

        private static object NativeCreationParameterSchema(NativeCreationParameter parameter)
        {
            return new
            {
                name = parameter.Name,
                isMandatory = parameter.IsMandatory,
                typeKnown = parameter.KnownType != null,
                type = PublicApiValueCodec.Schema(parameter.KnownType ?? typeof(object)),
                inputHint = parameter.KnownType == null
                    ? "Type is not declared by CreationParameterInfo. Supply a scalar, an issued $ref, or an explicit {$type, value} for a public Siemens value."
                    : "Supply a value accepted by the declared public property/field type."
            };
        }

        public object DescribeEngineeringCreation(string entityRef, string composition, int limit, string cursor)
        {
            if (composition == null || composition.Length > 512) throw new ArgumentException("The composition name must contain at most 512 characters.");
            var target = ResolveApiTarget(entityRef);
            var schemas = NativeCreationSchemas(target, composition).OrderBy(schema => schema.Composition, StringComparer.Ordinal)
                .ThenBy(schema => schema.TypeName, StringComparer.Ordinal).ToArray();
            var scope = "native-creation:" + ApiProjectScope() + "\n" + entityRef + "\n" + composition + "\n" +
                string.Join("\n", schemas.Select(schema => schema.Composition + ":" + schema.TypeName));
            var request = new ModelContextProtocol.PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > schemas.Length) throw new ArgumentException("The cursor is outside the creation schemas.");
            var rows = schemas.Skip(offset).Take(request.GetBoundedLimit()).Select(schema => new
            {
                composition = schema.Composition,
                typeName = schema.TypeName,
                parameters = schema.Parameters.Select(NativeCreationParameterSchema).ToArray(),
                note = "Creation parameters expose names and mandatory flags only. A type is shown when the created type has a matching public property or field; otherwise use an explicit $type for a typed Siemens value."
            }).ToArray();
            var more = offset + rows.Length < schemas.Length;
            return new
            {
                items = rows,
                page = new ModelContextProtocol.PageInfo
                {
                    Returned = rows.Length,
                    HasMore = more,
                    NextCursor = more ? ModelContextProtocol.PageRequest.CreateCursor(offset + rows.Length, scope) : null
                },
                note = "Creation metadata describes available native creation routes. It does not verify that creation will succeed in the current project state."
            };
        }

        private static object NativeInvocationSchema(EngineeringInvocationInfo info)
        {
            return new
            {
                name = info.Name,
                parameters = info.ParameterInfos.Select(parameter => new
                {
                    name = parameter.Name,
                    type = PublicApiValueCodec.Schema(parameter.Type)
                }).ToArray(),
                effects = "unclassified native effects; invocation requires ReadWrite and explicit acceptance"
            };
        }

        private static IEnumerable<EngineeringInvocationInfo> NativeInvocationInfos(object target)
        {
            if (target is IEngineeringComposition composition) return composition.GetInvocationInfos();
            if (target is IEngineeringAssociation association) return association.GetInvocationInfos();
            if (target is IEngineeringObject engineeringObject) return engineeringObject.GetInvocationInfos();
            throw new ArgumentException("The selected reference does not expose native engineering actions.");
        }

        public object DescribeEngineeringActions(string entityRef, int limit, string cursor)
        {
            var target = ResolveApiTarget(entityRef);
            var infos = NativeInvocationInfos(target).OrderBy(info => info.Name, StringComparer.Ordinal).ToArray();
            var scope = "native-actions:" + ApiProjectScope() + "\n" + entityRef + "\n" +
                string.Join("\n", infos.Select(info => info.Name + "(" + string.Join(",", info.ParameterInfos.Select(parameter => parameter.Type.FullName)) + ")"));
            var request = new ModelContextProtocol.PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > infos.Length) throw new ArgumentException("The cursor is outside the action schemas.");
            var rows = infos.Skip(offset).Take(request.GetBoundedLimit()).Select(NativeInvocationSchema).ToArray();
            var more = offset + rows.Length < infos.Length;
            return new
            {
                items = rows,
                page = new ModelContextProtocol.PageInfo
                {
                    Returned = rows.Length,
                    HasMore = more,
                    NextCursor = more ? ModelContextProtocol.PageRequest.CreateCursor(offset + rows.Length, scope) : null
                },
                note = "Action effects are not classified by Siemens metadata. Review the exact action and its effects before invoking it."
            };
        }

#if TIA_MCP_READ_WRITE
        private static Dictionary<string, JsonElement> NativeNamedValues(JsonElement values, string description)
        {
            if (values.ValueKind != JsonValueKind.Object) throw new ArgumentException(description + " must be a JSON object keyed by exact parameter names.");
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in values.EnumerateObject())
            {
                if (property.Name.Length == 0 || property.Name.Length > 256) throw new ArgumentException(description + " contains an invalid parameter name.");
                if (result.ContainsKey(property.Name)) throw new ArgumentException(description + " contains a duplicate parameter name.");
                result.Add(property.Name, property.Value);
            }
            if (result.Count > 128) throw new ArgumentException(description + " exceeds the 128-parameter limit.");
            return result;
        }

        private static object? NativeConvertWithReferences(JsonElement value, Type type, Portal portal)
        {
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$ref", out var refValue))
            {
                var properties = value.EnumerateObject().ToArray();
                var names = properties.Select(property => property.Name).ToArray();
                if (properties.Length > 2 || names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                    names.Any(name => name != "$ref" && name != "type") || refValue.ValueKind != JsonValueKind.String)
                    throw new ArgumentException("A reference value may contain only $ref and its descriptive type field.");
                if (properties.Length == 2 && value.GetProperty("type").ValueKind != JsonValueKind.String)
                    throw new ArgumentException("The descriptive reference type must be a string.");
                using (var normalized = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, string> { ["$ref"] = refValue.GetString() ?? "" })))
                    return PublicApiValueCodec.Convert(normalized.RootElement, type, reference => portal.ResolveApiTarget(reference), Portal.ResolveApiType);
            }
            var expectedType = Nullable.GetUnderlyingType(type) ?? type;
            if (type != typeof(object) && value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty("$type", out var typedName) && value.TryGetProperty("value", out var payload))
            {
                var properties = value.EnumerateObject().ToArray();
                if (properties.Length != 2 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 2)
                    throw new ArgumentException("A typed value must contain exactly $type and value.");
                var declaredType = Portal.ResolveApiType(typedName.GetString() ?? "");
                if (declaredType != expectedType) throw new ArgumentException("The explicit $type does not match the expected parameter type.");
                value = payload;
            }
            return PublicApiValueCodec.Convert(value, type, reference => portal.ResolveApiTarget(reference), Portal.ResolveApiType);
        }

        private static object? NativeActionResult(object? result, Portal portal, string entityRef)
        {
            return portal.ProjectApiValue(result, entityRef);
        }

        public object? CreateEngineeringObject(string entityRef, string composition, string typeName, JsonElement parameters,
            string expectedProjectPath, bool acceptNativeEffects)
        {
            ProfileAccessPolicy.DemandWrite("CreateEngineeringObject");
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the native creation effects.");
            if (string.IsNullOrWhiteSpace(typeName) || typeName.Length > 512) throw new ArgumentException("Supply the exact public type name from DescribeEngineeringCreation.");

            var target = ResolveApiTarget(entityRef);
            var schemas = NativeCreationSchemas(target, composition).Where(schema => schema.TypeName == typeName).ToArray();
            if (schemas.Length != 1) throw new ArgumentException("Select one exact creation type and composition from DescribeEngineeringCreation.");
            var schema = schemas[0];
            var supplied = NativeNamedValues(parameters, "Creation parameters");
            var parameterNames = new HashSet<string>(schema.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
            var unknown = supplied.Keys.Where(name => !parameterNames.Contains(name)).ToArray();
            if (unknown.Length > 0) throw new ArgumentException("Unknown creation parameter: " + unknown[0] + ".");

            var converted = new List<KeyValuePair<string, object>>();
            foreach (var parameter in schema.Parameters)
            {
                if (!supplied.TryGetValue(parameter.Name, out var input))
                {
                    if (parameter.IsMandatory) throw new ArgumentException("Missing mandatory creation parameter: " + parameter.Name + ".");
                    continue;
                }
                var value = NativeConvertWithReferences(input, parameter.KnownType ?? typeof(object), this);
                converted.Add(new KeyValuePair<string, object>(parameter.Name, value!));
            }

            var createdType = ResolveApiType(typeName);
            if (!Portal.ApiTrustedType(createdType)) throw new ArgumentException("The selected type is not a public Siemens Engineering type.");
            IEngineeringObject? created;
            try
            {
                if (target is IEngineeringComposition nativeComposition)
                {
                    created = nativeComposition.Create(createdType, converted.Count == 0 ? null : converted);
                }
                else if (target is IEngineeringObject nativeObject)
                {
                    if (string.IsNullOrEmpty(schema.Composition)) throw new ArgumentException("The selected creation schema has no composition name.");
                    created = nativeObject.Create(schema.Composition, createdType, converted.Count == 0 ? null : converted);
                }
                else throw new ArgumentException("The selected reference is not a native engineering object or composition.");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Native creation failed or may have partially applied. Read the parent composition before retrying. Native error type: " + error.GetType().Name + ".");
            }

            object? resultReference;
            string? projectionError = null;
            try { resultReference = ProjectApiValue(created, entityRef); }
            catch (Exception error) { resultReference = null; projectionError = error.GetType().Name; }
            return new { nativeCallCompleted = true, entityRef = projectionError == null ? resultReference : "<unavailable: " + projectionError + ">", typeName, composition = schema.Composition,
                projectionError, additionalSaveOrDownloadPerformed = false, effects = "unclassified native effects" };
        }

        private sealed class NativeAttributeChange
        {
            public string Name = "";
            public Type ValueType = typeof(object);
            public object? Expected;
            public object? Replacement;
            public bool Readable;
        }

        private static Type[] NativeSupportedAttributeTypes(EngineeringAttributeInfo attribute)
        {
#if TIA_MCP_V17
            return Array.Empty<Type>();
#else
            return attribute.SupportedTypes.Where(type => type != null).Distinct().ToArray();
#endif
        }

        private static Type NativeSelectAttributeType(JsonElement value, object? current, Type[] supportedTypes)
        {
            if (supportedTypes.Length == 0)
            {
                if (current != null) return current.GetType();
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$type", out var explicitName))
                {
                    var explicitType = Portal.ResolveApiType(explicitName.GetString() ?? "");
                    if (!NativeCodecSupports(explicitType)) throw new ArgumentException("The explicit $type is unsupported by the value codec.");
                    return explicitType;
                }
                throw new ArgumentException("The type is not declared and the current value is null; supply an explicit {$type, value}.");
            }
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$type", out var typedName))
            {
                var requested = Portal.ResolveApiType(typedName.GetString() ?? "");
                var matches = supportedTypes.Where(type => type == requested).ToArray();
                if (matches.Length == 1) return matches[0];
                throw new ArgumentException("The explicit $type is not one of this attribute's supported types.");
            }
            if (current != null)
            {
                var currentType = current.GetType();
                if (supportedTypes.Contains(currentType)) return currentType;
            }
            if (supportedTypes.Length == 1) return supportedTypes[0];
            throw new ArgumentException("This attribute accepts multiple types. Supply an explicit {$type, value} to select one.");
        }

        public object UpdateEngineeringAttributes(string entityRef, JsonElement changes, string expectedProjectPath, bool acceptNativeEffects)
        {
            ProfileAccessPolicy.DemandWrite("UpdateEngineeringAttributes");
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the native attribute update effects.");
            var target = ResolveApiTarget(entityRef) as IEngineeringObject
                ?? throw new ArgumentException("The selected reference does not expose native engineering attributes.");
            var supplied = NativeNamedValues(changes, "Attribute changes");
            if (supplied.Count == 0) throw new ArgumentException("Supply at least one attribute change.");

            var infos = target.GetAttributeInfos().ToDictionary(info => info.Name, StringComparer.Ordinal);
            var validated = new List<NativeAttributeChange>();
            foreach (var entry in supplied)
            {
                var name = entry.Key;
                if (!infos.TryGetValue(name, out var info)) throw new ArgumentException("No exact public attribute is named " + name + ".");
                var access = info.AccessMode.ToString();
                var readable = access == "Read" || access == "ReadWrite";
                var writable = access == "Write" || access == "ReadWrite";
                if (!writable) throw new ArgumentException("Attribute " + name + " is not writable.");
                if (entry.Value.ValueKind != JsonValueKind.Object) throw new ArgumentException("Each change must contain expectedValue and value fields.");
                var fields = entry.Value.EnumerateObject().ToArray();
                if (fields.Length != 2 || fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
                    !entry.Value.TryGetProperty("expectedValue", out var expectedInput) || !entry.Value.TryGetProperty("value", out var replacementInput))
                    throw new ArgumentException("Each change must contain exactly expectedValue and value fields.");
                var current = readable ? target.GetAttribute(name) : null;
                var supported = NativeSupportedAttributeTypes(info);
#if TIA_MCP_V17
                var valueType = current?.GetType();
                valueType = valueType ?? (replacementInput.ValueKind == JsonValueKind.Object && replacementInput.TryGetProperty("$type", out var explicitType)
                    ? Portal.ResolveApiType(explicitType.GetString() ?? "") : null);
                if (valueType == null) throw new ArgumentException("V17 cannot infer the writable type; supply an explicit {$type, value} when the current value is null.");
#else
                var valueType = NativeSelectAttributeType(replacementInput, current, supported);
#endif
                if (!NativeCodecSupports(valueType)) throw new ArgumentException("The selected attribute type is unsupported by the value codec.");
                if (supported.Length > 0 && !supported.Contains(valueType)) throw new ArgumentException("The selected type is not supported by attribute " + name + ".");
                object? expected = null;
                if (readable)
                {
                    expected = current == null && expectedInput.ValueKind == JsonValueKind.Null
                        ? null
                        : NativeConvertWithReferences(expectedInput, current?.GetType() ?? valueType, this);
                    if (!PublicApiValueCodec.ValueEquals(current, expected)) throw new ArgumentException("The current value of " + name + " differs from expectedValue; read it again.");
                }
                else if (expectedInput.ValueKind != JsonValueKind.Null)
                    throw new ArgumentException("For write-only attribute " + name + ", expectedValue must be null because the current value cannot be read.");
                var replacement = NativeConvertWithReferences(replacementInput, valueType, this);
                validated.Add(new NativeAttributeChange { Name = name, ValueType = valueType, Expected = expected, Replacement = replacement, Readable = readable });
            }

            var applied = new List<string>();
            try
            {
                foreach (var change in validated)
                {
                    target.SetAttribute(change.Name, change.Replacement);
                    applied.Add(change.Name);
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Attribute update failed after applying: " + string.Join(", ", applied) + ". Read the target back before retrying. Native error type: " + error.GetType().Name + ".");
            }
            var output = validated.Select(change =>
            {
                if (NativeSensitive(change.Name)) return new { name = change.Name, value = (object)"<redacted>" };
                if (!change.Readable) return new { name = change.Name, value = (object)"<unavailable: write-only>" };
                try { return new { name = change.Name, value = (object)ProjectApiValue(target.GetAttribute(change.Name), entityRef)! }; }
                catch (Exception error) { return new { name = change.Name, value = (object)("<unavailable: " + error.GetType().Name + ">") }; }
            }).ToArray();
            return new
            {
                nativeCallCompleted = true,
                attributes = output,
                additionalSaveOrDownloadPerformed = false,
                effects = "unclassified native effects"
            };
        }

        public object? InvokeEngineeringAction(string entityRef, string actionName, JsonElement arguments,
            string expectedProjectPath, bool acceptNativeEffects)
        {
            ProfileAccessPolicy.DemandWrite("InvokeEngineeringAction");
            RequireApiProjectScope(expectedProjectPath);
            if (!acceptNativeEffects) throw new ArgumentException("Set acceptNativeEffects to true after reviewing the unclassified action effects.");
            if (string.IsNullOrWhiteSpace(actionName) || actionName.Length > 512) throw new ArgumentException("Supply the exact action name from DescribeEngineeringActions.");
            if (arguments.ValueKind != JsonValueKind.Array) throw new ArgumentException("Action arguments must be a JSON array in the exact schema order.");

            var target = ResolveApiTarget(entityRef);
            var infos = NativeInvocationInfos(target).Where(info => info.Name == actionName).ToArray();
            if (infos.Length != 1) throw new ArgumentException("The action name is unavailable or ambiguous on this target.");
            var info = infos[0];
            if (arguments.GetArrayLength() != info.ParameterInfos.Count) throw new ArgumentException("Supply one value per action parameter in schema order.");
            var values = new List<KeyValuePair<Type, object>>();
            var index = 0;
            foreach (var input in arguments.EnumerateArray())
            {
                var parameterType = info.ParameterInfos[index].Type;
                if (parameterType.IsByRef || !NativeCodecSupports(parameterType))
                    throw new ArgumentException("The action parameter type is unsupported by the value codec: " + parameterType.FullName + ".");
                var value = NativeConvertWithReferences(input, parameterType, this);
                values.Add(new KeyValuePair<Type, object>(parameterType, value!));
                index++;
            }
            object? result;
            try
            {
                if (target is IEngineeringComposition composition)
                    result = composition.Invoke(actionName, values.Count == 0 ? null : values);
                else if (target is IEngineeringAssociation association)
                    result = association.Invoke(actionName, values.Count == 0 ? null : values);
                else if (target is IEngineeringObject engineeringObject)
                    result = engineeringObject.Invoke(actionName, values.Count == 0 ? null : values);
                else throw new ArgumentException("The selected reference does not expose native engineering actions.");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Native action failed or may have partially applied. Read the target state before retrying. Native error type: " + error.GetType().Name + ".");
            }
            object? projected;
            string? projectionError = null;
            var sensitiveAction = NativeSensitive(actionName) || info.ParameterInfos.Any(parameter => NativeSensitive(parameter.Name));
            try { projected = sensitiveAction ? "<redacted>" : NativeActionResult(result, this, entityRef); }
            catch (Exception error) { projectionError = error.GetType().Name; projected = "<unavailable: " + projectionError + ">"; }
            return new { nativeCallCompleted = true, result = projected, projectionError, additionalSaveOrDownloadPerformed = false, effects = "unclassified native effects" };
        }
#endif
    }
}
