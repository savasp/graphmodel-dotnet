#!/usr/bin/env bash

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

if ! command -v uvx >/dev/null 2>&1; then
    echo "ERROR: uvx is required to run the pinned zizmor version." >&2
    echo "Install uv from https://docs.astral.sh/uv/ and rerun this command." >&2
    exit 1
fi

# CI runs zizmor with a GitHub token, which enables its online audits
# (impostor commits, ref confusion). Match that locally when possible.
if [[ -z "${GH_TOKEN:-}" ]] && command -v gh >/dev/null 2>&1; then
    GH_TOKEN="$(gh auth token 2>/dev/null || true)"
    if [[ -n "$GH_TOKEN" ]]; then
        export GH_TOKEN
    fi
fi

exec uvx zizmor==1.26.1 --persona regular .github/workflows
