#!/usr/bin/env bash
# Enforce the QT3 parse-only pass-rate baseline.
#
# Reads tests/XTransform.Conformance/baseline.json and compares against the
# machine-readable summary at artifacts/conformance/qt3-summary.json produced
# by the conformance test run. Exits non-zero if:
#   - current pass-rate is below baseline.minimumPassRate
#   - current pass count is more than 5 below baseline.passed
#   - in-scope count changed by more than 1% from baseline.inScopeCount
#
# Usage: eng/check-conformance-baseline.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline="$root/tests/XTransform.Conformance/baseline.json"
summary="$root/artifacts/conformance/qt3-summary.json"

[ -f "$summary" ]  || { echo "ERROR: $summary not found. Run the conformance project first." >&2; exit 2; }
[ -f "$baseline" ] || { echo "ERROR: $baseline not found." >&2; exit 2; }

# Pure-bash JSON scraper (works without jq/python). Strips an optional UTF-8 BOM.
# Matches lines like "\"key\" : 123" or "\"key\" : 0.9956" or "\"key\" : \"abc\"".
read_field() {
  local file=$1 key=$2
  awk -v k="$key" '
    NR==1 { sub(/^\xef\xbb\xbf/, "", $0) }
    {
      if (match($0, "\"" k "\"[[:space:]]*:[[:space:]]*([^,}]+)", m)) {
        v = m[1]
        gsub(/[ \t"]/, "", v)
        print v
        exit
      }
    }
  ' "$file"
}

base_min_rate=$(read_field "$baseline" minimumPassRate)
base_passed=$(read_field "$baseline"  passed)
base_scope=$(read_field "$baseline"   inScopeCount)
cur_rate=$(read_field "$summary"      passRate)
cur_passed=$(read_field "$summary"    passed)
cur_scope=$(read_field "$summary"     inScopeExecuted)

printf 'Baseline: minimumPassRate=%s passed=%s inScopeCount=%s\n' "$base_min_rate" "$base_passed" "$base_scope"
printf 'Current : passRate=%s        passed=%s inScopeExecuted=%s\n' "$cur_rate" "$cur_passed" "$cur_scope"

fail=0

if awk "BEGIN{exit !($cur_rate + 0 < $base_min_rate + 0)}"; then
  echo "FAIL: pass-rate $cur_rate below baseline minimum $base_min_rate" >&2
  fail=1
fi

if awk "BEGIN{exit !($cur_passed + 0 < $base_passed + 0 - 5)}"; then
  echo "FAIL: passed count $cur_passed is more than 5 below baseline $base_passed" >&2
  fail=1
fi

drift=$(awk "BEGIN{d=($cur_scope - $base_scope)/$base_scope; if(d<0) d=-d; printf \"%.4f\", d}")
if awk "BEGIN{exit !($drift > 0.01)}"; then
  echo "FAIL: in-scope count drifted by $drift (threshold 0.01). Baseline=$base_scope Current=$cur_scope. Investigate filter or test-suite pin." >&2
  fail=1
fi

if [ "$fail" -eq 0 ]; then
  echo "OK: conformance baseline satisfied."
fi
exit "$fail"
