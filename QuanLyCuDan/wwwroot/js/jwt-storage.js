(() => {
    "use strict";
    const key = "qlcd.accessToken";
    const token = document.getElementById("session-token")?.dataset.token;
    try {
        if (!token) { localStorage.removeItem(key); return; }
        const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
        const expiresAt = Number(payload.exp) * 1000;
        if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) { localStorage.removeItem(key); return; }
        localStorage.setItem(key, JSON.stringify({ accessToken: token, expiresAt }));
    } catch {
        // Cookie authentication and ordinary MVC forms still work if storage is unavailable.
        try { localStorage.removeItem(key); } catch { }
    }
})();
