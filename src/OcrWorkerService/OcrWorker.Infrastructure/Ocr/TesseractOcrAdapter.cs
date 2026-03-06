using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Docnet.Core;
using Docnet.Core.Models;
using OcrWorker.Domain.Ports;
using SharedKernel;

namespace OcrWorker.Infrastructure.Ocr;

/// <summary>
/// OCR adapter that shells out to the tesseract CLI for text extraction.
/// Uses Docnet to render PDF pages to images before passing to Tesseract.
/// Requires tesseract to be installed on the system (apt-get install tesseract-ocr / brew install tesseract).
/// </summary>
public sealed class TesseractOcrAdapter : IOcrPort
{
    private readonly string _tessDataPath;
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly Regex KeyValuePattern = new(
        @"^([A-Za-z][A-Za-z\s]*\w)\s*:\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public TesseractOcrAdapter(string tessDataPath)
    {
        _tessDataPath = tessDataPath;
    }

    public async Task<Result<OcrResult>> ExtractTextAsync(
        Stream documentContent,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            using var ms = new MemoryStream();
            await documentContent.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (bytes.Length == 0)
            {
                return Result<OcrResult>.Failure(
                    new Error("OCR_ERROR", "Document content is empty."));
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var isPdf = PdfExtensions.Contains(extension);

            string rawText;

            if (isPdf)
            {
                rawText = await ExtractFromPdfAsync(bytes, ct);
            }
            else
            {
                rawText = await RunTesseractAsync(bytes, ct);
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return Result<OcrResult>.Failure(
                    new Error("OCR_ERROR", "No text could be extracted from the document."));
            }

            var fields = ParseFields(rawText);
            return Result<OcrResult>.Success(new OcrResult(rawText.Trim(), fields));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<OcrResult>.Failure(
                new Error("OCR_ERROR", $"OCR processing failed: {ex.Message}"));
        }
    }

    private async Task<string> RunTesseractAsync(byte[] imageBytes, CancellationToken ct)
    {
        // Write to temp file, run tesseract CLI, read output
        var tempInput = Path.GetTempFileName();
        var tempOutput = Path.GetTempFileName();

        try
        {
            await File.WriteAllBytesAsync(tempInput, imageBytes, ct);

            var psi = new ProcessStartInfo
            {
                FileName = "tesseract",
                ArgumentList = { tempInput, tempOutput, "--tessdata-dir", _tessDataPath, "-l", "eng" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start tesseract process.");

            // Drain stdout/stderr before WaitForExit to avoid deadlock when pipe buffers fill
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var stderr = await stderrTask;
            await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Tesseract exited with code {process.ExitCode}: {stderr}");
            }

            // Tesseract appends .txt to output filename
            var outputFile = tempOutput + ".txt";
            if (!File.Exists(outputFile))
                throw new InvalidOperationException("Tesseract did not produce output file.");

            return await File.ReadAllTextAsync(outputFile, ct);
        }
        finally
        {
            TryDelete(tempInput);
            TryDelete(tempOutput);
            TryDelete(tempOutput + ".txt");
        }
    }

    private async Task<string> ExtractFromPdfAsync(byte[] pdfBytes, CancellationToken ct)
    {
        var textBuilder = new StringBuilder();

        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1080, 1920));
        var pageCount = docReader.GetPageCount();

        for (var i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var pageReader = docReader.GetPageReader(i);
            var rawImage = pageReader.GetImage();
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            if (rawImage == null || rawImage.Length == 0 || width <= 0 || height <= 0)
                continue;

            var bmpBytes = ConvertBgraToBmp(rawImage, width, height);
            var pageText = await RunTesseractAsync(bmpBytes, ct);

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                textBuilder.AppendLine(pageText.Trim());
            }
        }

        return textBuilder.ToString();
    }

    private static byte[] ConvertBgraToBmp(byte[] bgraData, int width, int height)
    {
        var bpp = 3;
        var rowStride = (width * bpp + 3) & ~3;
        var pixelDataSize = rowStride * height;
        var headerSize = 54;
        var fileSize = headerSize + pixelDataSize;

        var bmp = new byte[fileSize];

        // File header
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(headerSize).CopyTo(bmp, 10);

        // Info header (BITMAPINFOHEADER)
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(height).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)(bpp * 8)).CopyTo(bmp, 28);
        BitConverter.GetBytes(pixelDataSize).CopyTo(bmp, 34);

        // Pixel data (BMP is bottom-up, Docnet is top-down BGRA)
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * width * 4;
            var dstRow = headerSize + (height - 1 - y) * rowStride;

            for (var x = 0; x < width; x++)
            {
                var srcIdx = srcRow + x * 4;
                var dstIdx = dstRow + x * 3;
                bmp[dstIdx] = bgraData[srcIdx];
                bmp[dstIdx + 1] = bgraData[srcIdx + 1];
                bmp[dstIdx + 2] = bgraData[srcIdx + 2];
            }
        }

        return bmp;
    }

    private static Dictionary<string, ExtractedField> ParseFields(string rawText)
    {
        var fields = new Dictionary<string, ExtractedField>();
        var matches = KeyValuePattern.Matches(rawText);

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            var normalizedKey = NormalizeFieldKey(key);

            if (!string.IsNullOrWhiteSpace(normalizedKey) && !string.IsNullOrWhiteSpace(value))
            {
                // CLI doesn't give per-field confidence; use 0.9 as baseline for successful extraction
                fields[normalizedKey] = new ExtractedField(normalizedKey, value, 0.9);
            }
        }

        return fields;
    }

    private static string NormalizeFieldKey(string key)
    {
        var parts = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
    }
}
