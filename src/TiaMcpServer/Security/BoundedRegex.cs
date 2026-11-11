using System;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Security
{
    public static class BoundedRegex
    {
        public const int MaximumPatternLength = 256;

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

        public static Regex Create(string pattern, RegexOptions options = RegexOptions.None)
        {
            ValidatePattern(pattern);
            return new Regex(pattern, options, MatchTimeout);
        }

        public static bool IsMatch(string input, string pattern, RegexOptions options = RegexOptions.None)
        {
            return Create(pattern, options).IsMatch(input);
        }

        public static string Replace(
            string input,
            string pattern,
            string replacement,
            RegexOptions options = RegexOptions.None)
        {
            return Create(pattern, options).Replace(input, replacement);
        }

        private static void ValidatePattern(string pattern)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            if (pattern.Length > MaximumPatternLength)
            {
                throw new ArgumentException(
                    $"Regular expression patterns cannot exceed {MaximumPatternLength} characters.",
                    nameof(pattern));
            }
        }
    }
}
