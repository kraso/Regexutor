using System.Reflection;
using System.Text;
using Regexutor.Core;

static int PrintUsage()
{
    Console.Error.WriteLine(
        """
        Regexutor CLI — evaluación POSIX BRE/ERE vía grep (misma lógica que la app de escritorio).

        Uso:
          regexutor-cli --version | -V
          regexutor-cli eval --dialect bre|ere --pattern <patrón> --cases <archivo>
          regexutor-cli grep-path

        Archivo de casos (--cases): una línea por prueba, formato TSV:
          texto_de_entrada<TAB>esperado_match
        donde esperado_match es true, false, 1, 0, sí, no (insensible a mayúsculas).

        Variables:
          REGEXUTOR_GREP  Ruta absoluta al binario grep si no está en PATH.

        La interfaz gráfica WPF solo está disponible en Windows; en Linux use este CLI.
        """
    );
    return 2;
}

static int PrintVersion()
{
    var asm = typeof(Program).Assembly;
    var informational =
        asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
    var assemblyVersion = asm.GetName().Version?.ToString(3);
    var text = informational
        ?? fileVer
        ?? assemblyVersion
        ?? "0.0.0";
    Console.WriteLine($"regexutor-cli {text}");
    return 0;
}

static RegexDialect ParseDialect(string? s)
{
    if (string.Equals(s, "ere", StringComparison.OrdinalIgnoreCase))
        return RegexDialect.PosixEre;
    if (string.Equals(s, "bre", StringComparison.OrdinalIgnoreCase))
        return RegexDialect.PosixBre;
    throw new ArgumentException("Dialecto inválido: use bre o ere.");
}

static bool ParseExpectedCell(string cell)
{
    var t = cell.Trim();
    if (string.IsNullOrEmpty(t))
        return false;
    if (t.Equals("true", StringComparison.OrdinalIgnoreCase) || t == "1" || t.Equals("sí", StringComparison.OrdinalIgnoreCase) || t.Equals("si", StringComparison.OrdinalIgnoreCase))
        return true;
    if (t.Equals("false", StringComparison.OrdinalIgnoreCase) || t == "0" || t.Equals("no", StringComparison.OrdinalIgnoreCase))
        return false;
    throw new FormatException($"Valor de esperado no reconocido: '{cell}'");
}

static List<RegexTestCase> LoadCases(string path)
{
    var lines = File.ReadAllLines(path, Encoding.UTF8);
    var list = new List<RegexTestCase>(lines.Length);
    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            continue;

        var tab = line.IndexOf('\t');
        if (tab < 0)
            throw new FormatException(
                $"Línea {i + 1}: se esperaba un carácter TAB (\\t) entre la entrada y true/false; no use espacios. "
                    + "Ej.: printf 'texto\\ttrue\\n' > casos.tsv"
            );

        var input = line[..tab]
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
        var expectedCell = line[(tab + 1)..];
        list.Add(new RegexTestCase(input, ParseExpectedCell(expectedCell)));
    }

    return list;
}

var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (argv.Length == 0)
    return PrintUsage();

if (argv.Length == 1 && (argv[0] == "--version" || argv[0] == "-V"))
    return PrintVersion();

if (argv[0] == "grep-path")
{
    var baseDir = AppContext.BaseDirectory;
    var g =
        GrepRegexRunner.TryLocateGrep(baseDir, searchPath: true)
        ?? GrepRegexRunner.TryLocateGrepOnPath();
    if (g is null)
    {
        Console.Error.WriteLine("No se encontró grep. Instale el paquete 'grep' o defina REGEXUTOR_GREP.");
        return 1;
    }

    Console.WriteLine(g);
    return 0;
}

if (argv[0] != "eval")
    return PrintUsage();

string? pattern = null;
string? casesPath = null;
string? dialectStr = null;
for (var i = 1; i < argv.Length; i++)
{
    var a = argv[i];
    if (a == "--pattern" && i + 1 < argv.Length)
        pattern = argv[++i];
    else if (a == "--cases" && i + 1 < argv.Length)
        casesPath = argv[++i];
    else if (a == "--dialect" && i + 1 < argv.Length)
        dialectStr = argv[++i];
    else
        return PrintUsage();
}

if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(casesPath) || string.IsNullOrEmpty(dialectStr))
    return PrintUsage();

RegexDialect dialect;
try
{
    dialect = ParseDialect(dialectStr);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

List<RegexTestCase> cases;
try
{
    cases = LoadCases(casesPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error leyendo casos: {ex.Message}");
    return 2;
}

if (cases.Count == 0)
{
    Console.Error.WriteLine("No hay casos de prueba en el archivo.");
    return 2;
}

var grepPath =
    GrepRegexRunner.TryLocateGrep(AppContext.BaseDirectory, searchPath: true)
    ?? GrepRegexRunner.TryLocateGrepOnPath();
if (grepPath is null)
{
    Console.Error.WriteLine("No se encontró grep. En Debian/Ubuntu: sudo apt install grep");
    return 1;
}

var runner = new GrepRegexRunner(grepPath, TimeSpan.FromSeconds(5));
RegexExerciseResult result;
try
{
    result = await runner.EvaluateAsync(dialect, pattern, cases, CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error de evaluación: {ex.Message}");
    return 1;
}

if (result.EngineError is not null)
{
    Console.Error.WriteLine(result.EngineError);
    return 1;
}

foreach (var r in result.TestCaseResults)
{
    var mark = r.Passed ? "OK" : "FAIL";
    Console.WriteLine($"{mark}\tmatch={r.DidMatch}\texpected={r.TestCase.ShouldMatch}\tinput={r.TestCase.Input.Replace('\n', '↵')}");
}

return result.Success ? 0 : 1;
