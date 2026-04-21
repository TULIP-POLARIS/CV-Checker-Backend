using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Domain.Entities;

namespace BusinessLogic.Services
{
    public class CVExtractionRunner
    {
        private readonly IReadOnlyList<PythonCommand> _pythonCommands;
        private readonly string _scriptPath;

        public CVExtractionRunner()
        {
            _pythonCommands = ResolvePythonCommands();
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
                    Error = $"Python script not found: {_scriptPath}. BaseDirectory: {AppContext.BaseDirectory}"
                };
            }

            string? lastError = null;

            foreach (var command in _pythonCommands)
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = command.Executable,
                        Arguments = string.IsNullOrWhiteSpace(command.ArgsPrefix)
                            ? $"\"{_scriptPath}\" \"{filePath}\""
                            : $"{command.ArgsPrefix} \"{_scriptPath}\" \"{filePath}\"",
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
                        var processError = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                        lastError = $"[{command.DisplayName}] {processError}";

                        if (IsLikelyIncompatiblePython(processError))
                            continue;

                        return new CVExtractionResult
                        {
                            Error = $"Python process failed: {lastError}"
                        };
                    }

                    if (string.IsNullOrWhiteSpace(stdout))
                    {
                        lastError = $"[{command.DisplayName}] Python script returned empty output.";
                        continue;
                    }

                    string jsonOutput = ExtractJsonFromOutput(stdout);

                    if (string.IsNullOrWhiteSpace(jsonOutput))
                    {
                        lastError = $"[{command.DisplayName}] Could not find JSON in Python output. Raw output: {stdout}";
                        continue;
                    }

                    var result = JsonSerializer.Deserialize<CVExtractionResult>(
                        jsonOutput,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    return result ?? new CVExtractionResult
                    {
                        Error = $"[{command.DisplayName}] Failed to deserialize extraction result."
                    };
                }
                catch (Exception ex)
                {
                    lastError = $"[{command.DisplayName}] {ex.Message}";
                }
            }

            return new CVExtractionResult
            {
                Error = $"Python process failed: {lastError ?? "No compatible Python 3 interpreter found."}"
            };
        }

        public async Task<PythonHealthResult> GetPythonHealthAsync()
        {
            var probes = new List<PythonProbeResult>();

            foreach (var command in _pythonCommands)
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = command.Executable,
                        Arguments = string.IsNullOrWhiteSpace(command.ArgsPrefix)
                            ? "--version"
                            : $"{command.ArgsPrefix} --version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = new Process { StartInfo = processStartInfo };
                    process.Start();

                    var stdout = await process.StandardOutput.ReadToEndAsync();
                    var stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
                    var versionText = output.Trim();
                    var isPython3 = versionText.StartsWith("Python 3.", StringComparison.OrdinalIgnoreCase);

                    probes.Add(new PythonProbeResult
                    {
                        Command = command.DisplayName,
                        ExitCode = process.ExitCode,
                        VersionOutput = versionText,
                        IsPython3 = isPython3,
                        IsHealthy = process.ExitCode == 0 && isPython3
                    });
                }
                catch (Exception ex)
                {
                    probes.Add(new PythonProbeResult
                    {
                        Command = command.DisplayName,
                        ExitCode = -1,
                        VersionOutput = ex.Message,
                        IsPython3 = false,
                        IsHealthy = false
                    });
                }
            }

            return new PythonHealthResult
            {
                ScriptPath = _scriptPath,
                ScriptFound = File.Exists(_scriptPath),
                AnyHealthyPython3 = probes.Any(x => x.IsHealthy),
                Probes = probes
            };
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

        private static IReadOnlyList<PythonCommand> ResolvePythonCommands()
        {
            var commands = new List<PythonCommand>();
            var configured = Environment.GetEnvironmentVariable("PYTHON_EXE");
            if (!string.IsNullOrWhiteSpace(configured))
                commands.Add(new PythonCommand(configured, string.Empty, "PYTHON_EXE"));

            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".venv", "Scripts", "python.exe")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".venv", "Scripts", "python.exe"))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    commands.Add(new PythonCommand(candidate, string.Empty, candidate));
            }

            if (OperatingSystem.IsWindows())
            {
                commands.Add(new PythonCommand("py", "-3", "py -3"));
            }
            else
            {
                commands.Add(new PythonCommand("python3", string.Empty, "python3"));
            }

            return commands
                .GroupBy(c => $"{c.Executable}|{c.ArgsPrefix}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static bool IsLikelyIncompatiblePython(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return false;

            var normalized = error.ToLowerInvariant();
            return normalized.Contains("future feature annotations is not defined", StringComparison.Ordinal)
                || normalized.Contains("syntaxerror", StringComparison.Ordinal);
        }

        private static string ResolveScriptPath()
        {
            var candidates = new[]
            {
        Path.Combine(AppContext.BaseDirectory, "PythonScripts", "cv_extract.py"),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PythonScripts", "cv_extract.py")),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PythonScripts", "cv_extract.py"))
    };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return candidates[0];
        }

        private sealed record PythonCommand(string Executable, string ArgsPrefix, string DisplayName);

        public sealed class PythonHealthResult
        {
            public string ScriptPath { get; set; } = string.Empty;
            public bool ScriptFound { get; set; }
            public bool AnyHealthyPython3 { get; set; }
            public List<PythonProbeResult> Probes { get; set; } = new();
        }

        public sealed class PythonProbeResult
        {
            public string Command { get; set; } = string.Empty;
            public int ExitCode { get; set; }
            public string VersionOutput { get; set; } = string.Empty;
            public bool IsPython3 { get; set; }
            public bool IsHealthy { get; set; }
        }
    }
}