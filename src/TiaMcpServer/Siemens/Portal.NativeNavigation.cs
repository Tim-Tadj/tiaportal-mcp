using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    public sealed class NativeReferenceStep
    {
        public string Composition { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int IdentityHash { get; set; }
    }

    public sealed class NativeChild
    {
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string EntityRef { get; set; } = "";
    }

    public partial class Portal
    {
        private sealed class IssuedNativeHandle
        {
            public string Project = "";
            public string Parent = "";
            public string Composition = "";
            public int Index;
            public IEngineeringObject Target = null!;
            public IEngineeringObject ProjectObject = null!;
            public TiaPortal Portal = null!;
        }

        private readonly object _nativeHandleSync = new object();
        private readonly Dictionary<string, IssuedNativeHandle> _nativeHandles = new Dictionary<string, IssuedNativeHandle>(StringComparer.Ordinal);
        private readonly Queue<string> _nativeHandleOrder = new Queue<string>();
        private const int MaximumNativeHandles = 1000;

        private const int MaximumNativeChildren = 5000;
        private const string SoftwareBridge = "$Software";

        private static string NativeName(IEngineeringObject item)
        {
            try
            {
                if (!item.GetAttributeInfos().Any(a => a.Name == "Name")) return "";
                var name = Convert.ToString(item.GetAttribute("Name")) ?? "";
                if (name.Length > 512) throw new ArgumentException("A native object name exceeds the navigation limit.");
                return name;
            }
            catch (EngineeringException) { return ""; }
        }

        private static string NativeType(IEngineeringObject item) => item.GetType().FullName ?? item.GetType().Name;

        private static string[]? NativeAttributeTypes(EngineeringAttributeInfo attribute)
        {
#if TIA_MCP_V17
            return null;
#else
            return attribute.SupportedTypes.Select(type => type.FullName ?? type.Name).ToArray();
#endif
        }

        private static IEngineeringObject[] NativeChildren(IEngineeringObject parent, string composition)
        {
            if (composition == SoftwareBridge)
            {
                if (!(parent is DeviceItem item)) throw new ArgumentException("The software bridge is only available on device items.");
                var software = item.GetService<SoftwareContainer>()?.Software as IEngineeringObject;
                return software == null ? Array.Empty<IEngineeringObject>() : new[] { software };
            }
            if (!parent.GetCompositionInfos().Any(c => c.Name == composition))
                throw new ArgumentException("The selected object does not declare that composition.");
            var value = parent.GetComposition(composition);
            if (value is IEngineeringObject single) return new[] { single };
            if (!(value is IEngineeringComposition children)) return Array.Empty<IEngineeringObject>();
            if (children.Count > MaximumNativeChildren)
                throw new ArgumentException("This composition exceeds the 5000-object navigation limit.");
            return Enumerable.Range(0, children.Count).Select(i => children[i]).ToArray();
        }

        internal static bool SameNativeObject(object expected, object actual)
        {
            try { return ReferenceEquals(expected, actual) || expected.Equals(actual); }
            catch (Exception) { return false; }
        }

        private IEngineeringObject NativeChildAt(IEngineeringObject parent, string composition, int index)
        {
            if (composition == SoftwareBridge)
            {
                if (index != 0 || !(parent is DeviceItem item)) throw new ArgumentException("The native reference is stale; browse the parent again.");
                return item.GetService<SoftwareContainer>()?.Software as IEngineeringObject
                    ?? throw new ArgumentException("The software is no longer available; browse the parent again.");
            }
            if (!parent.GetCompositionInfos().Any(c => c.Name == composition))
                throw new ArgumentException("The native composition is no longer available; browse the parent again.");
            var value = parent.GetComposition(composition);
            if (value is IEngineeringObject single && index == 0) return single;
            if (value is IEngineeringComposition children && index >= 0 && index < children.Count) return children[index];
            throw new ArgumentException("The native reference is stale; browse the parent again.");
        }

        private IEngineeringObject ResolveNativeReference(string entityRef, out string project)
        {
            project = DiscoveryProjectScope();
            var root = _project as IEngineeringObject ?? throw new ArgumentException("The selected project does not expose native engineering navigation.");
            if (string.IsNullOrEmpty(entityRef)) return root;
            if (entityRef.Length > 128) throw new ArgumentException("The entity reference is invalid.");
            var chain = new List<IssuedNativeHandle>();
            var current = entityRef;
            lock (_nativeHandleSync)
            {
                while (!string.IsNullOrEmpty(current))
                {
                    if (chain.Count >= 20 || !_nativeHandles.TryGetValue(current, out var handle))
                        throw new ArgumentException("The entity reference expired or was not issued by this session; browse its parent again.");
                    if (!ReferenceEquals(handle.Portal, _portal) || !string.Equals(handle.Project, project, StringComparison.OrdinalIgnoreCase) || !SameNativeObject(handle.ProjectObject, root))
                        throw new ArgumentException("The entity reference belongs to a different project or project session.");
                    chain.Add(handle);
                    current = handle.Parent;
                }
            }
            chain.Reverse();
            var target = root;
            foreach (var handle in chain)
            {
                var selected = NativeChildAt(target, handle.Composition, handle.Index);
                if (!SameNativeObject(handle.Target, selected))
                    throw new ArgumentException("The entity reference is stale; browse its parent again.");
                target = selected;
            }
            return target;
        }

        private string IssueNativeReference(string project, string parentRef, string composition, int index, IEngineeringObject target)
        {
            var id = Guid.NewGuid().ToString("N");
            var root = _project as IEngineeringObject ?? throw new ArgumentException("The selected project is unavailable.");
            lock (_nativeHandleSync)
            {
                var ancestor = parentRef;
                var protectedHandles = new HashSet<string>(StringComparer.Ordinal);
                var depth = 0;
                while (!string.IsNullOrEmpty(ancestor))
                {
                    if (++depth >= 20 || !_nativeHandles.TryGetValue(ancestor, out var parent))
                        throw new ArgumentException("The reference expired or exceeds the maximum navigation depth; browse its parent again.");
                    protectedHandles.Add(ancestor);
                    ancestor = parent.Parent;
                }
                while (_nativeHandleOrder.Count >= MaximumNativeHandles)
                {
                    var oldest = _nativeHandleOrder.Dequeue();
                    if (protectedHandles.Contains(oldest)) _nativeHandleOrder.Enqueue(oldest);
                    else _nativeHandles.Remove(oldest);
                }
                _nativeHandles.Add(id, new IssuedNativeHandle { Portal = _portal!, Project = project, Parent = parentRef, Composition = composition,
                    Index = index, Target = target, ProjectObject = root });
                _nativeHandleOrder.Enqueue(id);
            }
            return id;
        }

        public object DescribeNativeSurface(string entityRef)
        {
            var target = ResolveApiTarget(entityRef) as IEngineeringObject
                ?? throw new ArgumentException("The reference does not expose native engineering self-description.");
            var compositions = target.GetCompositionInfos().Select(i => i.Name).OrderBy(i => i, StringComparer.Ordinal).Take(201).ToList();
            if (target is DeviceItem) compositions.Add(SoftwareBridge);
            var attributes = target.GetAttributeInfos().OrderBy(i => i.Name, StringComparer.Ordinal).Take(201)
                .Select(i => new { i.Name, Access = i.AccessMode.ToString(), Types = NativeAttributeTypes(i) }).ToArray();
            var actions = target.GetInvocationInfos().OrderBy(i => i.Name, StringComparer.Ordinal).Take(201)
                .Select(i => new { i.Name, Parameters = i.ParameterInfos.Take(32).Select(p => new { p.Name, Type = p.Type.FullName ?? p.Type.Name }).ToArray() }).ToArray();
            var services = target is IEngineeringServiceProvider provider
                ? provider.GetServiceInfos().Select(i => i.Type.FullName ?? i.Type.Name).OrderBy(i => i, StringComparer.Ordinal).Take(201).ToArray()
                : Array.Empty<string>();
            return new { Kind = NativeType(target), Name = NativeName(target), Compositions = compositions.Take(200).ToArray(), CompositionsTruncated = compositions.Count > 200,
                Attributes = attributes.Take(200).ToArray(), AttributesTruncated = attributes.Length > 200,
                DeclaredActions = actions.Take(200).ToArray(), ActionsTruncated = actions.Length > 200,
                DeclaredServices = services.Take(200).ToArray(), ServicesTruncated = services.Length > 200,
                Note = "Use DescribeEngineeringActions and InvokeEngineeringAction for actions; GetApiService for declared services. $Software is the explicit device-item software bridge. References expire when evicted or when the selected project session changes; browse the parent again." };
        }

        public PagedResponse<ModelContextProtocol.Attribute> ReadNativeAttributes(string entityRef, int limit, string cursor)
        {
            var target = ResolveApiTarget(entityRef) as IEngineeringObject
                ?? throw new ArgumentException("The reference does not expose native engineering attributes.");
            var reference = ApiContextScope();
            var infos = target.GetAttributeInfos().OrderBy(i => i.Name, StringComparer.Ordinal).ToArray();
            var scope = "native-attributes:" + reference + "\n" + entityRef + "\n" + string.Join("\n", infos.Select(i => i.Name + ":" + i.AccessMode));
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > infos.Length) throw new ArgumentException("The cursor is outside the attributes.");
            var rows = infos.Skip(offset).Take(request.GetBoundedLimit()).Select(info =>
            {
                object? value;
                try
                {
                    var sensitive = new[] { "password", "secret", "credential", "token", "privatekey" }
                        .Any(term => info.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                    value = sensitive ? "<redacted>" : Helper.GetAttributeValue(target.GetAttribute(info.Name));
                }
                catch (Exception ex) { value = "<unavailable: " + ex.GetType().Name + ">"; }
                return new ModelContextProtocol.Attribute { Name = info.Name, AccessMode = info.AccessMode.ToString(), Value = value };
            }).ToArray();
            var more = offset + rows.Length < infos.Length;
            return new PagedResponse<ModelContextProtocol.Attribute> { Items = rows, Page = new PageInfo { Returned = rows.Length,
                HasMore = more, NextCursor = more ? PageRequest.CreateCursor(offset + rows.Length, scope) : null } };
        }

        public PagedResponse<NativeChild> BrowseNativeComposition(string entityRef, string composition, int limit, string cursor)
        {
            if (string.IsNullOrWhiteSpace(composition) || composition.Length > 512)
                throw new ArgumentException("Supply an exact declared composition name.");
            var target = ResolveApiTarget(entityRef) as IEngineeringObject
                ?? throw new ArgumentException("The reference does not expose native engineering compositions.");
            var reference = ApiContextScope();
            var items = NativeChildren(target, composition);
            var descriptors = items.Select((item, index) => new NativeReferenceStep { Composition = composition, Index = index, Name = NativeName(item), Type = NativeType(item), IdentityHash = item.GetHashCode() }).ToArray();
            var scope = "native-composition:" + reference + "\n" + entityRef + "\n" + composition + "\n" + JsonSerializer.Serialize(descriptors);
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > descriptors.Length) throw new ArgumentException("The cursor is outside the composition.");
            var rows = descriptors.Skip(offset).Take(request.GetBoundedLimit()).Select(step =>
            {
                return new NativeChild { Name = step.Name, Kind = step.Type,
                    EntityRef = entityRef.StartsWith("api:", StringComparison.Ordinal) || entityRef.StartsWith("$", StringComparison.Ordinal)
                        ? IssueApiReference(items[step.Index], entityRef, parent => NativeChildren((IEngineeringObject)parent, composition)[step.Index])
                        : IssueNativeReference(reference, entityRef, composition, step.Index, items[step.Index]) };
            }).ToArray();
            var more = offset + rows.Length < descriptors.Length;
            return new PagedResponse<NativeChild> { Items = rows, Page = new PageInfo { Returned = rows.Length, HasMore = more,
                NextCursor = more ? PageRequest.CreateCursor(offset + rows.Length, scope) : null } };
        }
    }
}
