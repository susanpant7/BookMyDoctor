#!/bin/sh
# The start command of the container.
#
# LITESTREAM_ENABLED=true  restores the database from the bucket (when the file is missing and a copy exists), then runs the app with
#                          Litestream copying every change to the bucket.
# anything else            runs the app alone, as before: no restore, no copy.
# LITESTREAM_RESTORE=false skips the restore (the app starts with an empty database; the old copy stays in the bucket).
set -eu

if [ "${LITESTREAM_ENABLED:-false}" != "true" ]; then
  exec dotnet MyHospitalAI.dll
fi

: "${LITESTREAM_BUCKET:?LITESTREAM_ENABLED is true, so LITESTREAM_BUCKET (the bucket name) is needed}"
: "${LITESTREAM_ENDPOINT:?LITESTREAM_ENABLED is true, so LITESTREAM_ENDPOINT is needed (B2: https://s3.<region>.backblazeb2.com, R2: https://<account id>.r2.cloudflarestorage.com)}"
: "${LITESTREAM_ACCESS_KEY_ID:?LITESTREAM_ENABLED is true, so LITESTREAM_ACCESS_KEY_ID is needed}"
: "${LITESTREAM_SECRET_ACCESS_KEY:?LITESTREAM_ENABLED is true, so LITESTREAM_SECRET_ACCESS_KEY is needed}"

# The database file is the one of the connection string ("Data Source=...", relative to /app), or the default.
SOURCE=$(printf '%s' "${ConnectionStrings__Default:-}" | sed -n 's/.*[Dd]ata [Ss]ource=\([^;]*\).*/\1/p')
SOURCE=${SOURCE:-data/myhospitalai.db}
case "$SOURCE" in
  /*) export LITESTREAM_DB_PATH="$SOURCE" ;;
  *) export LITESTREAM_DB_PATH="/app/$SOURCE" ;;
esac
export LITESTREAM_PATH="${LITESTREAM_PATH:-myhospitalai}"
# "auto" is right for Cloudflare R2. Backblaze B2 needs its own region, the middle part of its S3 endpoint (s3.<region>.backblazeb2.com).
export LITESTREAM_REGION="${LITESTREAM_REGION:-auto}"
export LITESTREAM_RETENTION="${LITESTREAM_RETENTION:-24h}"

mkdir -p "$(dirname "$LITESTREAM_DB_PATH")"

if [ "${LITESTREAM_RESTORE:-true}" = "true" ]; then
  # An empty bucket (the first deploy) can answer "NoSuchKey" instead of "no copy", which -if-replica-exists doesn't catch:
  # then there is nothing to restore, and the app starts with a new database. Any other error stops the start, so a copy that
  # exists but can't be read is never replaced by an empty database.
  if ! output=$(litestream restore -config /app/litestream.yml -if-db-not-exists -if-replica-exists "$LITESTREAM_DB_PATH" 2>&1); then
    printf '%s\n' "$output"
    case "$output" in
      *NoSuchKey*) echo "No copy of the database in the bucket yet: starting with a new database." ;;
      *) exit 1 ;;
    esac
  elif [ -n "$output" ]; then
    printf '%s\n' "$output"
  fi
else
  echo "LITESTREAM_RESTORE is false: starting without restoring the database."
fi

exec litestream replicate -config /app/litestream.yml -exec "dotnet MyHospitalAI.dll"
