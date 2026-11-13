(() => {
    "use strict";
    const storageKey = "qlcd.accessToken";
    let bearer, until = 0, pending;
    function clearToken() {
        bearer = undefined;
        until = 0;
        localStorage.removeItem(storageKey);
    }
    function loadToken() {
        try {
            const saved = JSON.parse(localStorage.getItem(storageKey));
            bearer = typeof saved?.accessToken === "string" ? saved.accessToken : undefined;
            until = Number.isFinite(saved?.expiresAt) ? saved.expiresAt : 0;
        } catch {
            clearToken();
        }
    }
    function expired() {
        clearToken();
        location.assign("/Account/Login");
        return new Error("Phiên đã hết hiệu lực. Hãy đăng nhập lại.");
    }
    async function parse(response) {
        if (!response.ok) {
            const problem = await response.json().catch(() => ({}));
            throw new Error(problem.errors ? Object.values(problem.errors).flat().join(" ") : problem.title || "Không thể thực hiện thao tác (" + response.status + ").");
        }
        return response.status === 204 ? null : response.json();
    }
    async function refresh() {
        // Lấy CSRF hiện hành, kể cả sau khi tài khoản cookie thay đổi.
        const antiResponse = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store" });
        if (antiResponse.status === 401) throw expired();
        const anti = await parse(antiResponse);
        let response;
        try {
            response = await fetch("/api/auth/refresh", {
                method: "POST", credentials: "same-origin", cache: "no-store",
                headers: { "X-CSRF-TOKEN": anti.token }
            });
        } catch {
            // Phản hồi có thể bị mất sau khi server đã đổi refresh token: không gửi lại.
            throw expired();
        }
        if (response.status === 401) throw expired();
        const result = await parse(response);
        bearer = result.accessToken;
        until = Date.now() + Math.max(0, result.expiresIn - 30) * 1000;
        localStorage.setItem(storageKey, JSON.stringify({ accessToken: bearer, expiresAt: until }));
        return bearer;
    }
    async function token(rejectedToken) {
        loadToken();
        if (bearer && Date.now() < until && bearer !== rejectedToken) return bearer;
        if (!pending) {
            // Các tab cùng origin tuần tự đổi refresh cookie để tránh dùng lại token cũ.
            pending = (navigator.locks
                ? navigator.locks.request("qlcd-refresh", refresh)
                : refresh()).finally(() => { pending = undefined; });
        }
        return pending;
    }
    window.authApi = async (path, method = "GET", body) => {
        const send = async accessToken => {
            const headers = { Authorization: "Bearer " + accessToken };
            if (body !== undefined) headers["Content-Type"] = "application/json";
            return fetch("/api/" + path, { method, credentials: "same-origin", headers, body: body === undefined ? undefined : JSON.stringify(body), cache: "no-store" });
        };
        const accessToken = await token();
        let response = await send(accessToken);
        if (response.status === 401) {
            response = await send(await token(accessToken));
            if (response.status === 401) throw expired();
        }
        const result = await parse(response);
        if (path === "auth/logout" || path === "auth/change-password") {
            clearToken();
        }
        return result;
    };
})();
