#!/usr/bin/env bash
# Local/CI gate: content validator, validator fixtures, content sync, engine-free tests, Unity tests.
# Usage: ./ci.sh [--skip-unity]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
KIT="$HERE/docs/BONKADOO_Complete_Game_Development_Kit"
PROJECT="$HERE/Bonkadoo"
UNITY_CLI="${UNITY_CLI:-$(command -v unity || echo "$HOME/.unity/bin/unity")}"

step() { printf '\n== %s\n' "$1"; }

step "content validator"
python3 "$KIT/tools/validate_content.py"

step "validator negative fixtures"
python3 -m unittest discover -s "$KIT/tests/validator"

step "unity content in sync with kit"
python3 "$PROJECT/Tools/sync_content.py" --check

step "engine-free tests (dotnet)"
dotnet test "$PROJECT/CoreTests" --nologo -v quiet

if [[ "${1:-}" == "--skip-unity" ]]; then
  echo "Unity tests skipped by flag"; exit 0
fi

mkdir -p "$PROJECT/TestResults"
for mode in EditMode PlayMode; do
  step "unity $mode"
  out="$PROJECT/TestResults/$(echo "$mode" | tr "[:upper:]" "[:lower:]").xml"
  "$UNITY_CLI" --no-banner --non-interactive test "$PROJECT" --mode "$mode" --output "$out" --timeout 1200
  python3 - "$out" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"{r.get('result')}: {r.get('passed')}/{r.get('total')} passed, {r.get('failed')} failed")
sys.exit(0 if r.get('result') == 'Passed' and int(r.get('total')) > 0 else 1)
PY
done
echo; echo "ALL GATES PASSED"
