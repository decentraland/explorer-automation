#!/usr/bin/env bash
set -euo pipefail

: "${FIXTURE_BASE_URL:?FIXTURE_BASE_URL must point to the running fixture}"

base_url="${FIXTURE_BASE_URL%/}"
auth_token="${COMMS_GATEKEEPER_AUTH_TOKEN:-fixture-comms-token}"

curl_fixture() {
  curl --fail --silent --show-error --retry 5 --retry-all-errors --retry-delay 1 \
    --connect-timeout 2 --max-time 10 "$@"
}

echo "fixture-preflight: checking Catalyst realm and gateway"
about_json="$(curl_fixture "${base_url}/about")"
jq -e '.healthy == true and .content.healthy == true and .lambdas.healthy == true' \
  <<<"${about_json}" >/dev/null
curl_fixture "${base_url}/content/status" >/dev/null
curl_fixture "${base_url}/lambdas/status" >/dev/null
manifest="$(curl_fixture "${base_url}/__fixture/gateway/manifest")"
jq -e '.services.comms.enabled == true' <<<"${manifest}" >/dev/null

echo "fixture-preflight: checking Comms Gatekeeper and LiveKit credentials"
curl_fixture "${base_url}/comms-gatekeeper/status" >/dev/null
room_id="fixture-preflight-$(date +%s)-$$"
credentials="$(curl_fixture -X POST \
  -H "Authorization: Bearer ${auth_token}" \
  -H 'Content-Type: application/json' \
  --data "{\"room_id\":\"${room_id}\",\"user_addresses\":[\"0x0000000000000000000000000000000000000001\",\"0x0000000000000000000000000000000000000002\"]}" \
  "${base_url}/comms-gatekeeper/private-voice-chat")"

jq -e '
  (type == "object") and
  ((to_entries | length) == 2) and
  (to_entries | all(.[];
    (.value.connection_url | startswith("livekit:")) and
    (.value.connection_url | contains("access_token=")) and
    (.value.connection_url | contains("fixture-livekit.invalid") | not)
  ))
' <<<"${credentials}" >/dev/null

echo "fixture-preflight: control plane passed; LiveKit connection URLs were issued"
