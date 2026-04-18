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

sendBtn.addEventListener('click', async () => {
    const email = emailInput.value.trim();
    if (!email) {
        showStatus('Please enter an email.', true);
        return;
    }

    sendBtn.disabled = true;
    sendBtn.textContent = 'Creating...';

    try {
        const res = await fetch('/api/users/invite', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email }),
        });

        if (!res.ok) {
            const body = await res.text();
            showStatus(`Error ${res.status}: ${body || res.statusText}`, true);
            return;
        }

        const data = await res.json();
        linkField.value = data.inviteUrl;
        resultBlock.style.display = 'block';
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
