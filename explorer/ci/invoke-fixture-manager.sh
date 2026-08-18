#!/usr/bin/env bash
set -Eeuo pipefail

usage() {
  cat <<'USAGE'
Usage:
  invoke-fixture-manager.sh create
  invoke-fixture-manager.sh status
  invoke-fixture-manager.sh destroy

The script invokes the e2e-fixture-manager Lambda through the AWS CLI. The
caller must already have configured AWS credentials, normally with GitHub
Actions OIDC.

Required environment:
  E2E_FIXTURE_MANAGER_FUNCTION_NAME
  FIXTURE_RUN_ID

Create-only environment:
  FIXTURE_PROFILE       Default: core-v1
  FIXTURE_TTL_MINUTES   Default: 90
  FIXTURE_SEED_VERSION  Default: empty-bootstrap
USAGE
}

action="${1:-}"
case "${action}" in
  create|status|destroy) ;;
  -h|--help) usage; exit 0 ;;
  *) usage >&2; exit 2 ;;
esac

: "${E2E_FIXTURE_MANAGER_FUNCTION_NAME:?E2E_FIXTURE_MANAGER_FUNCTION_NAME is required}"
: "${FIXTURE_RUN_ID:?FIXTURE_RUN_ID is required}"

aws_region="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
: "${aws_region:?AWS_REGION or AWS_DEFAULT_REGION is required}"

if ! [[ "${FIXTURE_RUN_ID}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,62}$ ]]; then
  echo "FIXTURE_RUN_ID contains unsupported characters" >&2
  exit 2
fi

if [[ -n "${RUNNER_TEMP:-}" ]]; then
  work_dir="${RUNNER_TEMP}/e2e-fixture-manager"
else
  work_dir="${TMPDIR:-/tmp}/e2e-fixture-manager"
fi
mkdir -p "${work_dir}"

invoke() {
  local payload="$1"
  local response_file="$work_dir/response.json"
  local metadata_file="$work_dir/metadata.json"

  aws lambda invoke \
    --region "${aws_region}" \
    --function-name "${E2E_FIXTURE_MANAGER_FUNCTION_NAME}" \
    --invocation-type RequestResponse \
    --cli-binary-format raw-in-base64-out \
    --payload "${payload}" \
    "${response_file}" > "${metadata_file}"

  if jq -e 'has("FunctionError")' "${metadata_file}" >/dev/null; then
    echo "e2e-fixture-manager Lambda returned an error:" >&2
    jq . "${response_file}" >&2 || cat "${response_file}" >&2
    return 1
  fi

  jq -e 'type == "object"' "${response_file}" >/dev/null || {
    echo "e2e-fixture-manager returned a non-object response:" >&2
    cat "${response_file}" >&2
    return 1
  }
  cat "${response_file}"
}

case "${action}" in
  create)
    ttl_minutes="${FIXTURE_TTL_MINUTES:-90}"
    profile="${FIXTURE_PROFILE:-core-v1}"
    seed_version="${FIXTURE_SEED_VERSION:-empty-bootstrap}"
    payload="$(jq -cn \
      --arg action create \
      --arg runId "${FIXTURE_RUN_ID}" \
      --arg profile "${profile}" \
      --arg seedVersion "${seed_version}" \
      --argjson ttlMinutes "${ttl_minutes}" \
      '{action:$action,runId:$runId,profile:$profile,seedVersion:$seedVersion,ttlMinutes:$ttlMinutes}')"
    initial="$(invoke "${payload}")"
    fixture_id="$(jq -r '.fixtureId // .runId // empty' <<<"${initial}")"
    [[ -n "${fixture_id}" ]] || {
      echo "create response did not contain fixtureId:" >&2
      jq . <<<"${initial}" >&2
      exit 1
    }

    deadline=$(( $(date +%s) + ${FIXTURE_READY_TIMEOUT_SECONDS:-600} ))
    while :; do
      status_payload="$(jq -cn --arg action status --arg fixtureId "${fixture_id}" \
        '{action:$action,fixtureId:$fixtureId}')"
      status="$(invoke "${status_payload}")"
      state="$(jq -r '.status // "UNKNOWN"' <<<"${status}")"
      endpoint="$(jq -r '.endpoint // empty' <<<"${status}")"

      if [[ "${state}" == "READY" && -n "${endpoint}" ]]; then
        printf '%s\n' "${status}"
        exit 0
      fi
      if [[ "${state}" == "FAILED" || "${state}" == "STOPPED" || "${state}" == "DESTROYED" || "${state}" == "UNKNOWN" ]]; then
        echo "fixture ${fixture_id} entered terminal state ${state}:" >&2
        jq . <<<"${status}" >&2
        exit 1
      fi
      if (( $(date +%s) >= deadline )); then
        echo "fixture ${fixture_id} did not become READY before the timeout:" >&2
        jq . <<<"${status}" >&2
        exit 1
      fi
      sleep 5
    done
    ;;
  status|destroy)
    payload="$(jq -cn --arg action "${action}" --arg fixtureId "${FIXTURE_RUN_ID}" \
      '{action:$action,fixtureId:$fixtureId}')"
    invoke "${payload}"
    ;;
esac
