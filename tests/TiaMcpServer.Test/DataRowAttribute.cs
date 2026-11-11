using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TiaMcpServer.Test
{
    // Attribute arguments must be compile-time constants. This local data-row
    // implementation resolves path tokens during MSTest discovery instead.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class DataRowAttribute : Attribute, ITestDataSource
    {
        private readonly object[] data;

        public DataRowAttribute(params object[] data)
        {
            this.data = data;
        }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            yield return data.Select(Settings.ResolveDataValue).ToArray();
        }

        public string? GetDisplayName(
            MethodInfo methodInfo,
            object?[]? resolvedData)
        {
            if (resolvedData == null)
            {
                return methodInfo.Name;
            }

            var arguments = string.Join(
                ", ",
                resolvedData.Select(value => value?.ToString() ?? "null"));
            return $"{methodInfo.Name} ({arguments})";
        }
    }
}
