(() => {
    const dialog = document.getElementById("password-dialog"), form = document.getElementById("change-password-form");
    if (!dialog || !form) return;
    document.getElementById("open-password-dialog").onclick = () => dialog.showModal();
    document.getElementById("close-password-dialog").onclick = () => dialog.close();
    const check = () => form.elements.confirmPassword.setCustomValidity(form.elements.newPassword.value === form.elements.confirmPassword.value ? "" : "Mật khẩu xác nhận không khớp.");
    form.elements.newPassword.addEventListener("input", check);
    form.elements.confirmPassword.addEventListener("input", check);
    dialog.addEventListener("close", () => { form.reset(); check(); });
})();
