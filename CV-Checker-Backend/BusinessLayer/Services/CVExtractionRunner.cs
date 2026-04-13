using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Domain.Entities;

namespace BusinessLogic.Services
{
    public class CVExtractionRunner
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        public CVExtractionRunner()
        {
            _pythonExe = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    ".venv",
                    "Scripts",
                    "python.exe"
                )
            );

            _scriptPath = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "PythonScripts",
                    "cv_extract.py"
                )
            );
        }

        public async Task<CVExtractionResult> ExtractFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new CVExtractionResult
                {
                    Error = "CV file path is invalid or file does not exist."
                };
            }

            if (!File.Exists(_pythonExe))
            {
                return new CVExtractionResult
                {
                    Error = $"Python executable not found: {_pythonExe}"
                };
            }

            if (!File.Exists(_scriptPath))
            {
                return new CVExtractionResult
                {
                    Error = $"Python script not found: {_scriptPath}"
                };
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    Arguments = $"\"{_scriptPath}\" \"{filePath}\"",
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
                        Error = $"Python process failed: {stderr}"
                    };
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    return new CVExtractionResult
                    {
                        Error = "Python script returned empty output."
                    };
                }

                string jsonOutput = ExtractJsonFromOutput(stdout);

                if (string.IsNullOrWhiteSpace(jsonOutput))
                {
                    return new CVExtractionResult
                    {
                        Error = $"Could not find JSON in Python output. Raw output: {stdout}"
                    };
                }

                var result = JsonSerializer.Deserialize<CVExtractionResult>(
                    jsonOutput,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return result ?? new CVExtractionResult
                {
                    Error = "Failed to deserialize extraction result."
                };
            }
            catch (Exception ex)
            {
                return new CVExtractionResult
                {
                    Error = ex.Message
                };
            }
        }

        private static string ExtractJsonFromOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return string.Empty;

            var lines = output
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Reverse();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    return trimmed;
            }

            return string.Empty;
        }
    }
}