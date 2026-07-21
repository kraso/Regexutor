(function () {
    var $ = function (id) { return document.getElementById(id); };

    var state = {
        view: "loading",
        exercise: null,
        pattern: "",
        results: null,
        summary: "",
        hints: [],
        theoryIdx: 0,
        piUser: null,
        examExercise: null,
        selectedExamTemplate: null,
        lastExamSnapshot: null,
    };

    function show(view) {
        state.view = view;
        document.querySelectorAll(".view").forEach(function (v) { v.style.display = "none"; });
        var el = $(view);
        if (el) el.style.display = "block";
        renderNav();
    }

    function renderNav() {
        var items = [
            { id: "practice", label: "Práctica" },
            { id: "theory", label: "Teoría" },
            { id: "exams", label: "Exámenes" },
            { id: "about", label: "Acerca de" },
        ];
        var nav = $("nav-links");
        nav.innerHTML = items.map(function (it) {
            var cls = it.id === state.view ? "active" : "";
            return '<a class="nav-link ' + cls + '" data-view="' + it.id + '">' + it.label + '</a>';
        }).join("");
        nav.querySelectorAll(".nav-link").forEach(function (a) {
            a.onclick = function () { show(a.dataset.view); };
        });
    }

    function renderPractice() {
        var sel = $("exercise-select");
        sel.innerHTML = '<option value="">— Selecciona ejercicio —</option>' +
            EXERCISES.map(function (e, i) {
                return '<option value="' + i + '">' + e.title + '</option>';
            }).join("");
        sel.onchange = function () {
            var idx = parseInt(sel.value);
            if (isNaN(idx)) { state.exercise = null; renderPracticeContent(); return; }
            state.exercise = EXERCISES[idx];
            state.pattern = "";
            state.results = null;
            state.summary = "";
            state.hints = [];
            renderPracticeContent();
        };
        renderPracticeContent();
    }

    function renderPracticeContent() {
        var ex = state.exercise;
        var detail = $("practice-detail");
        var promptEl = $("exercise-prompt");
        var dialectEl = $("exercise-dialect");
        var inputEl = $("pattern-input");
        var btn = $("eval-btn");
        var hintBtn = $("hint-btn");
        var resultsEl = $("test-results");
        var summaryEl = $("eval-summary");
        var hintsEl = $("eval-hints");

        if (!ex) {
            detail.style.display = "none";
            return;
        }

        detail.style.display = "";
        promptEl.textContent = ex.prompt;
        dialectEl.textContent = ex.dialect === "ERE" ? "POSIX ERE (grep -E)" : "POSIX BRE (grep)";
        inputEl.value = state.pattern;
        btn.disabled = false;
        hintBtn.disabled = false;

        if (state.results) {
            renderResults(resultsEl, state.results);
            summaryEl.textContent = state.summary;
            summaryEl.className = "summary " + (state.results.every(function(r){return r.passed}) ? "ok" : "err");
            summaryEl.style.display = "";
            if (state.hints.length) {
                hintsEl.innerHTML = state.hints.map(function (h) { return "<li>" + h + "</li>"; }).join("");
                hintsEl.style.display = "";
            } else {
                hintsEl.style.display = "none";
            }
        } else {
            resultsEl.innerHTML = "";
            summaryEl.style.display = "none";
            hintsEl.style.display = "none";
        }
    }

    function renderResults(container, results) {
        var html = '<table><thead><tr><th>Entrada</th><th>Esperado</th><th>Obtenido</th><th>Estado</th></tr></thead><tbody>';
        results.forEach(function (r) {
            var display = r.input === "" ? "∅" : r.input.replace(/ /g, "·").replace(/\t/g, "→");
            var cls = r.passed ? "pass" : "fail";
            html += '<tr class="' + cls + '"><td class="mono">' + escHtml(display) +
                "</td><td>" + (r.expected ? "match" : "no match") +
                "</td><td>" + (r.didMatch ? "match" : "no match") +
                "</td><td>" + (r.passed ? "OK" : "FAIL") + "</td></tr>";
        });
        html += "</tbody></table>";
        container.innerHTML = html;
    }

    function escHtml(s) {
        return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }

    function generateHint(exercise) {
        if (!exercise) return "";
        var id = exercise.id || "";
        var p = (exercise.prompt || "").toLowerCase();
        var d = exercise.dialect;

        if (p.indexOf("toda la línea") >= 0 || p.indexOf("solo si") >= 0) {
            return "Este ejercicio pide 'toda la línea'. Usa ^ al inicio y $ al final para anclar.";
        }
        if (id.indexOf("posix") >= 0 || p.indexOf("[[:") >= 0) {
            return "Usa clases POSIX como [[:digit:]], [[:alpha:]], [[:space:]]. Recuerda los dobles corchetes.";
        }
        if (id.indexOf("backref") >= 0 || p.indexOf("\\1") >= 0) {
            return "Agrupa con ( ) y repite con \\1. Ejemplo: ^(.)\\1$ para dos caracteres iguales.";
        }
        if (id.indexOf("alternation") >= 0 || id.indexOf("yes-no") >= 0) {
            return "Usa alternancia | entre paréntesis: (yes|no). Para cuantificar el grupo: (yes|no)+";
        }
        if (id.indexOf("negated") >= 0 || p.indexOf("no contiene") >= 0 || p.indexOf("sin") >= 0) {
            return "Para 'sin dígitos', usa una clase negada con repetición: ^[^0-9]*$ o ^[^[:digit:]]*$";
        }
        if (id.indexOf("quantifier") >= 0 || p.indexOf("{m,n}") >= 0) {
            return "Usa cuantificadores de rango: {min,max}. Ejemplo: ^[0-9]{2,4}$ para 2-4 dígitos.";
        }
        if (id.indexOf("ends-with") >= 0) {
            return "Para 'termina en X', usa $ al final: algo$";
        }
        if (id.indexOf("starts-with") >= 0 || p.indexOf("primer carácter") >= 0) {
            return "Para 'empieza con X', usa ^ al inicio: ^algo";
        }
        if (id.indexOf("email") >= 0) {
            return "Busca el patrón @ como separador. No necesitas validación completa, solo detectar algo@algo.";
        }
        if (id.indexOf("phone") >= 0) {
            return "Formato fijo: 3 dígitos, guion, 3 dígitos, guion, 3 dígitos. Los guiones son literales.";
        }
        if (id.indexOf("host-port") >= 0) {
            return "HOST: minúsculas a-z de 3-6 caracteres. PORT: 2-4 dígitos. El ':' es literal.";
        }
        if (id.indexOf("literal-plus") >= 0) {
            return "En BRE, '+' es literal. No necesitas escaparlo para buscar C++.";
        }
        if (id.indexOf("escaped-plus") >= 0 || id.indexOf("bre-2") >= 0) {
            return "En BRE GNU, \\+ es el cuantificador 'uno o más'. Sin escape, + es literal.";
        }
        if (id.indexOf("literal-dot") >= 0 || id.indexOf("bre-3") >= 0) {
            return "En BRE, el punto es cualquier carácter. Para punto literal, escapa: \\.";
        }
        if (d === "BRE") {
            return "Recuerda: en BRE, + ? | ( ) no son operadores. Usa \\+ \\? \\| \\( \\) o cambia a ERE.";
        }
        if (d === "ERE") {
            return "En ERE (grep -E), + ? | ( ) son operadores normales. No necesitas escaparlos.";
        }
        return "Escribe tu expresión regular y pulsa Evaluar para comprobar.";
    }

    function showHint() {
        var ex = state.exercise;
        if (!ex) return;
        var hint = generateHint(ex);
        var hintsEl = $("eval-hints");
        hintsEl.innerHTML = "<li>" + hint + "</li>";
        hintsEl.style.display = "";
    }

    async function evaluate() {
        if (!state.exercise) return;
        var btn = $("eval-btn");
        btn.disabled = true;
        btn.textContent = "Evaluando…";

        var pattern = $("pattern-input").value.trim();
        state.pattern = pattern;
        state.hints = [];

        if (!pattern) {
            state.results = state.exercise.tests.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
            state.summary = "Patrón vacío.";
            renderPracticeContent();
            btn.disabled = false;
            btn.textContent = "Evaluar";
            return;
        }

        if (pattern !== pattern.trimEnd()) {
            state.hints.push("Se ignoraron saltos de línea al final.");
        }

        state.results = await GrepEngine.evaluateAll(pattern, state.exercise.tests, state.exercise.dialect);
        var ok = state.results.filter(function (r) { return r.passed; }).length;
        var total = state.results.length;
        state.summary = ok === total
            ? "Perfecto: " + ok + "/" + total + " casos correctos."
            : "Aún no: " + ok + "/" + total + " casos correctos. Ajusta la expresión.";

        if (ok < total) {
            state.hints.push("Revisa los casos FAIL. Si el enunciado pide 'toda la línea', usa ^ y $.");
        }

        renderPracticeContent();
        btn.disabled = false;
        btn.textContent = "Evaluar";
    }

    function renderTheory() {
        var sel = $("theory-select");
        sel.innerHTML = THEORY.map(function (t, i) {
            return '<option value="' + i + '">' + t.title + '</option>';
        }).join("");
        sel.onchange = function () {
            state.theoryIdx = parseInt(sel.value);
            renderTheoryContent();
        };
        renderTheoryContent();
    }

    function renderTheoryContent() {
        var t = THEORY[state.theoryIdx] || THEORY[0];
        $("theory-title").textContent = t.title;
        $("theory-body").textContent = t.body;
    }

    function renderExams() {
        var sel = $("exam-template-select");
        var templates = ExamCatalog.getTemplates();
        sel.innerHTML = '<option value="">— Selecciona plantilla —</option>' +
            templates.map(function (t) {
                return '<option value="' + t.id + '">' + t.title + '</option>';
            }).join("");
        sel.onchange = function () {
            state.selectedExamTemplate = sel.value || null;
        };
        $("gen-exam-btn").onclick = generateExam;
        $("repeat-exam-btn").onclick = repeatExam;
        renderExamContent();
    }

    function renderExamContent() {
        var ex = state.examExercise;
        var detail = $("exam-detail");
        var promptEl = $("exam-prompt");
        var inputEl = $("exam-pattern-input");
        var btn = $("exam-eval-btn");
        var resultsEl = $("exam-results");
        var summaryEl = $("exam-summary");
        var dialectEl = $("exam-dialect");

        if (!ex) {
            detail.style.display = "none";
            return;
        }

        detail.style.display = "";
        promptEl.textContent = ex.prompt;
        dialectEl.textContent = ex.dialect === "ERE" ? "POSIX ERE (grep -E)" : "POSIX BRE (grep)";
        inputEl.value = state.pattern;
        btn.disabled = false;

        if (state.results) {
            renderResults(resultsEl, state.results);
            summaryEl.textContent = state.summary;
            summaryEl.className = "summary " + (state.results.every(function(r){return r.passed}) ? "ok" : "err");
            summaryEl.style.display = "";
        } else {
            resultsEl.innerHTML = "";
            summaryEl.style.display = "none";
        }
    }
    }

    function generateExam() {
        if (!state.selectedExamTemplate) return;
        var ex = ExamCatalog.buildExam(state.selectedExamTemplate);
        state.examExercise = ex;
        state.lastExamSnapshot = ex;
        state.pattern = "";
        state.results = null;
        state.summary = "";
        $("repeat-exam-btn").disabled = !state.lastExamSnapshot;
        renderExamContent();
    }

    function repeatExam() {
        if (!state.lastExamSnapshot) return;
        var snap = state.lastExamSnapshot;
        state.examExercise = Object.assign({}, snap, {
            id: "exam-replay-" + Date.now(),
            title: snap.title + " (repetición)"
        });
        state.pattern = "";
        state.results = null;
        state.summary = "";
        renderExamContent();
    }

    async function evaluateExam() {
        if (!state.examExercise) return;
        var btn = $("exam-eval-btn");
        btn.disabled = true;
        btn.textContent = "Evaluando…";

        var pattern = $("exam-pattern-input").value.trim();
        state.pattern = pattern;

        state.results = await GrepEngine.evaluateAll(pattern, state.examExercise.tests, state.examExercise.dialect);
        var ok = state.results.filter(function (r) { return r.passed; }).length;
        var total = state.results.length;
        state.summary = ok === total
            ? "Perfecto: " + ok + "/" + total + " casos correctos."
            : "Aún no: " + ok + "/" + total + " casos correctos.";

        renderExamContent();
        btn.disabled = false;
        btn.textContent = "Evaluar";
    }

    function renderAbout() {
        $("about-version").textContent = "1.2.0";
    }

    function setLoggedIn(username) {
        $("pi-login-btn").style.display = "none";
        $("pi-user-badge").style.display = "";
        $("pi-username").textContent = username;
    }

    async function initPiLogin() {
        if (!PiAuth.isAvailable()) {
            $("pi-login-btn").disabled = true;
            $("pi-login-btn").textContent = "Pi no disponible";
            return;
        }
        $("pi-login-btn").disabled = true;
        $("pi-login-btn").textContent = "Conectando…";
        try {
            var user = await PiAuth.login();
            state.piUser = user;
            setLoggedIn(user.username);
        } catch (e) {
            $("pi-login-btn").disabled = false;
            $("pi-login-btn").textContent = "Iniciar sesión con Pi";
        }
    }

    async function boot() {
        $("eval-btn").onclick = evaluate;
        $("exam-eval-btn").onclick = evaluateExam;
        $("pi-login-btn").onclick = initPiLogin;
        $("hint-btn").onclick = showHint;

        var saved = PiAuth.tryRestore();
        if (saved) {
            setLoggedIn(saved.username);
        }

        renderPractice();
        renderTheory();
        renderExams();
        renderAbout();
        show("practice");

        $("loading-status").style.display = "";
        $("loading-status").textContent = "Cargando motor grep (WASM)…";
        try {
            await GrepEngine.init();
            $("loading-status").style.display = "none";
        } catch (e) {
            $("loading-status").textContent = "Error grep: " + e.message;
            $("loading-status").style.display = "";
        }

        $("loading-status").style.display = "none";
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
