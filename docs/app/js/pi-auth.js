window.PiAuth = (function () {
    var _user = null;

    function isAvailable() {
        return typeof Pi !== "undefined";
    }

    async function login() {
        if (!isAvailable()) {
            throw new Error("Pi Network no disponible en este navegador.");
        }
        await Pi.init({ version: "2.0" });
        var auth = await Pi.authenticate(["username"]);
        _user = auth.user;
        return auth.user;
    }

    function getUser() { return _user; }
    function isLoggedIn() { return _user !== null; }

    return { login: login, getUser: getUser, isLoggedIn: isLoggedIn, isAvailable: isAvailable };
})();
