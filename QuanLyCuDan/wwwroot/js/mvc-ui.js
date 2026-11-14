(() => {
    "use strict";
    const assignment = document.querySelector("[data-role-assignment]");
    if (assignment) {
        const role = assignment.elements.RoleId, scope = assignment.elements.ScopeType, unit = assignment.elements.UnitId;
        const help = assignment.querySelector("[data-assignment-help]"), submit = assignment.querySelector("[type=submit]");
        function updateUnits() {
            const needsUnit = scope.value === "AREA" || scope.value === "GROUP";
            unit.disabled = !needsUnit; unit.required = needsUnit;
            for (const option of unit.options) {
                option.hidden = !!option.value && option.dataset.type !== scope.value;
                option.disabled = option.hidden;
            }
            if (!needsUnit || unit.selectedOptions[0]?.disabled) unit.value = "";
            const choices = [...unit.options].filter(o => o.value && !o.disabled);
            if (needsUnit && !unit.value && choices.length === 1) unit.value = choices[0].value;
        }
        function updateRole() {
            const allowed = role.selectedOptions[0]?.dataset.scopes?.split(",").filter(Boolean) || [];
            for (const option of scope.options) { option.hidden = !allowed.includes(option.value); option.disabled = option.hidden; }
            if (!allowed.includes(scope.value)) scope.value = allowed[0] || "";
            submit.disabled = !allowed.length;
            help.textContent = allowed.length ? "Chỉ hiển thị phạm vi tương thích với quyền của vai trò đã chọn." : "Vai trò này chưa có quyền tương thích. Hãy cấu hình quyền trước khi gán.";
            help.classList.toggle("error", !allowed.length);
            updateUnits();
        }
        role.addEventListener("change", updateRole); scope.addEventListener("change", updateUnits); updateRole();
        const from = assignment.elements.ValidFrom, until = assignment.elements.ValidUntil;
        function checkDates() {
            until.min = from.value;
            until.setCustomValidity(until.value && until.value <= from.value ? "Thời hạn kết thúc phải sau thời điểm bắt đầu." : "");
        }
        from.addEventListener("input", checkDates); until.addEventListener("input", checkDates);
    }
    for (const form of document.querySelectorAll("[data-password-form]")) {
        const password = form.elements.NewPassword || form.elements.newPassword, confirm = form.elements.confirmPassword;
        const check = () => confirm.setCustomValidity(password.value === confirm.value ? "" : "Mật khẩu xác nhận không khớp.");
        password.addEventListener("input", check); confirm.addEventListener("input", check);
    }
    for (const form of document.querySelectorAll("form[data-confirm]")) {
        if (form.getAttribute("action")?.startsWith("/Permissions/Delete"))
            form.addEventListener("submit", event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); });
    }
})();
