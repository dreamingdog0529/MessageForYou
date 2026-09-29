using System;
using System.Runtime.CompilerServices;

namespace M4U.Common
{
    public static class Throw
    {
        public static void IfNull(object? obj, [CallerArgumentExpression(nameof(obj))] string? paramName = null)
        {
            if (obj == null) throw new ArgumentNullException(paramName);
        }
    }
}