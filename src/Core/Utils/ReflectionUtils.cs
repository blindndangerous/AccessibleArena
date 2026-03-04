using System;
using System.Reflection;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Shared reflection constants and helpers used across the mod.
    /// </summary>
    public static class ReflectionUtils
    {
        /// <summary>BindingFlags for private/protected instance members.</summary>
        public const BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>BindingFlags for public instance members.</summary>
        public const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        /// <summary>BindingFlags for all (public + non-public) instance members.</summary>
        public const BindingFlags AllInstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Finds a type by full name across all loaded assemblies.
        /// Falls back to name-only matching if the full name is not found.
        /// </summary>
        public static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName);
                    if (type != null)
                        return type;

                    // Also try to find by name only (without namespace)
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == fullName || t.FullName == fullName)
                            return t;
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }
            return null;
        }
    }
}
