window.PiAuth = (function () {
    var _user = null;
    var STORAGE_KEY = "regexutor_pi_user";

    function isAvailable() {
        return typeof Pi !== "undefined";
    }

    function _saveUser(user) {
        _user = user;
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(user)); } catch (e) {}
    }

    function _loadSaved() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            if (raw) { _user = JSON.parse(raw); return _user; }
        } catch (e) {}
        return null;
    }

    function _clearSaved() {
        _user = null;
        try { localStorage.removeItem(STORAGE_KEY); } catch (e) {}
    }

    async function login() {
        if (!isAvailable()) {
            throw new Error("Pi Network no disponible.");
        }
        await Pi.init({ version: "2.0" });
        var auth = await Pi.authenticate(["username"]);
        _saveUser(auth.user);
        return auth.user;
    }

    function getUser() { return _user; }
    function isLoggedIn() { return _user !== null; }
    function tryRestore() { return _loadSaved(); }
    function logout() { _clearSaved(); }

    return { login: login, getUser: getUser, isLoggedIn: isLoggedIn, isAvailable: isAvailable, tryRestore: tryRestore, logout: logout };
})();
