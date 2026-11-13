(() => {
    "use strict";
    for (const form of document.querySelectorAll("form[data-auto-submit]")) {
        const select = form.querySelector("select");
        if (!select) continue;
        form.querySelector("[data-auto-submit-button]").hidden = true;
        select.addEventListener("change", () => form.requestSubmit());
    }
    const dialog = document.getElementById("edit-user-dialog");
    if (dialog) {
        const form = dialog.querySelector("form");
        for (const link of document.querySelectorAll("[data-edit-user]")) link.addEventListener("click", event => {
            event.preventDefault();
            for (const [field, value] of Object.entries({ id: link.dataset.id, RowVersion: link.dataset.version, IsActive: link.dataset.active, FullName: link.dataset.name })) form.elements[field].value = value;
            dialog.showModal();
        });
        dialog.querySelector("[data-close-user-dialog]").onclick = () => dialog.close();
    }
    for (const form of document.querySelectorAll("form[data-admin-confirm]")) form.addEventListener("submit", async event => {
        if (form.dataset.confirmed) return;
        event.preventDefault();
        const confirmed = window.actionDialog ? await window.actionDialog({ title: "Xác nhận thao tác", description: form.dataset.adminConfirm, confirmText: "Xác nhận", danger: true }) : window.confirm(form.dataset.adminConfirm);
        if (confirmed) { form.dataset.confirmed = "true"; form.requestSubmit(); }
    });
    for (const form of document.querySelectorAll("form[data-revoke-assignment]")) {
        if (!window.actionDialog) continue;
        form.querySelector("[data-revoke-reason]").hidden = true;
        form.elements.Reason.required = false;
        form.addEventListener("submit", async event => {
            if (form.dataset.confirmed) return;
            event.preventDefault();
            const reason = await window.actionDialog({ title: "Thu hồi vai trò", input: true, label: "Lý do thu hồi", confirmText: "Thu hồi", danger: true });
            if (typeof reason === "string" && reason.trim()) { form.elements.Reason.value = reason.trim(); form.dataset.confirmed = "true"; form.requestSubmit(); }
        });
    }
})();
