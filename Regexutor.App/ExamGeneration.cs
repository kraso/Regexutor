using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Regexutor.Core;

namespace Regexutor.App;

/// <summary>Plantilla de examen: cada <see cref="Build"/> produce un <see cref="RegexExercise"/> distinto (casos y/o enunciado numérico).</summary>
public sealed record ExamTemplate(string Id, string Title, string ShortHelp, Func<Random, RegexExercise> Build);

public static class ExamCatalog
{
    public static IReadOnlyList<ExamTemplate> Templates { get; } =
    [
        new ExamTemplate(
            "exam-ere-keyvalue",
            "ERE: KEY=VALUE (casos aleatorios)",
            "Reglas fijas; entradas válidas/ inválidas distintas en cada intento.",
            BuildKeyValueEre),

        new ExamTemplate(
            "exam-ere-digit-range",
            "ERE: solo dígitos con longitud aleatoria",
            "Los límites m y n cambian; lee el enunciado antes de escribir el patrón.",
            BuildDigitRangeEre),

        new ExamTemplate(
            "exam-ere-hex-color",
            "ERE: color hexadecimal #RRGGBB",
            "Mayúsculas A-F; exactamente 6 hex tras #.",
            BuildHexColorEre),

        new ExamTemplate(
            "exam-ere-no-digits-line",
            "ERE: línea sin ningún dígito",
            "Toda la línea sin [0-9]; casos de texto aleatorios.",
            BuildNoDigitsWholeLineEre),

        new ExamTemplate(
            "exam-ere-alnum-token",
            "ERE: token alfanumérico (primera 'palabra')",
            "Una palabra: letras y dígitos, al menos una letra; casos mezclados.",
            BuildAlnumTokenEre),

        new ExamTemplate(
            "exam-bre-contains-literal",
            "BRE: contiene literal fijo (palabra aleatoria)",
            "La palabra objetivo cambia; en BRE casi todo es literal salvo escapes.",
            BuildBreContainsLiteral),

        new ExamTemplate(
            "exam-ere-yyyy-mm-dd",
            "ERE: fecha tipo AAAA-MM-DD (solo forma, no calendario)",
            "Dígitos fijos en posiciones; mes 01-12 y día 01-31 simplificados.",
            BuildIsoDateEre),
    ];

    private static string RandUpper(Random r, int len)
    {
        var sb = new StringBuilder(len);
        for (var i = 0; i < len; i++)
            sb.Append((char)('A' + r.Next(26)));
        return sb.ToString();
    }

    private static string RandDigits(Random r, int len)
    {
        var sb = new StringBuilder(len);
        for (var i = 0; i < len; i++)
            sb.Append((char)('0' + r.Next(10)));
        return sb.ToString();
    }

    private static void Shuffle<T>(IList<T> list, Random r)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = r.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static RegexExercise Ephemeral(string kind, string title, string prompt, RegexDialect dialect, IReadOnlyList<RegexTestCase> cases)
    {
        var stamp = Guid.NewGuid().ToString("N")[..10];
        return new RegexExercise(
            Id: $"exam-gen-{kind}-{stamp}",
            Title: $"{title} · intento {stamp}",
            Prompt: prompt + $"\n\nIdentificador de intento: {stamp} (sirve para distinguir capturas o dudas).",
            Dialect: dialect,
            TestCases: cases,
            IsEphemeralExam: true
        );
    }

    private static RegexExercise BuildKeyValueEre(Random r)
    {
        var valids = new List<RegexTestCase>();
        for (var i = 0; i < 5; i++)
        {
            var keyLen = r.Next(3, 9); // 3..8
            var valLen = r.Next(1, 5); // 1..4
            valids.Add(new RegexTestCase($"{RandUpper(r, keyLen)}={RandDigits(r, valLen)}", ShouldMatch: true));
        }

        var invalids = new List<RegexTestCase>
        {
            new($"{RandUpper(r, 2)}={RandDigits(r, 1)}", ShouldMatch: false), // key corta
            new($"{RandUpper(r, 9)}={RandDigits(r, 1)}", ShouldMatch: false), // key larga
            new($"{RandUpper(r, 4).ToLowerInvariant()}={RandDigits(r, 2)}", ShouldMatch: false), // minúsculas
            new($"{RandUpper(r, 4)}=", ShouldMatch: false), // sin valor
            new($"{RandUpper(r, 4)}={RandDigits(r, 5)}", ShouldMatch: false), // valor largo
            new($"{RandUpper(r, 4)}={RandDigits(r, 2)} ", ShouldMatch: false), // espacio final
            new($" {RandUpper(r, 4)}={RandDigits(r, 2)}", ShouldMatch: false), // espacio inicial
            new($"{RandUpper(r, 4)}= {RandDigits(r, 2)}", ShouldMatch: false), // espacio junto a =
            new($"{RandUpper(r, 4)}=x{r.Next(10)}", ShouldMatch: false), // no dígitos
            new($"{RandUpper(r, 4)}=12+3", ShouldMatch: false),
        };

        var all = new List<RegexTestCase>();
        all.AddRange(valids);
        all.AddRange(invalids);
        Shuffle(all, r);

        const string rules =
            "Examen (ERE). La línea completa debe cumplir:\n" +
            "- KEY: exactamente entre 3 y 8 letras MAYÚSCULAS (A-Z)\n" +
            "- '=' literal\n" +
            "- VALUE: entre 1 y 4 dígitos (0-9)\n" +
            "Sin espacios ni caracteres extra antes/después.";

        return Ephemeral("kv", "EXAMEN ERE · KEY=VALUE", rules, RegexDialect.PosixEre, all);
    }

    private static RegexExercise BuildDigitRangeEre(Random r)
    {
        var m = r.Next(1, 4); // 1..3
        var n = r.Next(m + 1, Math.Min(m + 4, 8)); // > m, cap

        string D(int len)
        {
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
                sb.Append((char)('0' + (i == len - 1 ? r.Next(1, 10) : r.Next(10)))); // avoid all-zero ambiguity for readability
            return sb.ToString();
        }

        var cases = new List<RegexTestCase>
        {
            new(D(m), ShouldMatch: true),
            new(D(n), ShouldMatch: true),
            new(m < n ? D((m + n) / 2) : D(m), ShouldMatch: true),
            new(m > 1 ? D(m - 1) : "a", ShouldMatch: false),
            new(D(n + 1 + r.Next(2)), ShouldMatch: false),
            new("", ShouldMatch: false),
            new(D(n) + " ", ShouldMatch: false),
            new(" " + D(m), ShouldMatch: false),
            new(D(m) + "x", ShouldMatch: false),
            new("x" + D(m), ShouldMatch: false),
        };

        if (m > 1)
            cases.Add(new RegexTestCase(D(1), ShouldMatch: false));

        Shuffle(cases, r);

        var prompt =
            $"Examen (ERE). Match SOLO si toda la línea está formada ÚNICAMENTE por dígitos (0-9) " +
            $"y su longitud está entre {m} y {n} caracteres (inclusive).";

        return Ephemeral("digits", "EXAMEN ERE · rango de dígitos", prompt, RegexDialect.PosixEre, cases);
    }

    private static RegexExercise BuildHexColorEre(Random r)
    {
        static string Hex6(Random rng)
        {
            const string h = "0123456789ABCDEF";
            var sb = new StringBuilder(7);
            sb.Append('#');
            for (var i = 0; i < 6; i++)
                sb.Append(h[rng.Next(h.Length)]);
            return sb.ToString();
        }

        var good = Hex6(r);
        var good2 = Hex6(r);

        var cases = new List<RegexTestCase>
        {
            new(good, ShouldMatch: true),
            new(good2, ShouldMatch: true),
            new("#" + RandDigits(r, 4), ShouldMatch: false), // solo 4 hex tras #
            new("#" + RandUpper(r, 5), ShouldMatch: false), // 5 hex
            new("#" + RandUpper(r, 7), ShouldMatch: false), // 7
            new("#abcdef", ShouldMatch: false), // minúsculas (A-F deben ir en mayúsculas)
            new(" " + good, ShouldMatch: false),
            new(good + " ", ShouldMatch: false),
            new("#GGGGGG", ShouldMatch: false),
            new("#" + new string('?', 6), ShouldMatch: false),
            new("", ShouldMatch: false),
        };

        Shuffle(cases, r);

        const string prompt =
            "Examen (ERE). Match SOLO si toda la línea es un color hexadecimal: '#' seguido de exactamente 6 caracteres en [0-9A-F] (mayúsculas en A-F).";

        return Ephemeral("hex", "EXAMEN ERE · #RRGGBB", prompt, RegexDialect.PosixEre, cases);
    }

    private static RegexExercise BuildNoDigitsWholeLineEre(Random r)
    {
        string AlphaLine(int len)
        {
            const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-\t ";
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
                sb.Append(letters[r.Next(letters.Length)]);
            return sb.ToString();
        }

        var cases = new List<RegexTestCase>
        {
            new(AlphaLine(r.Next(3, 12)), ShouldMatch: true),
            new(AlphaLine(r.Next(1, 20)), ShouldMatch: true),
            new("", ShouldMatch: true),
            new("___", ShouldMatch: true),
            new(AlphaLine(4) + RandDigits(r, 1), ShouldMatch: false),
            new(RandDigits(r, 3), ShouldMatch: false),
            new("id=" + RandDigits(r, 2), ShouldMatch: false),
            new(AlphaLine(3) + "\t" + RandDigits(r, 1), ShouldMatch: false),
            new("x" + new string('y', r.Next(2, 8)) + "0", ShouldMatch: false),
        };

        Shuffle(cases, r);

        const string prompt =
            "Examen (ERE). Match SOLO si la línea completa NO contiene ningún dígito ASCII 0-9 (puede estar vacía o tener letras, espacios, guiones, tabs, etc.).";

        return Ephemeral("nodig", "EXAMEN ERE · línea sin dígitos", prompt, RegexDialect.PosixEre, cases);
    }

    private static RegexExercise BuildAlnumTokenEre(Random r)
    {
        // "Toda la línea" = un token: [A-Za-z][A-Za-z0-9]* al menos una letra total
        string Good()
        {
            var len = r.Next(1, 10);
            var sb = new StringBuilder(len);
            sb.Append((char)('a' + r.Next(26)));
            for (var i = 1; i < len; i++)
            {
                if (r.Next(2) == 0)
                    sb.Append((char)('0' + r.Next(10)));
                else
                    sb.Append((char)('a' + r.Next(26)));
            }

            return sb.ToString();
        }

        var cases = new List<RegexTestCase>
        {
            new(Good(), ShouldMatch: true),
            new(Good(), ShouldMatch: true),
            new("A", ShouldMatch: true),
            new("Z9", ShouldMatch: true),
            new(RandDigits(r, r.Next(2, 6)), ShouldMatch: false), // solo dígitos
            new("9abc", ShouldMatch: false), // empieza en dígito
            new("a-b", ShouldMatch: false), // guion no pedido en el token simple - actually "simple" token alphanumeric no hyphen - invalid
            new("a b", ShouldMatch: false),
            new("", ShouldMatch: false),
            new(Good() + " ", ShouldMatch: false),
            new(" " + Good(), ShouldMatch: false),
        };

        Shuffle(cases, r);

        const string prompt =
            "Examen (ERE). Match SOLO si toda la línea es UN solo token: al menos una letra (a-z o A-Z) y el resto solo letras o dígitos (sin espacios ni otros símbolos). " +
            "No puede ser solo dígitos ni empezar por dígito.";

        return Ephemeral("alnum", "EXAMEN ERE · token alfanumérico", prompt, RegexDialect.PosixEre, cases);
    }

    private static readonly string[] BreLiteralWords = ["TODO", "FIXME", "NULL", "WARN", "BUG", "HACK"];

    private static RegexExercise BuildBreContainsLiteral(Random r)
    {
        var word = BreLiteralWords[r.Next(BreLiteralWords.Length)];
        var cases = new List<RegexTestCase>
        {
            new($"Revisar {word} antes de merge", ShouldMatch: true),
            new($"// {word}: explicación", ShouldMatch: true),
            new(word + " en cualquier sitio", ShouldMatch: true),
            new("sin palabra clave aquí", ShouldMatch: false),
            new(word[..^1], ShouldMatch: false), // prefijo: falta el último carácter
            new("prefijo_" + word + "_sufijo", ShouldMatch: true),
            new(word.ToLowerInvariant(), ShouldMatch: false), // mayúsculas exactas
        };

        Shuffle(cases, r);

        var prompt =
            $"Examen (BRE). Haz match en líneas que **contengan literalmente** la subcadena '{word}' (mayúsculas exactas). " +
            "No ancles a toda la línea salvo que lo necesites.";

        return Ephemeral("bre-lit", "EXAMEN BRE · literal", prompt, RegexDialect.PosixBre, cases);
    }

    private static RegexExercise BuildIsoDateEre(Random r)
    {
        // AAAA-MM-DD with YYYY 2000-2099, MM 01-12, DD 01-31 (not validating real dates)
        var y = 2000 + r.Next(100);
        var mo = r.Next(1, 13);
        var d = r.Next(1, 32);
        var cases = new List<RegexTestCase>
        {
            new($"{y:D4}-{mo:D2}-{d:D2}", ShouldMatch: true),
            new("2026-04-27", ShouldMatch: true),
            new("1999-01-01", ShouldMatch: false), // year out
            new("2026-1-05", ShouldMatch: false), // month one digit
            new("2026-01-5", ShouldMatch: false), // day one digit
            new("2026/01/05", ShouldMatch: false),
            new("2026-01-05 ", ShouldMatch: false),
            new(" 2026-01-05", ShouldMatch: false),
            new("2026-13-01", ShouldMatch: false), // month 13
            new("2026-00-10", ShouldMatch: false),
            new("2026-04-32", ShouldMatch: false), // day 32
            new("", ShouldMatch: false),
        };

        Shuffle(cases, r);

        const string prompt =
            "Examen (ERE). Match SOLO si toda la línea tiene forma AAAA-MM-DD donde:\n" +
            "- AAAA es 2000–2099\n" +
            "- MM es 01–12 (dos dígitos)\n" +
            "- DD es 01–31 (dos dígitos)\n" +
            "Separadores '-' literales. No valides si el día existe en el calendario.";

        return Ephemeral("date", "EXAMEN ERE · fecha AAAA-MM-DD", prompt, RegexDialect.PosixEre, cases);
    }
}
