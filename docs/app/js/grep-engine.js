window.GrepEngine = (function () {
    let _cli = null;
    let _ready = false;

    async function init() {
        if (_ready) return;
        _cli = await new Aioli(["grep/3.7"]);
        _ready = true;
    }

    function _args(pattern, dialect) {
        return dialect === "BRE" ? ["-c", pattern] : ["-E", "-c", pattern];
    }

    async function evaluate(pattern, input, dialect) {
        if (!pattern) {
            return { match: false, count: 0 };
        }
        _cli.stdin = input + "\n";
        try {
            var result = await _cli.exec("grep", _args(pattern, dialect));
            var count = parseInt(result.trim()) || 0;
            return { match: count > 0, count: count };
        } catch (e) {
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
