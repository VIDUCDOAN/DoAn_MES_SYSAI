'use strict';

(() => {
    async function load() {
        const entries = await miniMes.request('/api/audit-logs');
        const body = document.getElementById('audits-body');
        body.replaceChildren();
        if (!entries.length) miniMes.cell(body.insertRow(), 'Chưa có nhật ký thay đổi.').colSpan = 6;
        for (const entry of entries) {
            const row = body.insertRow();
            const utc = /Z$|[+-]\d\d:\d\d$/.test(entry.when) ? entry.when : `${entry.when}Z`;
            miniMes.cell(row, new Date(utc).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }));
            miniMes.cell(row, entry.who);
            miniMes.cell(row, entry.whereIp);
            miniMes.cell(row, entry.actionType);
            miniMes.cell(row, `${entry.entityName} #${entry.entityId}`);
            const details = document.createElement('details');
            const summary = document.createElement('summary');
            summary.textContent = 'Xem chi tiết';
            const pre = document.createElement('pre');
            pre.textContent = JSON.stringify({
                before: entry.oldDataJson ? JSON.parse(entry.oldDataJson) : null,
                after: entry.newDataJson ? JSON.parse(entry.newDataJson) : null,
                correlationId: entry.correlationId
            }, null, 2);
            details.append(summary, pre);
            miniMes.cell(row, '').append(details);
        }
    }
    document.getElementById('reload-audits').addEventListener('click', () => load().catch(error => miniMes.message(error.message, true)));
    load().catch(error => miniMes.message(error.message, true));
})();
