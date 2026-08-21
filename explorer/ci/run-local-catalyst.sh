#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  ./explorer/ci/run-local-catalyst.sh --build-url <url-or-metaforge-ref> [options]

Options:
  --build-url <value>     Unity Explorer build URL or MetaForge build ref.
  --filter <expression>   NUnit filter. Default: Category=InWorld.
  --infra-dir <path>      explorer-e2e-infra checkout. Default: ../explorer-e2e-infra.
  --fixture-port <port>   HTTPS host port. Must be 443 because the realm URL is passed to Unity. Default: 443.
  --no-build               Reuse the existing Catalyrst image (the default).
  --keep-fixture           Leave Docker services running after the test.
  --health-only            Start and validate the fixture, but do not run Unity.
  -h, --help               Show this help.

The AltTester license is deliberately not accepted as a command-line argument.
For local runs it must already be configured in MetaForge; GitHub Actions gets it
from the ALTTESTER_LICENSE secret.
USAGE
}

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "${script_dir}/../.." && pwd)"

infra_dir="${EXPLORER_E2E_INFRA_DIR:-${repo_dir}/../explorer-e2e-infra}"
fixture_https_port="${FIXTURE_HTTPS_PORT:-443}"
fixture_http_port="${FIXTURE_HTTP_PORT:-8080}"
fixture_domain="${FIXTURE_BASE_DOMAIN:-localhost}"
fixture_url="${FIXTURE_BASE_URL:-https://${fixture_domain}}"
fixture_ca_file="${FIXTURE_CA_FILE:-}"
build_url=""
test_filter="Category=InWorld"
build_image="${FIXTURE_BUILD_IMAGE:-0}"
keep_fixture=0
health_only=0

while (($# > 0)); do
  case "$1" in
    --build-url)
      [[ $# -ge 2 ]] || { echo "--build-url requires a value" >&2; exit 2; }
      build_url="$2"
      shift 2
      ;;
    --filter)
      [[ $# -ge 2 ]] || { echo "--filter requires a value" >&2; exit 2; }
      test_filter="$2"
      shift 2
      ;;
    --infra-dir)
      [[ $# -ge 2 ]] || { echo "--infra-dir requires a value" >&2; exit 2; }
      infra_dir="$2"
      shift 2
      ;;
    --fixture-port)
      [[ $# -ge 2 ]] || { echo "--fixture-port requires a value" >&2; exit 2; }
      fixture_https_port="$2"
      shift 2
      ;;
    --no-build)
      build_image=0
      shift
      ;;
    --keep-fixture)
      keep_fixture=1
      shift
      ;;
    --health-only)
      health_only=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ "${fixture_https_port}" != "443" ]]; then
  echo "The Unity realm URL must be reachable over HTTPS; use HTTPS port 443." >&2
  exit 2
fi

if [[ -z "${fixture_ca_file}" ]] && command -v mkcert >/dev/null 2>&1; then
  candidate_ca="$(mkcert -CAROOT)/rootCA.pem"
  [[ -s "${candidate_ca}" ]] && fixture_ca_file="${candidate_ca}"
fi

if [[ "$health_only" != 1 && -z "$build_url" ]]; then
  echo "--build-url is required unless --health-only is used." >&2
  usage >&2
  exit 2
fi

if [[ ! -x "${infra_dir}/scripts/fixture-up.sh" ]]; then
  echo "explorer-e2e-infra was not found at: ${infra_dir}" >&2
  echo "Clone it next to explorer-automation or pass --infra-dir <path>." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required to run explorer-e2e-infra." >&2
  exit 1
fi

cleanup() {
  local exit_code=$?
  if [[ "$keep_fixture" == 1 ]]; then
    echo "Keeping Catalyst fixture running at ${fixture_url}."
  else
    echo "Stopping Catalyst fixture..."
    (cd "$infra_dir" && ./scripts/fixture-down.sh) || {
      echo "warning: failed to stop explorer-e2e-infra" >&2
      [[ "$exit_code" == 0 ]] && exit_code=1
    }
  fi
  exit "$exit_code"
}
trap cleanup EXIT INT TERM

echo "Starting Catalyrst fixture from ${infra_dir}..."
echo "Catalyrst image: ${CATALYRST_IMAGE:-explorer-e2e-infra-catalyrst:latest}"
(
  cd "$infra_dir"
  FIXTURE_BASE_DOMAIN="$fixture_domain" \
  FIXTURE_PUBLIC_URL="$fixture_url" \
  FIXTURE_HTTP_PORT="$fixture_http_port" \
  FIXTURE_HTTPS_PORT="$fixture_https_port" \
  FIXTURE_BUILD_IMAGE="$build_image" \
  ./scripts/fixture-up.sh
)

echo "Validating fixture contract..."
fixture_config="$({
  cd "$repo_dir"
  FIXTURE_BASE_URL="$fixture_url" \
  FIXTURE_BASE_DOMAIN="$fixture_domain" \
  FIXTURE_CA_FILE="$fixture_ca_file" \
  ./explorer/ci/configure-fixture.sh
})"
printf '%s\n' "$fixture_config"

fixture_app_args="--dclenv org --realm ${fixture_url} --gateway ${fixture_url} --dcl-lists-url ${fixture_url} --accept-untrusted-realm --comms-adapter offline:offline"

if [[ "$health_only" == 1 ]]; then
  echo "Fixture is ready. Unity test was skipped (--health-only)."
  exit 0
fi

if ! command -v mf >/dev/null 2>&1; then
  echo "MetaForge (mf) is required to run the Unity test." >&2
  exit 1
fi

echo "Running Explorer build ${build_url} against ${fixture_url}"
echo "NUnit filter: ${test_filter}"
echo "Explorer app args: ${fixture_app_args}"

mf explorer test "$build_url" \
  --non-interactive \
  --filter "$test_filter" \
  --app-args="${fixture_app_args}"
