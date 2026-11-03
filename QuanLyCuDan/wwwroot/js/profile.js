(() => {
    const notice = error => { const n = document.getElementById("notice"); n.textContent = error.message; n.className = "error"; n.hidden = false; };
    document.getElementById("password").onsubmit = async e => {
        e.preventDefault(); e.submitter.disabled = true;
        try { await window.authApi("auth/change-password", "POST", Object.fromEntries(new FormData(e.target))); location.assign("/Account/Login"); } catch (error) { notice(error); } finally { e.submitter.disabled = false; }
    };
})();
