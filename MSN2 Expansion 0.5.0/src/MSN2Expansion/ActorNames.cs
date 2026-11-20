using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MSN2Expansion
{
    // Pack text names NPCs by their vanilla names ("Camila"). When the optional MSN2 Characters plugin gives an actor
    // another look and name (skin "lola"), journal, tracker, dialogue and notifications show that name instead.
    // Soft dependency: the map is read by reflection, and without the plugin text is unchanged.
    public static class ActorNames
    {
        private static FieldInfo _field;
        private static bool _looked;

        private static Dictionary<string, string> Map()
        {
            if (!_looked)
            {
                _looked = true;
                _field = Type.GetType("MSN2Characters.Skins.SkinApplier, MSN2Characters")?.GetField("DisplayNames", BindingFlags.Public | BindingFlags.Static);
            }
            return _field?.GetValue(null) as Dictionary<string, string>;
        }

        public static string Apply(string text)
        {
            var map = Map();
            if (map == null || map.Count == 0 || string.IsNullOrEmpty(text)) return text;
            foreach (var kv in map)
                if (text.IndexOf(kv.Key, StringComparison.Ordinal) >= 0)
                    text = Regex.Replace(text, $@"\b{Regex.Escape(kv.Key)}\b", kv.Value, RegexOptions.CultureInvariant);
            return text;
        }
    }
}
