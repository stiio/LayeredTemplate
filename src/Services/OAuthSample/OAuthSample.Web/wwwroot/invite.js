import { mgr } from './oidc-config.js';

const guestBlock = document.getElementById('guest');
const formBlock = document.getElementById('form-block');
const signInBtn = document.getElementById('sign-in');
const signedInAs = document.getElementById('signed-in-as');
const emailInput = document.getElementById('email');
const sendBtn = document.getElementById('send');
const resultBlock = document.getElementById('result');
const linkField = document.getElementById('link');
const copyBtn = document.getElementById('copy');
const statusArea = document.getElementById('status-area');

function showStatus(message, isError = false) {
    statusArea.innerHTML = `<div class="status ${isError ? 'status-err' : 'status-ok'}">${message}</div>`;
    if (!isError) {
        setTimeout(() => { statusArea.innerHTML = ''; }, 3000);
    }
}

async function init() {
    const user = await mgr.getUser();
    if (!user || user.expired) {
        guestBlock.style.display = 'block';
        formBlock.style.display = 'none';
        return;
    }

    guestBlock.style.display = 'none';
    formBlock.style.display = 'block';
    const name = user.profile?.name || user.profile?.email || user.profile?.sub || 'user';
    signedInAs.textContent = `Signed in as ${name}.`;
}

signInBtn.addEventListener('click', () => {
    // Preserve where we came from so callback.html can bring us back here after the OIDC roundtrip.
    mgr.signinRedirect({ state: { returnTo: '/invite.html' } });
});

sendBtn.addEventListener('click', async () => {
    const email = emailInput.value.trim();
    if (!email) {
        showStatus('Please enter an email.', true);
        return;
    }

    // Re-read user — token in sessionStorage might have expired between page load and submit.
    const user = await mgr.getUser();
    if (!user || user.expired || !user.access_token) {
        showStatus('Your session expired. <a href="/invite.html">Reload</a>.', true);
        return;
    }

    sendBtn.disabled = true;
    sendBtn.textContent = 'Creating...';

    try {
        const res = await fetch('/api/users/invite', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + user.access_token,
            },
            body: JSON.stringify({ email }),
        });

        if (res.status === 401) {
            showStatus('Unauthorized — your access_token is invalid or expired. <a href="/invite.html">Reload</a>.', true);
            return;
        }
        if (!res.ok) {
            const body = await res.text();
            showStatus(`Error ${res.status}: ${body || res.statusText}`, true);
            return;
        }

        const data = await res.json();
        linkField.value = data.inviteUrl;
        resultBlock.style.display = 'block';
        const inviteIdSpan = document.getElementById('invite-id-value');
        if (inviteIdSpan) inviteIdSpan.textContent = data.inviteId;
        showStatus(`Invite created. Expires ${new Date(data.expiresAt).toLocaleString()}.`);
    } catch (e) {
        showStatus('Request failed: ' + e.message, true);
    } finally {
        sendBtn.disabled = false;
        sendBtn.textContent = 'Create invite';
    }
});

copyBtn.addEventListener('click', async () => {
    try {
        await navigator.clipboard.writeText(linkField.value);
        const original = copyBtn.textContent;
        copyBtn.textContent = 'Copied!';
        setTimeout(() => { copyBtn.textContent = original; }, 1500);
    } catch {
        linkField.select();
        document.execCommand('copy');
    }
});

init().catch(e => console.error(e));
