// Shade - deterministic name-only matching shared by local search and suggestion controls.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shade
{
    public static class NameSearch
    {
        public static IEnumerable<string> Match(IEnumerable<string> names, string query)
        {
            string text = (query ?? "").Trim();
            string[] terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int Rank(string name)
            {
                if (name.Equals(text, StringComparison.OrdinalIgnoreCase)) return 0;
                if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase)) return 1;
                if (name.Split('.').Any(part => part.StartsWith(text, StringComparison.OrdinalIgnoreCase))) return 2;
                return 3;
            }
            var candidates = names.Where(name => terms.All(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
            return text.Length == 0 ? candidates.OrderBy(name => name, StringComparer.Ordinal)
                : candidates.OrderBy(Rank).ThenBy(name => name.IndexOf(text, StringComparison.OrdinalIgnoreCase) is int at && at >= 0 ? at : int.MaxValue)
                    .ThenBy(name => name.Length).ThenBy(name => name, StringComparer.Ordinal);
        }
    }
}
