using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Goap.BuildTools
{
    public static class BuildRunner
    {
        /// <summary>Dossier racine des builds, relatif à la racine du projet. Ignoré par git.</summary>
        public const string OutputDir = "Build/Windows";

        /// <summary>
        /// Chemin (relatif à la racine projet) du script PowerShell d'upload.
        /// Doit rester synchronisé avec Tools/upload-itchio.ps1.
        /// </summary>
        private const string ScriptRelativePath = "Tools/upload-itchio.ps1";

        /// <summary>Variable d'environnement portant le tag de livraison (ex. `v0.3.1`).</summary>
        private const string VersionVariable = "PROJECT_VERSION";

        /// <summary>Le commit livré, ajouté à la version pour que les journaux se rattachent au dépôt.</summary>
        private const string ShaVariable = "PROJECT_SHA";

        /// <summary>Argument CLI équivalent à <see cref="VersionVariable"/> (utilisé via game-ci/unity-builder).</summary>
        private const string VersionArg = "-buildVersion";

        /// <summary>Argument CLI équivalent à <see cref="ShaVariable"/>.</summary>
        private const string ShaArg = "-buildSha";

        // ------------------------------------------------------------------
        // Menu items
        // ------------------------------------------------------------------

        [MenuItem("Tools/Build and Publish on itchio")]
        public static void BuildAndPublish()
        {
            if (!BuildPlayerNow(out string outputDir))
                return;

            string root = Directory.GetCurrentDirectory();
            string script = Path.Combine(root, ScriptRelativePath);

            if (!File.Exists(script))
            {
                Debug.LogError($"[{nameof(BuildRunner)}] Script d'envoi introuvable : {script}");
                return;
            }

            string version = ResolveVersion();
            string versionArg = string.IsNullOrWhiteSpace(version)
                ? string.Empty
                : $" -Version \"{NormalizeVersion(version)}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
                            $"-BuildDir \"{outputDir}\"{versionArg}",
                WorkingDirectory = root,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Debug.LogError($"[{nameof(BuildRunner)}] Impossible de démarrer powershell.exe.");
                return;
            }

            Debug.Log($"[{nameof(BuildRunner)}] Envoi en cours (PID {process.Id}) pour {outputDir}...");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.LogError($"[{nameof(BuildRunner)}] Envoi échoué (code {process.ExitCode}). " +
                               $"Relancez '{ScriptRelativePath}' manuellement pour voir la sortie complète.");
                return;
            }

            Debug.Log($"[{nameof(BuildRunner)}] Envoi terminé avec succès pour {outputDir}.");
        }

        [MenuItem("Tools/Build")]
        public static void BuildWindows()
        {
            if (BuildPlayerNow(out _) && Application.isBatchMode)
                EditorApplication.Exit(0);
            else if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        /// <summary>La build elle-même. Rend `false` si elle a échoué.</summary>
        private static bool BuildPlayerNow(out string outputDir)
        {
            outputDir = null;

            // 1. S'assurer que la cible active correspond à ce qu'on construit.
            //    Sinon BuildPipeline.BuildPlayer échoue avec un message obscur
            //    (surtout en local, quand l'éditeur est resté sur une autre plateforme).
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.Log($"[{nameof(BuildRunner)}] Bascule vers StandaloneWindows64...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                {
                    Fail("Impossible de basculer vers StandaloneWindows64 (module non installé ?).");
                    return false;
                }
            }

            string previousVersion = PlayerSettings.bundleVersion;
            string version = ResolveVersion();
            string sha = ResolveSha();

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

            string root = Path.GetFullPath(Directory.GetCurrentDirectory());
            outputDir = Path.GetFullPath(Path.Combine(root, OutputDir, folderName));

            // On ne supprime QUE le dossier de cette version, pas les autres.
            if (Directory.Exists(outputDir))
            {
                try
                {
                    Directory.Delete(outputDir, true);
                }
                catch (Exception e)
                {
                    Fail($"Impossible de nettoyer '{outputDir}' : {e.Message}");
                    return false;
                }
            }
            Directory.CreateDirectory(outputDir);

            // Nom d'exécutable assaini : Unity échoue si ProductName contient
            // un caractère invalide pour un nom de fichier.
            string exeName = SanitizeFileName(Application.productName);
            if (string.IsNullOrEmpty(exeName))
                exeName = "Game";

            BuildPlayerOptions options = new()
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDir, $"{exeName}.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                // Pour une build de release, on peut passer à BuildOptions.StrictMode
                // (mais cela fait échouer la build sur de simples warnings).
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
                try { AssetDatabase.SaveAssets(); }
                catch (Exception e) { Debug.LogWarning($"[{nameof(BuildRunner)}] SaveAssets: {e.Message}"); }
            }

            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result} — {summary.totalErrors} erreur(s).");
                return false;
            }

            // Marqueur lisible par la CI. Son emplacement (Build/Windows/<version>/build-info.txt)
            // doit rester compatible avec le `find Build/Windows -maxdepth 2 -name build-info.txt`
            // du workflow. Il est exclu de l'upload itch via --ignore dans upload-itchio.ps1.
            try
            {
                File.WriteAllText(
                    Path.Combine(outputDir, "build-info.txt"),
                    $"folder={folderName}\nversion={stamped}\npath={outputDir}\n");
            }
            catch (Exception e)
            {
                // Ne pas tuer une build réussie pour un fichier de métadonnées.
                Debug.LogWarning($"[{nameof(BuildRunner)}] Écriture de build-info.txt ignorée : {e.Message}");
            }

            Debug.Log($"BUILD_OUTPUT_PATH={outputDir}");
            Debug.Log($"[{nameof(BuildRunner)}] Build OK : {summary.totalSize / (1024 * 1024)} Mo, "
                      + $"version {stamped}, dans {outputDir}");

            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Version effective : argument CLI `-buildVersion` (via game-ci) sinon
        /// variable d'environnement <see cref="VersionVariable"/> (usage local).
        /// </summary>
        private static string ResolveVersion()
        {
            return GetArgValue(VersionArg)
                   ?? Environment.GetEnvironmentVariable(VersionVariable);
        }

        /// <summary>SHA effectif : argument CLI `-buildSha` sinon <see cref="ShaVariable"/>.</summary>
        private static string ResolveSha()
        {
            return GetArgValue(ShaArg)
                   ?? Environment.GetEnvironmentVariable(ShaVariable);
        }

        /// <summary>
        /// Récupère la valeur d'un argument CLI de la forme `-name value`.
        /// Renvoie null si l'argument est absent ou sans valeur.
        /// </summary>
        private static string GetArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    string value = args[i + 1];
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            return null;
        }

        /// <summary>
        /// Nom du dossier de build.
        /// Priorité : PROJECT_VERSION → sinon version actuelle du projet → sinon "dev".
        /// Toujours sans suffixe "+sha" et sans "v" initial.
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

            return SanitizeFileName(baseName);
        }

        private static string NormalizeVersion(string version)
        {
            string trimmed = version.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V'))
                trimmed = trimmed.Substring(1);
            return trimmed;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

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
