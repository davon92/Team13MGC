#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

namespace Arawn.XNodeIntegration.Editor
{
    [InitializeOnLoad]
    public static class DefineSymbols
    {
        private const string INTEGRATION_SYMBOL = "XNODE";

        private static readonly BuildTargetGroup[] TargetGroups =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,
            BuildTargetGroup.PS5,
            BuildTargetGroup.XboxOne,
            BuildTargetGroup.Switch
        };

        private static bool _busy;

        static DefineSymbols()
        {
            AssemblyReloadEvents.afterAssemblyReload += UpdateDefines;
            EditorApplication.projectChanged         += UpdateDefines;
            UpdateDefines();            // initial pass
        }

        private static void UpdateDefines()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                bool xnodePresent = TypeExists("XNode.Node");

                foreach (BuildTargetGroup group in TargetGroups)
                {
                    if (group == BuildTargetGroup.Unknown) continue;

                    var nbt = NamedBuildTarget.FromBuildTargetGroup(group);
                    string current = PlayerSettings.GetScriptingDefineSymbols(nbt);
                    var symbols = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .ToList();

                    if (xnodePresent && !symbols.Contains(INTEGRATION_SYMBOL))
                        symbols.Add(INTEGRATION_SYMBOL);
                    else if (!xnodePresent)
                        symbols.Remove(INTEGRATION_SYMBOL);

                    string updated = string.Join(";", symbols);
                    if (updated != current)
                        PlayerSettings.SetScriptingDefineSymbols(nbt, updated);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[xNode DefineSymbols] {ex}");
            }
            finally
            {
                _busy = false;
            }
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────
        private static bool TypeExists(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(a => {
                                       try   { return a.GetTypes(); }
                                       catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
                                   })
                                   .Any(t => t.FullName == fullName);
    }
}
#endif
