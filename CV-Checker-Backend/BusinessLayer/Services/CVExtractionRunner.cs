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
            _pythonExe = ResolvePythonExecutable();
            _scriptPath = ResolveScriptPath();
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
                        Error = $"Python process failed: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr)}"
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

        private static string ResolvePythonExecutable()
        {
            var configured = Environment.GetEnvironmentVariable("PYTHON_EXE");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".venv", "Scripts", "python.exe")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".venv", "Scripts", "python.exe"))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return "python";
        }

        private static string ResolveScriptPath()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PythonScripts", "cv_extract.py")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PythonScripts", "cv_extract.py"))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            // Keeps previous behavior in the error message if not found.
            return candidates[0];
        }
    }
}