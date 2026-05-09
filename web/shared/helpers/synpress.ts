// Synpress's `ethereumWalletMockFixtures` defaults its mock account to
// `0xd73b04b0e696b0945283defa3eee453814758f1a`, derived from this private key.
// Specs that seed an SSO identity but don't call `setupBroadcastWallet`
// (which would override the mock account) must use the SAME key here so the
// seeded identity, profile mock, and the address returned by
// `window.ethereum.request({method: 'eth_requestAccounts'})` all match.
// Mismatch = marketplace gets a wallet for one address but reads identity
// for another and never marks itself connected.
//
// Bump only with a deliberate `@synthetixio/ethereum-wallet-mock` version
// upgrade — this is a versioned contract with the package, not a secret.
export const SYNPRESS_DEFAULT_KEY = '0xea084c575a01e2bbefcca3db101eaeab1d8af15554640a510c73692db24d0a6a' as const
