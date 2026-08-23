#!/usr/bin/env bash
set -euo pipefail

: "${FIXTURE_BASE_URL:?FIXTURE_BASE_URL must point to the running fixture edge}"

fixture_url="${FIXTURE_BASE_URL%/}"
fixture_domain="${FIXTURE_BASE_DOMAIN:-${fixture_url#*://}}"
fixture_domain="${fixture_domain%%/*}"

curl_tls_args=()
if [[ -n "${FIXTURE_CA_FILE:-}" ]]; then
  curl_tls_args+=(--cacert "${FIXTURE_CA_FILE}")
elif command -v mkcert >/dev/null 2>&1 && [[ -s "$(mkcert -CAROOT)/rootCA.pem" ]]; then
  curl_tls_args+=(--cacert "$(mkcert -CAROOT)/rootCA.pem")
fi

# macOS runners use Bash 3, where expanding an empty array with `set -u`
# raises an "unbound variable" error. Keep the optional TLS arguments in one
# helper so the public-HTTPS path works on both Bash 3 and newer Bash.
curl_fixture() {
  if ((${#curl_tls_args[@]})); then
    curl "${curl_tls_args[@]}" "$@"
  else
    curl "$@"
  fi
}

if [[ -z "${fixture_domain}" ]]; then
  echo "FIXTURE_BASE_DOMAIN could not be derived from ${fixture_url}" >&2
  exit 1
fi

if [[ "${fixture_url}" != https://* ]]; then
  echo "FIXTURE_BASE_URL must use HTTPS because the Unity realm URL is fetched directly" >&2
  exit 1
fi

about_json="$(curl_fixture --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/about")"

expected_content_url="${fixture_url}/content"
expected_lambdas_url="${fixture_url}/lambdas"
printf '%s\n' "${about_json}" | jq -e \
  --arg expected_content_url "${expected_content_url}" \
  --arg expected_lambdas_url "${expected_lambdas_url}" \
  '.healthy == true
   and .content.healthy == true
   and .lambdas.healthy == true
   and (.content.publicUrl | rtrimstr("/")) == $expected_content_url
   and (.lambdas.publicUrl | rtrimstr("/")) == $expected_lambdas_url' >/dev/null

curl_fixture --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/content/status" >/dev/null
curl_fixture --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/lambdas/status" >/dev/null

fixture_uri_scheme="${fixture_url%%://*}"
fixture_authority="${fixture_url#*://}"
fixture_authority="${fixture_authority%%/*}"
friends_api_scheme="ws"
if [[ "${fixture_uri_scheme}" == "https" ]]; then
  friends_api_scheme="wss"
fi

# Catalyst is the complete realm for this fixture. Keep org as the environment
# so non-Catalyst services continue using their normal endpoints. Unity PR 9822
# adds the gateway override used here to route Social HTTP through the fixture.
# Gatekeeper and Social RPC have explicit endpoint overrides because they are
# not part of the gateway URL transformation.
# Keep value-taking arguments separated by spaces. MetaForge forwards this
# string to Unity differently on macOS and Windows; the `--realm=...` form is
# parsed as a flag name on macOS and silently falls back to the org realm.
# The fixture scene is deployed at Genesis Plaza (0,0). Keep the startup
# parcel explicit because MetaForge's default is 100,100, which is empty in
# this deterministic realm and leaves the client on the splash screen.
# Keep the fixture feature flags local as well. Production currently enables
# asset-bundle fallback, which routes scene discovery to ABGen instead of the
# Catalyst content endpoint. The fixture serves that flag as disabled so the
# seeded scene at 0,0 is loaded from Catalyst.
friends_api_url="${friends_api_scheme}://${fixture_authority}/social-rpc"
fixture_app_args="--dclenv org --realm ${fixture_url} --gateway ${fixture_url} --position 0,0 --optimized-assets-url ${fixture_url} --dcl-lists-url ${fixture_url} --feature-flags-url ${fixture_url}/__fixture/feature-flags --gatekeeper-url ${fixture_url}/comms-gatekeeper --friends-api-url ${friends_api_url} --accept-untrusted-realm"

if [[ -n "${GITHUB_ENV:-}" ]]; then
  {
    echo "FIXTURE_BASE_URL=${fixture_url}"
    echo "FIXTURE_BASE_DOMAIN=${fixture_domain}"
    echo "FIXTURE_APP_ARGS=${fixture_app_args}"
  } >> "${GITHUB_ENV}"
fi

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Fixture infrastructure"
    echo
    echo "- Base URL: \`${fixture_url}\`"
    echo "- Realm URL: \`${fixture_url}\`"
    echo "- Base domain: \`${fixture_domain}\`"
    echo "- App args: \`${fixture_app_args}\`"
  } >> "${GITHUB_STEP_SUMMARY}"
fi

cat <<JSON
{
  "baseUrl": "${fixture_url}",
  "realmUrl": "${fixture_url}",
  "baseDomain": "${fixture_domain}",
  "appArgs": "${fixture_app_args}"
}
JSON
