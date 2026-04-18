import { mgr } from './oidc-config.js';

const params = new URLSearchParams(location.search);
const inviteId = params.get('inviteId');

async function main() {
    const user = await mgr.getUser();

    if (user && !user.expired) {
        // Already signed in — accept_invite set the Identity-cookie at Auth.Web, OIDC callback
        // returned tokens, and now we're back here with inviteId in the URL.
        showComplete(user);
        return;
    }

    // Not signed in yet. Trigger OIDC authorize — Auth.Web has the Identity-cookie from
    // accept_invite, so it will silently issue the auth code. Pass inviteId as custom `state`
    // so it survives the redirect roundtrip; callback.html reads it back.
    await mgr.signinRedirect({ state: { inviteId } });
}

function showComplete(user) {
    document.getElementById('loading').style.display = 'none';
    const block = document.getElementById('complete');
    block.style.display = 'block';

    const name = user.profile?.name || user.profile?.email || user.profile?.sub || 'user';
    document.getElementById('user-info').textContent = `Signed in as ${name}.`;
    document.getElementById('invite-id-display').textContent = inviteId || '(none)';
}

main().catch(e => {
    document.getElementById('loading').textContent = 'Error: ' + e.message;
    console.error(e);
});
