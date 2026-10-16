using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Runs a selected Python analysis on explicit player input and exchanges JSON files.</summary>
public sealed class PythonAIBridge : MonoBehaviour
{
    [Serializable] public sealed class PondEvidence
    {
        public float water_clarity;
        public float vegetation_health;
        public float wildlife_activity;
        public float rainfall;
        public float dead_vegetation;
    }
    [Serializable] private sealed class DiagnosisOutput
    {
        public string diagnosis;
        public float confidence;
        public CauseProbabilities probabilities;
    }
    [Serializable] private sealed class CauseProbabilities
    {
        public float contamination, drought, disease, habitat_disturbance;
    }
    private sealed class RunResult { public bool success; public string message; public string json; }

    public static PythonAIBridge Instance { get; private set; }
    private bool running;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool IsRunning => running;

    public System.Collections.IEnumerator DiagnosePond(PondEvidence evidence, Action<bool, string> completed)
    {
        if (running) { completed?.Invoke(false, "Python analysis is already running."); yield break; }
        running = true;
        string script = ResolveBayesianScript();
        if (string.IsNullOrEmpty(script))
        {
            running = false;
            completed?.Invoke(false, "PythonAI/bayesian.py was not found. The investigation is ready to retry once the PythonAI files are installed.");
            yield break;
        }

        string exchange = Path.Combine(Application.persistentDataPath, "ForestGuardian", "PythonAI");
        string inputPath = Path.Combine(exchange, "pond_input.json");
        string outputPath = Path.Combine(exchange, "pond_output.json");
        string preparationError = null;
        try
        {
            Directory.CreateDirectory(exchange);
            File.WriteAllText(inputPath, JsonUtility.ToJson(evidence, true));
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (Exception ex) { preparationError = ex.Message; }
        if (preparationError != null)
        {
            running = false;
            completed?.Invoke(false, "Could not write Python JSON input: " + preparationError);
            yield break;
        }
        bool windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        Task<RunResult> task = Task.Run(() => RunPython(script, inputPath, outputPath, windows));
        while (!task.IsCompleted) yield return null;
        RunResult result = task.IsFaulted
            ? new RunResult { success = false, message = task.Exception != null ? task.Exception.GetBaseException().Message : "Python process failed." }
            : task.Result;
        running = false;
        if (result.success)
        {
            try
            {
                DiagnosisOutput diagnosis = JsonUtility.FromJson<DiagnosisOutput>(result.json);
                if (diagnosis == null || string.IsNullOrEmpty(diagnosis.diagnosis) || diagnosis.probabilities == null)
                    result = new RunResult { success = false, message = "Python returned an invalid diagnosis JSON document." };
                else
                    result.message = $"Python Bayesian result: {diagnosis.diagnosis.Replace('_', ' ')} ({diagnosis.confidence:P0}). Posterior probabilities — contamination {diagnosis.probabilities.contamination:P0}, drought {diagnosis.probabilities.drought:P0}, disease {diagnosis.probabilities.disease:P0}, habitat disturbance {diagnosis.probabilities.habitat_disturbance:P0}.";
            }
            catch (Exception ex) { result = new RunResult { success = false, message = "Python returned malformed diagnosis JSON: " + ex.Message }; }
        }
        completed?.Invoke(result.success, result.message);
    }

    private static string ResolveBayesianScript()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string[] candidates =
        {
            projectRoot != null ? Path.Combine(projectRoot, "PythonAI", "bayesian.py") : string.Empty,
            Path.Combine(Application.streamingAssetsPath, "PythonAI", "bayesian.py")
        };
        foreach (string path in candidates) if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        return null;
    }

    private static RunResult RunPython(string script, string input, string output, bool windows)
    {
        string errors = string.Empty;
        try
        {
            Process process = null;
            Exception launchError = null;
            string[] interpreters = windows ? new[] { "python", "py" } : new[] { "python3", "python" };
            foreach (string interpreter in interpreters)
            {
                try
                {
                    ProcessStartInfo start = new ProcessStartInfo
                    {
                        FileName = interpreter,
                        Arguments = (interpreter == "py" ? "-3 " : "") + Quote(script) + " " + Quote(input) + " " + Quote(output),
                        UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(script)
                    };
                    process = Process.Start(start);
                    if (process != null) break;
                }
                catch (Exception ex) { launchError = ex; }
            }
            if (process == null) return new RunResult { success = false, message = "Could not start Python (`python`/`py`): " + launchError?.Message };
            using (process)
            {
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); } catch { }
                    return new RunResult { success = false, message = "Python analysis timed out after 10 seconds." };
                }
                errors = stderr.GetAwaiter().GetResult();
                if (process.ExitCode != 0) return new RunResult { success = false, message = "Python analysis failed: " + errors.Trim() };
            }
            if (!File.Exists(output)) return new RunResult { success = false, message = "Python finished without producing its JSON result." };
            return new RunResult { success = true, json = File.ReadAllText(output) };
        }
        catch (Exception ex)
        {
            return new RunResult { success = false, message = "Python analysis could not run: " + ex.Message + (string.IsNullOrWhiteSpace(errors) ? "" : " " + errors.Trim()) };
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
