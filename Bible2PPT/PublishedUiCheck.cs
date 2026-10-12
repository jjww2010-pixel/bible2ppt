using System;
using System.IO;
using System.Windows.Forms;
using Bible2PPT.PPT;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Services.BuildService;
using Bible2PPT.Services.TemplateService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible2PPT;

internal static class PublishedUiCheck
{
    // Uses the real MainForm and event handlers. Rendering is deliberately
    // disabled: hosted Windows runners do not have Microsoft PowerPoint.
    public static int Run(string[] args)
    {
        if (args.Length != 2) return 2;
        var report = Path.GetFullPath(args[1]);
        var directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-ui", Guid.NewGuid().ToString("N"));
        var result = 1;
        try
        {
            Directory.CreateDirectory(directory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var services = new ServiceCollection();
            services.AddBibleIndexService(options => options.UseSqlite($"Data Source={directory}/index.db;Pooling=False"));
            services.AddBibleService(options => options.UseSqlite($"Data Source={directory}/bible.db;Pooling=False"));
            services.AddDbContextFactory<BuildContext>(options => options.UseSqlite($"Data Source={directory}/build.db;Pooling=False"));
            services.AddTemplateService();
            using var provider = services.BuildServiceProvider();
            provider.UseBibleIndexService(); provider.UseBibleService(); provider.UseBuildService();
            var builder = new UiOnlyBuilder(provider.GetRequiredService<ZippedBibleService>(),
                provider.GetRequiredService<VerseQueryParser>(), provider.GetRequiredService<IDbContextFactory<BuildContext>>());
            try
            {
                using var form = new MainForm(builder, provider.GetRequiredService<BibleService>(), provider.GetRequiredService<TemplateService>());
                form.Shown += async (_, _) =>
                {
                    try
                    {
                        await form.CheckUiAsync();
                        File.WriteAllText(report, "UI OK\nBooks=66\nNavigation=3\nHiddenTemplateLibrary=EXPECTED\nTemplateExtraction=OK\nBookSelection=IJohn\nBibleRemoveAdd=OK\nSettings=OK\nTranslations=3\nTranslationSelection=OK\nPowerPoint=NOT_TESTED\n");
                        result = 0;
                    }
                    catch (Exception ex) { File.WriteAllText(report, ex.ToString()); }
                    finally { form.Close(); }
                };
                Application.Run(form);
            }
            finally { builder.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            return result;
        }
        catch (Exception ex) { File.WriteAllText(report, ex.ToString()); return 1; }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class UiOnlyBuilder : Builder
    {
        public UiOnlyBuilder(ZippedBibleService zipped, VerseQueryParser parser, IDbContextFactory<BuildContext> factory)
            : base(null!, zipped, parser, factory) { }
        protected override IPresentationWriter CreatePresentation(Job job, string output) =>
            throw new InvalidOperationException("UI diagnostic does not render PowerPoint slides.");
    }
}
