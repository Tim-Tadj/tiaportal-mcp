using System;

namespace TiaMcp.Contracts;

internal static class UnicodeScalarValidator
{
    public static void EnsureValid(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length ||
                    !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new InvalidOperationException(
                        "Presentation text contains an unpaired UTF-16 surrogate.");
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw new InvalidOperationException(
                    "Presentation text contains an unpaired UTF-16 surrogate.");
            }
        }
    }
}
