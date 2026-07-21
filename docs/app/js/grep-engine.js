window.GrepEngine = (function () {
    var _cli = null;
    var _ready = false;
    var lastErrors = [];
    var lastDebug = "";

    async function init() {
        if (_ready) return;
        _cli = await new Aioli(["grep/3.7"]);
        _ready = true;
    }

    async function evaluateAll(pattern, testCases, dialect) {
        lastErrors = [];
        lastDebug = "";
        if (!pattern) {
            return testCases.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
        }

        if (!_cli) {
            lastErrors.push({ pattern: pattern, input: "(init)", error: "Motor grep no inicializado" });
            return testCases.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
        }

        var tmpFile = "/tmp/_rgx_batch.txt";
        var lines = testCases.map(function (tc) { return tc[0]; });
        var content = lines.join("\n") + "\n";
        await _cli.fs.writeFile(tmpFile, content);

        var verifyContent = "";
        try {
            verifyContent = await _cli.exec("cat", [tmpFile]);
        } catch (ce) {
            lastErrors.push({ pattern: pattern, input: "(cat)", error: String(ce) });
        }

        var grepArgs = dialect === "BRE"
            ? ["-n", pattern, tmpFile]
            : ["-E", "-n", pattern, tmpFile];

        var matchedLines = {};
        var rawOutput = "";
        var threw = false;
        try {
            rawOutput = await _cli.exec("grep", grepArgs);
            var outLines = rawOutput.split("\n");
            for (var i = 0; i < outLines.length; i++) {
                var m = outLines[i].match(/^(\d+):/);
                if (m) matchedLines[parseInt(m[1])] = true;
            }
        } catch (e) {
            threw = true;
            var errMsg = "";
            if (e && e.stdout) errMsg += "STDOUT:[" + e.stdout + "] ";
            if (e && e.stderr) errMsg += "STDERR:[" + e.stderr + "] ";
            if (e && e.message) errMsg += "MSG:[" + e.message + "] ";
            if (!errMsg) errMsg = JSON.stringify(e);
            rawOutput = errMsg;
        }

        lastDebug = "CMD: grep " + grepArgs.join(" ") +
            "\nVERIFY cat: [" + verifyContent.replace(/\n/g, "\\n") + "]" +
            "\nOUTPUT: [" + (rawOutput || "").replace(/\n/g, "\\n") + "]" +
            "\nMATCHED: " + JSON.stringify(matchedLines) +
            "\nTHREW: " + threw;

        alert(lastDebug);

        try { await _cli.fs.unlink(tmpFile); } catch (_) {}

        var results = [];
        for (var i = 0; i < testCases.length; i++) {
            var tc = testCases[i];
            var didMatch = !!matchedLines[i + 1];
            results.push({
                input: tc[0],
                expected: tc[1],
                didMatch: didMatch,
                passed: didMatch === tc[1]
            });
        }
        return results;
    }

    function getErrors() { return lastErrors; }
    function getDebug() { return lastDebug; }

    return { init: init, evaluateAll: evaluateAll, getErrors: getErrors, getDebug: getDebug };
})();
