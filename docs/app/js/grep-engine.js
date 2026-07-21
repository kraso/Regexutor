window.GrepEngine = (function () {
    var _cli = null;
    var _ready = false;

    async function init() {
        if (_ready) return;
        _cli = await new Aioli(["grep/3.7"]);
        _ready = true;
    }

    async function evaluate(pattern, input, dialect) {
        if (!pattern) {
            return { match: false, count: 0 };
        }
        var tmpFile = "/tmp/_rgx_test.txt";
        await _cli.fs.writeFile(tmpFile, input + "\n");
        var args = dialect === "BRE"
            ? ["-c", pattern, tmpFile]
            : ["-E", "-c", pattern, tmpFile];
        try {
            var result = await _cli.exec("grep", args);
            var count = parseInt(result.trim()) || 0;
            await _cli.fs.unlink(tmpFile);
            return { match: count > 0, count: count };
        } catch (e) {
            try { await _cli.fs.unlink(tmpFile); } catch (_) {}
            return { match: false, count: 0 };
        }
    }

    async function evaluateAll(pattern, testCases, dialect) {
        var results = [];
        for (var i = 0; i < testCases.length; i++) {
            var tc = testCases[i];
            var r = await evaluate(pattern, tc[0], dialect);
            results.push({
                input: tc[0],
                expected: tc[1],
                didMatch: r.match,
                passed: r.match === tc[1]
            });
        }
        return results;
    }

    return { init: init, evaluate: evaluate, evaluateAll: evaluateAll };
})();
