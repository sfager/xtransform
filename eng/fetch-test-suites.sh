#!/usr/bin/env bash
# Clones the pinned W3C test suites into .testsuites/ (git-ignored).
# Usage: eng/fetch-test-suites.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=/dev/null
source "$root/eng/test-suites.env"
dest="$root/.testsuites"
mkdir -p "$dest"

fetch() {
  local name=$1 repo=$2 commit=$3 dir="$dest/$1"
  if [ ! -d "$dir/.git" ]; then
    git init -q "$dir"
    git -C "$dir" remote add origin "$repo"
  fi
  if [ "$(git -C "$dir" rev-parse HEAD 2>/dev/null || true)" != "$commit" ]; then
    echo "Fetching $name @ ${commit:0:12}"
    git -C "$dir" fetch -q --depth 1 origin "$commit"
    git -C "$dir" checkout -q --detach FETCH_HEAD
  else
    echo "$name already at ${commit:0:12}"
  fi
}

fetch qt3tests "$QT3_REPO" "$QT3_COMMIT"
fetch xslt30-test "$XSLT_REPO" "$XSLT_COMMIT"
