import { UserManager } from 'https://cdn.jsdelivr.net/npm/oidc-client-ts@3/+esm';

const authServer = 'https://localhost:8080';
const clientOrigin = window.location.origin;

export const config = {
    authority: authServer,
    client_id: 'default_client',
    redirect_uri: clientOrigin + '/callback.html',
    post_logout_redirect_uri: clientOrigin + '/index.html',
    response_type: 'code',
    // openid           — triggers id_token issuance (required)
    // profile / email  — gate name / given_name / family_name / email / email_verified in both tokens
    // roles            — gate role claim (custom scope; backend reads it from access_token)
    // offline_access   — requests a refresh_token
    scope: 'profile email phone roles offline_access openid',
};

export const mgr = new UserManager(config);
export { authServer };
