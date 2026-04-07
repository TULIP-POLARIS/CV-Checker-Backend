using Domain.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusinessLogic.Services
{
    public class CVExtractionRunner  //this class writes temp files,starts Python processes,reads stdout/stderr,deserializes JSON
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        public CVExtractionRunner()
        {
            _pythonExe = "python";
            _scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "PythonScripts", "cv_extract.py");
        }

        public async Task<CVExtractionResult> ExtractFromBytesAsync(byte[] fileData, string? fileName)
        {
            if (fileData == null || fileData.Length == 0)
            {
                return new CVExtractionResult
                {
                    Error = "CV file is empty."
                };
            }

            var extension = GetSafeExtension(fileName);
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

            try
            {
                await File.WriteAllBytesAsync(tempFilePath, fileData);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    Arguments = $"\"{_scriptPath}\" \"{tempFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processStartInfo };

                process.Start();

                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    return new CVExtractionResult
                    {
                        Error = $"Python process failed. {stderr}"
                    };
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    return new CVExtractionResult
                    {
                        Error = "Python script returned empty output."
                    };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<CVExtractionResult>(stdout, options);

                if (result == null)
                {
                    return new CVExtractionResult
                    {
                        Error = "Failed to deserialize Python output."
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CVExtractionResult
                {
                    Error = $"Extraction failed: {ex.Message}"
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch
                {
                    // ignore temp file cleanup failure
                }
            }
        }

        private static string GetSafeExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return ".pdf";

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".pdf" => ".pdf",
                ".docx" => ".docx",
                ".txt" => ".txt",
                _ => ".pdf"
            };
        }
    }
}

