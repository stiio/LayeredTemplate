import { UserManager } from 'https://cdn.jsdelivr.net/npm/oidc-client-ts@3/+esm';

const authServer = 'https://localhost:8080';
const clientOrigin = window.location.origin;

export const config = {
    authority: authServer,
    client_id: 'default_client',
    redirect_uri: clientOrigin + '/callback.html',
    post_logout_redirect_uri: clientOrigin + '/index.html',
    response_type: 'code',
    scope: 'openid profile email offline_access',
};

export const mgr = new UserManager(config);
export { authServer };
