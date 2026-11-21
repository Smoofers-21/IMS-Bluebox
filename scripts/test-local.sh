#!/usr/bin/env bash
set -euo pipefail

# The web application/SQL suite creates and drops only databases in this local
# disposable container. Never use the Azure portfolio connection for this mode.
cd "$(dirname "${BASH_SOURCE[0]}")/.."
command -v docker >/dev/null
command -v dotnet >/dev/null
command -v openssl >/dev/null
docker info >/dev/null
dotnet --version
docker version --format 'Docker client {{.Client.Version}}; server {{.Server.Version}}'

umask 077
sql_test_dir="$(mktemp -d "${TMPDIR:-/tmp}/bluebox-local-sql.XXXXXX")"
sql_test_container="bluebox-local-sql-$$-${RANDOM}"
cleanup() {
  docker rm -f "$sql_test_container" >/dev/null 2>&1 || true
  rm -rf -- "$sql_test_dir"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

sql_test_password="$(openssl rand -hex 24)Aa1!"
printf 'ACCEPT_EULA=Y\nMSSQL_PID=Developer\nMSSQL_SA_PASSWORD=%s\n' "$sql_test_password" > "$sql_test_dir/server.env"
docker run --detach --rm --name "$sql_test_container" \
  --publish 127.0.0.1::1433 --env-file "$sql_test_dir/server.env" \
  mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04 >/dev/null

sql_test_ready=false
for attempt in {1..30}; do
  if docker exec "$sql_test_container" /bin/bash -c \
    'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q "SELECT 1"' >/dev/null 2>&1; then
    sql_test_ready=true
    break
  fi
  sleep 2
done
if [[ "$sql_test_ready" != true ]]; then
  echo "Local SQL Server did not become ready. No application tests were run." >&2
  exit 1
fi

sql_test_endpoint="$(docker port "$sql_test_container" 1433/tcp)"
sql_test_port="${sql_test_endpoint##*:}"
[[ "$sql_test_port" =~ ^[0-9]+$ ]]
export BLUEBOX_TEST_SQLSERVER="Server=127.0.0.1,$sql_test_port;User ID=sa;Password=$sql_test_password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
unset BLUEBOX_PRIVATE_CONFIG

echo "Running local web application and SQL Server integration tests on a disposable instance."
dotnet restore BlueBox.sln --locked-mode
dotnet test BlueBox.sln --no-restore --configuration Release "$@"
