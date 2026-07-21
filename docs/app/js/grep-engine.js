window.GrepEngine = (function () {
    var _cli = null;
    var _ready = false;

    async function init() {
        if (_ready) return;
        _cli = await new Aioli(["grep/3.7"]);
        _ready = true;
    }

    async function evaluateAll(pattern, testCases, dialect) {
        if (!pattern) {
            return testCases.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
        }

        if (!_cli) {
            return testCases.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
        }

        var tmpFile = "/tmp/_rgx_batch.txt";
        var content = testCases.map(function (tc) { return tc[0]; }).join("\n") + "\n";
        await _cli.fs.writeFile(tmpFile, content);

        var grepArgs = dialect === "BRE"
            ? ["-n", pattern, tmpFile]
            : ["-E", "-n", pattern, tmpFile];

        var matchedLines = {};
        try {
            var result = await _cli.exec("grep", grepArgs);
            var outLines = result.split("\n");
            for (var i = 0; i < outLines.length; i++) {
                var m = outLines[i].match(/^(\d+):/);
                if (m) matchedLines[parseInt(m[1])] = true;
            }
        } catch (e) {
        }

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

    return { init: init, evaluateAll: evaluateAll };
})();
