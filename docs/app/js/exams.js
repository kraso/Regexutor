window.ExamCatalog = (function () {
    function randInt(r, min, max) { return Math.floor(r() * (max - min + 1)) + min; }
    function randUpper(r, len) {
        var s = "";
        for (var i = 0; i < len; i++) s += String.fromCharCode(65 + Math.floor(r() * 26));
        return s;
    }
    function randDigits(r, len) {
        var s = "";
        for (var i = 0; i < len; i++) s += String(Math.floor(r() * 10));
        return s;
    }
    function shuffle(arr, r) {
        for (var i = arr.length - 1; i > 0; i--) {
            var j = Math.floor(r() * (i + 1));
            var tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
        }
        return arr;
    }
    function hex6(r) {
        var h = "0123456789ABCDEF";
        var s = "#";
        for (var i = 0; i < 6; i++) s += h[Math.floor(r() * 16)];
        return s;
    }

    function buildKeyValue(r) {
        var valids = [];
        for (var i = 0; i < 5; i++) {
            var kl = randInt(r, 3, 8), vl = randInt(r, 1, 4);
            valids.push([randUpper(r, kl) + "=" + randDigits(r, vl), true]);
        }
        var invalids = [
            [randUpper(r, 2) + "=" + randDigits(r, 1), false],
            [randUpper(r, 9) + "=" + randDigits(r, 1), false],
            [randUpper(r, 4).toLowerCase() + "=" + randDigits(r, 2), false],
            [randUpper(r, 4) + "=", false],
            [randUpper(r, 4) + "=" + randDigits(r, 5), false],
            [randUpper(r, 4) + "=" + randDigits(r, 2) + " ", false],
            [" " + randUpper(r, 4) + "=" + randDigits(r, 2), false],
            [randUpper(r, 4) + "= " + randDigits(r, 2), false],
        ];
        return {
            id: "exam-gen-kv-" + Date.now(),
            title: "EXAMEN ERE · KEY=VALUE",
            prompt: "Examen (ERE). La línea completa debe cumplir:\n- KEY: exactamente entre 3 y 8 letras MAYÚSCULAS (A-Z)\n- '=' literal\n- VALUE: entre 1 y 4 dígitos (0-9)\nSin espacios ni caracteres extra.",
            dialect: "ERE",
            tests: shuffle(valids.concat(invalids), r)
        };
    }

    function buildDigitRange(r) {
        var m = randInt(r, 1, 3);
        var n = randInt(r, m + 1, m + 4);
        function D(len) {
            var s = "";
            for (var i = 0; i < len; i++) s += String(randInt(r, i === len - 1 ? 1 : 0, 9));
            return s;
        }
        var cases = [
            [D(m), true], [D(n), true],
            [m < n ? D(Math.floor((m + n) / 2)) : D(m), true],
            [m > 1 ? D(m - 1) : "a", false],
            [D(n + 1 + randInt(r, 0, 2)), false],
            ["", false], [D(n) + " ", false], [" " + D(m), false],
            [D(m) + "x", false], ["x" + D(m), false]
        ];
        return {
            id: "exam-gen-digits-" + Date.now(),
            title: "EXAMEN ERE · rango de dígitos",
            prompt: "Examen (ERE). Match SOLO si toda la línea es solo dígitos (0-9) y su longitud está entre " + m + " y " + n + " caracteres.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildHexColor(r) {
        var cases = [
            [hex6(r), true], [hex6(r), true],
            ["#" + randDigits(r, 4), false],
            ["#" + randUpper(r, 5), false],
            ["#" + randUpper(r, 7), false],
            ["#abcdef", false],
            ["#GGGGGG", false], ["", false]
        ];
        return {
            id: "exam-gen-hex-" + Date.now(),
            title: "EXAMEN ERE · #RRGGBB",
            prompt: "Examen (ERE). Match SOLO si toda la línea es '#' seguido de exactamente 6 caracteres hex en [0-9A-F] (mayúsculas).",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildNoDigits(r) {
        function AlphaLine(len) {
            var ch = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-\t ";
            var s = "";
            for (var i = 0; i < len; i++) s += ch[Math.floor(r() * ch.length)];
            return s;
        }
        var cases = [
            [AlphaLine(randInt(r, 3, 12)), true],
            [AlphaLine(randInt(r, 1, 20)), true],
            ["", true], ["___", true],
            [AlphaLine(4) + randDigits(r, 1), false],
            [randDigits(r, 3), false],
            ["id=" + randDigits(r, 2), false],
        ];
        return {
            id: "exam-gen-nodig-" + Date.now(),
            title: "EXAMEN ERE · línea sin dígitos",
            prompt: "Examen (ERE). Match SOLO si la línea completa NO contiene ningún dígito ASCII 0-9.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildAlnumToken(r) {
        function Good() {
            var len = randInt(r, 1, 9);
            var s = String.fromCharCode(97 + randInt(r, 0, 25));
            for (var i = 1; i < len; i++) s += r() > 0.5 ? String(randInt(r, 0, 9)) : String.fromCharCode(97 + randInt(r, 0, 25));
            return s;
        }
        var cases = [
            [Good(), true], [Good(), true], ["A", true], ["Z9", true],
            [randDigits(r, randInt(r, 2, 5)), false],
            ["9abc", false], ["a-b", false], ["a b", false], ["", false]
        ];
        return {
            id: "exam-gen-alnum-" + Date.now(),
            title: "EXAMEN ERE · token alfanumérico",
            prompt: "Examen (ERE). Match SOLO si toda la línea es UN solo token alfanumérico: al menos una letra, resto letras o dígitos, sin espacios.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildBreLiteral(r) {
        var words = ["TODO", "FIXME", "NULL", "WARN", "BUG", "HACK"];
        var word = words[randInt(r, 0, words.length - 1)];
        var cases = [
            ["Revisar " + word + " antes de merge", true],
            ["// " + word + ": explicación", true],
            [word + " en cualquier sitio", true],
            ["sin palabra clave aquí", false],
            [word.slice(0, -1), false],
            [word.toLowerCase(), false],
        ];
        return {
            id: "exam-gen-bre-lit-" + Date.now(),
            title: "EXAMEN BRE · literal",
            prompt: "Examen (BRE). Haz match en líneas que contengan literalmente '" + word + "' (mayúsculas exactas).",
            dialect: "BRE",
            tests: shuffle(cases, r)
        };
    }

    function buildIsoDate(r) {
        var y = 2000 + randInt(r, 0, 99);
        var mo = randInt(r, 1, 12);
        var d = randInt(r, 1, 31);
        var pad = function(n) { return n < 10 ? "0" + n : "" + n; };
        var cases = [
            [y + "-" + pad(mo) + "-" + pad(d), true],
            ["2026-04-27", true],
            ["2026-1-05", false], ["2026-01-5", false],
            ["2026/01/05", false], ["2026-01-05 ", false],
            ["2026-13-01", false], ["2026-00-10", false],
            ["2026-04-32", false], ["", false]
        ];
        return {
            id: "exam-gen-date-" + Date.now(),
            title: "EXAMEN ERE · fecha AAAA-MM-DD",
            prompt: "Examen (ERE). Match SOLO si la línea tiene forma AAAA-MM-DD (AAAA 2000-2099, MM 01-12, DD 01-31).",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    var templates = [
        { id: "exam-ere-keyvalue", title: "ERE: KEY=VALUE", shortHelp: "Reglas fijas; entradas aleatorias.", build: buildKeyValue },
        { id: "exam-ere-digit-range", title: "ERE: dígitos con longitud", shortHelp: "Los límites m y n cambian.", build: buildDigitRange },
        { id: "exam-ere-hex-color", title: "ERE: #RRGGBB", shortHelp: "Hex en mayúsculas.", build: buildHexColor },
        { id: "exam-ere-no-digits", title: "ERE: sin dígitos", shortHelp: "Toda la línea sin [0-9].", build: buildNoDigits },
        { id: "exam-ere-alnum", title: "ERE: token alfanumérico", shortHelp: "Una palabra alfanumérica.", build: buildAlnumToken },
        { id: "exam-bre-literal", title: "BRE: literal fijo", shortHelp: "Palabra aleatoria.", build: buildBreLiteral },
        { id: "exam-ere-date", title: "ERE: fecha AAAA-MM-DD", shortHelp: "Sin calendario real.", build: buildIsoDate },
    ];

    function getTemplates() { return templates; }
    function buildExam(templateId) {
        var t = templates.find(function(x) { return x.id === templateId; });
        if (!t) return null;
        return t.build(Math.random);
    }

    return { getTemplates: getTemplates, buildExam: buildExam };
})();
