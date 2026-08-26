#!/usr/bin/env bash
set -euo pipefail

: "${FIXTURE_BASE_URL:?FIXTURE_BASE_URL must point to the running fixture}"

base_url="${FIXTURE_BASE_URL%/}"
auth_token="${COMMS_GATEKEEPER_AUTH_TOKEN:-fixture-comms-token}"

curl_fixture() {
  curl --fail --silent --show-error --retry 5 --retry-all-errors --retry-delay 1 \
    --connect-timeout 2 --max-time 10 "$@"
}

check_livekit_data_plane() {
  local connection_url livekit_endpoint livekit_url probe_log probe_pid identity
  local room_id="${room_id:-fixture-preflight-$(date +%s)-$$}"
  local api_key="${FIXTURE_LIVEKIT_API_KEY:-fixture-livekit-key}"
  local api_secret="${FIXTURE_LIVEKIT_API_SECRET:-fixture-livekit-secret-0123456789-0123456789}"
  local timeout_seconds="${FIXTURE_LIVEKIT_SMOKE_TIMEOUT_SECONDS:-20}"

  connection_url="$(jq -er 'to_entries[0].value.connection_url' <<<"${credentials}")"
  livekit_endpoint="${connection_url#livekit:}"
  livekit_endpoint="${livekit_endpoint%%\?*}"
  case "${livekit_endpoint}" in
    ws://*|wss://*) livekit_url="${livekit_endpoint}" ;;
    *) livekit_url="wss://${livekit_endpoint}" ;;
  esac

  if ! command -v lk >/dev/null 2>&1; then
    echo "fixture-preflight: lk (LiveKit CLI) is required for the real LiveKit smoke test" >&2
    return 1
  fi

  identity="fixture-preflight-${GITHUB_RUN_ID:-local}-$$"
  probe_log="$(mktemp)"
  probe_pid=""
  cleanup_probe() {
    if [[ -n "${probe_pid}" ]] && kill -0 "${probe_pid}" 2>/dev/null; then
      kill "${probe_pid}" 2>/dev/null || true
      wait "${probe_pid}" 2>/dev/null || true
    fi
    rm -f "${probe_log}"
  }
  trap cleanup_probe RETURN

  echo "fixture-preflight: joining LiveKit room through ${livekit_url}"
  lk room join \
    --url "${livekit_url}" \
    --api-key "${api_key}" \
    --api-secret "${api_secret}" \
    --identity "${identity}" \
    "${room_id}" >"${probe_log}" 2>&1 &
  probe_pid=$!

  for _ in $(seq 1 "${timeout_seconds}"); do
    if grep -qiE 'connected to room|connected.*room' "${probe_log}"; then
      echo "fixture-preflight: LiveKit signaling and WebRTC connection passed"
      return 0
    fi
    if ! kill -0 "${probe_pid}" 2>/dev/null; then
      echo "fixture-preflight: LiveKit CLI exited before joining the room" >&2
      cat "${probe_log}" >&2
      return 1
    fi
    sleep 1
  done

  echo "fixture-preflight: LiveKit room join timed out after ${timeout_seconds}s" >&2
  cat "${probe_log}" >&2
  return 1
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

if [[ "${FIXTURE_LIVEKIT_REAL_SMOKE:-0}" == "1" ]]; then
  check_livekit_data_plane
else
  echo "fixture-preflight: control plane passed; LiveKit connection URLs were issued (real data-plane smoke disabled)"
fi
