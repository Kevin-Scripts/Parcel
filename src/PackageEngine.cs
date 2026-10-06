using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Parcel
{
    public sealed class ModeRules
    {
        public string[] include { get; set; }
        public string[] exclude { get; set; }
        public string[] escrowIgnore { get; set; }
    }
    public sealed class PackageConfig
    {
        public string resourceName { get; set; }
        public string[] commonInclude { get; set; }
        public string[] commonExclude { get; set; }
        public string[] requiredFiles { get; set; }
        public Dictionary<string, ModeRules> modes { get; set; }
    }
    public sealed class PackageFile
    {
        public string Path { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public long Size { get; set; }
    }
    public sealed class PackagePlan
    {
        public string Root;
        public string Name;
        public string Mode;
        public string[] Ignore;
        public List<PackageFile> Files;
        public string Manifest;
    }
    public static class PackageEngine
    {
        public static readonly string[] AlwaysExclude = {
            ".*", "*/.*", "node_modules", "*/node_modules", "releases", "*/releases",
            "Parcel", "*/Parcel", "Parcel.exe", "*/Parcel.exe", "FivemResourceReady", "*/FivemResourceReady", "FivemResourceReady.exe", "*/FivemResourceReady.exe"
        };
        public static readonly string[] DefaultExclude = {
            "tests", "*/tests", "test", "*/test", "coverage", "*/coverage",
            "*.log", "*.tmp", "*.bak", "*.map", "*.zip", "*.psd", "*.blend",
            "Thumbs.db", "*/Thumbs.db", "desktop.ini", "*/desktop.ini",
            "packaging.json", "package-assets.cmd", "scripts/package-assets.ps1", "scripts/PACKAGING.md"
        };
        public static PackageConfig Load(string root)
        {
            CheckRoot(root);
            string path = System.IO.Path.Combine(root, "packaging.json");
            if (File.Exists(path)) {
                PackageConfig config = new JavaScriptSerializer().Deserialize<PackageConfig>(File.ReadAllText(path));
                if (config == null || config.modes == null || !config.modes.ContainsKey("escrow") || !config.modes.ContainsKey("source"))
                    throw new InvalidDataException("packaging.json must contain escrow and source mode rules.");
                return config;
            }
            var escrowExclude = new List<string>();
            // Only trim known UI source layouts when a compiled build exists.
            foreach (string ui in new[] { "web", "ui", "html" }) {
                if (!File.Exists(System.IO.Path.Combine(root, ui, "dist", "index.html"))) continue;
                foreach (string part in new[] { "src", "public", "assets", "package.json", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "vite.config.*", "tsconfig*", "jsconfig*" })
                    escrowExclude.Add(ui + "/" + part);
            }
            return new PackageConfig {
                resourceName = new DirectoryInfo(root).Name,
                commonInclude = new string[0], commonExclude = DefaultExclude,
                requiredFiles = new[] { "fxmanifest.lua" },
                modes = new Dictionary<string, ModeRules> {
                    { "escrow", new ModeRules { include = new string[0], exclude = escrowExclude.ToArray(), escrowIgnore = new[] {
                        "config.lua", "config/*.lua", "config/**/*.lua", "configs/*.lua", "configs/**/*.lua",
                        "bridge/*.lua", "bridge/**/*.lua", "locales/*.lua", "locales/**/*.lua"
                    } } },
                    { "source", new ModeRules { include = new string[0], exclude = new string[0], escrowIgnore = new[] { "*", "**/*" } } }
                }
            };
        }
        public static string[] OrEmpty(string[] value) { return value ?? new string[0]; }
        public static bool Match(string path, string pattern)
        {
            return Regex.IsMatch(path, "^" + Regex.Escape(pattern.Replace('\\', '/')).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }
        static void CheckRoot(string root)
        {
            if (!Directory.Exists(root) || !File.Exists(System.IO.Path.Combine(root, "fxmanifest.lua")))
                throw new InvalidDataException("Select a resource folder containing fxmanifest.lua, or select fxmanifest.lua itself.");
            CheckPath(root, "fxmanifest.lua");
        }
        static void CheckPath(string root, string relative)
        {
            string path = root;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links and junctions cannot be packaged: " + path);
            foreach (string segment in relative.Split('/')) {
                path = System.IO.Path.Combine(path, segment);
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic links and junctions cannot be packaged: " + relative);
            }
        }
        public static PackagePlan Scan(string root, string name, string mode, PackageConfig config, string[] exclusions, string[] ignore)
        {
            root = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            CheckRoot(root);
            if (!Regex.IsMatch(name, "^[a-zA-Z0-9][a-zA-Z0-9_-]*$"))
                throw new InvalidDataException("Resource name must contain only letters, numbers, hyphens and underscores.");
            string[] includes = OrEmpty(config.commonInclude).Concat(OrEmpty(config.modes[mode].include)).ToArray();
            foreach (string include in includes)
                if (System.IO.Path.IsPathRooted(include) || include.Split('/', '\\').Contains(".."))
                    throw new InvalidDataException("Include paths must stay inside the resource: " + include);
            foreach (string pattern in ignore)
                if (pattern.IndexOfAny(new[] { '\'', '\\', '\r', '\n', ':' }) >= 0 || pattern.StartsWith("/") || pattern.Split('/').Contains(".."))
                    throw new InvalidDataException("Invalid editable-file pattern: " + pattern + ". Use / as the path separator.");
            var files = new List<PackageFile>();
            Walk(root, root, files, includes, exclusions, null);
            var included = new HashSet<string>(files.Where(f => f.Status == "Included").Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            foreach (string required in OrEmpty(config.requiredFiles).Concat(new[] { "fxmanifest.lua" }))
                if (!included.Contains(required)) throw new InvalidDataException("Required file is missing or excluded: " + required);
            string manifest = File.ReadAllText(System.IO.Path.Combine(root, "fxmanifest.lua"));
            // Check explicit local references without attempting to execute a Lua manifest.
            foreach (Match match in Regex.Matches(manifest, @"\bui_page\s*(?:\(\s*)?['""]([^'""\r\n]+)['""]")) {
                string page = match.Groups[1].Value;
                if (!page.Contains("://") && !included.Contains(page))
                    throw new InvalidDataException("The manifest's UI page is missing or excluded: " + page);
            }
            string[] finalIgnore = mode == "source" ? new[] { "*", "**/*" } : ignore;
            manifest = manifest.TrimEnd() + "\n\n-- Prepared by Parcel (" + mode + ").\nescrow_ignore {\n"
                + string.Join("\n", finalIgnore.Select(p => "    '" + p + "',")) + "\n}\n";
            return new PackagePlan { Root = root, Name = name, Mode = mode, Ignore = finalIgnore,
                Files = files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList(), Manifest = manifest };
        }
        static void Walk(string root, string directory, List<PackageFile> files, string[] includes, string[] exclusions, string inherited)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory)) {
                string relative = path.Substring(root.Length + 1).Replace('\\', '/');
                FileAttributes attributes = File.GetAttributes(path);
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                string enforced = AlwaysExclude.FirstOrDefault(p => Match(relative, p));
                if ((attributes & FileAttributes.ReparsePoint) != 0 || enforced != null) {
                    files.Add(new PackageFile { Path = relative + (isDirectory ? "/" : ""), Status = "Excluded", Reason = enforced == null ? "Symbolic link / junction" : "Always excluded: " + enforced });
                    continue;
                }
                string rule = inherited ?? exclusions.FirstOrDefault(p => Match(relative, p));
                if (isDirectory) {
                    if (rule != null) files.Add(new PackageFile { Path = relative + "/", Status = "Excluded", Reason = "Excluded folder: " + rule });
                    else Walk(root, path, files, includes, exclusions, null);
                    continue;
                }
                bool allowed = includes.Length == 0 || includes.Any(p => relative.Equals(p, StringComparison.OrdinalIgnoreCase) || relative.StartsWith(p.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
                files.Add(new PackageFile { Path = relative, Size = new FileInfo(path).Length,
                    Status = rule == null && allowed ? "Included" : "Excluded",
                    Reason = rule != null ? "Rule: " + rule : allowed ? "Selected for package" : "Outside include list" });
            }
        }
        public static void Create(PackagePlan plan, string destination)
        {
            string full = System.IO.Path.GetFullPath(destination);
            if (File.Exists(full)) throw new IOException("A file already exists at that location. Choose a new ZIP name.");
            if (plan.Files.Any(f => f.Status == "Included" && string.Equals(System.IO.Path.Combine(plan.Root, f.Path.Replace('/', '\\')), full, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("The output cannot replace a resource file.");
            bool created = false;
            try {
                using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write)) {
                    created = true;
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create)) {
                        foreach (PackageFile file in plan.Files.Where(f => f.Status == "Included")) {
                            CheckPath(plan.Root, file.Path);
                            var entry = archive.CreateEntry(plan.Name + "/" + file.Path, CompressionLevel.Optimal);
                            using (Stream target = entry.Open()) {
                                if (file.Path == "fxmanifest.lua") {
                                    byte[] bytes = new UTF8Encoding(false).GetBytes(plan.Manifest);
                                    target.Write(bytes, 0, bytes.Length);
                                } else {
                                    using (var source = File.OpenRead(System.IO.Path.Combine(plan.Root, file.Path))) source.CopyTo(target);
                                }
                            }
                        }
                    }
                }
                if (new FileInfo(full).Length >= 1000000000) throw new IOException("The ZIP exceeds the 1 GB upload limit.");
            } catch {
                if (created && File.Exists(full)) File.Delete(full);
                throw;
            }
        }
    }
}
