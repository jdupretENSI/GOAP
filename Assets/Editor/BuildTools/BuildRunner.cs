using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Goap.BuildTools
{
    public static class BuildRunner
    {
        /// <summary>Dossier racine des builds, relatif à la racine du projet. Ignoré par git.</summary>
        public const string OutputDir = "Build/Windows";

        /// <summary>Variable d'environnement portant le tag de livraison (ex. `v0.3.1`).</summary>
        private const string VersionVariable = "PROJECT_VERSION";

        /// <summary>Le commit livré, ajouté à la version pour que les journaux se rattachent au dépôt.</summary>
        private const string ShaVariable = "PROJECT_SHA";

        [MenuItem("Tools/Build and Publish on itchio")]
        public static void BuildAndPublish()
        {
            if (!BuildPlayerNow(out string outputDir))
                return;

            string root = Directory.GetCurrentDirectory();
            string script = Path.Combine(root, "Tools", "upload-itchio.ps1");

            if (!File.Exists(script))
            {
                Debug.LogError($"[{nameof(BuildRunner)}] Script d'envoi introuvable : {script}");
                return;
            }

            string version = Environment.GetEnvironmentVariable(VersionVariable);
            string versionArg = string.IsNullOrWhiteSpace(version)
                ? string.Empty
                : $" -Version \"{NormalizeVersion(version)}\"";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -SkipBuild " +
                            $"-BuildDir \"{outputDir}\"{versionArg}",
                WorkingDirectory = root,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(psi);
            Debug.Log($"[{nameof(BuildRunner)}] Envoi lancé (PID {process?.Id}) pour {outputDir}.");
        }

        [MenuItem("Tools/Build")]
        public static void BuildWindows()
        {
            if (BuildPlayerNow(out _) && Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        /// <summary>La build elle-même. Rend `false` si elle a échoué.</summary>
        private static bool BuildPlayerNow(out string outputDir)
        {
            outputDir = null;

            string previousVersion = PlayerSettings.bundleVersion;
            string version = Environment.GetEnvironmentVariable(VersionVariable);
            string sha = Environment.GetEnvironmentVariable(ShaVariable);

            string stamped = previousVersion;
            if (!string.IsNullOrWhiteSpace(version))
            {
                stamped = NormalizeVersion(version);
                if (!string.IsNullOrWhiteSpace(sha))
                    stamped += "+" + sha.Trim();

                PlayerSettings.bundleVersion = stamped;
            }

            string[] scenes = EditorBuildSettings.scenes
                                                 .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                                                 .Select(s => s.path)
                                                 .ToArray();

            if (scenes.Length == 0)
            {
                Fail("Aucune scène activée dans les Build Settings : il n'y a rien à construire.");
                return false;
            }

            // Nom du dossier = version du jeu, sans SHA, sans "v".
            string folderName = BuildFolderName(version, stamped);

            string root = Directory.GetCurrentDirectory();
            outputDir = Path.Combine(root, OutputDir, folderName);

            // On ne supprime QUE le dossier de cette version, pas les autres.
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            BuildPlayerOptions options = new()
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDir, $"{Application.productName}.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                Fail($"Exception pendant la build : {e.Message}");
                return false;
            }
            finally
            {
                PlayerSettings.bundleVersion = previousVersion;
                AssetDatabase.SaveAssets();
            }

            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result} — {summary.totalErrors} erreur(s).");
                return false;
            }

            Debug.Log($"[{nameof(BuildRunner)}] Build OK : {summary.totalSize / (1024 * 1024)} Mo, "
                      + $"version {stamped}, dans {Path.Combine(OutputDir, folderName)}");

            return true;
        }

        /// <summary>
        /// Nom du dossier de build.
        /// Priorité : PROJECT_VERSION → sinon version actuelle du projet → sinon "dev".
        /// </summary>
        private static string BuildFolderName(string envVersion, string stampedVersion)
        {
            string baseName;

            if (!string.IsNullOrWhiteSpace(envVersion))
                baseName = NormalizeVersion(envVersion);
            else if (!string.IsNullOrWhiteSpace(stampedVersion))
                baseName = stampedVersion;
            else
                baseName = "dev";

            // Retire un éventuel suffixe "+sha" pour garder un nom de dossier propre.
            int plus = baseName.IndexOf('+');
            if (plus > 0)
                baseName = baseName.Substring(0, plus);

            return SanitizeFolderName(baseName);
        }

        private static string NormalizeVersion(string version)
        {
            string trimmed = version.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V'))
                trimmed = trimmed.Substring(1);
            return trimmed;
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[{nameof(BuildRunner)}] {message}");

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}