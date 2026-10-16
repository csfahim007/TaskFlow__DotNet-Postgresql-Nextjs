#!/usr/bin/env bash
set -euo pipefail

required_variables=(
  DEPLOY_HOST
  DEPLOY_PORT
  DEPLOY_USER
  DEPLOY_PATH
  DEPLOY_SSH_KEY
  DEPLOY_KNOWN_HOSTS
)

for variable in "${required_variables[@]}"; do
  if [[ -z "${!variable:-}" ]]; then
    printf 'Required deployment setting is missing: %s\n' "$variable" >&2
    exit 1
  fi
done

if [[ ! "$DEPLOY_PORT" =~ ^[0-9]+$ ]]; then
  echo "DEPLOY_PORT must be a number." >&2
  exit 1
fi

if [[ ! "$DEPLOY_HOST" =~ ^[A-Za-z0-9.-]+$ || ! "$DEPLOY_USER" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "DEPLOY_HOST or DEPLOY_USER contains unsupported characters." >&2
  exit 1
fi

if [[ ! "$DEPLOY_PATH" =~ ^/[A-Za-z0-9_./-]+$ ]]; then
  echo "DEPLOY_PATH must be an absolute path without whitespace or shell metacharacters." >&2
  exit 1
fi

for command in ssh ssh-keygen rsync; do
  if ! command -v "$command" >/dev/null 2>&1; then
    printf 'Required deployment command is unavailable: %s\n' "$command" >&2
    exit 1
  fi
done

temporary_directory="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/taskflow-deploy.XXXXXX")"
trap 'rm -rf -- "$temporary_directory"' EXIT

identity_file="$temporary_directory/deploy_key"
known_hosts_file="$temporary_directory/known_hosts"
printf '%s\n' "$DEPLOY_SSH_KEY" > "$identity_file"
printf '%s\n' "$DEPLOY_KNOWN_HOSTS" > "$known_hosts_file"
chmod 600 "$identity_file" "$known_hosts_file"

if ! ssh-keygen -y -f "$identity_file" >/dev/null 2>&1; then
  echo "DEPLOY_SSH_KEY is not a usable private SSH key." >&2
  exit 1
fi

known_host="[$DEPLOY_HOST]:$DEPLOY_PORT"
if ! ssh-keygen -F "$known_host" -f "$known_hosts_file" >/dev/null; then
  printf 'DEPLOY_KNOWN_HOSTS has no verified entry for %s.\n' "$known_host" >&2
  exit 1
fi

ssh_options=(
  -i "$identity_file"
  -p "$DEPLOY_PORT"
  -o IdentitiesOnly=yes
  -o StrictHostKeyChecking=yes
  -o "UserKnownHostsFile=$known_hosts_file"
)
ssh_rsh="ssh -i $identity_file -p $DEPLOY_PORT -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=$known_hosts_file"
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

rsync --archive --compress \
  --exclude='/.git/' \
  --exclude='/.env*' \
  --exclude='**/.env*' \
  --exclude='**/node_modules/' \
  --exclude='**/.next/' \
  --exclude='**/bin/' \
  --exclude='**/obj/' \
  --exclude='**/logs/' \
  --rsh="$ssh_rsh" \
  "$repository_root/" "$DEPLOY_USER@$DEPLOY_HOST:$DEPLOY_PATH/"

ssh "${ssh_options[@]}" "$DEPLOY_USER@$DEPLOY_HOST" "bash -s -- $DEPLOY_PATH" <<'REMOTE'
set -euo pipefail
cd "$1"
docker compose up -d --build
docker compose ps
curl --fail --silent --show-error --retry 12 --retry-connrefused --retry-delay 5 \
  -H 'Host: api-taskflow.cloudafk.xyz' \
  http://127.0.0.1:8081/health/ready >/dev/null
curl --fail --silent --show-error --retry 12 --retry-connrefused --retry-delay 5 \
  -H 'Host: taskflow.cloudafk.xyz' \
  http://127.0.0.1:8081/login >/dev/null
REMOTE
