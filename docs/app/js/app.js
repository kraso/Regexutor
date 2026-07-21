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
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
