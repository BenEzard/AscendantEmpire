import { PublicClientApplication, type AccountInfo } from "@azure/msal-browser";

const clientId = import.meta.env.VITE_MSAL_CLIENT_ID as string | undefined;
const apiScope = import.meta.env.VITE_MSAL_API_SCOPE as string | undefined;

// Trap: Must match the redirect Uri exactly
const redirectUri = `${window.location.origin}/auth-redirect/index.html`;

let clientPromise: Promise<PublicClientApplication> | null = null;

export function isMicrosoftSignInConfigured(): boolean {
    return Boolean(clientId  && apiScope);
}

function getClient(): Promise<PublicClientApplication> {
    if (!clientPromise) {
        const client = new PublicClientApplication({
            auth: { clientId: clientId!, authority: "https://login.microsoftonline.com/common", redirectUri },
            cache: { cacheLocation: "sessionStorage" },
        });
        
        // Trap: MSAL rejects every call until the client is initialized
        clientPromise = client.initialize().then(() => client);
    }
    return clientPromise;
}

export async function acquireMicrosoftAccessToken(): Promise<string> {
    const client = await getClient();
    const scopes = [apiScope!];
    const account: AccountInfo | null = client.getActiveAccount();

    if (account) {
        try {
            return (await client.acquireTokenSilent({ scopes, account })).accessToken;
        } catch {
            // Fall through to the popup: the cached session expired or needs interaction.
        }
    }

    // Trap: Login popup must be given the api scope; without it, the token returned is for Microsoft Graph and not our API.
    const result = await client.loginPopup({ scopes, prompt: "select_account", redirectUri });

    client.setActiveAccount(result.account);
    return result.accessToken;
}
