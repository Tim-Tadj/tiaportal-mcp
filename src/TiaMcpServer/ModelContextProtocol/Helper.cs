using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TiaMcpServer.ModelContextProtocol
{
    public class Helper
    {
        private const int MaximumAttributeStringLength = 1024;
        private const string TruncationSuffix = " [truncated]";

        public static string? GetNamespace(PlcBlock block)
        {
#if TIA_MCP_V17
            return null;
#else
            return block.Namespace;
#endif
        }

        public static string? GetNamespace(PlcType type)
        {
#if TIA_MCP_V17
            return null;
#else
            return type.Namespace;
#endif
        }

        public static List<Attribute> GetAttributeList(IEngineeringObject obj)
        {
            var attributes = new List<Attribute>();

            if (obj != null)
            {
                foreach (var attr in obj.GetAttributeInfos())
                {
                    string? name = null;
                    string? accessMode = null;
                    object? value;

                    try
                    {
                        name = attr.Name;
                        accessMode = Enum.GetName(typeof(EngineeringAttributeAccessMode), attr.AccessMode);
                        value = NormalizeAttributeValue(obj.GetAttribute(name));
                    }
                    catch (Exception ex)
                    {
                        value = BuildUnavailableValue(ex);
                    }

                    attributes.Add(new Attribute
                    {
                        Name = name,
                        Value = value,
                        AccessMode = accessMode
                    });
                }
            }

            return attributes;
        }

        public static object? NormalizeAttributeValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            switch (value)
            {
                case string stringValue:
                    return ToBoundedString(stringValue);
                case char charValue:
                    return charValue.ToString();
                case bool _:
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case decimal _:
                    return value;
                case float floatValue:
                    return float.IsNaN(floatValue) || float.IsInfinity(floatValue)
                        ? floatValue.ToString("R", CultureInfo.InvariantCulture)
                        : value;
                case double doubleValue:
                    return double.IsNaN(doubleValue) || double.IsInfinity(doubleValue)
                        ? doubleValue.ToString("R", CultureInfo.InvariantCulture)
                        : value;
                case DateTime dateTimeValue:
                    return dateTimeValue.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dateTimeOffsetValue:
                    return dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture);
                case TimeSpan timeSpanValue:
                    return timeSpanValue.ToString("c", CultureInfo.InvariantCulture);
                case Guid guidValue:
                    return guidValue.ToString("D");
                case Enum enumValue:
                    return ToBoundedString(enumValue.ToString());
                case Array arrayValue:
                    return ToBoundedString($"<{value.GetType().Name}: {arrayValue.Length} items>");
                default:
                    try
                    {
                        return ToBoundedString(
                            Convert.ToString(value, CultureInfo.InvariantCulture)
                            ?? value.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        return BuildUnavailableValue(ex);
                    }
            }
        }

        private static string BuildUnavailableValue(Exception exception)
        {
            string message;

            try
            {
                message = exception.Message;
            }
            catch (Exception)
            {
                message = string.Empty;
            }

            var value = string.IsNullOrWhiteSpace(message)
                ? $"<unavailable: {exception.GetType().Name}>"
                : $"<unavailable: {exception.GetType().Name}: {message}>";

            return ToBoundedString(value);
        }

        private static string ToBoundedString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var truncated = value.Length > MaximumAttributeStringLength;
            var contentLimit = truncated
                ? MaximumAttributeStringLength - TruncationSuffix.Length
                : MaximumAttributeStringLength;
            var builder = new StringBuilder(Math.Min(MaximumAttributeStringLength, value.Length));
            var index = 0;

            while (index < value.Length && builder.Length < contentLimit)
            {
                var character = value[index];

                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        if (builder.Length + 2 > contentLimit)
                        {
                            truncated = true;
                            break;
                        }

                        builder.Append(character);
                        builder.Append(value[index + 1]);
                        index += 2;
                        continue;
                    }

                    builder.Append('\uFFFD');
                    index++;
                    continue;
                }

                builder.Append(char.IsLowSurrogate(character) ? '\uFFFD' : character);
                index++;
            }

            if (index < value.Length)
            {
                truncated = true;
            }

            if (truncated)
            {
                builder.Append(TruncationSuffix);
            }

            return builder.ToString();
        }

        public static BlockGroupInfo BuildBlockHierarchy(
            PlcBlockGroup group,
            ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary)
        {
            var groupInfo = new BlockGroupInfo
            {
                Name = group.Name
            };

            var blockList = new List<ResponseBlockInfo>();
            foreach (var block in group.Blocks)
            {
                var includeStandard = detailLevel >= ResponseDetailLevel.Standard;
                var includeFull = detailLevel == ResponseDetailLevel.Full;
                blockList.Add(new ResponseBlockInfo
                {
                    Name = block.Name,
                    TypeName = block.GetType().Name,
                    Namespace = includeStandard ? GetNamespace(block) : null,
                    ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                    MemoryLayout = includeStandard
                        ? Enum.GetName(typeof(MemoryLayout), block.MemoryLayout)
                        : null,
                    IsConsistent = block.IsConsistent,
                    HeaderName = includeStandard ? block.HeaderName : null,
                    ModifiedDate = includeStandard ? block.ModifiedDate : null,
                    IsKnowHowProtected = includeStandard ? block.IsKnowHowProtected : null,
                    Attributes = includeFull ? Helper.GetAttributeList(block) : null,
                    Description = includeFull ? block.ToString() : null
                });
            }
            groupInfo.Blocks = blockList;

            var groupList = new List<BlockGroupInfo>();
            foreach (var subGroup in group.Groups)
            {
                groupList.Add(BuildBlockHierarchy(subGroup, detailLevel));
            }
            groupInfo.Groups = groupList;

            return groupInfo;
        }
    }
}
