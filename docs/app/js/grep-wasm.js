window.GrepEngine = (function () {
    var _evalCount = 0;

    async function init() {
    }

    async function evaluateAll(pattern, testCases, dialect) {
        if (!pattern) {
            return testCases.map(function (tc) {
                return { input: tc[0], expected: tc[1], didMatch: false, passed: !tc[1] };
            });
        }

        _evalCount++;
        var cli = await new Aioli(["grep/3.7"]);

        var tmpFile = "/tmp/_rgx_" + _evalCount + ".txt";
        var content = testCases.map(function (tc) { return tc[0]; }).join("\n") + "\n";
        await cli.fs.writeFile(tmpFile, content);

        var grepArgs = dialect === "BRE"
            ? ["-n", pattern, tmpFile]
            : ["-E", "-n", pattern, tmpFile];

        var matchedLines = {};
        try {
            var result = await cli.exec("grep", grepArgs);
            var outLines = result.split("\n");
            for (var i = 0; i < outLines.length; i++) {
                var m = outLines[i].match(/^(\d+):/);
                if (m) matchedLines[parseInt(m[1])] = true;
            }
        } catch (e) {
        }

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
