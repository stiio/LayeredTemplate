import { mgr, authServer } from './oidc-config.js';

// --- Helpers ---

function decodeJwt(token) {
    try {
        const parts = token.split('.');
        if (parts.length !== 3) return null;
        return JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
    } catch {
        return null;
    }
}

function renderClaims(tbodyId, claims) {
    const tbody = document.getElementById(tbodyId);
    tbody.innerHTML = '';
    if (!claims) {
        tbody.innerHTML = '<tr><td colspan="2">Unable to decode token</td></tr>';
        return;
    }
    for (const [key, value] of Object.entries(claims)) {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td>${key}</td><td>${typeof value === 'object' ? JSON.stringify(value) : value}</td>`;
        tbody.appendChild(tr);
    }
}

function showStatus(message, isError = false) {
    const area = document.getElementById('status-area');
    area.innerHTML = `<div class="status ${isError ? 'status-err' : 'status-ok'}">${message}</div>`;
    setTimeout(() => { area.innerHTML = ''; }, 5000);
}

function showUser(user) {
    document.getElementById('guest').style.display = 'none';
    document.getElementById('dashboard').style.display = 'block';

    document.getElementById('access-token').value = user.access_token || '';
    document.getElementById('id-token').value = user.id_token || '';
    document.getElementById('refresh-token').value = user.refresh_token || '(none)';

    renderClaims('id-claims', user.profile);
    renderClaims('at-claims', decodeJwt(user.access_token));
}

function showGuest() {
    document.getElementById('guest').style.display = 'block';
    document.getElementById('dashboard').style.display = 'none';
}

// --- Init ---

mgr.getUser().then(user => {
    if (user && !user.expired) {
        showUser(user);
    } else {
        showGuest();
    }
});

// --- Login ---

document.getElementById('login').addEventListener('click', () => {
    mgr.signinRedirect();
});

// --- Refresh Token ---

document.getElementById('refresh').addEventListener('click', async () => {
    try {
        const user = await mgr.signinSilent();
        showUser(user);
        showStatus('Tokens refreshed successfully.');
    } catch (e) {
        showStatus('Refresh failed: ' + e.message, true);
    }
});

// --- Call UserInfo ---

document.getElementById('userinfo').addEventListener('click', async () => {
    const user = await mgr.getUser();
    if (!user) { showStatus('Not logged in.', true); return; }

    try {
        const res = await fetch(authServer + '/connect/userinfo', {
            headers: { 'Authorization': 'Bearer ' + user.access_token }
        });
        const data = await res.json();
        document.getElementById('userinfo-result').textContent = JSON.stringify(data, null, 2);

        if (!res.ok) {
            showStatus(`UserInfo returned ${res.status}`, true);
        }
    } catch (e) {
        showStatus('Fetch error: ' + e.message, true);
    }
});

// --- Logout ---

document.getElementById('logout').addEventListener('click', () => {
    mgr.signoutRedirect();
});
