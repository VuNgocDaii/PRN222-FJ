(() => {
    const title = document.getElementById('Input_Title');
    const body = document.getElementById('Input_Body');
    const update = () => {
        document.getElementById('announcement-preview-title').textContent = title.value || 'Tiêu đề thông báo';
        document.getElementById('announcement-preview-body').textContent = body.value || 'Nội dung thông báo';
    };
    if (title && body) { title.addEventListener('input', update); body.addEventListener('input', update); update(); }
    document.querySelectorAll('form[method="post"]').forEach(form => form.addEventListener('submit', event => {
        if (form.dataset.confirm && !window.confirm(form.dataset.confirm)) { event.preventDefault(); return; }
        if (!event.defaultPrevented && form.checkValidity()) {
            const button = event.submitter;
            if (button) { button.disabled = true; button.textContent = 'Đang xử lý…'; }
        }
    }));
})();
