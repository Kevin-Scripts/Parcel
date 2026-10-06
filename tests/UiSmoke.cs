using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Parcel;

static class UiSmoke
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int result = 1;
        var form = new ReadyForm(false) { ShowInTaskbar = false, Opacity = 0 };
        Exception failure = null;
        form.ErrorReporter = e => { failure = e; };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        form.Shown += async delegate {
            try {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-output", "ui-resource");
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "fxmanifest.lua"), "fx_version 'cerulean'\ngame 'gta5'\nclient_script 'client.lua'\n");
                File.WriteAllText(Path.Combine(root, "client.lua"), "print('UI test')\n");
                Directory.CreateDirectory(Path.Combine(root, "sub"));
                File.WriteAllText(Path.Combine(root, "sub", "extra.lua"), "print('extra')\n");
                File.WriteAllText(Path.Combine(root, "sub", "other.lua"), "print('other')\n");
                await (Task)typeof(ReadyForm).GetMethod("LoadResource", flags).Invoke(form, new object[] { root });
                if (failure != null) throw failure;
                var field = typeof(ReadyForm).GetField("plan", flags);
                var plan = (PackagePlan)field.GetValue(form);
                if (plan == null || plan.Mode != "escrow") throw new Exception("Escrow preview failed");
                ((ComboBox)typeof(ReadyForm).GetField("mode", flags).GetValue(form)).SelectedIndex = 1;
                DateTime limit = DateTime.UtcNow.AddSeconds(15);
                while ((field.GetValue(form) == null || ((PackagePlan)field.GetValue(form)).Mode != "source") && DateTime.UtcNow < limit && failure == null) await Task.Delay(20);
                if (failure != null) throw failure;
                plan = (PackagePlan)field.GetValue(form);
                if (plan == null || plan.Mode != "source") throw new Exception("Source preview failed");
                if (!((Button)typeof(ReadyForm).GetField("create", flags).GetValue(form)).Enabled) throw new Exception("Export button disabled");
                using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-preview.png"));
                }
                Func<string, string> statusOf = p => ((PackagePlan)field.GetValue(form)).Files.First(f => f.Path == p).Status;
                var addExclusions = typeof(ReadyForm).GetMethod("AddExclusions", flags);
                await (Task)addExclusions.Invoke(form, new object[] { new[] { "sub/extra.lua" } });
                if (failure != null) throw failure;
                if (statusOf("sub/extra.lua") != "Excluded" || statusOf("sub/other.lua") != "Included") throw new Exception("Single-file exclusion failed");
                await (Task)addExclusions.Invoke(form, new object[] { new[] { "sub" } });
                if (failure != null) throw failure;
                if (statusOf("sub/") != "Excluded") throw new Exception("Folder exclusion failed");
                Console.WriteLine("PASS: UI loads resource, switches modes, populates preview and enables export.");
                result = 0;
            } catch (Exception error) { Console.Error.WriteLine(error); }
            finally { form.Close(); }
        };
        Application.Run(form);
        form.Dispose();
        return result;
    }
}
