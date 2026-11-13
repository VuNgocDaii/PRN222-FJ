(() => {
    "use strict";
    const normalize = value => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").toLowerCase();
    const rows = [...document.querySelectorAll(".registration-row")];
    document.getElementById("registration-search").addEventListener("input", event => {
        const query = normalize(event.target.value.trim());
        rows.forEach(row => row.hidden = !normalize([row.dataset.name, row.dataset.username, row.dataset.phone].join(" ")).includes(query));
        document.getElementById("registration-empty").hidden = !rows.length || rows.some(row => !row.hidden);
    });
    document.querySelectorAll("dialog.registration-dialog").forEach(dialog => {
        let submitting = false;
        const forms = [...dialog.querySelectorAll(".review-form")];
        const update = () => forms.forEach(form => {
            const approving = form.elements.Approve.value === "true";
            form.querySelector('[type="submit"]').disabled = submitting || approving && (!form.elements.ResidentId?.value || !form.elements.Verified?.checked);
        });
        dialog.querySelectorAll("[data-review-close]").forEach(button => button.onclick = () => dialog.close());
        dialog.querySelector("[data-review-reject]")?.addEventListener("click", () => dialog.querySelector(".rejection-form").requestSubmit());
        forms.forEach(form => {
            form.elements.Verified?.addEventListener("change", update);
            form.addEventListener("submit", event => {
                const approving = form.elements.Approve.value === "true";
                if (submitting || approving && (!form.elements.ResidentId?.value || !form.elements.Verified?.checked)) {
                    event.preventDefault();
                    return;
                }
                submitting = true;
                dialog.querySelectorAll("button").forEach(button => button.disabled = true);
                form.querySelector('[type="submit"]').textContent = "Đang xử lý…";
            });
        });
        dialog.addEventListener("cancel", event => { if (submitting) event.preventDefault(); });
        dialog.addEventListener("close", () => {
            forms.forEach(form => form.reset());
            update();
        });
        update();
    });
    document.querySelectorAll("[data-review-open]").forEach(button => button.onclick = () => {
        document.getElementById(button.dataset.reviewOpen).showModal();
    });
})();