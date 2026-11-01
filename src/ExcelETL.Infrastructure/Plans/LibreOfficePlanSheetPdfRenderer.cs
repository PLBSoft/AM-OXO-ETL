using System.Diagnostics;
using ExcelETL.Application.Plans;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExcelETL.Infrastructure.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.5, decisions D7/D10/D12): prepares
// the sheet (OpenXmlPlanWorkbookPreparer), prints it once with LibreOffice in headless mode, then splits
// the PDF page by page with PDFsharp (launching LibreOffice is most of the cost, so one launch per sheet,
// not per page). One conversion at a time per profile, across processes too: two LibreOffice instances
// sharing a profile collide (the second hands its job to the first and exits without a PDF). That
// happens when IIS overlaps two worker processes during a recycle, or when two test projects run at
// once -- hence a lock file next to the profile, on top of the in-process semaphore.
public sealed class LibreOfficePlanSheetPdfRenderer(
    IOptions<PlanRenderingOptions> options,
    ILogger<LibreOfficePlanSheetPdfRenderer> logger) : IPlanSheetPdfRenderer
{
    private static readonly SemaphoreSlim OneConversionAtATime = new(1, 1);

    private const string InputFileName = "plans.xlsx";
    private const string OutputFileName = "plans.pdf";

    public async Task<RenderedPlanSheet?> RenderAsync(
        byte[] workbook, string planSheetName, CancellationToken cancellationToken = default)
    {
        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, planSheetName);
        if (prepared is null)
        {
            return null;
        }

        if (prepared.PreparedWorkbook is null)
        {
            return new RenderedPlanSheet(prepared.SheetName, []);
        }

        var stopwatch = Stopwatch.StartNew();
        var pdf = await ConvertAsync(prepared.PreparedWorkbook, cancellationToken);
        var pages = SplitPages(pdf);

        if (pages.Count != prepared.Layout.Pages.Count)
        {
            logger.LogWarning(
                "Plan sheet {SheetName}: {Expected} page(s) detected but LibreOffice printed {Actual}",
                prepared.SheetName, prepared.Layout.Pages.Count, pages.Count);
        }

        logger.LogInformation(
            "Plan sheet {SheetName} printed to {PageCount} PDF page(s) in {ElapsedMs}ms",
            prepared.SheetName, pages.Count, stopwatch.ElapsedMilliseconds);

        return new RenderedPlanSheet(prepared.SheetName, pages);
    }

    private async Task<byte[]> ConvertAsync(byte[] preparedWorkbook, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!File.Exists(settings.SofficePath))
        {
            throw new PlanRenderingException(
                $"LibreOffice was not found at '{settings.SofficePath}' (setting {PlanRenderingOptions.SectionName}:SofficePath).");
        }

        var workDirectory = Path.Combine(Path.GetTempPath(), "am-oxo-etl-plans", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var profileDirectory = settings.ProfileDirectory
            ?? Path.Combine(Path.GetTempPath(), "am-oxo-etl-libreoffice-profile");

        await OneConversionAtATime.WaitAsync(cancellationToken);
        try
        {
            await using var profileLock = await AcquireProfileLockAsync(
                profileDirectory, TimeSpan.FromSeconds(2 * settings.TimeoutSeconds), cancellationToken);

            var inputPath = Path.Combine(workDirectory, InputFileName);
            await File.WriteAllBytesAsync(inputPath, preparedWorkbook, cancellationToken);

            await RunAsync(settings, profileDirectory, workDirectory, inputPath, cancellationToken);

            var outputPath = Path.Combine(workDirectory, OutputFileName);
            if (!File.Exists(outputPath))
            {
                throw new PlanRenderingException("LibreOffice ended without producing a PDF.");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            OneConversionAtATime.Release();
            TryDelete(workDirectory);
        }
    }

    private static async Task RunAsync(
        PlanRenderingOptions settings, string profileDirectory, string workDirectory, string inputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(settings.SofficePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workDirectory,
        };
        startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(profileDirectory).AbsoluteUri);
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(workDirectory);
        startInfo.ArgumentList.Add(inputPath);

        using var process = Process.Start(startInfo)
            ?? throw new PlanRenderingException("LibreOffice could not be started.");
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        _ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new PlanRenderingException($"LibreOffice did not finish within {settings.TimeoutSeconds} s.");
        }

        if (process.ExitCode != 0)
        {
            throw new PlanRenderingException(
                $"LibreOffice failed (exit code {process.ExitCode}): {(await standardError).Trim()}");
        }
    }

    private static async Task<FileStream> AcquireProfileLockAsync(
        string profileDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var lockPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(profileDirectory)) + ".lock";
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (IOException ex)
            {
                throw new PlanRenderingException("LibreOffice is busy with another process for too long.", ex);
            }
        }
    }

    private static IReadOnlyList<byte[]> SplitPages(byte[] pdf)
    {
        using var source = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var pages = new List<byte[]>(source.PageCount);
        foreach (var page in source.Pages)
        {
            using var single = new PdfDocument();
            single.AddPage(page);
            using var output = new MemoryStream();
            single.Save(output);
            pages.Add(output.ToArray());
        }

        return pages;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A killed process may still hold a file for a moment; the folder sits in the temp directory.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
