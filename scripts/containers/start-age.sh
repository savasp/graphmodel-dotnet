#!/bin/bash

# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

set -euo pipefail

runtime="${CONTAINER_RUNTIME:-}"
if [ -z "$runtime" ]; then
    if command -v podman >/dev/null 2>&1 && podman info >/dev/null 2>&1; then
        runtime=podman
    elif command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
        runtime=docker
    else
        echo "No usable Podman or Docker runtime found." >&2
        exit 1
    fi
fi

if [ "$runtime" != podman ] && [ "$runtime" != docker ]; then
    echo "CONTAINER_RUNTIME must be 'podman' or 'docker'." >&2
    exit 1
fi

container_name="${AGE_CONTAINER_NAME:-cvoya-age}"
image="${AGE_CONTAINER_IMAGE:-apache/age:release_PG18_1.7.0}"
host_port="${AGE_PORT:-5455}"

if "$runtime" ps -a --format '{{.Names}}' | grep -Fqx "$container_name"; then
    "$runtime" rm --force "$container_name" >/dev/null
fi

"$runtime" run --detach \
    --name "$container_name" \
    --publish "$host_port:5432" \
    --env POSTGRES_USER=postgres \
    --env POSTGRES_PASSWORD=postgres \
    --env POSTGRES_DB=postgres \
    "$image" >/dev/null

attempt=0
until "$runtime" exec "$container_name" pg_isready -U postgres -d postgres >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 60 ]; then
        "$runtime" logs "$container_name" >&2
        echo "Apache AGE did not become ready in time." >&2
        exit 1
    fi
    sleep 2
done

"$runtime" exec "$container_name" \
    psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
    -c 'CREATE EXTENSION IF NOT EXISTS age' >/dev/null

echo "Apache AGE is ready."
echo "export AGE_CONNECTION_STRING='Host=localhost;Port=$host_port;Username=postgres;Password=postgres;Database=postgres'"
