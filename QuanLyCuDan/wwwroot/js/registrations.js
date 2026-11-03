(() => {
    "use strict";
    const normalize = value => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
    const cards = [...document.querySelectorAll(".registration-row")];
    document.getElementById("registration-search").addEventListener("input", event => {
        const query = normalize(event.target.value.trim());
        cards.forEach(card => card.hidden = !normalize([card.dataset.name, card.dataset.username, card.dataset.phone].join(" ")).includes(query));
        document.getElementById("registration-empty").hidden = !cards.length || cards.some(card => !card.hidden);
    });
    document.querySelectorAll("[data-review-open]").forEach(button => button.onclick = () => {
        const dialog = document.getElementById(button.dataset.reviewOpen);
        dialog.showModal();
        dialog.querySelector('input[type="search"], textarea')?.focus();
    });
    document.querySelectorAll("[data-review-close]").forEach(button => button.onclick = () => button.closest("dialog").close());
    document.querySelectorAll("[data-review-reject]").forEach(button => button.onclick = () => {
        button.closest("dialog").querySelector(".rejection-form").requestSubmit();
    });
    document.querySelectorAll(".review-form").forEach(form => {
        const dialog = form.closest("dialog");
        const select = form.elements.ResidentId;
        const verified = form.elements.Verified;
        const reason = form.elements.Reason;
        const submit = form.querySelector('[type="submit"]');
        const search = form.querySelector(".resident-search");
        const options = select ? [...select.options].slice(1).map(option => ({ value: option.value, text: option.textContent })) : [];
        let submitting = false;
        function update() { submit.disabled = submitting || !!select && (!select.value || !verified.checked); }
        function filter() {
            const previous = select.value;
            const matches = options.filter(option => normalize(option.text).includes(normalize(search.value.trim())));
            select.replaceChildren(new Option(matches.length ? "Chọn hồ sơ để xác minh" : "Không tìm thấy hồ sơ phù hợp", ""));
            matches.forEach(option => select.add(new Option(option.text, option.value)));
            select.value = matches.some(option => option.value === previous) ? previous : "";
            verified.checked = false;
            update();
        }
        search?.addEventListener("input", filter);
        select?.addEventListener("change", () => { verified.checked = false; update(); });
        verified?.addEventListener("change", update);
        reason?.addEventListener("input", () => reason.setCustomValidity(""));
        dialog.addEventListener("cancel", event => { if (submitting) event.preventDefault(); });
        dialog.addEventListener("close", () => {
            form.reset();
            dialog.querySelectorAll("details").forEach(details => details.open = false);
            reason?.setCustomValidity("");
            if (select) filter();
            update();
        });
        form.addEventListener("submit", event => {
            if (submitting || select && (!select.value || !verified.checked)) { event.preventDefault(); return; }
            if (reason && !reason.value.trim()) {
                event.preventDefault();
                reason.setCustomValidity("Nhập lý do từ chối đăng ký.");
                reason.reportValidity();
                return;
            }
            submitting = true;
            submit.disabled = true;
            dialog.querySelectorAll("button").forEach(button => button.disabled = true);
            submit.textContent = "Đang xử lý…";
        });
        update();
    });
})();
