using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Regexutor.Core;

public enum RegexDialect
{
    PosixBre = 0,
    PosixEre = 1,
}

public sealed record RegexTestCase(string Input, bool ShouldMatch);

public sealed record RegexExercise(
    string Id,
    string Title,
    string Prompt,
    RegexDialect Dialect,
    IReadOnlyList<RegexTestCase> TestCases,
    bool IsEphemeralExam = false
);

public sealed record RegexExerciseResult(
    bool Success,
    IReadOnlyList<RegexTestCaseResult> TestCaseResults,
    string? EngineError
);

public sealed record RegexTestCaseResult(
    RegexTestCase TestCase,
    bool DidMatch,
    bool Passed
);

public interface IRegexRunner
{
    Task<RegexExerciseResult> EvaluateAsync(
        RegexDialect dialect,
        string pattern,
        IReadOnlyList<RegexTestCase> testCases,
        CancellationToken cancellationToken
    );
}

public sealed class GrepRegexRunner : IRegexRunner
{
    private readonly string _grepPath;
    private readonly TimeSpan _perTestTimeout;

    public GrepRegexRunner(string grepPath, TimeSpan perTestTimeout)
    {
        _grepPath = grepPath;
        _perTestTimeout = perTestTimeout;
    }

    public static string? TryLocateGrepOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var candidate = Path.Combine(dir.Trim(), "grep.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public async Task<RegexExerciseResult> EvaluateAsync(
        RegexDialect dialect,
        string pattern,
        IReadOnlyList<RegexTestCase> testCases,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            var emptyResults = testCases
                .Select(tc => new RegexTestCaseResult(tc, DidMatch: false, Passed: !tc.ShouldMatch))
                .ToList();

            return new RegexExerciseResult(
                Success: emptyResults.All(r => r.Passed),
                TestCaseResults: emptyResults,
                EngineError: null
            );
        }

        var results = new List<RegexTestCaseResult>(testCases.Count);
        foreach (var tc in testCases)
        {
            var eval = await EvaluateSingleAsync(dialect, pattern, tc.Input, cancellationToken);
            if (eval.EngineError is not null)
            {
                return new RegexExerciseResult(
                    Success: false,
                    TestCaseResults: results,
                    EngineError: eval.EngineError
                );
            }

            var didMatch = eval.DidMatch ?? false;
            results.Add(new RegexTestCaseResult(tc, didMatch, Passed: didMatch == tc.ShouldMatch));
        }

        return new RegexExerciseResult(
            Success: results.All(r => r.Passed),
            TestCaseResults: results,
            EngineError: null
        );
    }

    private async Task<(bool? DidMatch, string? EngineError)> EvaluateSingleAsync(
        RegexDialect dialect,
        string pattern,
        string input,
        CancellationToken cancellationToken
    )
    {
        // grep is line-oriented; for learning, we accept multi-line inputs and treat "any line matches" as match.
        // `-q` => quiet. Exit codes: 0 match, 1 no match, 2 error.
        //
        // IMPORTANT (Windows + Git for Windows grep):
        // Feeding via stdin can be finicky with line endings/encodings and cause surprising ^/$ failures.
        // Writing to a temp file makes behavior deterministic.
        var args = new List<string>();
        if (dialect == RegexDialect.PosixEre)
            args.Add("-E");

        args.Add("-q");
        // IMPORTANT: read the pattern from a file (-f) instead of passing it on argv.
        // Git for Windows runs grep via an MSYS runtime that can mangle argv (notably `{}` brace expansion),
        // which breaks patterns like {m,n} unless escaped. A pattern file avoids that class of bugs entirely.
        var tmpPath = Path.Combine(Path.GetTempPath(), $"regexutor_{Guid.NewGuid():N}.txt");
        var patternPath = Path.Combine(Path.GetTempPath(), $"regexutor_pat_{Guid.NewGuid():N}.txt");
        args.Add("-f");
        args.Add(patternPath);
        args.Add(tmpPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _grepPath,
            RedirectStandardInput = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Git for Windows grep runs on an MSYS runtime which can "glob" arguments containing [].
        // That breaks regex patterns like [[:digit:]] by treating them as file globs.
        // Disable globbing for deterministic behavior.
        startInfo.Environment["MSYS2_NOGLOB"] = "1";
        startInfo.Environment["MSYS_NOGLOB"] = "1";

        // Use ArgumentList to avoid fragile manual quoting/parsing.
        foreach (var a in args)
            startInfo.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = startInfo };

        try
        {
            await WriteTempPatternAsync(patternPath, pattern, cancellationToken);
            await WriteTempInputAsync(tmpPath, input, cancellationToken);
            proc.Start();
        }
        catch (Exception ex)
        {
            TryDelete(tmpPath);
            TryDelete(patternPath);
            return (null, $"No se pudo iniciar grep. Detalles: {ex.Message}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_perTestTimeout);

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(proc);
            TryDelete(tmpPath);
            TryDelete(patternPath);
            return (null, $"Timeout: la evaluación tardó más de {_perTestTimeout.TotalMilliseconds:0} ms.");
        }

        var exit = proc.ExitCode;
        TryDelete(tmpPath);
        TryDelete(patternPath);
        if (exit == 0) return (true, null);
        if (exit == 1) return (false, null);

        var err = await proc.StandardError.ReadToEndAsync(cancellationToken);
        err = string.IsNullOrWhiteSpace(err) ? "grep devolvió un error (exit code 2)." : err.Trim();
        return (null, ExplainGrepError(err));
    }

    private static string ExplainGrepError(string rawError)
    {
        // Git for Windows grep errors are typically English; translate the most common ones.
        // We always keep the original text as "detalle técnico" so it can be copied for debugging.
        var lower = rawError.ToLowerInvariant();

        // Example:
        // /usr/bin/grep: character class syntax is [[:space:]], not [:space:]
        if (lower.Contains("character class syntax is") && lower.Contains("[[:") && lower.Contains("not [:"))
        {
            return
                "Error de sintaxis en una clase POSIX.\n" +
                "En POSIX, las clases van dentro de dobles corchetes y con ':' delante y detrás, así: [[:space:]] (no [:space:]).\n" +
                "Ejemplos: [[:digit:]] [[:alpha:]] [[:alnum:]] [[:space:]]\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        if (lower.Contains("invalid regular expression") || lower.Contains("invalid regexp"))
        {
            return
                "La expresión regular no es válida para el dialecto actual (POSIX).\n" +
                "Revisa caracteres especiales, corchetes sin cerrar, paréntesis, y cuantificadores.\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        if (lower.Contains("unmatched [") || lower.Contains("unterminated ["))
        {
            return
                "Parece que abriste un '[' pero no lo cerraste. Revisa clases como [0-9] o clases POSIX [[:digit:]].\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        if (lower.Contains("repetition-operator operand invalid") || lower.Contains("repetition operator operand invalid"))
        {
            return
                "Hay un cuantificador mal colocado (por ejemplo, '*' o '+' sin nada antes).\n" +
                "Ejemplo incorrecto: *abc. Ejemplo correcto: a*bc.\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        if (lower.Contains("back reference") || lower.Contains("backreference") || lower.Contains("invalid back reference"))
        {
            return
                "Referencia hacia atrás inválida (\\1, \\2, …).\n" +
                "Asegúrate de que exista un par de paréntesis de captura ANTES (ERE: ( ); BRE: \\( \\)) y de que el número no supere los grupos reales.\n" +
                "En BRE, los paréntesis de captura van escapados: \\( … \\).\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        if (lower.Contains("no such file or directory"))
        {
            return
                "El motor intentó abrir un archivo que no existe.\n" +
                "Esto suele pasar cuando grep interpreta el patrón como un nombre de archivo (muy típico con corchetes [] en Git for Windows).\n" +
                "La app ya intenta evitarlo; si persiste, revisa que tu patrón sea válido y que no haya caracteres invisibles al pegar.\n" +
                "Si estabas usando una clase POSIX, recuerda la forma correcta: [[:digit:]] (no [:digit:]).\n" +
                "\n" +
                $"Detalle técnico: {rawError}";
        }

        return $"Error del motor (grep):\n{rawError}";
    }

    private static async Task WriteTempInputAsync(string path, string input, CancellationToken cancellationToken)
    {
        // Normalize Windows line endings to \n to keep ^/$ predictable.
        var normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        if (!normalized.EndsWith('\n'))
            normalized += "\n";

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(path, normalized, utf8NoBom, cancellationToken);
    }

    private static async Task WriteTempPatternAsync(string path, string pattern, CancellationToken cancellationToken)
    {
        // Normalize line endings; keep pattern as a single line (grep -f reads line-by-line).
        var normalized = (pattern ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(path, normalized + "\n", utf8NoBom, cancellationToken);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }
}

public abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
