using System.Collections.Generic;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace TiaMcpServer.ModelContextProtocol
{
    public enum ResponseDetailLevel
    {
        Summary,
        Standard,
        Full
    }

    public class PageRequest
    {
        public const int DefaultLimit = 50;
        public const int MaximumLimit = 200;

        public int Limit { get; set; } = DefaultLimit;
        public string? Cursor { get; set; }

        public int GetBoundedLimit()
        {
            if (Limit <= 0)
            {
                return DefaultLimit;
            }

            return Limit > MaximumLimit ? MaximumLimit : Limit;
        }

        public int GetOffset(string scope)
        {
            if (string.IsNullOrWhiteSpace(Cursor))
            {
                return 0;
            }

            try
            {
                var value = Encoding.UTF8.GetString(Convert.FromBase64String(Cursor));
                var parts = value.Split(':');

                if (parts.Length != 3 ||
                    parts[0] != "v1" ||
                    parts[1] != CreateScopeHash(scope) ||
                    !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) ||
                    offset < 0)
                {
                    throw new FormatException();
                }

                return offset;
            }
            catch (Exception ex) when (
                ex is FormatException ||
                ex is ArgumentException)
            {
                throw new ArgumentException("The paging cursor is invalid.", nameof(Cursor), ex);
            }
        }

        public static string CreateCursor(int offset, string scope)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            var value =
                $"v1:{CreateScopeHash(scope)}:{offset.ToString(CultureInfo.InvariantCulture)}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string CreateScopeHash(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new ArgumentException("A paging cursor scope is required.", nameof(scope));
            }

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(scope));
                var builder = new StringBuilder(24);
                for (var index = 0; index < 12; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    public class PageInfo
    {
        public int Returned { get; set; }
        public bool HasMore { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NextCursor { get; set; }
    }

    public class PagedResponse<T> : ResponseMessage
    {
        public IEnumerable<T>? Items { get; set; }
        public PageInfo? Page { get; set; }
    }
}
