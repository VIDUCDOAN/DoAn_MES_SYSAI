'use strict';

(() => {
    const list = document.getElementById('product-list');
    const body = document.getElementById('products-body');
    const form = document.getElementById('product-form');
    const editor = document.getElementById('product-editor');
    const idInput = document.getElementById('product-id');
    const versionInput = document.getElementById('product-version');
    const save = document.getElementById('save-product');
    let items = [];
    let busy = false;

    function reset() {
        if (!form) return;
        form.reset();
        idInput.value = '0';
        versionInput.value = '0';
        document.getElementById('editor-title').textContent = editor.dataset.canAdd === 'true' ? 'Thêm sản phẩm' : 'Chọn sản phẩm để sửa';
        save.disabled = editor.dataset.canAdd !== 'true';
    }

    function actionButton(label, action, product) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = action === 'delete' ? 'button button-danger' : 'button button-quiet';
        button.textContent = label;
        button.dataset.action = action;
        button.dataset.id = product.id;
        return button;
    }

    async function load() {
        items = await miniMes.request('/api/products');
        body.replaceChildren();
        document.getElementById('product-count').textContent = `${items.length} sản phẩm`;
        if (!items.length) {
            const row = body.insertRow();
            const cell = miniMes.cell(row, 'Chưa có sản phẩm. Hãy thêm sản phẩm đầu tiên.');
            cell.colSpan = 4;
        }
        for (const product of items) {
            const row = body.insertRow();
            miniMes.cell(row, product.modelCode);
            miniMes.cell(row, product.modelName);
            miniMes.cell(row, product.description);
            const actions = miniMes.cell(row, '');
            actions.className = 'row-actions';
            if (list.dataset.canEdit === 'true') actions.append(actionButton('Sửa', 'edit', product));
            if (list.dataset.canDelete === 'true') actions.append(actionButton('Ngừng sử dụng', 'delete', product));
        }
    }

    form?.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy) return;
        document.getElementById('model-name').value = document.getElementById('model-name').value.trim();
        if (!form.reportValidity()) return;
        const id = Number(idInput.value);
        const payload = {
            modelCode: document.getElementById('model-code').value.trim(),
            modelName: document.getElementById('model-name').value,
            description: document.getElementById('description').value,
            ...(id ? { version: Number(versionInput.value) } : {})
        };
        busy = true;
        save.disabled = true;
        try {
            await miniMes.request(id ? `/api/products/${id}` : '/api/products', {
                method: id ? 'PUT' : 'POST', body: JSON.stringify(payload)
            });
            reset();
            await load();
            miniMes.message(id ? 'Đã cập nhật sản phẩm.' : 'Đã thêm sản phẩm.');
        } catch (error) { miniMes.message(error.message, true); }
        finally {
            busy = false;
            save.disabled = Number(idInput.value) === 0 && editor.dataset.canAdd !== 'true';
        }
    });

    body.addEventListener('click', async event => {
        const button = event.target.closest('button[data-action]');
        if (!button || busy) return;
        const product = items.find(p => p.id === Number(button.dataset.id));
        if (!product) return;
        if (button.dataset.action === 'edit' && form) {
            idInput.value = product.id;
            versionInput.value = product.version;
            document.getElementById('model-code').value = product.modelCode;
            document.getElementById('model-name').value = product.modelName;
            document.getElementById('description').value = product.description ?? '';
            document.getElementById('editor-title').textContent = `Sửa sản phẩm ${product.modelCode}`;
            save.disabled = false;
            document.getElementById('model-name').focus();
            return;
        }
        if (button.dataset.action === 'delete' && window.confirm(`Ngừng sử dụng sản phẩm ${product.modelCode}? Lịch sử vẫn được lưu.`)) {
            busy = true;
            button.disabled = true;
            try {
                await miniMes.request(`/api/products/${product.id}`, { method: 'DELETE', body: JSON.stringify({ version: product.version }) });
                reset();
                await load();
                miniMes.message('Đã ngừng sử dụng sản phẩm.');
            } catch (error) { miniMes.message(error.message, true); }
            finally { busy = false; button.disabled = false; }
        }
    });

    document.getElementById('cancel-edit')?.addEventListener('click', () => { if (!busy) reset(); });
    document.getElementById('reload-products').addEventListener('click', () => {
        if (busy) return;
        reset();
        load().catch(error => miniMes.message(error.message, true));
    });
    load().catch(error => miniMes.message(error.message, true));
})();
