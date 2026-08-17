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

if [[ -z "${fixture_domain}" ]]; then
  echo "FIXTURE_BASE_DOMAIN could not be derived from ${fixture_url}" >&2
  exit 1
fi

if [[ "${fixture_domain}" == *:* ]]; then
  echo "FIXTURE_BASE_DOMAIN must be a hostname without a port; --base-domain does not support ports" >&2
  exit 1
fi

about_json="$(curl "${curl_tls_args[@]}" --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/about")"

printf '%s\n' "${about_json}" | jq -e '.healthy == true and .content.healthy == true and .lambdas.healthy == true' >/dev/null

curl "${curl_tls_args[@]}" --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/content/status" >/dev/null
curl "${curl_tls_args[@]}" --fail --silent --show-error --retry 10 --retry-delay 1 \
  --connect-timeout 2 --max-time 10 "${fixture_url}/lambdas/status" >/dev/null

if [[ "${fixture_url}" != https://* ]]; then
  echo "FIXTURE_BASE_URL must use HTTPS because --base-domain generates HTTPS URLs" >&2
  exit 1
fi

fixture_app_args="--dclenv org --base-domain ${fixture_domain}"

if [[ -n "${GITHUB_ENV:-}" ]]; then
  {
    echo "FIXTURE_BASE_URL=${fixture_url}"
    echo "FIXTURE_BASE_DOMAIN=${fixture_domain}"
    echo "FIXTURE_APP_ARGS=${fixture_app_args}"
  } >> "${GITHUB_ENV}"
fi

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Catalyst fixture"
    echo
    echo "- Base URL: \`${fixture_url}\`"
    echo "- Base domain: \`${fixture_domain}\`"
    echo "- App args: \`${fixture_app_args}\`"
  } >> "${GITHUB_STEP_SUMMARY}"
fi

cat <<JSON
{
  "baseUrl": "${fixture_url}",
  "baseDomain": "${fixture_domain}",
  "appArgs": "${fixture_app_args}"
}
JSON
