(() => {
    "use strict";
    const dialog = document.getElementById("password-dialog");
    const form = document.getElementById("change-password-form");
    const notice = document.getElementById("password-notice");
    let submitting = false;
    document.getElementById("open-password-dialog").onclick = () => dialog.showModal();
    document.getElementById("close-password-dialog").onclick = () => dialog.close();
    dialog.addEventListener("close", () => {
        form.reset();
        notice.hidden = true;
        form.elements.confirmPassword.setCustomValidity("");
    });
    form.elements.confirmPassword.addEventListener("input", () => form.elements.confirmPassword.setCustomValidity(""));
    form.elements.newPassword.addEventListener("input", () => form.elements.confirmPassword.setCustomValidity(""));
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (submitting) return;
        const { currentPassword, newPassword, confirmPassword } = form.elements;
        if (newPassword.value !== confirmPassword.value) {
            confirmPassword.setCustomValidity("Mật khẩu xác nhận không khớp.");
            confirmPassword.reportValidity();
            return;
        }
        submitting = true;
        const button = form.querySelector('[type="submit"]');
        button.disabled = true;
        notice.hidden = true;
        try {
            await window.authApi("auth/change-password", "POST", {
                currentPassword: currentPassword.value,
                newPassword: newPassword.value
            });
            location.assign("/Account/Login");
        } catch (error) {
            notice.textContent = error.message;
            notice.hidden = false;
        } finally {
            submitting = false;
            button.disabled = false;
        }
    });
})();
