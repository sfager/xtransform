#!/usr/bin/env bash
# Run the real-world stylesheet tests (P3-17). They are [Explicit] in the normal unit run,
# so select them by name here. Exits non-zero if any stylesheet's output differs.
#
# Usage: eng/check-realworld.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

dotnet test tests/XTransform.Tests \
  --filter "Name~RealWorld_" \
  --nologo
