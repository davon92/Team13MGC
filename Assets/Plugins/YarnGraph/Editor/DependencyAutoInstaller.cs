#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Arawn.Editor
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-250)]
    public static class DependencyAutoInstaller
    {
        private readonly struct Pkg
        {
            public readonly string TypeName;
            public readonly string UpmId;
            public readonly string DisplayName;

            public Pkg(string typeName, string upmId, string displayName)
            {
                TypeName = typeName;
                UpmId = upmId;
                DisplayName = displayName;
            }

            public bool IsGitHub => UpmId.StartsWith("https://github.com/");
            public string PackageName => IsGitHub ? ExtractPackageName(UpmId) : UpmId;

            private static string ExtractPackageName(string url)
            {
                // https://github.com/Siccity/xNode.git#1.8.0 → com.siccity.xnode.manual
                Uri uri = new(url.Split('#')[0]);
                string[] parts = uri.AbsolutePath.Trim('/').Split('/');
                return $"com.{parts[0].ToLowerInvariant()}.{parts[1].ToLowerInvariant()}.manual";
            }
        }

        private static readonly Pkg[] Packages =
        {
            new Pkg("Yarn.Unity.DialogueRunner",
                "https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git#v3.0.3",
                "Yarn Spinner 3.0.3 (MIT)"),

            new Pkg("XNode.Node",
                "https://github.com/Siccity/xNode.git#1.8.0",
                "xNode 1.8.0 (MIT)"),

            new Pkg("Unity.EditorCoroutines.Editor.EditorCoroutine",
                "com.unity.editorcoroutines",
                "Unity Editor Coroutines (Unity Registry)")
        };

        private const string PromptKey = "Arawn.DependencyAutoInstallerPrompted";
        private static readonly Queue<Pkg> _queue = new();
        private static AddRequest _addRequest;

        static DependencyAutoInstaller() => EditorApplication.update += FirstCheck;

        private static void FirstCheck()
        {
            EditorApplication.update -= FirstCheck;

            foreach (var pkg in Packages)
                if (!TypeExists(pkg.TypeName))
                    _queue.Enqueue(pkg);

            if (_queue.Count == 0) return;
            if (SessionState.GetBool(PromptKey, false)) return;
            SessionState.SetBool(PromptKey, true);

            string list = string.Join("\n • ", _queue.Select(p => p.DisplayName));
            bool install = EditorUtility.DisplayDialog(
                "Install required dependencies?",
                "The integration needs the following Unity packages:\n\n • " + list +
                "\n\nInstall them now?",
                "Install", "Cancel");

            if (!install)
            {
                _queue.Clear();
                return;
            }

            InstallNext();
        }

        private static void InstallNext()
        {
            if (_queue.Count == 0) return;

            var pkg = _queue.Dequeue();
            if (pkg.IsGitHub)
                InstallGitHubPackage(pkg);
            else
            {
                _addRequest = Client.Add(pkg.UpmId);
                EditorApplication.update += Progress;
            }
        }

        private static void Progress()
        {
            if (!_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
                Debug.Log($"Installed: {_addRequest.Result.packageId}");
            else
                Debug.LogError($"Package install failed: {_addRequest.Error.message}");

            EditorApplication.update -= Progress;
            AssetDatabase.Refresh();
            InstallNext();
        }

        private static void InstallGitHubPackage(Pkg pkg)
        {
            try
            {
                string[] parts = pkg.UpmId.Split('#');
                string zipUrl = parts[0].Replace(".git", "") + $"/archive/refs/tags/{parts[1]}.zip";
                string tmpZip = Path.Combine(Path.GetTempPath(), $"{pkg.PackageName}.zip");
                string tmpExtract = Path.Combine(Path.GetTempPath(), $"{pkg.PackageName}_extracted");
                string destDir = Path.Combine("Packages", pkg.PackageName);

                new WebClient().DownloadFile(zipUrl, tmpZip);

                if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true);
                ZipFile.ExtractToDirectory(tmpZip, tmpExtract);

                string contentDir = Directory.GetDirectories(tmpExtract).FirstOrDefault();
                if (contentDir == null)
                {
                    Debug.LogError($"[Dependency] Could not find extracted directory for {pkg.DisplayName}");
                    return;
                }

                if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                CopyDirectory(contentDir, destDir);

                // Create package.json
                string jsonPath = Path.Combine(destDir, "package.json");
                if (!File.Exists(jsonPath))
                {
                    File.WriteAllText(jsonPath,
$@"{{
  ""name"": ""{pkg.PackageName}"",
  ""version"": ""{parts[1]}"",
  ""displayName"": ""{pkg.DisplayName}"",
  ""description"": ""{pkg.DisplayName} manually installed"",
  ""unity"": ""2021.3"",
  ""author"": {{
    ""name"": ""Manual GitHub Installer""
  }}
}}");
                }

                Debug.Log($"<color=green>[Dependency]</color> {pkg.DisplayName} installed to Packages/{pkg.PackageName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Dependency] Failed to install {pkg.DisplayName}:\n{ex}");
            }
            finally
            {
                AssetDatabase.Refresh();
                InstallNext();
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(sourceDir.Length + 1);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
        }

        private static bool TypeExists(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
                })
                .Any(t => t.FullName == fullName);
    }
}
#endif
