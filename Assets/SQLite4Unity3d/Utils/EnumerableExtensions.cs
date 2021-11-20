using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace SQLite4Unity3d.Utils
{
    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> enumerable, [NotNull] Action<T> elementAction)
        {
            if (elementAction == null)
            {
                throw new ArgumentNullException(nameof(elementAction));
            }

            foreach (var element in enumerable)
            {
                elementAction.Invoke(element);
            }
        }
    }
}