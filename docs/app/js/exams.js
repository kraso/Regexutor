window.ExamCatalog = (function () {
    var _cycleIdx = {};

    function shuffle(arr, r) {
        var a = arr.slice();
        for (var i = a.length - 1; i > 0; i--) {
            var j = Math.floor(r() * (i + 1));
            var tmp = a[i]; a[i] = a[j]; a[j] = tmp;
        }
        return a;
    }

    function simpleRand(seed) {
        var x = Math.sin(seed + 1) * 10000;
        return x - Math.floor(x);
    }

    function makeR(seed) {
        var s = seed;
        return function () {
            s = (s * 16807 + 12345) % 2147483647;
            return (s & 0x7fffffff) / 0x7fffffff;
        };
    }

    var templates = [
        {
            id: "exam-ere-keyvalue", title: "ERE: KEY=VALUE", shortHelp: "KEY mayúsculas 3-8, VALUE dígitos 1-4.",
            variants: [
                function (r) { return buildKV(r, 3, 8, 1, 4); },
                function (r) { return buildKV(r, 4, 6, 2, 3); },
                function (r) { return buildKV(r, 3, 5, 1, 2); },
                function (r) { return buildKV(r, 5, 8, 3, 4); },
                function (r) { return buildKV(r, 3, 7, 1, 3); },
                function (r) { return buildKV(r, 4, 8, 2, 4); }
            ]
        },
        {
            id: "exam-ere-digit-range", title: "ERE: dígitos con longitud", shortHelp: "Solo dígitos, longitud m-n.",
            variants: [
                function (r) { return buildDigits(r, 2, 4); },
                function (r) { return buildDigits(r, 1, 3); },
                function (r) { return buildDigits(r, 3, 5); },
                function (r) { return buildDigits(r, 2, 6); },
                function (r) { return buildDigits(r, 4, 7); },
                function (r) { return buildDigits(r, 1, 5); }
            ]
        },
        {
            id: "exam-ere-hex-color", title: "ERE: #RRGGBB", shortHelp: "Hexadecimal mayúsculas.",
            variants: [
                function (r) { return buildHex(r, false); },
                function (r) { return buildHex(r, true); },
                function (r) { return buildHex(r, false); },
                function (r) { return buildHex(r, true); },
                function (r) { return buildHex(r, false); },
                function (r) { return buildHex(r, true); }
            ]
        },
        {
            id: "exam-ere-no-digits", title: "ERE: sin dígitos", shortHelp: "Ningún dígito 0-9.",
            variants: [
                function (r) { return buildNoDig(r, true); },
                function (r) { return buildNoDig(r, false); },
                function (r) { return buildNoDig(r, true); },
                function (r) { return buildNoDig(r, false); },
                function (r) { return buildNoDig(r, true); },
                function (r) { return buildNoDig(r, false); }
            ]
        },
        {
            id: "exam-ere-alnum", title: "ERE: token alfanumérico", shortHelp: "Una palabra [a-z0-9].",
            variants: [
                function (r) { return buildAlnum(r, 1, 5); },
                function (r) { return buildAlnum(r, 2, 8); },
                function (r) { return buildAlnum(r, 1, 3); },
                function (r) { return buildAlnum(r, 3, 7); },
                function (r) { return buildAlnum(r, 1, 9); },
                function (r) { return buildAlnum(r, 2, 6); }
            ]
        },
        {
            id: "exam-bre-literal", title: "BRE: literal fijo", shortHelp: "Palabra clave exacta.",
            variants: [
                function (r) { return buildBRE(r, "TODO"); },
                function (r) { return buildBRE(r, "FIXME"); },
                function (r) { return buildBRE(r, "NULL"); },
                function (r) { return buildBRE(r, "WARN"); },
                function (r) { return buildBRE(r, "BUG"); },
                function (r) { return buildBRE(r, "HACK"); }
            ]
        },
        {
            id: "exam-ere-date", title: "ERE: fecha AAAA-MM-DD", shortHelp: "Formato ISO.",
            variants: [
                function (r) { return buildDate(r, 2024, 2026); },
                function (r) { return buildDate(r, 2020, 2023); },
                function (r) { return buildDate(r, 2027, 2030); },
                function (r) { return buildDate(r, 2000, 2010); },
                function (r) { return buildDate(r, 2015, 2020); },
                function (r) { return buildDate(r, 2030, 2040); }
            ]
        }
    ];

    function buildKV(r, kMin, kMax, vMin, vMax) {
        function K() {
            var len = Math.floor(r() * (kMax - kMin + 1)) + kMin;
            var s = "";
            for (var i = 0; i < len; i++) s += String.fromCharCode(65 + Math.floor(r() * 26));
            return s;
        }
        function V() {
            var len = Math.floor(r() * (vMax - vMin + 1)) + vMin;
            var s = "";
            for (var i = 0; i < len; i++) s += String(Math.floor(r() * 10));
            return s;
        }
        var valids = [];
        for (var i = 0; i < 5; i++) valids.push([K() + "=" + V(), true]);
        var invalids = [
            [K().slice(0, 2) + "=" + V(), false],
            [K() + K().slice(0, 3) + "=" + V(), false],
            [K().toLowerCase() + "=" + V(), false],
            [K() + "=", false],
            [K() + "=" + V() + V() + V(), false],
            [K() + "=" + V() + " ", false],
            [" " + K() + "=" + V(), false],
        ];
        return {
            id: "exam-gen-kv-" + Date.now(),
            title: "EXAMEN ERE · KEY=VALUE",
            prompt: "Examen (ERE). La línea completa debe cumplir:\n- KEY: entre " + kMin + " y " + kMax + " letras MAYÚSCULAS (A-Z)\n- '=' literal\n- VALUE: entre " + vMin + " y " + vMax + " dígitos (0-9)\nSin espacios ni caracteres extra.",
            dialect: "ERE",
            tests: shuffle(valids.concat(invalids), r)
        };
    }

    function buildDigits(r, m, n) {
        function D(len) {
            var s = "";
            for (var i = 0; i < len; i++) s += String(Math.floor(r() * (i === len - 1 ? 9 : 10)));
            return s;
        }
        var cases = [
            [D(m), true], [D(n), true],
            [D(Math.floor((m + n) / 2)), true],
            [m > 1 ? D(m - 1) : "a", false],
            [D(n + 1 + Math.floor(r() * 2)), false],
            ["", false], [D(n) + " ", false], [" " + D(m), false],
            [D(m) + "x", false], ["x" + D(m), false]
        ];
        return {
            id: "exam-gen-digits-" + Date.now(),
            title: "EXAMEN ERE · rango de dígitos",
            prompt: "Examen (ERE). Match SOLO si toda la línea es solo dígitos (0-9) de entre " + m + " y " + n + " caracteres.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildHex(r, lower) {
        function hex() {
            var h = lower ? "0123456789abcdef" : "0123456789ABCDEF";
            var s = "#";
            for (var i = 0; i < 6; i++) s += h[Math.floor(r() * 16)];
            return s;
        }
        function rnd(len) {
            var h = lower ? "0123456789abcdef" : "0123456789ABCDEF";
            var s = "#";
            for (var i = 0; i < len; i++) s += h[Math.floor(r() * 16)];
            return s;
        }
        var cases = [
            [hex(), true], [hex(), true], [hex(), true],
            [rnd(4), false], [rnd(5), false], [rnd(7), false],
            ["#abcdef", false], ["#GGGGGG", false], ["", false]
        ];
        return {
            id: "exam-gen-hex-" + Date.now(),
            title: "EXAMEN ERE · #RRGGBB",
            prompt: "Examen (ERE). Match SOLO si toda la línea es '#' seguido de exactamente 6 caracteres hex en [" + (lower ? "0-9a-f" : "0-9A-F") + "] (" + (lower ? "minúsculas" : "mayúsculas") + ").",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildNoDig(r, withTabs) {
        var ch = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-";
        if (withTabs) ch += "\t ";
        function line(len) {
            var s = "";
            for (var i = 0; i < len; i++) s += ch[Math.floor(r() * ch.length)];
            return s;
        }
        var cases = [
            [line(Math.floor(r() * 10) + 3), true],
            [line(Math.floor(r() * 15) + 1), true],
            ["", true], ["___", true],
            [line(4) + String(Math.floor(r() * 10)), false],
            [String(Math.floor(r() * 900) + 100), false],
            ["id=" + String(Math.floor(r() * 90) + 10), false],
        ];
        return {
            id: "exam-gen-nodig-" + Date.now(),
            title: "EXAMEN ERE · línea sin dígitos",
            prompt: "Examen (ERE). Match SOLO si la línea completa NO contiene ningún dígito ASCII 0-9.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildAlnum(r, minLen, maxLen) {
        function good() {
            var len = Math.floor(r() * (maxLen - minLen + 1)) + minLen;
            var s = String.fromCharCode(97 + Math.floor(r() * 26));
            for (var i = 1; i < len; i++) s += r() > 0.5 ? String(Math.floor(r() * 10)) : String.fromCharCode(97 + Math.floor(r() * 26));
            return s;
        }
        function rndDigits() {
            var s = "";
            var len = Math.floor(r() * 4) + 2;
            for (var i = 0; i < len; i++) s += String(Math.floor(r() * 10));
            return s;
        }
        var cases = [
            [good(), true], [good(), true], ["a", true], ["z9", true],
            [rndDigits(), false],
            ["9abc", false], ["a-b", false], ["a b", false], ["", false]
        ];
        return {
            id: "exam-gen-alnum-" + Date.now(),
            title: "EXAMEN ERE · token alfanumérico",
            prompt: "Examen (ERE). Match SOLO si toda la línea es UN solo token alfanumérico minúsculo: entre " + minLen + " y " + maxLen + " caracteres, sin espacios ni guiones.",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function buildBRE(r, word) {
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
            title: "EXAMEN BRE · literal '" + word + "'",
            prompt: "Examen (BRE). Haz match en líneas que contengan literalmente '" + word + "' (mayúsculas exactas).",
            dialect: "BRE",
            tests: shuffle(cases, r)
        };
    }

    function buildDate(r, yMin, yMax) {
        var y = yMin + Math.floor(r() * (yMax - yMin + 1));
        var mo = Math.floor(r() * 12) + 1;
        var d = Math.floor(r() * 28) + 1;
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
            prompt: "Examen (ERE). Match SOLO si la línea tiene forma AAAA-MM-DD (AAAA " + yMin + "-" + yMax + ", MM 01-12, DD 01-31).",
            dialect: "ERE",
            tests: shuffle(cases, r)
        };
    }

    function getTemplates() { return templates; }

    function buildExam(templateId) {
        var t = templates.find(function (x) { return x.id === templateId; });
        if (!t || !t.variants || t.variants.length === 0) return null;
        if (_cycleIdx[templateId] === undefined) _cycleIdx[templateId] = 0;
        var idx = _cycleIdx[templateId] % t.variants.length;
        _cycleIdx[templateId]++;
        var r = makeR(idx * 1000 + t.variants.length * 7);
        return t.variants[idx](r);
    }

    function getCycleInfo(templateId) {
        var t = templates.find(function (x) { return x.id === templateId; });
        if (!t) return null;
        var idx = (_cycleIdx[templateId] || 0) % t.variants.length;
        return { current: idx + 1, total: t.variants.length };
    }

    return { getTemplates: getTemplates, buildExam: buildExam, getCycleInfo: getCycleInfo };
})();
