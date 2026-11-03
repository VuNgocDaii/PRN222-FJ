(() => {
    "use strict";
    const $ = id => document.getElementById(id);
    const api = window.authApi;
    const display = window.accountDisplay;
    let currentUserId;
    let ownRoleIds = new Set();
    let allowed = new Set();
    let canAssign = false;
    let editingPermission = null;
    let users = [], roles = [], permissions = [], units = [], page = 1, total = 0, search = "";
    const notice = (message, error = false) => { $("notice").textContent = message; $("notice").className = error ? "error" : ""; $("notice").hidden = false; };
    function bindForm(id, callback) {
        $(id).addEventListener("submit", async e => {
            e.preventDefault(); const button = e.submitter; if (button) button.disabled = true;
            try { await callback(Object.fromEntries(new FormData(e.target)), e.target); }
            catch (error) { notice(error.message, true); }
            finally { if (button) button.disabled = false; }
        });
    }
    function action(text, callback, danger = false) {
        const b = document.createElement("button"); b.type = "button"; b.className = danger ? "danger" : "secondary"; b.textContent = text;
        b.onclick = async () => { b.disabled = true; try { await callback(); } catch (error) { notice(error.message, true); } finally { b.disabled = false; } };
        return b;
    }
    function cell(tr, value) { const td = document.createElement("td"); td.textContent = value ?? "—"; tr.append(td); return td; }
    function table(id, data, render, cols) {
        $(id).replaceChildren();
        if (!data.length) { const tr = document.createElement("tr"); const td = cell(tr, "Chưa có dữ liệu."); td.colSpan = cols; td.className = "empty"; $(id).append(tr); }
        data.forEach(item => { const tr = document.createElement("tr"); render(tr, item); $(id).append(tr); });
    }
    function options(select, data, label, placeholder) {
        const previous = select.value; select.replaceChildren();
        if (placeholder) select.add(new Option(placeholder, ""));
        data.forEach(x => select.add(new Option(label(x), x.id)));
        if (data.some(x => String(x.id) === previous)) select.value = previous;
    }
    const date = display.date;
    function renderPermissionCatalog() {
        table("permission-catalog", permissions, (tr, permission) => {
            cell(tr, permission.code);
            cell(tr, permission.name);
            cell(tr, permission.module);
            cell(tr, permission.scopes.map(display.scope).join(", "));
            cell(tr, permission.roleCount);
            const controls = cell(tr, "");
            if (allowed.has("permissions.update")) {
                controls.append(action("Sửa", () => editPermission(permission)));
            }
            if (permission.roleCount === 0 && allowed.has("permissions.delete")) {
                controls.append(action("Xóa", async () => {
                    if (!await window.actionDialog({ title: "Xóa quyền truy cập?", description: permission.name, confirmText: "Xóa quyền", danger: true })) {
                        return;
                    }
                    await api("permissions/" + permission.id, "DELETE", { version: permission.version });
                    await loadPermissions();
                    notice("Đã xóa quyền.");
                }, true));
            }
        }, 6);
    }
    function editPermission(permission) {
        editingPermission = permission;
        const form = $("permission-editor");
        form.elements.code.value = permission.code;
        form.elements.code.readOnly = true;
        form.elements.name.value = permission.name;
        form.elements.module.value = permission.module;
        form.elements.description.value = permission.description || "";
        form.querySelectorAll('[name="scopes"]').forEach(input => {
            input.checked = permission.scopes.includes(input.value);
            input.disabled = permission.roleCount > 0;
        });
        $("permission-editor-title").textContent = "Sửa quyền";
        $("save-permission").textContent = "Lưu quyền";
        $("save-permission").hidden = false;
        $("cancel-permission-edit").hidden = false;
        $("permission-editor-panel").hidden = false;
        form.elements.name.focus();
    }
    function resetPermissionEditor() {
        editingPermission = null;
        const form = $("permission-editor");
        form.reset();
        form.elements.code.readOnly = false;
        form.querySelectorAll('[name="scopes"]').forEach(input => input.disabled = false);
        $("permission-editor-title").textContent = "Thêm quyền";
        $("save-permission").textContent = "Thêm quyền";
        $("save-permission").hidden = !allowed.has("permissions.create");
        $("cancel-permission-edit").hidden = true;
        $("permission-editor-panel").hidden = !allowed.has("permissions.create");
    }
    async function loadPermissions() {
        permissions = await api("permissions");
        renderPermissionCatalog();
        renderPermissions();
        renderScopes();
    }
    async function loadUsers() {
        const result = await api("users?page=" + page + "&search=" + encodeURIComponent(search)); users = result.items; total = result.total;
        table("user-rows", users, (tr, u) => {
            cell(tr, u.userName); cell(tr, u.fullName); cell(tr, !u.isActive ? "Đã khóa" : u.lockoutEnd && new Date(u.lockoutEnd) > new Date() ? "Tạm khóa đăng nhập" : "Hoạt động"); cell(tr, display.verification(u.verificationStatus));
            const controls = cell(tr, "");
            if (allowed.has("accounts.update")) controls.append(action("Sửa tên", async () => {
                const fullName = await window.actionDialog({ title: "Sửa thông tin tài khoản", description: u.userName, input: true, label: "Họ và tên", value: u.fullName, maxLength: 150, confirmText: "Lưu thay đổi" }); if (fullName === null) return;
                await api("users/" + u.id, "PUT", { fullName, isActive: u.isActive, rowVersion: u.rowVersion }); await loadUsers(); notice("Đã cập nhật tài khoản.");
            }));
            if (u.id !== currentUserId && allowed.has("accounts.lock") && allowed.has("accounts.update")) controls.append(action(u.isActive ? "Khóa" : "Mở khóa", async () => {
                if (!await window.actionDialog({ title: u.isActive ? "Khóa tài khoản?" : "Mở khóa tài khoản?", description: u.fullName + " (" + u.userName + ")", confirmText: u.isActive ? "Khóa tài khoản" : "Mở khóa", danger: u.isActive })) return;
                await api("users/" + u.id, "PUT", { fullName: u.fullName, isActive: !u.isActive, rowVersion: u.rowVersion }); await loadUsers(); notice("Đã cập nhật trạng thái.");
            }, u.isActive));
        }, 5);
        $("page-info").textContent = "Trang " + page + " · " + total + " tài khoản"; $("prev-page").disabled = page <= 1; $("next-page").disabled = page * 30 >= total;
        options($("assignment-user"), users, x => x.userName + " · " + x.fullName);
        if (canAssign) await loadAssignments();
    }
    async function loadRoles() {
        roles = await api("roles"); options($("edit-role"), roles, x => x.displayName);
        options($("assignment-role"), roles.filter(x => x.isActive && x.permissionIds.length), x => x.displayName);
        renderRole(); renderPermissions(); renderScopes();
    }
    function renderRole() {
        const role = roles.find(x => x.id === $("edit-role").value), form = $("role-details");
        if (!role) return;
        form.elements.displayName.value = role.displayName; form.elements.description.value = role.description || ""; form.elements.isActive.checked = role.isActive;
        const protectedRole = role.isSystemRole || ownRoleIds.has(role.id);
        form.querySelectorAll("input,button").forEach(el => el.disabled = protectedRole || !allowed.has("roles.update"));
        form.querySelector("button").hidden = protectedRole || !allowed.has("roles.update");
        $("role-note").textContent = role.isSystemRole ? "Vai trò hệ thống được bảo vệ. Tạo vai trò tùy chỉnh để cấu hình quyền." : ownRoleIds.has(role.id) ? "Bạn không thể sửa vai trò đang áp dụng cho tài khoản của mình." : "Chọn các quyền cần thiết cho vai trò.";
    }
    function renderPermissions() {
        const role = roles.find(x => x.id === $("edit-role").value);
        $("permission-list").replaceChildren();
        if (!role) return;
        const protectedRole = role.isSystemRole || ownRoleIds.has(role.id);
        const canEditPermissions = !protectedRole && allowed.has("roles.permissions.update");
        $("role-permissions").querySelector("button").hidden = !canEditPermissions;
        $("permission-note").textContent = protectedRole ? "Vai trò này chỉ cho phép xem quyền." : "Chọn quyền cho vai trò.";
        permissions.filter(p => canEditPermissions || role.permissionIds.includes(p.id)).forEach(p => {
            const label = document.createElement("label"), check = document.createElement("input"), span = document.createElement("span"), small = document.createElement("small");
            check.type = "checkbox"; check.value = p.id; check.name = "permission"; check.checked = role.permissionIds.includes(p.id); check.disabled = !canEditPermissions;
            span.textContent = p.name; small.textContent = p.scopes.map(display.scope).join(", "); span.append(small); label.append(check, span); $("permission-list").append(label);
        });
    }
    function renderScopes() {
        const role = roles.find(x => x.id === $("assignment-role").value);
        const selected = permissions.filter(p => role?.permissionIds.includes(p.id));
        const scopes = ["SYSTEM", "SELF", "AREA", "GROUP"].filter(s => selected.length && selected.every(p => p.scopes.includes(s)));
        const previous = $("assignment-scope").value;
        $("assignment-scope").replaceChildren(...scopes.map(s => new Option(display.scope(s), s)));
        if (scopes.includes(previous)) $("assignment-scope").value = previous;
        $("assign-role").querySelector("button").disabled = !scopes.length;
        renderUnits();
    }
    function renderUnits() {
        const scope = $("assignment-scope").value;
        options($("assignment-unit"), units.filter(x => x.unitType === scope), x => x.name, "Chọn địa bàn");
        $("assignment-unit").disabled = scope !== "AREA" && scope !== "GROUP";
        $("assignment-unit-label").hidden = $("assignment-unit").disabled;
        $("assignment-unit").required = !$("assignment-unit").disabled;
    }
    async function loadAssignments() {
        const id = $("assignment-user").value;
        $("assign-role").closest(".panel").hidden = !id || id === currentUserId || users.find(u => u.id === id)?.isActive === false;
        if (!id) { table("assignment-rows", [], () => {}, 5); return; }
        const rows = await api("users/" + id + "/role-assignments");
        table("assignment-rows", rows, (tr, x) => {
            cell(tr, x.roleName); const unit = units.find(u => u.id === (x.residentialAreaId || x.residentialGroupId));
            cell(tr, display.scope(x.scopeType) + (unit ? " · " + unit.name : "")); cell(tr, date(x.validFrom) + " → " + date(x.validUntil));
            cell(tr, x.revokedAt ? "Đã thu hồi" : x.validUntil && new Date(x.validUntil + "Z") <= new Date() ? "Hết hạn" : new Date(x.validFrom + "Z") > new Date() ? "Chưa bắt đầu" : "Có hiệu lực");
            const controls = cell(tr, "");
            if (!x.revokedAt) controls.append(action("Thu hồi", async () => {
                const reason = await window.actionDialog({ title: "Thu hồi vai trò", input: true, label: "Lý do thu hồi", confirmText: "Thu hồi", danger: true }); if (!reason?.trim()) return;
                await api("users/" + id + "/role-assignments/" + x.id + "/revoke", "POST", { reason, rowVersion: x.rowVersion }); await loadAssignments(); notice("Đã thu hồi vai trò.");
            }, true));
        }, 5);
    }
    bindForm("search-users", async data => { search = data.search; page = 1; await loadUsers(); });
    bindForm("create-role", async (data, form) => { await api("roles", "POST", data); form.reset(); await loadRoles(); notice("Đã tạo vai trò. Hãy chọn quyền cho vai trò."); });
    bindForm("role-details", async (data, form) => {
        const role = roles.find(x => x.id === $("edit-role").value);
        await api("roles/" + role.id, "PUT", { displayName: data.displayName, description: data.description, isActive: form.elements.isActive.checked, concurrencyStamp: role.concurrencyStamp });
        await loadRoles(); notice("Đã cập nhật vai trò.");
    });
    bindForm("role-permissions", async () => {
        const role = roles.find(x => x.id === $("edit-role").value);
        const permissionIds = [...$("permission-list").querySelectorAll("input:checked")].map(x => Number(x.value));
        await api("roles/" + role.id + "/permissions", "PUT", { permissionIds, concurrencyStamp: role.concurrencyStamp }); await loadRoles(); notice("Đã lưu quyền.");
    });
    bindForm("assign-role", async data => {
        const user = $("assignment-user").value; if (!user) throw new Error("Hãy chọn tài khoản.");
        await api("users/" + user + "/role-assignments", "POST", { roleId: data.roleId, scopeType: data.scopeType, unitId: data.unitId ? Number(data.unitId) : null, validFrom: new Date(data.validFrom).toISOString(), validUntil: data.validUntil ? new Date(data.validUntil).toISOString() : null });
        await loadAssignments(); notice("Đã gán vai trò.");
    });
    $("edit-role").onchange = () => {
        renderRole();
        renderPermissions();
    };
    $("assignment-scope").onchange = renderUnits;
    $("cancel-permission-edit").onclick = resetPermissionEditor;
    bindForm("permission-editor", async data => {
        const scopes = [...$("permission-editor").querySelectorAll('[name="scopes"]:checked')].map(input => input.value);
        if (!scopes.length) {
            throw new Error("Chọn ít nhất một phạm vi áp dụng.");
        }
        const body = {
            name: data.name,
            module: data.module,
            description: data.description,
            scopes
        };
        if (editingPermission) {
            body.version = editingPermission.version;
            await api("permissions/" + editingPermission.id, "PUT", body);
        } else {
            body.code = data.code;
            await api("permissions", "POST", body);
        }
        resetPermissionEditor();
        await loadPermissions();
        notice("Đã lưu Permission.");
    });
    $("assignment-role").onchange = renderScopes;
    $("assignment-user").onchange = () => loadAssignments().catch(e => notice(e.message, true));
    $("prev-page").onclick = () => { page--; loadUsers().catch(e => notice(e.message, true)); };
    $("next-page").onclick = () => { page++; loadUsers().catch(e => notice(e.message, true)); };
    document.querySelectorAll("[data-tab]").forEach(button => button.onclick = async () => {
        document.querySelectorAll(".tab-panel").forEach(x => x.hidden = x.id !== button.dataset.tab);
        document.querySelectorAll("[data-tab]").forEach(x => x.classList.toggle("active", x === button));
        $("page-title").textContent = button.textContent; $("notice").hidden = true;
        const url = new URL(window.location.href);
        url.searchParams.set("tab", button.dataset.tab);
        window.history.replaceState(null, "", url);
    });
    const now = new Date(); now.setMinutes(now.getMinutes() - now.getTimezoneOffset()); $("assign-role").elements.validFrom.value = now.toISOString().slice(0, 16);
    (async () => {
        try {
            const effective = await api("me/permissions");
            currentUserId = effective[0]?.userId;
            ownRoleIds = new Set(effective.map(x => x.roleId));
            allowed = new Set(effective.filter(x => x.scopeType === "SYSTEM").map(x => x.permissionCode));
            const canRoles = allowed.has("roles.read");
            const canPermissions = allowed.has("permissions.read");
            canAssign = canRoles && canPermissions && allowed.has("roles.assign") && allowed.has("accounts.read");
            document.querySelector('[data-tab="users"]').hidden = !allowed.has("accounts.read");
            document.querySelector('[data-tab="roles"]').hidden = !canRoles;
            document.querySelector('[data-tab="permissions"]').hidden = !canPermissions;
            $("permission-assignment-panel").hidden = !canPermissions;
            $("assignments").hidden = !canAssign;
            $("create-role-panel").hidden = !allowed.has("roles.create");
            if (canPermissions) {
                permissions = await api("permissions");
                renderPermissionCatalog();
            }
            resetPermissionEditor();
            if (canRoles) await loadRoles();
            if (canAssign) { units = await api("role-assignment-units"); renderUnits(); }
            if (allowed.has("accounts.read")) await loadUsers();
            const requested = new URLSearchParams(window.location.search).get("tab");
            const tabs = [...document.querySelectorAll("[data-tab]")].filter(x => !x.hidden);
            const first = tabs.find(x => x.dataset.tab === requested) || tabs[0];
            if (first) first.click();
        } catch (error) { notice(error.message, true); }
    })();
})();
