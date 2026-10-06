using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Parcel;

static class PackageTests
{
    static int checks;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Put(string root, string relative, string content) { string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, content); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
    static int Main()
    {
        try {
            string tests = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-output", Guid.NewGuid().ToString("N"));
            string root = Path.Combine(tests, "test-resource");
            Directory.CreateDirectory(root);
            string original = "fx_version 'cerulean'\ngame 'gta5'\nui_page 'web/dist/index.html'\nescrow_ignore {'custom.lua'}\n";
            Put(root, "fxmanifest.lua", original);
            Put(root, "client/main.lua", "print('example')");
            Put(root, "config.lua", "Config = {}");
            Put(root, "custom.lua", "return {}");
            Put(root, "web/dist/index.html", "<html></html>");
            Put(root, "web/src/App.vue", "<template></template>");
            Put(root, "web/package.json", "{}");
            Put(root, "web/dist/app.js.map", "{}");
            Put(root, "web/node_modules/test/index.js", "secret dependency");
            Put(root, ".env", "secret");
            Put(root, ".headroom-tools/test.txt", "private");
            Put(root, "tests/test.lua", "test");
            Put(root, "releases/previous.zip", "old archive");
            PackageConfig config = PackageEngine.Load(root);
            foreach (string mode in new[] { "escrow", "source" }) {
                string[] excluded = config.commonExclude.Concat(config.modes[mode].exclude).ToArray();
                PackagePlan plan = PackageEngine.Scan(root, config.resourceName, mode, config, excluded, config.modes[mode].escrowIgnore);
                string zip = Path.Combine(tests, mode + ".zip");
                PackageEngine.Create(plan, zip);
                using (var stream = File.OpenRead(zip)) using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
                    var names = archive.Entries.Select(e => e.FullName).ToArray();
                    Assert(names.Contains("test-resource/client/main.lua"), "Runtime file missing");
                    Assert(names.Contains("test-resource/web/src/App.vue") == (mode == "source"), "Wrong UI source selection");
                    Assert(!names.Any(p => p.Contains("node_modules") || p.Contains(".headroom-tools") || p.Contains(".env") || p.Contains("/tests/") || p.Contains("/releases/") || p.EndsWith(".map")), "Excluded content leaked");
                    using (var reader = new StreamReader(archive.GetEntry("test-resource/fxmanifest.lua").Open())) {
                        string manifest = reader.ReadToEnd();
                        Assert(manifest.Contains("escrow_ignore {'custom.lua'}"), "Existing escrow rules lost");
                        Assert(manifest.Contains(mode == "source" ? "'**/*'" : "'config.lua'"), "Generated escrow rules missing");
                    }
                    foreach (var entry in archive.Entries) using (var source = entry.Open()) source.CopyTo(Stream.Null);
                }
                byte[] before = File.ReadAllBytes(zip);
                bool refused = false;
                try { PackageEngine.Create(plan, zip); } catch (IOException) { refused = true; }
                Assert(refused && before.SequenceEqual(File.ReadAllBytes(zip)), "Existing ZIP overwritten");
                Assert(File.ReadAllText(Path.Combine(root, "fxmanifest.lua")) == original, "Original manifest changed");
            }
            Reject(() => PackageEngine.Scan(root, "bad/name", "escrow", config, new string[0], new string[0]), "Unsafe name accepted");
            Reject(() => PackageEngine.Scan(root, "test", "escrow", config, new[] { "web/dist" }, new string[0]), "Missing UI accepted");
            Reject(() => PackageEngine.Scan(root, "test", "escrow", config, new string[0], new[] { "../secret.lua" }), "Unsafe ignore accepted");
            File.Delete(Path.Combine(root, "web/dist/index.html"));
            var noBuild = PackageEngine.Load(root);
            Assert(!noBuild.modes["escrow"].exclude.Contains("web/src"), "Source excluded without a UI build");
            // Exercise a custom configuration independently of the development workspace.
            Put(root, "web/dist/index.html", "<html></html>");
            Put(root, "server/data_schema.sql", "SELECT 1;");
            Put(root, "packaging.json", "{\"resourceName\":\"custom-resource\",\"commonInclude\":[\"fxmanifest.lua\",\"web/dist\",\"server\"],\"commonExclude\":[],\"requiredFiles\":[\"server/data_schema.sql\"],\"modes\":{\"escrow\":{\"exclude\":[],\"escrowIgnore\":[]},\"source\":{\"include\":[\"web/src\"],\"exclude\":[],\"escrowIgnore\":[\"*\",\"**/*\"]}}}");
            var real = PackageEngine.Load(root);
            foreach (string mode in new[] { "escrow", "source" }) {
                var plan = PackageEngine.Scan(root, real.resourceName, mode, real,
                    real.commonExclude.Concat(real.modes[mode].exclude).ToArray(), real.modes[mode].escrowIgnore);
                Assert(plan.Files.Any(f => f.Path == "server/data_schema.sql" && f.Status == "Included"), "SQL missing");
                Assert(!plan.Files.Any(f => f.Status == "Included" && f.Path.StartsWith("Parcel")), "App included in resource");
            }
            Console.WriteLine("PASS: " + checks + " checks; escrow/source ZIPs, exclusions, preserved originals, existing rules, validation and project config.");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
