(() => {
    "use strict";
    const dialog = document.getElementById("action-dialog");
    const form = document.getElementById("action-dialog-form");
    const input = document.getElementById("action-dialog-input");
    const cancel = document.getElementById("action-dialog-cancel");
    let complete;
    let result = null;
    window.actionDialog = (options) => new Promise(resolve => {
        if (dialog.open) { resolve(null); return; }
        complete = resolve;
        result = null;
        form.reset();
        document.getElementById("action-dialog-title").textContent = options.title;
        document.getElementById("action-dialog-description").textContent = options.description || "";
        document.getElementById("action-dialog-field").hidden = !options.input;
        document.getElementById("action-dialog-label").textContent = options.label || options.title;
        input.required = !!options.input;
        input.value = options.value || "";
        input.maxLength = options.maxLength || 1000;
        const submit = document.getElementById("action-dialog-submit");
        submit.textContent = options.confirmText || "Xác nhận";
        submit.className = options.danger ? "danger" : "primary";
        dialog.showModal();
        // Hủy được chọn trước để tránh vô tình xác nhận bằng bàn phím.
        (options.input ? input : cancel).focus();
    });
    input.addEventListener("input", () => input.setCustomValidity(""));
    form.addEventListener("submit", event => {
        event.preventDefault();
        if (input.required && !input.value.trim()) {
            input.setCustomValidity("Vui lòng nhập nội dung.");
            input.reportValidity();
            return;
        }
        result = input.required ? input.value.trim() : true;
        dialog.close();
    });
    cancel.onclick = () => dialog.close();
    dialog.addEventListener("close", () => {
        input.setCustomValidity("");
        const resolve = complete;
        complete = null;
        resolve?.(result);
    });
})();
