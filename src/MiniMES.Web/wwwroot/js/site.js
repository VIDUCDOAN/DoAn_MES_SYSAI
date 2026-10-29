'use strict';

window.miniMes = {
    async request(url, options = {}) {
        const headers = { Accept: 'application/json', ...options.headers };
        if (options.body !== undefined) headers['Content-Type'] = 'application/json';
        if (options.method && options.method !== 'GET') {
            headers['X-CSRF-TOKEN'] = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
        }
        const response = await fetch(url, { ...options, headers, credentials: 'same-origin' });
        if (response.status === 401) {
            window.location.assign('/Account/Login');
            throw new Error('Phiên đăng nhập đã hết hạn.');
        }
        if (!response.ok) {
            const problem = await response.json().catch(() => null);
            const errors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : null;
            const fallback = response.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.' : 'Không thể xử lý yêu cầu. Vui lòng thử lại.';
            throw new Error(errors || problem?.title || fallback);
        }
        return response.status === 204 ? null : response.json();
    },
    message(text, isError = false) {
        const target = document.getElementById('page-message');
        target.textContent = text;
        target.className = isError ? 'message error' : 'message success';
        target.hidden = false;
    },
    cell(row, value) {
        const cell = document.createElement('td');
        cell.textContent = value ?? '—';
        row.append(cell);
        return cell;
    }
};
