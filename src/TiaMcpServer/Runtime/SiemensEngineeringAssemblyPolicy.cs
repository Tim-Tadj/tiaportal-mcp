using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TiaMcpServer.Runtime
{
    internal static class SiemensEngineeringAssemblyPolicy
    {
        private const string RootAssemblyName = "Siemens.Engineering";
        private const string RootPublicKeyToken = "d29ec89bac048f84";

        internal static bool IsSupportedName(string? assemblyName)
        {
            return string.Equals(
                    assemblyName,
                    RootAssemblyName,
                    StringComparison.Ordinal) ||
                (assemblyName?.StartsWith(
                    RootAssemblyName + ".",
                    StringComparison.Ordinal) ?? false);
        }

        internal static void DemandTrustedRequest(AssemblyName assemblyName)
        {
            var tokenText = GetPublicKeyTokenText(assemblyName);
            if (tokenText.Length == 0)
            {
                throw new FileLoadException(
                    $"The requested Siemens Engineering assembly '{assemblyName.FullName}' " +
                    "is not strong-name signed.");
            }

            if (string.Equals(
                    assemblyName.Name,
                    RootAssemblyName,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    tokenText,
                    RootPublicKeyToken,
                    StringComparison.Ordinal))
            {
                throw new FileLoadException(
                    $"The requested root assembly '{assemblyName.FullName}' does not have the " +
                    "expected Siemens Engineering public-key token.");
            }
        }

        internal static bool MatchesRequestedIdentity(
            AssemblyName requested,
            AssemblyName candidate)
        {
            return string.Equals(
                    requested.Name,
                    candidate.Name,
                    StringComparison.Ordinal) &&
                Equals(requested.Version, candidate.Version) &&
                string.Equals(
                    requested.CultureName ?? string.Empty,
                    candidate.CultureName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    GetPublicKeyTokenText(requested),
                    GetPublicKeyTokenText(candidate),
                    StringComparison.Ordinal);
        }

        internal static T? FindFirstMatch<T>(
            AssemblyName requested,
            IEnumerable<T> candidates,
            Func<T, AssemblyName?> identitySelector)
            where T : class
        {
            foreach (var candidate in candidates)
            {
                var candidateIdentity = identitySelector(candidate);
                if (candidateIdentity != null &&
                    MatchesRequestedIdentity(requested, candidateIdentity))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string GetPublicKeyTokenText(AssemblyName assemblyName)
        {
            var publicKeyToken = assemblyName.GetPublicKeyToken();
            if (publicKeyToken == null)
            {
                return string.Empty;
            }

            return BitConverter
                .ToString(publicKeyToken)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
