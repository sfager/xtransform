#!/usr/bin/env bash
# Enforce the XSLT (gate G3) conformance baseline.
#
# Reads tests/XTransform.Conformance/xslt-baseline.json and the per-shard summaries
# artifacts/conformance/xslt-summary-<shard>.json written by the XSLT conformance run
# (shards: "core" = everything except unicode-90, "unicode-90" = the Unicode 9.0 stress set).
# For every shard whose summary exists it fails if
#   - the passed count is more than 5 below the baseline, or
#   - the in-scope count drifted by more than 1% from the baseline.
# When both shards are present it also fails if the combined pass-rate
# (passed / in-scope) is below baseline.minimumPassRate (0.40 for gate G3).
#
# Usage: eng/check-xslt-baseline.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline="$root/tests/XTransform.Conformance/xslt-baseline.json"
dir="$root/artifacts/conformance"

[ -f "$baseline" ] || { echo "ERROR: $baseline not found." >&2; exit 2; }

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

fail=0
total_passed=0
total_scope=0
shards_seen=0

for shard in core unicode90; do
  file_shard=$shard
  [ "$shard" = "unicode90" ] && file_shard="unicode-90"
  summary="$dir/xslt-summary-$file_shard.json"
  [ -f "$summary" ] || { echo "note: $summary not found; skipping shard $file_shard"; continue; }

  base_passed=$(read_field "$baseline" "${shard}Passed")
  base_scope=$(read_field "$baseline" "${shard}InScope")
  cur_passed=$(read_field "$summary" passed)
  cur_scope=$(read_field "$summary" inScopeDiscovered)

  printf 'Shard %-10s baseline passed=%s inScope=%s | current passed=%s inScope=%s\n' \
    "$file_shard" "$base_passed" "$base_scope" "$cur_passed" "$cur_scope"

  if awk "BEGIN{exit !($cur_passed + 0 < $base_passed + 0 - 5)}"; then
    echo "FAIL: $file_shard passed count $cur_passed is more than 5 below baseline $base_passed" >&2
    fail=1
  fi

  drift=$(awk "BEGIN{d=($cur_scope - $base_scope)/$base_scope; if(d<0) d=-d; printf \"%.4f\", d}")
  if awk "BEGIN{exit !($drift > 0.01)}"; then
    echo "FAIL: $file_shard in-scope count drifted by $drift (threshold 0.01). Investigate the filter or the test-suite pin." >&2
    fail=1
  fi

  total_passed=$((total_passed + cur_passed))
  total_scope=$((total_scope + cur_scope))
  shards_seen=$((shards_seen + 1))
done

if [ "$shards_seen" -eq 2 ]; then
  min_rate=$(read_field "$baseline" minimumPassRate)
  rate=$(awk "BEGIN{printf \"%.4f\", $total_passed / $total_scope}")
  printf 'Combined: passed=%s inScope=%s passRate=%s (minimum %s)\n' "$total_passed" "$total_scope" "$rate" "$min_rate"
  if awk "BEGIN{exit !($rate + 0 < $min_rate + 0)}"; then
    echo "FAIL: combined XSLT pass-rate $rate is below the gate minimum $min_rate" >&2
    fail=1
  fi
fi

if [ "$shards_seen" -eq 0 ]; then
  echo "ERROR: no XSLT summaries found under $dir. Run the XSLT conformance tests first." >&2
  exit 2
fi

if [ "$fail" -eq 0 ]; then
  echo "OK: XSLT conformance baseline satisfied."
fi
exit "$fail"
