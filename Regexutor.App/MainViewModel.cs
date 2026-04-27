using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Regexutor.Core;

namespace Regexutor.App;

public sealed class MainViewModel : BindableBase
{
    private const int TabPractice = 0;
    private const int TabTheory = 1;
    private const int TabCheatSheet = 2;
    private const int TabExams = 3;
    private const int TabAbout = 4;

    private readonly IRegexRunner? _runner;
    private readonly string? _grepPath;

    public string AppName => "Regexutor ©";
    public string AppVersion => "1.0.0";
    public string AppDate => "27/04/2026";
    public string AppAuthor => "Marcos Calabrés Ibáñez";
    public string AppEmail => "markbiophysicist@gmail.com";

    public ObservableCollection<RegexExercise> PracticeExercises { get; } = new();

    private RegexExercise? _currentExamExercise;
    public RegexExercise? CurrentExamExercise
    {
        get => _currentExamExercise;
        private set
        {
            if (!SetProperty(ref _currentExamExercise, value))
                return;
            RefreshDisplayedExerciseMetadata();
            RefreshCanEvaluate();
            _evaluateAsyncInner.RaiseCanExecuteChanged();
            _goToTheoryAsyncInner.RaiseCanExecuteChanged();
        }
    }
    public ObservableCollection<ExamTemplate> ExamTemplates { get; } = new();
    public ObservableCollection<TestCaseRow> LastResults { get; } = new();
    public ObservableCollection<string> LastHints { get; } = new();

    public ObservableCollection<TheoryTopic> TheoryTopics { get; } = new();

    public ObservableCollection<TheoryTopic> CheatSheetTopics { get; } = new();

    private TheoryTopic? _selectedCheatSheetTopic;
    public TheoryTopic? SelectedCheatSheetTopic
    {
        get => _selectedCheatSheetTopic;
        set
        {
            if (!SetProperty(ref _selectedCheatSheetTopic, value)) return;
            SelectedCheatSheetTitle = value?.Title ?? string.Empty;
            SelectedCheatSheetBody = value?.Body ?? string.Empty;
        }
    }

    private string _selectedCheatSheetTitle = string.Empty;
    public string SelectedCheatSheetTitle
    {
        get => _selectedCheatSheetTitle;
        private set => SetProperty(ref _selectedCheatSheetTitle, value);
    }

    private string _selectedCheatSheetBody = string.Empty;
    public string SelectedCheatSheetBody
    {
        get => _selectedCheatSheetBody;
        private set => SetProperty(ref _selectedCheatSheetBody, value);
    }

    private TheoryTopic? _selectedTheoryTopic;
    public TheoryTopic? SelectedTheoryTopic
    {
        get => _selectedTheoryTopic;
        set
        {
            if (!SetProperty(ref _selectedTheoryTopic, value)) return;
            SelectedTheoryTitle = value?.Title ?? string.Empty;
            SelectedTheoryBody = value?.Body ?? string.Empty;
        }
    }

    private string _selectedTheoryTitle = string.Empty;
    public string SelectedTheoryTitle
    {
        get => _selectedTheoryTitle;
        private set => SetProperty(ref _selectedTheoryTitle, value);
    }

    private string _selectedTheoryBody = string.Empty;
    public string SelectedTheoryBody
    {
        get => _selectedTheoryBody;
        private set => SetProperty(ref _selectedTheoryBody, value);
    }

    private RegexExercise? _selectedExercise;
    public RegexExercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            if (!SetProperty(ref _selectedExercise, value)) return;
            LastResults.Clear();
            LastSummary = string.Empty;
            OnSelectedExerciseChanged();
        }
    }

    private string _pattern = string.Empty;
    public string Pattern
    {
        get => _pattern;
        set
        {
            if (!SetProperty(ref _pattern, value)) return;
            RefreshCanEvaluate();
        }
    }

    // Mantener un patrón independiente por pestaña (Práctica/Teoría/Exámenes).
    // Así lo escrito en una pestaña no se copia a las otras, pero se conserva al volver.
    private readonly Dictionary<int, string> _patternByTab = new()
    {
        [TabPractice] = string.Empty,   // Práctica
        [TabTheory] = string.Empty,      // Teoría
        [TabCheatSheet] = string.Empty,  // Esquema
        [TabExams] = string.Empty,       // Exámenes
        [TabAbout] = string.Empty,       // Acerca de
    };

    private void SavePatternForTab(int tabIndex) => _patternByTab[tabIndex] = Pattern ?? string.Empty;

    private void LoadPatternForTab(int tabIndex) =>
        Pattern = _patternByTab.TryGetValue(tabIndex, out var p) ? p : string.Empty;

    private string _engineStatus = string.Empty;
    public string EngineStatus
    {
        get => _engineStatus;
        private set => SetProperty(ref _engineStatus, value);
    }

    private Brush _engineStatusBrush = Brushes.Gray;
    public Brush EngineStatusBrush
    {
        get => _engineStatusBrush;
        private set => SetProperty(ref _engineStatusBrush, value);
    }

    private bool _canEvaluate;
    public bool CanEvaluate
    {
        get => _canEvaluate;
        private set => SetProperty(ref _canEvaluate, value);
    }

    private bool _isEvaluating;
    public bool IsEvaluating
    {
        get => _isEvaluating;
        private set
        {
            if (!SetProperty(ref _isEvaluating, value)) return;
            EvaluateButtonText = value ? "Evaluando..." : "Evaluar";
            RefreshCanEvaluate();
        }
    }

    private string _evaluateButtonText = "Evaluar";
    public string EvaluateButtonText
    {
        get => _evaluateButtonText;
        private set => SetProperty(ref _evaluateButtonText, value);
    }

    private string _lastSummary = string.Empty;
    public string LastSummary
    {
        get => _lastSummary;
        private set => SetProperty(ref _lastSummary, value);
    }

    private bool _hasHints;
    public bool HasHints
    {
        get => _hasHints;
        private set => SetProperty(ref _hasHints, value);
    }

    private string _selectedDialectLabel = string.Empty;
    public string SelectedDialectLabel
    {
        get => _selectedDialectLabel;
        private set => SetProperty(ref _selectedDialectLabel, value);
    }

    private string _selectedPrompt = string.Empty;
    public string SelectedPrompt
    {
        get => _selectedPrompt;
        private set => SetProperty(ref _selectedPrompt, value);
    }

    private readonly AsyncCommand _evaluateAsyncInner;
    private readonly AsyncCommand _goToTheoryAsyncInner;
    public ICommand EvaluateCommand => _evaluateAsyncInner;
    public ICommand GoToTheoryCommand => _goToTheoryAsyncInner;

    private readonly RelayCommand _generateExamInner;
    private readonly RelayCommand _repeatExamInner;
    public ICommand GenerateExamCommand => _generateExamInner;
    public ICommand RepeatLastExamCommand => _repeatExamInner;

    private ExamTemplate? _selectedExamTemplate;
    public ExamTemplate? SelectedExamTemplate
    {
        get => _selectedExamTemplate;
        set
        {
            if (!SetProperty(ref _selectedExamTemplate, value))
                return;
            _generateExamInner.RaiseCanExecuteChanged();
        }
    }

    private RegexExercise? _lastGeneratedExamSnapshot;

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            var old = _selectedTabIndex;
            if (!SetProperty(ref _selectedTabIndex, value))
                return;

            SavePatternForTab(old);
            LoadPatternForTab(value);
            RefreshDisplayedExerciseMetadata();
            RefreshCanEvaluate();
            _evaluateAsyncInner.RaiseCanExecuteChanged();
            _goToTheoryAsyncInner.RaiseCanExecuteChanged();
        }
    }

    public MainViewModel()
    {
        _grepPath = GrepRegexRunner.TryLocateGrep(AppContext.BaseDirectory, searchPath: true);
        if (_grepPath is not null)
        {
            _runner = new GrepRegexRunner(_grepPath, perTestTimeout: TimeSpan.FromMilliseconds(250));
            EngineStatus = "POSIX";
            EngineStatusBrush = Brushes.Black;
        }
        else
        {
            _runner = null;
            EngineStatus = "Motor POSIX NO disponible (grep.exe no encontrado).";
            EngineStatusBrush = Brushes.DarkRed;
        }

        _generateExamInner = new RelayCommand(
            GenerateRandomExam,
            () => _selectedExamTemplate is not null && _runner is not null);
        _repeatExamInner = new RelayCommand(
            RepeatLastExam,
            () => _lastGeneratedExamSnapshot is not null && _runner is not null);

        SeedExercises();
        foreach (var t in ExamCatalog.Templates)
            ExamTemplates.Add(t);
        SelectedExamTemplate = ExamTemplates.FirstOrDefault();

        _evaluateAsyncInner = new AsyncCommand(EvaluateAsync, EvaluateCanExecute);
        _goToTheoryAsyncInner = new AsyncCommand(GoToTheoryAsync, () => ActiveTheorySource is not null);

        SeedTheory();
        SeedCheatSheet();
        SelectedExercise = PracticeExercises.FirstOrDefault();
        SelectedTheoryTopic = TheoryTopics.FirstOrDefault();
        SelectedCheatSheetTopic = CheatSheetTopics.FirstOrDefault();
        RefreshCanEvaluate();

        _evaluateAsyncInner.RaiseCanExecuteChanged();
        _goToTheoryAsyncInner.RaiseCanExecuteChanged();
    }

    private RegexExercise? ActiveTheorySource => SelectedTabIndex == TabExams ? CurrentExamExercise : SelectedExercise;

    private bool EvaluateCanExecute() =>
        _runner is not null && !IsEvaluating &&
        (SelectedTabIndex == TabPractice || SelectedTabIndex == TabExams) &&
        (SelectedTabIndex == TabExams ? CurrentExamExercise is not null : SelectedExercise is not null);

    private void RefreshCanEvaluate() => CanEvaluate = EvaluateCanExecute();

    private void RefreshDisplayedExerciseMetadata()
    {
        if (SelectedTabIndex == TabExams)
        {
            if (CurrentExamExercise is null)
            {
                SelectedDialectLabel = "—";
                SelectedPrompt =
                    "Selecciona una plantilla y pulsa «Nuevo examen aleatorio». Cada intento genera un enunciado y una tabla de casos distintos (salvo «Repetir último»).";
                return;
            }

            ApplyDialectAndPrompt(CurrentExamExercise);
            return;
        }

        if (SelectedExercise is null)
        {
            SelectedDialectLabel = string.Empty;
            SelectedPrompt = string.Empty;
            return;
        }

        ApplyDialectAndPrompt(SelectedExercise);
    }

    private void ApplyDialectAndPrompt(RegexExercise ex)
    {
        SelectedDialectLabel = ex.Dialect switch
        {
            RegexDialect.PosixBre => "POSIX BRE",
            RegexDialect.PosixEre => "POSIX ERE",
            _ => ex.Dialect.ToString()
        };
        SelectedPrompt = ex.Prompt;

        // Badge arriba a la derecha: solo el dialecto actual (si el motor existe)
        if (_runner is not null)
            EngineStatus = SelectedDialectLabel;
    }

    private void GenerateRandomExam()
    {
        if (_runner is null || SelectedExamTemplate is null)
            return;

        var ex = SelectedExamTemplate.Build(Random.Shared);
        _lastGeneratedExamSnapshot = ex;
        CurrentExamExercise = ex;
        SelectedTabIndex = TabExams;
        Pattern = string.Empty;
        SavePatternForTab(TabExams);
        LastResults.Clear();
        LastSummary = string.Empty;
        LastHints.Clear();
        HasHints = false;
        _repeatExamInner.RaiseCanExecuteChanged();
    }

    private void RepeatLastExam()
    {
        if (_runner is null || _lastGeneratedExamSnapshot is null)
            return;

        var snap = _lastGeneratedExamSnapshot;
        var replay = snap with
        {
            Id = $"exam-replay-{Guid.NewGuid():N}",
            Title = snap.Title.Contains("(repetición", StringComparison.OrdinalIgnoreCase)
                ? snap.Title
                : snap.Title + " (repetición, mismos casos)",
            IsEphemeralExam = true
        };
        CurrentExamExercise = replay;
        SelectedTabIndex = TabExams;
        Pattern = string.Empty;
        LastResults.Clear();
        LastSummary = string.Empty;
        LastHints.Clear();
        HasHints = false;
        SavePatternForTab(TabExams);
    }

    private void OnSelectedExerciseChanged()
    {
        LastResults.Clear();
        LastSummary = string.Empty;
        if (SelectedTabIndex != TabExams)
            RefreshDisplayedExerciseMetadata();
        RefreshCanEvaluate();
        _evaluateAsyncInner.RaiseCanExecuteChanged();
        _goToTheoryAsyncInner.RaiseCanExecuteChanged();
    }

    private void SeedExercises()
    {
        // Nivel 1: "calentar" con ERE (más cercano a lo que la gente usa con egrep).
        PracticeExercises.Add(new RegexExercise(
            Id: "ere-1-digits",
            Title: "Nivel 1 (ERE): detectar números (solo dígitos)",
            Prompt: "Escribe una ERE que haga match cuando una línea contenga al menos un dígito (0-9).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("hola", ShouldMatch: false),
                new RegexTestCase("id=7", ShouldMatch: true),
                new RegexTestCase("123", ShouldMatch: true),
                new RegexTestCase("sin_numeros_aqui", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("v1.2.3", ShouldMatch: true),
                new RegexTestCase("\t42\t", ShouldMatch: true),
                new RegexTestCase("_0_", ShouldMatch: true),
                new RegexTestCase("π", ShouldMatch: false),
                new RegexTestCase("007", ShouldMatch: true),
                new RegexTestCase("NaN", ShouldMatch: false),
                new RegexTestCase("---", ShouldMatch: false),
                new RegexTestCase("x\ty5z", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-1-starts-with-digit",
            Title: "Nivel 1 (ERE): primer carácter es dígito",
            Prompt: "Escribe una ERE que haga match SOLO si el primer carácter de la línea es un dígito (0-9). Puede haber más texto después.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("1abc", ShouldMatch: true),
                new RegexTestCase("0", ShouldMatch: true),
                new RegexTestCase("9z", ShouldMatch: true),
                new RegexTestCase("a1", ShouldMatch: false),
                new RegexTestCase(" 1", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("\t2", ShouldMatch: false),
                new RegexTestCase("42 respuestas", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-1-anchors-only-digits",
            Title: "Nivel 1 (ERE): anclas ^$ (solo dígitos)",
            Prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por dígitos. Pista: usa ^ y $.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("123", ShouldMatch: true),
                new RegexTestCase("0010", ShouldMatch: true),
                new RegexTestCase("0", ShouldMatch: true),
                new RegexTestCase("id=7", ShouldMatch: false),
                new RegexTestCase("7 ", ShouldMatch: false),
                new RegexTestCase(" 7", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("12a34", ShouldMatch: false),
                new RegexTestCase("12.34", ShouldMatch: false),
                new RegexTestCase("00000000000", ShouldMatch: true),
                new RegexTestCase("12e3", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-2-email-like",
            Title: "Nivel 2 (ERE): detectar 'algo@algo' (simplificado)",
            Prompt: "Escribe una ERE que haga match si existe un patrón tipo usuario@dominio (simplificado, sin validar RFC).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("contacto@ejemplo.com", ShouldMatch: true),
                new RegexTestCase("sin arroba", ShouldMatch: false),
                new RegexTestCase("a@b", ShouldMatch: true),
                new RegexTestCase("@dominio.com", ShouldMatch: false),
                new RegexTestCase("@@", ShouldMatch: false),
                new RegexTestCase("user.name+tag@host", ShouldMatch: true),
                new RegexTestCase("solo@", ShouldMatch: false),
                new RegexTestCase("@", ShouldMatch: false),
                new RegexTestCase("x@y", ShouldMatch: true),
                new RegexTestCase("mail@a.co", ShouldMatch: true),
                new RegexTestCase("bad@@here", ShouldMatch: false),
                new RegexTestCase("name@sub.domain.org", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-2-ends-with-com",
            Title: "Nivel 2 (ERE): termina en .com",
            Prompt: "Escribe una ERE que haga match SOLO si la línea termina literalmente en '.com' (punto + com). Mayúsculas/minúsculas exactas en 'com'.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("ejemplo.com", ShouldMatch: true),
                new RegexTestCase("a.com", ShouldMatch: true),
                new RegexTestCase("ejemplo.comm", ShouldMatch: false),
                new RegexTestCase("ejemplo.co", ShouldMatch: false),
                new RegexTestCase("com", ShouldMatch: false),
                new RegexTestCase(".com", ShouldMatch: true),
                new RegexTestCase("ejemplo.Com", ShouldMatch: false),
                new RegexTestCase("ejemplo.com ", ShouldMatch: false),
                new RegexTestCase(" sub.ejemplo.com", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-2-alternation-cat-dog",
            Title: "Nivel 2 (ERE): alternancia | (cat o dog)",
            Prompt: "Escribe una ERE que haga match si una línea contiene 'cat' o 'dog' como subcadena.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("cat", ShouldMatch: true),
                new RegexTestCase("dog", ShouldMatch: true),
                new RegexTestCase("hotdog", ShouldMatch: true),
                new RegexTestCase("caterpillar", ShouldMatch: true),
                new RegexTestCase("do g", ShouldMatch: false),
                new RegexTestCase("bird", ShouldMatch: false),
                new RegexTestCase("CAT", ShouldMatch: false),
                new RegexTestCase("scatter", ShouldMatch: false),
                new RegexTestCase("the dog ran", ShouldMatch: true),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("catalog", ShouldMatch: true),
                new RegexTestCase("dogma", ShouldMatch: true),
                new RegexTestCase("catdog", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-2-alternation-yes-no-line",
            Title: "Nivel 2 (ERE): toda la línea = yes/no pegados (grupo y +)",
            Prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por una o más repeticiones pegadas de las subcadenas literales yes o no (solo minúsculas). " +
                "Válidos: yes, no, yesno, nonoyes, yesyes. No puede haber espacios ni otros caracteres. " +
                "Debes agrupar yes|no entre paréntesis y cuantificar ese grupo (p. ej. +) y anclar toda la línea con ^ y $.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("yes", ShouldMatch: true),
                new RegexTestCase("no", ShouldMatch: true),
                new RegexTestCase("yesno", ShouldMatch: true),
                new RegexTestCase("nonoyes", ShouldMatch: true),
                new RegexTestCase("yesyes", ShouldMatch: true),
                new RegexTestCase("noyes", ShouldMatch: true),
                new RegexTestCase("yeyes", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("yes ", ShouldMatch: false),
                new RegexTestCase(" yes", ShouldMatch: false),
                new RegexTestCase("YES", ShouldMatch: false),
                new RegexTestCase("maybe", ShouldMatch: false),
                new RegexTestCase("yesx", ShouldMatch: false),
                new RegexTestCase("non", ShouldMatch: false),
                new RegexTestCase("ye", ShouldMatch: false),
                new RegexTestCase("on", ShouldMatch: false),
                new RegexTestCase("yes-yes", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-3-backref-double-char",
            Title: "Nivel 3 (ERE): dos caracteres idénticos (referencia \\1)",
            Prompt: "Escribe una ERE anclada (^ y $) tal que toda la línea sea exactamente DOS caracteres iguales. " +
                "Usa un primer carácter capturado entre ( ) y repítelo con \\1 (misma captura). Cualquier carácter vale (letra, dígito, espacio…).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("aa", ShouldMatch: true),
                new RegexTestCase("zz", ShouldMatch: true),
                new RegexTestCase("99", ShouldMatch: true),
                new RegexTestCase("  ", ShouldMatch: true),
                new RegexTestCase("ab", ShouldMatch: false),
                new RegexTestCase("a", ShouldMatch: false),
                new RegexTestCase("aaa", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("a\t", ShouldMatch: false),
                new RegexTestCase("\t\t", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-3-backref-word-hyphen",
            Title: "Nivel 3 (ERE): palabra-palabra repetida (\\1)",
            Prompt: "Escribe una ERE con ^ y $: la línea debe ser una palabra solo en minúsculas [a-z]+, un guion -, y la MISMA palabra otra vez (referencia \\1). " +
                "Ejemplos válidos: cat-cat, go-go, a-a. No: cat-dog, go-goo, Cat-cat.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("cat-cat", ShouldMatch: true),
                new RegexTestCase("go-go", ShouldMatch: true),
                new RegexTestCase("a-a", ShouldMatch: true),
                new RegexTestCase("zoo-zoo", ShouldMatch: true),
                new RegexTestCase("cat-dog", ShouldMatch: false),
                new RegexTestCase("go-goo", ShouldMatch: false),
                new RegexTestCase("cat", ShouldMatch: false),
                new RegexTestCase("cat-cat-cat", ShouldMatch: false),
                new RegexTestCase("Cat-cat", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-3-posix-class-alpha",
            Title: "Nivel 3 (ERE): clases POSIX [[:alpha:]]",
            Prompt: "Escribe una ERE que haga match si la línea contiene al menos una letra ASCII (a-z/A-Z). Pista: usa [[:alpha:]].",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("123", ShouldMatch: false),
                new RegexTestCase("abc", ShouldMatch: true),
                new RegexTestCase("ABC", ShouldMatch: true),
                new RegexTestCase("id=7", ShouldMatch: true),
                new RegexTestCase("___", ShouldMatch: false),
                new RegexTestCase("123.", ShouldMatch: false), // punto no es [[:alpha:]]
                new RegexTestCase("___a___", ShouldMatch: true),
                new RegexTestCase("\tZ\t", ShouldMatch: true),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("123x456", ShouldMatch: true),
                new RegexTestCase("___m___", ShouldMatch: true),
                new RegexTestCase("[[", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-3-posix-space",
            Title: "Nivel 3 (ERE): contiene espacio en blanco POSIX",
            Prompt: "Escribe una ERE que haga match si la línea contiene al menos un carácter de espacio en blanco según [[:space:]] (espacio, tab, etc.).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("a b", ShouldMatch: true),
                new RegexTestCase("sin", ShouldMatch: false),
                new RegexTestCase("\tunico", ShouldMatch: true),
                new RegexTestCase("sin-espacio", ShouldMatch: false),
                new RegexTestCase("x\ty", ShouldMatch: true),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("___", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-3-negated-class-no-digits",
            Title: "Nivel 3 (ERE): negación [^...] (sin dígitos)",
            Prompt: "Escribe una ERE que haga match SOLO si la línea NO contiene ningún dígito.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("hola", ShouldMatch: true),
                new RegexTestCase("id=7", ShouldMatch: false),
                new RegexTestCase("abc123", ShouldMatch: false),
                new RegexTestCase("___--__", ShouldMatch: true),
                new RegexTestCase("", ShouldMatch: true),
                new RegexTestCase("solo\tguiones-y_", ShouldMatch: true),
                new RegexTestCase("0", ShouldMatch: false),
                new RegexTestCase("a0b", ShouldMatch: false),
                new RegexTestCase(" \t", ShouldMatch: true),
                new RegexTestCase("a|b", ShouldMatch: false),
                new RegexTestCase("no-digits-here", ShouldMatch: true),
                new RegexTestCase("unicode∞", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-4-quantifiers-range",
            Title: "Nivel 4 (ERE): cuantificadores {m,n} (2 a 4 dígitos)",
            Prompt: "Escribe una ERE que haga match SOLO si la línea es un número de 2 a 4 dígitos (sin espacios).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("7", ShouldMatch: false),
                new RegexTestCase("12", ShouldMatch: true),
                new RegexTestCase("999", ShouldMatch: true),
                new RegexTestCase("2026", ShouldMatch: true),
                new RegexTestCase("12345", ShouldMatch: false),
                new RegexTestCase("12 ", ShouldMatch: false),
                new RegexTestCase("01", ShouldMatch: true),
                new RegexTestCase("000", ShouldMatch: true),
                new RegexTestCase("12x", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("0000", ShouldMatch: false),
                new RegexTestCase("10", ShouldMatch: true),
                new RegexTestCase("+99", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-4-only-letter-a",
            Title: "Nivel 4 (ERE): solo letras a (una o más)",
            Prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por una o más letras 'a' minúscula (sin otros caracteres).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("a", ShouldMatch: true),
                new RegexTestCase("aaa", ShouldMatch: true),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("aaA", ShouldMatch: false),
                new RegexTestCase("aab", ShouldMatch: false),
                new RegexTestCase(" a", ShouldMatch: false),
                new RegexTestCase("aa ", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-5-simple-phone",
            Title: "Nivel 5 (ERE): patrón completo (teléfono 000-000-000)",
            Prompt: "Escribe una ERE que haga match SOLO si la línea tiene el formato 000-000-000 (3 dígitos, guion, 3 dígitos, guion, 3 dígitos).",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("123-456-789", ShouldMatch: true),
                new RegexTestCase("123456789", ShouldMatch: false),
                new RegexTestCase("123-45-6789", ShouldMatch: false),
                new RegexTestCase("123-456-789 ", ShouldMatch: false),
                new RegexTestCase("012-000-999", ShouldMatch: true),
                new RegexTestCase("12-345-678", ShouldMatch: false),
                new RegexTestCase("123-456-78a", ShouldMatch: false),
                new RegexTestCase("000-000-000", ShouldMatch: true),
                new RegexTestCase("123-456-7890", ShouldMatch: false),
                new RegexTestCase("999-888-777", ShouldMatch: true),
                new RegexTestCase("123_456_789", ShouldMatch: false),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "ere-5-host-port",
            Title: "Nivel 5 (ERE): host:puerto (simple)",
            Prompt: "Escribe una ERE que haga match SOLO si toda la línea tiene forma HOST:PORT donde HOST es letras minúsculas (a-z) de 3 a 6 caracteres y PORT es exactamente 2 a 4 dígitos.",
            Dialect: RegexDialect.PosixEre,
            TestCases: new[]
            {
                new RegexTestCase("api:80", ShouldMatch: true),
                new RegexTestCase("tests:8080", ShouldMatch: true),
                new RegexTestCase("db:443", ShouldMatch: false),
                new RegexTestCase("dbs:443", ShouldMatch: true),
                new RegexTestCase("API:80", ShouldMatch: false),
                new RegexTestCase("ab:80", ShouldMatch: false),
                new RegexTestCase("toolonghostname:80", ShouldMatch: false),
                new RegexTestCase("host:8", ShouldMatch: false),
                new RegexTestCase("host:80808", ShouldMatch: false),
                new RegexTestCase("host:80a", ShouldMatch: false),
                new RegexTestCase("host80", ShouldMatch: false),
            }
        ));

        // Nivel 1 BRE: introducir que + y ? NO son especiales (en BRE) a menos que escapes / grep -E.
        PracticeExercises.Add(new RegexExercise(
            Id: "bre-1-literal-plus",
            Title: "Nivel 1 (BRE): '+' es literal",
            Prompt: "En BRE, el carácter '+' NO es cuantificador. Haz match en líneas que contengan literalmente 'C++'.",
            Dialect: RegexDialect.PosixBre,
            TestCases: new[]
            {
                new RegexTestCase("Me gusta C++", ShouldMatch: true),
                new RegexTestCase("Me gusta C+", ShouldMatch: false),
                new RegexTestCase("C--", ShouldMatch: false),
                new RegexTestCase("C++++", ShouldMatch: true),
                new RegexTestCase("C+", ShouldMatch: false),
                new RegexTestCase("prefijo C++ sufijo", ShouldMatch: true),
                new RegexTestCase("c++", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("C++11", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "bre-2-escaped-plus-quantifier",
            Title: "Nivel 2 (BRE): \\+ como cuantificador (GNU grep)",
            Prompt: "En muchos grep (GNU), en BRE puedes usar \\+ como 'uno o más'. Haz match si la línea contiene al menos un dígito usando BRE + \\+.",
            Dialect: RegexDialect.PosixBre,
            TestCases: new[]
            {
                new RegexTestCase("hola", ShouldMatch: false),
                new RegexTestCase("id=7", ShouldMatch: true),
                new RegexTestCase("123", ShouldMatch: true),
                new RegexTestCase("sin_numeros", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
                new RegexTestCase("x0x", ShouldMatch: true),
                new RegexTestCase("0", ShouldMatch: true),
                new RegexTestCase("a\tb", ShouldMatch: false),
                new RegexTestCase("999999", ShouldMatch: true),
                new RegexTestCase("_8_", ShouldMatch: true),
                new RegexTestCase("42e10", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "bre-3-literal-dot",
            Title: "Nivel 3 (BRE): punto literal \\.",
            Prompt: "En BRE, '.' coincide con cualquier carácter. Haz match en líneas que contengan un punto literal seguido de la subcadena 'txt' (como en nombres de fichero). Usa '\\.' para el punto literal.",
            Dialect: RegexDialect.PosixBre,
            TestCases: new[]
            {
                new RegexTestCase("readme.txt", ShouldMatch: true),
                new RegexTestCase("config.txtext", ShouldMatch: true),
                new RegexTestCase("txttxt", ShouldMatch: false),
                new RegexTestCase("readme-md", ShouldMatch: false),
                new RegexTestCase(".txt", ShouldMatch: true),
                new RegexTestCase("readme.txt ", ShouldMatch: true),
                new RegexTestCase("txt", ShouldMatch: false),
                new RegexTestCase("a.txtx", ShouldMatch: true),
            }
        ));

        PracticeExercises.Add(new RegexExercise(
            Id: "bre-4-backref-word-hyphen",
            Title: "Nivel 4 (BRE): palabra-palabra repetida con \\1",
            Prompt: "En BRE, agrupa con \\( \\) y repite con \\1. Para “una o más” letras minúsculas en GNU grep usa \\+. " +
                "Haz match SOLO si toda la línea es: [a-z]+, guion, la misma palabra otra vez (mismo patrón que en ERE pero escapado).",
            Dialect: RegexDialect.PosixBre,
            TestCases: new[]
            {
                new RegexTestCase("cat-cat", ShouldMatch: true),
                new RegexTestCase("go-go", ShouldMatch: true),
                new RegexTestCase("a-a", ShouldMatch: true),
                new RegexTestCase("zoo-zoo", ShouldMatch: true),
                new RegexTestCase("cat-dog", ShouldMatch: false),
                new RegexTestCase("go-goo", ShouldMatch: false),
                new RegexTestCase("cat", ShouldMatch: false),
                new RegexTestCase("cat-cat-cat", ShouldMatch: false),
                new RegexTestCase("Cat-cat", ShouldMatch: false),
                new RegexTestCase("", ShouldMatch: false),
            }
        ));
    }

    private void SeedTheory()
    {
        TheoryTopics.Add(new TheoryTopic(
            Title: "Temario — Índice de capítulos",
            Body:
                "En la lista de la izquierda, cada entrada es un capítulo. Orden sugerido de lectura:\n\n" +
                "• Nivel 1 — Qué es una regex; POSIX BRE vs ERE; anclas ^ y $.\n" +
                "• Nivel 2 — Clases [ ] (resumen y capítulo ampliado «Grupos entre corchetes»).\n" +
                "• Nivel 3 — Agrupación con paréntesis en tres capítulos: (I) subexpresión, (II) alternancia | y precedencia, " +
                "(III) cuantificar grupos, anidación y BRE.\n" +
                "• Nivel 3 — Clases POSIX [[:...]].\n" +
                "• Nivel 4 — Cuantificadores *, +, ?, {m,n} (y nota sobre “lazy” / PCRE).\n" +
                "• Nivel 5 — Referencias hacia atrás \\1–\\9 (GNU grep).\n\n" +
                "Selecciona un título para ver el contenido a la derecha. Los ejercicios de «Ver teoría relacionada» abren el capítulo más cercano al enunciado."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 1 — Qué es una regex (y cómo piensa grep)",
            Body:
                "Una expresión regular (regex) es un patrón para buscar texto.\n\n" +
                "En esta app evaluamos tus patrones con grep, que trabaja por líneas:\n" +
                "- La entrada se parte en líneas.\n" +
                "- La regex hace match si al menos una línea coincide.\n\n" +
                "Consejo: cuando el enunciado diga “toda la línea”, casi siempre necesitas anclas ^ y $."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 1 — POSIX BRE vs POSIX ERE",
            Body:
                "En POSIX hay dos dialectos habituales:\n\n" +
                "1) BRE (Basic Regular Expressions) — grep\n" +
                "   - Algunos operadores son literales o requieren escape: \\( \\) \\{ \\} \\+ \\?\n" +
                "2) ERE (Extended Regular Expressions) — grep -E\n" +
                "   - Operadores como +, ?, |, () suelen ser “normales” (sin escape).\n\n" +
                "Si un ejercicio dice ERE, estás en modo grep -E. Si dice BRE, estás en modo grep.\n\n" +
                "Agrupación ( ) y alternancia | en ERE se desarrollan en el temario bajo «Nivel 3 — Agrupación (I)», «(II)» y «(III)»."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 1 — Anclas ^ y $ (toda la línea)",
            Body:
                "^ ancla al inicio de la línea.\n" +
                "$ ancla al final de la línea.\n\n" +
                "Ejemplos:\n" +
                "- ^[0-9]+$  → solo dígitos en toda la línea\n" +
                "- ^$        → línea vacía\n\n" +
                "Sin anclas, grep puede hacer match en cualquier parte de la línea."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 2 — Clases de caracteres y rangos",
            Body:
                "Una clase de caracteres va entre corchetes:\n" +
                "- [0-9]  un dígito\n" +
                "- [a-z]  una letra minúscula (según locale)\n" +
                "- [^0-9] un carácter que NO sea dígito (negación)\n\n" +
                "Importante: una clase como [^0-9] solo describe 1 carácter.\n" +
                "Para “no contiene dígitos” en toda la línea, usa repetición y anclas: ^[^0-9]*$"
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 2 — Grupos entre corchetes [...]: literales, organización y cuántificación",
            Body:
                "Qué es un grupo entre corchetes\n" +
                "La forma [...] define un conjunto de alternativas LITERALES: en cada posición del texto, " +
                "hace match con exactamente UN carácter que pertenezca a lo que hay dentro. " +
                "No es la agrupación de subpatrones con paréntesis ( ) ni la alternancia con |: eso está en «Nivel 3 — Agrupación (I)», «(II)» y «(III)». " +
                "Aquí [ ] es una clase de caracteres (un solo carácter por posición), no un “grupo ( )” de ERE.\n\n" +
                "Cómo se organiza lo de dentro\n" +
                "- Varios símbolos seguidos significan OR: [abc] es ‘a’ o ‘b’ o ‘c’.\n" +
                "- Un guion entre dos caracteres define un rango según el orden del juego de caracteres: [0-9], [a-z], [A-Z].\n" +
                "- El guion literal: suele ir al final (p. ej. [a-z-]) o escapado, según motor, para no confundirlo con rango.\n" +
                "- Si el primer carácter tras [ es ^, el resto es negación: [^0-9] = “un carácter que no sea dígito”.\n" +
                "- Puedes combinar rangos y símbulos sueltos: [A-Za-z_], [0-9a-fA-F].\n\n" +
                "Cuantificación (cómo se repite esa “elección”)\n" +
                "Los cuantificadores (* + ? {m,n}) se aplican al átomo INMEDIATAMENTE anterior. " +
                "Una clase [ ... ] cuenta como un solo átomo:\n" +
                "- [0-9]{3}     → tres dígitos seguidos (tres veces la clase “un dígito”).\n" +
                "- [a-z]+       → una o más letras minúsculas consecutivas.\n" +
                "- [a-z]{1,3}   → entre 1 y 3 letras minúsculas seguidas.\n" +
                "- [[:digit:]]{2,4} en ERE: la clase POSIX [[:digit:]] es un dígito; {2,4} cuantifica esa clase.\n\n" +
                "Encadenar varias clases\n" +
                "[0-9][0-9] son dos átomos: “un dígito” y otro “un dígito” → dos dígitos. " +
                "Con anclas: ^[0-9]+$ = toda la línea solo dígitos (al menos uno si + en lugar de *).\n\n" +
                "Relación con [[:clase:]]\n" +
                "Las clases POSIX van siempre dentro de corchetes EXTERIORES: [[:digit:]], [^[:space:]], etc. " +
                "Los corchetes exteriores son el “contenedor”; lo interior describe el conjunto permitido en esa posición.\n\n" +
                "Resumen mental\n" +
                "[...] = “elige 1 carácter entre estos”. Cuantificador = “repite esa elección”. " +
                "^ y $ = “toda la línea debe obedecer el patrón del medio”."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 3 — Agrupación (I): paréntesis ( ) y subexpresión",
            Body:
                "Capítulo 1 de 3 — Qué es agrupar con ( )\n" +
                "En POSIX ERE (grep -E), lo encerrado entre ( y ) es una subexpresión: el motor la trata como UN solo átomo. " +
                "Sirve para repetir un bloque entero con cuantificadores y para acotar qué parte del patrón afecta cada | (véase capítulos II y III).\n\n" +
                "Diferencia clave frente a [ ]\n" +
                "[ ] elige un solo carácter entre opciones de un carácter. ( ) agrupa una secuencia de símbolos del patrón: puede ser “abc”, “file\\.txt”, " +
                "“(yes|no)” con alternancia dentro, etc.\n\n" +
                "Ejemplos sin alternancia\n" +
                "- (ab)+     → una o más veces el par literal ab: ab, ababab…\n" +
                "- (ab)?     → cero o una vez ab.\n" +
                "- ab+       → sin paréntesis: una a y luego una o más b (no es lo mismo que (ab)+).\n\n" +
                "Siguientes capítulos del temario\n" +
                "- «Nivel 3 — Agrupación (II)»: operador | y precedencia frente a la concatenación.\n" +
                "- «Nivel 3 — Agrupación (III)»: cuantificar grupos con | dentro, anidación y notas BRE."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 3 — Agrupación (II): alternancia | y precedencia",
            Body:
                "Capítulo 2 de 3 — El operador |\n" +
                "| significa “o”: en ese punto del patrón el motor puede encajar una rama u otra. " +
                "Las ramas son los trozos de patrón separados por | que pertenecen al mismo nivel (el definido por los paréntesis que los rodean).\n\n" +
                "Precedencia: concatenación antes que |\n" +
                "En ERE, juntar símbolos (concatenación) tiene mayor precedencia que |. El | parte el patrón en alternativas “anchas”:\n" +
                "- ab|cd     → (ab)|(cd), no a(b|c)d.\n" +
                "- gray|grey → la palabra gray o la palabra grey completas.\n" +
                "- prefijo común + variantes: gr(ay|ey) → gray o grey manteniendo gr.\n\n" +
                "Por qué hacen falta paréntesis\n" +
                "Sin ( ), el | no “agrupa” letras sueltas: ab|cd ya son dos palabras alternativas. " +
                "Si quieres alternativas más cortas dentro de un contexto fijo, encierra las alternativas entre ( ).\n\n" +
                "Relación con el capítulo I y III\n" +
                "El capítulo (I) explica que ( ) forma un átomo; aquí ves cómo | reparte el interior o el patrón global. " +
                "El capítulo (III) explica (a|b)+, (yes|no)+ y anidación."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 3 — Agrupación (III): cuantificar grupos, anidación y BRE",
            Body:
                "Capítulo 3 de 3 — Cuantificadores pegados al grupo\n" +
                "*, +, ? y {m,n} se aplican al átomo inmediatamente anterior. Si ese átomo es ( … ), el cuantificador repite todo el grupo:\n" +
                "- (a|b)+        → una o más veces a o b en cada repetición (p. ej. abaab).\n" +
                "- (cat|dog)+   → una o más veces la subcadena cat o dog en cada repetición.\n" +
                "- ^(yes|no)+$  → toda la línea solo con trozos yes o no pegados (ejercicio relacionado en práctica).\n" +
                "Sin paréntesis, ab+ es “a” + una o más “b”, no repetición del par ab.\n\n" +
                "Anidación\n" +
                "Puedes anidar ( ( ) ): cada ( ) abre un nuevo nivel de agrupación y de alternancia local. " +
                "Los motores POSIX imponen un límite de profundidad; patrones absurdamente anidados pueden fallar.\n\n" +
                "Límites y estilo\n" +
                "Ramas vacías o | colgando al inicio/fin del patrón suelen ser válidas en POSIX ERE pero son confusas; evítalas.\n\n" +
                "BRE frente a ERE (grep sin -E vs grep -E)\n" +
                "En BRE, la agrupación y la alternancia suelen escribirse \\( \\) y \\|. En ERE de esta app, ( ) y | van sin escape. " +
                "Si el ejercicio indica BRE, revisa el tema «POSIX BRE vs POSIX ERE».\n\n" +
                "Referencias hacia atrás (\\1, \\2, …)\n" +
                "Para exigir que un trozo repetido sea exactamente igual al que ya encajó un paréntesis, usa \\1 (primer grupo), \\2 (segundo), etc. " +
                "Tema completo: «Nivel 5 — Referencias hacia atrás \\1–\\9».\n\n" +
                "Resumen\n" +
                "( ) agrupa; | parte alternativas con reglas de precedencia; los cuantificadores repiten el grupo entero cuando van después de )."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 3 — Clases POSIX ([:digit:], [:space:], ...)",
            Body:
                "Las clases POSIX se escriben así (dobles corchetes y : delante y detrás):\n" +
                "- [[:alnum:]]  letras o dígitos\n" +
                "- [[:alpha:]]  letras\n" +
                "- [[:blank:]]  espacios en blanco horizontales (espacio y tab)\n" +
                "- [[:cntrl:]]  caracteres de control\n" +
                "- [[:digit:]]  dígitos\n" +
                "- [[:graph:]]  visibles (printables excepto el espacio)\n" +
                "- [[:lower:]]  letras minúsculas\n" +
                "- [[:print:]]  imprimibles (incluye el espacio)\n" +
                "- [[:punct:]]  signos de puntuación\n" +
                "- [[:space:]]  espacios en blanco (incluye tab, salto de línea, etc.)\n" +
                "- [[:upper:]]  letras mayúsculas\n" +
                "- [[:xdigit:]] dígitos hex (0-9, a-f, A-F)\n\n" +
                "Ejemplos:\n" +
                "- ^[[:digit:]]+$             → solo dígitos\n" +
                "- ^[[:xdigit:]]{2,8}$        → 2 a 8 hex\n" +
                "- ^[^[:digit:]]*$            → sin dígitos\n" +
                "- ^[[:alpha:]][[:alnum:]_]*$ → identificador simple\n\n" +
                "Ojo: NO es válido escribir [:digit:] sin los corchetes externos."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 4 — Cuantificadores *, +, ?, {m,n}",
            Body:
                "Átomo anterior y agrupación\n" +
                "Un cuantificador siempre modifica el átomo inmediatamente anterior: un carácter, una clase […] o un grupo entre ( ). " +
                "Para repetir un bloque entero (varias letras, o varias opciones unidas con |), agrupa con ( ): p. ej. (ab)+, (yes|no)+. " +
                "Precedencia de |, subexpresión y cuantificación de grupos: temario «Nivel 3 — Agrupación (I)», «(II)» y «(III)».\n\n" +
                "Los cuantificadores indican cuántas repeticiones:\n" +
                "- *  cero o más\n" +
                "- +  una o más\n" +
                "- ?  cero o una\n" +
                "- {m,n} entre m y n (según implementación)\n\n" +
                "En POSIX ERE (grep -E), las llaves {m,n} son cuantificadores y normalmente NO hace falta escaparlas.\n" +
                "Si antes te parecía que tenías que escribir \\{2,4\\}, eso era un síntoma de un problema de integración en Windows/Git grep, no de la teoría ERE.\n\n" +
                "Ejemplos:\n" +
                "- ^[0-9]{2,4}$  → 2 a 4 dígitos\n" +
                "- ^[^[:digit:]]*$ → sin dígitos en toda la línea\n\n" +
                "Codicia (greedy) y qué NO ofrece grep POSIX en esta app\n" +
                "En POSIX ERE/BRE los cuantificadores *, +, ?, {m,n} son codiciosos: repiten lo máximo posible sin romper el resto del patrón.\n\n" +
                "Los cuantificadores “perezosos” de Perl/PCRE (*?, +?, ??, {n,m}?) no forman parte del POSIX que documenta grep en modos " +
                "normales (-E / sin -E). En GNU grep -E, secuencias como a+? no equivalen a “+ no codicioso” de Perl; no las uses para lazy matching.\n\n" +
                "GNU grep tiene también grep -P (Perl), pero Regexutor solo evalúa BRE y ERE POSIX; además -P suele exigir locale UTF-8 y puede fallar en entornos Windows.\n\n" +
                "Para repetir un mismo trozo ya capturado (backreference \\1), véase el tema «Nivel 5 — Referencias hacia atrás \\1–\\9»."
        ));

        TheoryTopics.Add(new TheoryTopic(
            Title: "Nivel 5 — Referencias hacia atrás \\1–\\9 (repetir lo capturado)",
            Body:
                "Qué es una referencia hacia atrás\n" +
                "Después de agrupar con paréntesis, \\1 significa “vuelve a encajar exactamente el mismo texto que ya coincidió con el PRIMER par de paréntesis de captura”. " +
                "\\2 referencia al segundo grupo, y así hasta \\9 en la práctica habitual de GNU grep.\n\n" +
                "ERE (grep -E) — ejemplos\n" +
                "- ^(.)\\1$           → línea de exactamente dos caracteres iguales (aa, 99, dos espacios…).\n" +
                "- ^([a-z]+)-\\1$    → palabra en minúsculas, guion, la misma palabra otra vez (cat-cat, go-go).\n\n" +
                "BRE (grep sin -E) — misma idea con escapes\n" +
                "Agrupación \\( … \\) y repetición con \\1. Para “una o más” letras en GNU BRE suele usarse \\+:\n" +
                "- ^\\([a-z]\\+\\)-\\1$   → mismo patrón palabra-palabra que arriba.\n\n" +
                "Estándar POSIX y GNU\n" +
                "Las referencias hacia atrás no están en el núcleo del estándar POSIX ERE, pero GNU grep (p. ej. el de Git for Windows) las acepta en -E y en BRE. " +
                "En otros motores “POSIX” podrían faltar: trata \\1 como capacidad de GNU salvo que verifiques tu herramienta.\n\n" +
                "Consejos\n" +
                "- El número del grupo cuenta por el orden de paréntesis de apertura (desde la izquierda del patrón).\n" +
                "- \\1 es metasímbolo: no es el dígito literal 1 del texto salvo que el motor lo interprete en contexto donde no aplica referencia.\n" +
                "- Practica los ejercicios «backref» en la pestaña Práctica (ERE y BRE)."
        ));
    }

    private void SeedCheatSheet()
    {
        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Esquema — Cómo leer este resumen",
            Body:
                "Este esquema está basado en un “cheat sheet” general de regex, traducido al español.\n\n" +
                "IMPORTANTE: Regexutor usa grep (POSIX BRE/ERE). Por eso:\n" +
                "- Algunas entradas del mundo PCRE/Perl (lookaround, modificadores /g, etc.) NO aplican aquí.\n" +
                "- Cuando una sintaxis NO sea POSIX, lo indicamos como “(no en POSIX grep)”.\n\n" +
                "Consejo: usa la pestaña Teoría para explicaciones largas; aquí es un mapa rápido."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Anclas (anchors)",
            Body:
                "^  Inicio de línea.\n" +
                "$  Fin de línea.\n" +
                "\\A Inicio de texto (no en POSIX grep).\n" +
                "\\Z Fin de texto o antes del salto final (no en POSIX grep).\n" +
                "\\z Fin de texto (no en POSIX grep).\n" +
                "\\b Límite de palabra (no en POSIX ERE/BRE de grep; a veces existe como extensión GNU en otros modos).\n" +
                "\\B No-límite de palabra (no en POSIX grep).\n" +
                "\\G Inicio de la coincidencia anterior (no en POSIX grep).\n"
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Clases de caracteres (character classes)",
            Body:
                ".   Cualquier carácter (en grep suele excluir el salto de línea).\n" +
                "\\d Dígito (no en POSIX grep; usa [0-9] o [[:digit:]]).\n" +
                "\\D No dígito (no en POSIX grep; usa [^0-9] o [^[:digit:]]).\n" +
                "\\s Espacio en blanco (no en POSIX grep; usa [[:space:]]).\n" +
                "\\S No espacio (no en POSIX grep; usa [^[:space:]]).\n" +
                "\\w “carácter de palabra” (no en POSIX grep; aproxima con [[:alnum:]_]).\n" +
                "\\W No-“palabra” (no en POSIX grep).\n" +
                "\\xhh Hex (no en POSIX grep).\n" +
                "\\cX Control (no en POSIX grep).\n"
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "POSIX [[:...:]] (grep)",
            Body:
                "[[:upper:]]  Letras mayúsculas.\n" +
                "[[:lower:]]  Letras minúsculas.\n" +
                "[[:alpha:]]  Letras.\n" +
                "[[:alnum:]]  Letras y dígitos.\n" +
                "[[:digit:]]  Dígitos.\n" +
                "[[:xdigit:]] Dígitos hex.\n" +
                "[[:punct:]]  Puntuación.\n" +
                "[[:blank:]]  Espacio y tab.\n" +
                "[[:space:]]  Espacios en blanco (incluye tab, etc.).\n" +
                "[[:cntrl:]]  Controles.\n" +
                "[[:graph:]]  Imprimibles excepto espacio.\n" +
                "[[:print:]]  Imprimibles (incluye espacio).\n" +
                "[[:word:]]   Letras/dígitos/_ (extensión GNU; no siempre en todos los POSIX).\n"
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Cuantificadores (quantifiers)",
            Body:
                "*    0 o más.\n" +
                "+    1 o más (ERE; en BRE suele ser literal o \\+ según GNU).\n" +
                "?    0 o 1 (ERE; en BRE suele ser literal o \\? según GNU).\n" +
                "{n}      Exactamente n (ERE; en BRE suele ser \\{n\\}).\n" +
                "{n,}     n o más (según implementación).\n" +
                "{n,m}    entre n y m.\n\n" +
                "“Lazy / no codicioso” (*?, +?, ??, {n,m}?) → (no en POSIX grep)."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Grupos y rangos (groups & ranges)",
            Body:
                "(ab)   Grupo (ERE). En BRE: \\(ab\\).\n" +
                "a|b    Alternancia (ERE). En BRE: a\\|b.\n" +
                "[abc]  Uno de a/b/c.\n" +
                "[^abc] Uno que NO sea a/b/c.\n" +
                "[a-q]  Rango de a a q.\n" +
                "[A-Q]  Rango de A a Q.\n" +
                "[0-7]  Dígito 0..7.\n" +
                "\\x     Referencia al grupo x (\\1..\\9) — GNU grep lo soporta en ERE/BRE, pero no es “núcleo POSIX”.\n\n" +
                "Nota: los rangos son inclusivos."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Secuencias de escape",
            Body:
                "\\\\   Escapa el carácter siguiente (depende del motor).\n" +
                "\\Q...\\E  Literaliza un bloque (no en POSIX grep).\n\n" +
                "En POSIX grep, lo más común es escapar metacaracteres puntuales: \\. \\[ \\] \\^ \\$ etc."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Metacaracteres comunes",
            Body:
                "^  $  .  |  (  )  [  ]  {  }  *  +  ?\n\n" +
                "En BRE, varios de estos NO son especiales salvo escape (p. ej. +, ?, |, ( ), { })."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Caracteres especiales (escapes típicos)",
            Body:
                "\\n  Nueva línea (no en POSIX grep como escape; en grep los patrones son por línea).\n" +
                "\\r  Retorno de carro (no en POSIX grep).\n" +
                "\\t  Tabulador (no estándar POSIX; puede funcionar en algunos motores, pero no cuentes con ello).\n" +
                "\\v  Tab vertical (no en POSIX grep).\n" +
                "\\f  Form feed (no en POSIX grep).\n" +
                "\\ooo Octal (no en POSIX grep).\n" +
                "\\xhh Hex (no en POSIX grep).\n"
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Aserciones / Lookaround (no en POSIX grep)",
            Body:
                "?=   Lookahead positivo (no en POSIX grep).\n" +
                "?!   Lookahead negativo (no en POSIX grep).\n" +
                "?<=  Lookbehind positivo (no en POSIX grep).\n" +
                "?<!  Lookbehind negativo (no en POSIX grep).\n" +
                "?>   Subexpresión atómica (no en POSIX grep).\n" +
                "?(cond)  Condicional (no en POSIX grep).\n" +
                "?#   Comentario (no en POSIX grep)."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Modificadores del patrón (flags) (no en POSIX grep)",
            Body:
                "g  Global (no aplica a grep: grep ya busca coincidencia por línea).\n" +
                "i  Case-insensitive (en grep se hace con -i, no con modificador en la regex).\n" +
                "m  Multiline (grep es por líneas; no es un flag dentro del patrón).\n" +
                "s  Dotall (no en POSIX grep).\n" +
                "x  Ignorar espacios/comentarios (no en POSIX grep).\n" +
                "U  Ungreedy (no en POSIX grep).\n" +
                "P  PCRE (grep -P; Regexutor no lo usa)."
        ));

        CheatSheetTopics.Add(new TheoryTopic(
            Title: "Reemplazo de cadenas (no en Regexutor)",
            Body:
                "Regexutor NO hace sustituciones; solo evalúa si hay match.\n\n" +
                "En otros motores, suelen existir referencias como $1, $2 o \\1 en el reemplazo, y tokens tipo $&, $`, $'."
        ));
    }

    private async Task EvaluateAsync()
    {
        var exercise = ActiveTheorySource;
        if (_runner is null || exercise is null)
            return;

        IsEvaluating = true;
        _evaluateAsyncInner.RaiseCanExecuteChanged();

        try
        {
            LastResults.Clear();
            LastSummary = string.Empty;
            LastHints.Clear();
            HasHints = false;

            var rawPattern = Pattern ?? string.Empty;
            var pattern = NormalizePattern(rawPattern);
            if (!string.Equals(rawPattern, pattern, StringComparison.Ordinal))
            {
                LastHints.Add("Se ignoraron saltos de línea pegados al final de la expresión (\\r/\\n).");
            }

            if (pattern.Length != pattern.Trim().Length)
            {
                LastHints.Add("Tu expresión tiene espacios al inicio o al final. Eso suele causar fallos invisibles. Revisa y elimina espacios sobrantes.");
            }

            AddDialectHints(exercise.Dialect, pattern, LastHints);
            AddExerciseSpecificHints(exercise, pattern, LastHints);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var res = await _runner.EvaluateAsync(exercise.Dialect, pattern, exercise.TestCases, cts.Token);

            if (res.EngineError is not null)
            {
                LastSummary = $"Error del motor: {res.EngineError}";
                HasHints = LastHints.Count > 0;
                return;
            }

            foreach (var r in res.TestCaseResults)
            {
                LastResults.Add(new TestCaseRow(
                    Input: r.TestCase.Input,
                    Expected: r.TestCase.ShouldMatch ? "match" : "no match",
                    Actual: r.DidMatch ? "match" : "no match",
                    Status: r.Passed ? "OK" : "FAIL"
                ));
            }

            var ok = res.TestCaseResults.Count(x => x.Passed);
            var total = res.TestCaseResults.Count;
            LastSummary = res.Success
                ? $"Perfecto: {ok}/{total} casos correctos."
                : $"Aún no: {ok}/{total} casos correctos. Ajusta la expresión y reintenta.";

            if (!res.Success)
            {
                LastHints.Add(
                    "Revisa los casos en FAIL: si el enunciado pide **toda la línea**, suele hacer falta `^` y `$` (y cuidado con espacios al final). " +
                    "Si pide que la línea **contenga** un texto, `^...$` suele ser demasiado estricto: obliga a que no haya nada antes ni después."
                );
            }

            HasHints = LastHints.Count > 0;
        }
        finally
        {
            IsEvaluating = false;
            _evaluateAsyncInner.RaiseCanExecuteChanged();
        }
    }

    private static string NormalizePattern(string pattern)
    {
        // Muy común al copiar/pegar desde otra app: termina con \r\n o \n.
        return pattern.TrimEnd('\r', '\n');
    }

    private static void AddDialectHints(RegexDialect dialect, string pattern, ObservableCollection<string> hints)
    {
        if (dialect != RegexDialect.PosixBre)
            return;

        // En BRE: |, +, ?, () no son operadores "normales" (salvo extensiones tipo GNU con \+ etc.).
        if (pattern.Contains('|'))
            hints.Add("Estás en BRE: el operador '|' no funciona como alternancia. Usa ERE (grep -E) o reescribe el patrón.");

        if (pattern.Contains('+') && !pattern.Contains("\\+"))
            hints.Add("Estás en BRE: '+' suele ser literal. En GNU grep, el cuantificador es '\\+' (o cambia a ERE).");

        if (pattern.Contains('?') && !pattern.Contains("\\?"))
            hints.Add("Estás en BRE: '?' suele ser literal. En GNU grep, el cuantificador es '\\?' (o cambia a ERE).");

        if ((pattern.Contains('(') || pattern.Contains(')')) && !(pattern.Contains("\\(") || pattern.Contains("\\)")))
            hints.Add("Estás en BRE: los paréntesis para agrupar suelen ser '\\(' y '\\)'. En ERE se usan '(' y ')'.");
    }

    private static void AddExerciseSpecificHints(RegexExercise ex, string pattern, ObservableCollection<string> hints)
    {
        // Heurística: si el enunciado dice "SOLO si toda la línea..." sugerir ^ y $ si faltan.
        var wantsWholeLine = ex.Prompt.Contains("toda la línea", StringComparison.OrdinalIgnoreCase)
                             || ex.Prompt.Contains("SOLO si", StringComparison.OrdinalIgnoreCase);

        var wantsNoContains = ex.Prompt.Contains("no contiene", StringComparison.OrdinalIgnoreCase)
                              || ex.Prompt.Contains("no contenga", StringComparison.OrdinalIgnoreCase);

        if (wantsWholeLine)
        {
            if (!pattern.TrimStart().StartsWith("^", StringComparison.Ordinal))
                hints.Add("Este ejercicio parece requerir 'toda la línea': considera empezar con '^' para anclar al inicio.");
            if (!pattern.TrimEnd().EndsWith("$", StringComparison.Ordinal))
                hints.Add("Este ejercicio parece requerir 'toda la línea': considera terminar con '$' para anclar al final.");
        }

        // "contengan" / "contenga" (pero no "no contengan") → match por subcadena; ^$ fuerza línea completa.
        var wantsContainsFragment = ex.Prompt.Contains("contengan", StringComparison.OrdinalIgnoreCase)
                                    && !ex.Prompt.Contains("no conteng", StringComparison.OrdinalIgnoreCase);

        if (wantsContainsFragment)
        {
            var trimmed = pattern.Trim();
            if (trimmed.StartsWith("^", StringComparison.Ordinal) && trimmed.EndsWith("$", StringComparison.Ordinal))
            {
                hints.Add(
                    "Este enunciado pide que la línea **contenga** el texto (puede haber caracteres antes o después). " +
                    "Con `^` al inicio y `$` al final exiges que **toda** la línea sea solo lo que va entre ellos. Para «contiene», quita `^` y `$` y deja el fragmento literal (en BRE, `+` ya es literal: `C++`)."
                );
            }
        }

        if (wantsNoContains)
        {
            // Para "NO contiene X", lo típico es ^[^X]*$ (o similar) para garantizar que no aparezca en toda la línea.
            var hasStartAnchor = pattern.TrimStart().StartsWith("^", StringComparison.Ordinal);
            var hasEndAnchor = pattern.TrimEnd().EndsWith("$", StringComparison.Ordinal);
            var hasRepetition = pattern.Contains('*') || pattern.Contains('+') || pattern.Contains('{');

            var looksLikeSingleCharClass =
                pattern.Trim().StartsWith("[", StringComparison.Ordinal) &&
                pattern.Trim().EndsWith("]", StringComparison.Ordinal) &&
                !hasStartAnchor && !hasEndAnchor;

            if (!hasStartAnchor || !hasEndAnchor || !hasRepetition || looksLikeSingleCharClass)
            {
                hints.Add(
                    "Para 'NO contiene ...', una clase de caracteres por sí sola suele ser insuficiente (solo comprueba 1 carácter). " +
                    "Normalmente necesitas anclas y repetición para cubrir toda la línea, por ejemplo: ^[^...]*$"
                );
            }
        }
    }

    private Task GoToTheoryAsync()
    {
        var src = ActiveTheorySource;
        if (src is null)
            return Task.CompletedTask;

        var topic = FindRelatedTheoryTopic(src);
        if (topic is not null)
            SelectedTheoryTopic = topic;

        // Switch to Teoría tab
        SelectedTabIndex = TabTheory;
        return Task.CompletedTask;
    }

    private TheoryTopic? FindRelatedTheoryTopic(RegexExercise ex)
    {
        // Heurística simple basada en el Id/título/prompt del ejercicio.
        var id = ex.Id ?? string.Empty;
        var title = ex.Title ?? string.Empty;
        var prompt = ex.Prompt ?? string.Empty;

        bool Has(string s, string needle) => s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        if (id.StartsWith("exam-gen-", StringComparison.Ordinal) || id.StartsWith("exam-replay-", StringComparison.Ordinal))
        {
            if (Has(prompt, "KEY") && Has(prompt, "VALUE"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 4 — Cuantificadores", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "color hexadecimal") || Has(prompt, "#RRGGBB"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 2 — Clases", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "AAAA-MM-DD"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 4 — Cuantificadores", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "token alfanumérico"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 2", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "contengan literalmente"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 1 — POSIX BRE", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "longitud está entre") || (Has(prompt, "dígitos") && Has(prompt, "longitud")))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 4 — Cuantificadores", StringComparison.OrdinalIgnoreCase));
            if (Has(prompt, "NO contiene ningún dígito"))
                return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 3 — Clases POSIX", StringComparison.OrdinalIgnoreCase));
        }

        if (Has(id, "starts-with-digit") || Has(id, "ends-with-com") || Has(id, "anchors") || Has(title, "anclas") || Has(prompt, "^") || Has(prompt, "$"))
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 1 — Anclas", StringComparison.OrdinalIgnoreCase));

        if (Has(id, "posix-space") || Has(id, "posix-class") || Has(title, "POSIX") || Has(prompt, "[[:"))
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 3 — Clases POSIX", StringComparison.OrdinalIgnoreCase));

        if (id == "ere-1-digits" || Has(id, "negated-class") || Has(id, "quantifiers-range") || Has(id, "only-letter-a") ||
            Has(id, "simple-phone") || Has(id, "host-port") || Has(id, "email-like"))
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 2 — Grupos entre corchetes", StringComparison.OrdinalIgnoreCase));

        if (Has(id, "yes-no-line") || Has(id, "alternation-yes-no"))
            return TheoryTopics.FirstOrDefault(t => t.Title.Contains("(III)", StringComparison.Ordinal));

        if (Has(id, "alternation") || Has(title, "alternancia") || Has(prompt, "|"))
            return TheoryTopics.FirstOrDefault(t => t.Title.Contains("(II)", StringComparison.Ordinal));

        if (Has(id, "backref"))
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 5 — Referencias", StringComparison.OrdinalIgnoreCase));

        if (Has(title, "cuantific") || Has(prompt, "{m,n}") || Has(prompt, "*") || Has(prompt, "+") || Has(prompt, "?"))
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 4 — Cuantificadores", StringComparison.OrdinalIgnoreCase));

        if (ex.Dialect == RegexDialect.PosixBre || ex.Dialect == RegexDialect.PosixEre)
            return TheoryTopics.FirstOrDefault(t => t.Title.StartsWith("Nivel 1 — POSIX BRE vs POSIX ERE", StringComparison.OrdinalIgnoreCase));

        return TheoryTopics.FirstOrDefault();
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed record TestCaseRow(string Input, string Expected, string Actual, string Status)
{
    /// <summary>Entrada con espacios/tabuladores visibles (la tabla oculta espacios finales).</summary>
    public string InputDisplay => TestCaseInputFormatting.ToDisplay(Input);

    /// <summary>Descripción de la cadena exacta (longitud, blancos al inicio/fin).</summary>
    public string InputTooltip => TestCaseInputFormatting.ToTooltip(Input);
}

internal static class TestCaseInputFormatting
{
    public static string ToDisplay(string input)
    {
        if (input.Length == 0)
            return "∅";

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            switch (c)
            {
                case ' ':
                    sb.Append('\u00B7'); // middle dot
                    break;
                case '\t':
                    sb.Append('→');
                    break;
                case '\r':
                    sb.Append('␍');
                    break;
                case '\n':
                    sb.Append('␊');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    public static string ToTooltip(string input)
    {
        if (input.Length == 0)
            return "Línea vacía (0 caracteres).";

        var sb = new StringBuilder(128);
        sb.Append("Longitud: ").Append(input.Length).Append(" caracteres.");
        if (char.IsWhiteSpace(input[0]))
            sb.Append(" Empieza con blanco (U+").Append(((ushort)input[0]).ToString("X4")).Append(").");
        if (input.Length > 1 && char.IsWhiteSpace(input[^1]))
            sb.Append(" Termina con blanco (U+").Append(((ushort)input[^1]).ToString("X4")).Append(").");
        sb.Append(" En la tabla, · = espacio U+0020.");
        return sb.ToString();
    }
}

public sealed record TheoryTopic(string Title, string Body);

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;

    public AsyncCommand(Func<Task> execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public async void Execute(object? parameter) => await _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

