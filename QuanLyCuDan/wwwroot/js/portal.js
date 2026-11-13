(() => {
    const menuToggle = document.querySelector('.mobile-menu-toggle');
    if (menuToggle) menuToggle.addEventListener('click', () => {
        const expanded = menuToggle.getAttribute('aria-expanded') !== 'true';
        menuToggle.setAttribute('aria-expanded', String(expanded));
        document.getElementById('portal-main-menu')?.classList.toggle('is-open', expanded);
    });
    const form = document.getElementById('resident-request-form');
    const type = document.getElementById('request-type');
    if (form && type) {
        const update = () => {
            for (const [field, show] of [['target', ['MOVE_IN', 'ADD_MEMBER'].includes(type.value)], ['head', type.value === 'CHANGE_HEAD']]) {
                const label = form.querySelector(`[data-request-field="${field}"]`);
                if (!label) continue;
                label.hidden = !show;
                const input = label.querySelector('input,select');
                input.disabled = !show;
                input.required = show;
            }
        };
        type.addEventListener('change', update);
        update();
    }
    document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', e => {
        if (!window.confirm(form.dataset.confirm)) e.preventDefault();
    }));
    document.querySelectorAll('form[method="post"]').forEach(form => form.addEventListener('submit', e => {
        if (!e.defaultPrevented && form.checkValidity()) {
            const button = form.querySelector('button[type="submit"]');
            if (button) { button.disabled = true; button.textContent = 'Đang xử lý…'; }
        }
    }));
})();
