#!/usr/bin/env python3
"""Zero-dependency validator for BONKADOO content. Exit 1 on any error; warnings never fail CI.

Usage: validate_content.py [--root DIR] [--strict-warnings]
"""
import csv, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_PUZZLES = 45

PUZZLE_ID = re.compile(r'^ch0[1-5]\.p0[1-9]$')
SCENE_ID = re.compile(r'^scene\.ch0[1-5]\.[a-z0-9_]+$')
EVENT_ID = re.compile(r'^evt\.[a-z0-9_.]+$')
FLAG_ID = re.compile(r'^flag\.[a-z0-9_.]+$')
KEY_ID = re.compile(r'^(dlg|txt|ui)\.[A-Za-z0-9_.]+$')
VERBS = {'Tap', 'Drag', 'UseItem', 'CombineItems', 'Talk', 'Inspect'}
# ADR-0002 allowlist: operator -> argument shape
PREDICATES = {
    'all': 'list', 'any': 'list', 'not': 'pred',
    'flagEquals': ('key', 'value'), 'hasItem': ('itemId',), 'metricAtLeast': ('key', 'value'),
    'npcDispositionAtLeast': ('npcId', 'value'), 'eventOccurred': ('type',),
    'currentScene': 'str', 'requestVerb': 'verb', 'requestTarget': 'str', 'requestSource': 'str',
}
EFFECTS = {
    'setFlag': ('key', 'value'), 'adjustMetric': ('key', 'delta'), 'addItem': ('itemId',), 'removeItem': ('itemId',),
    'emitEvent': ('type',), 'queueDialogue': ('key',), 'queueAnimation': ('cue',), 'unlockExit': ('exitId',),
    'setNpcDisposition': ('npcId', 'value'),
}
INT_FIELDS = {'delta', 'value'}


def load(root, rel):
    return json.loads((root / rel).read_text(encoding='utf-8'))


class Report:
    def __init__(self):
        self.errors, self.warnings = [], []

    def err(self, msg): self.errors.append(msg)
    def warn(self, msg): self.warnings.append(msg)


def check_predicate(node, path, r, ctx):
    if not isinstance(node, dict) or len(node) != 1:
        return r.err(f'{path}: predicate must be an object with exactly one operator')
    (op, arg), = node.items()
    shape = PREDICATES.get(op)
    if shape is None:
        return r.err(f'{path}: predicate "{op}" not allowlisted')
    if shape == 'list':
        if not isinstance(arg, list): return r.err(f'{path}.{op}: expected list')
        for i, c in enumerate(arg): check_predicate(c, f'{path}.{op}[{i}]', r, ctx)
    elif shape == 'pred':
        check_predicate(arg, f'{path}.not', r, ctx)
    elif shape == 'str':
        if not isinstance(arg, str) or not arg: r.err(f'{path}.{op}: expected non-empty string')
        elif op == 'currentScene': ctx['scenes_read'].add(arg)
    elif shape == 'verb':
        if arg not in VERBS: r.err(f'{path}.{op}: unknown verb {arg!r}')
    else:
        check_args(op, arg, shape, path, r, predicate=True)
        if op == 'flagEquals' and isinstance(arg, dict) and isinstance(arg.get('key'), str):
            ctx['flags_read'].add(arg['key'])
        if op == 'metricAtLeast' and isinstance(arg, dict):
            ctx['metrics'].add(arg.get('key'))
        if op == 'eventOccurred' and isinstance(arg, dict):
            ctx['events_read'].add(arg.get('type'))


def check_args(op, arg, fields, path, r, predicate=False):
    if not isinstance(arg, dict):
        return r.err(f'{path}.{op}: expected object')
    extra = set(arg) - set(fields)
    missing = set(fields) - set(arg)
    if extra: r.err(f'{path}.{op}: unexpected field(s) {sorted(extra)}')
    if missing: r.err(f'{path}.{op}: missing field(s) {sorted(missing)}')
    for f in fields:
        v = arg.get(f)
        if f == 'value' and op in ('flagEquals', 'setFlag'):
            if not isinstance(v, (bool, int, str)): r.err(f'{path}.{op}.value: must be bool, int or string')
        elif f in INT_FIELDS:
            if not isinstance(v, int) or isinstance(v, bool): r.err(f'{path}.{op}.{f}: must be integer')
        elif not isinstance(v, str) or not v:
            r.err(f'{path}.{op}.{f}: must be non-empty string')


def check_effect(node, path, r, ctx):
    if not isinstance(node, dict) or len(node) != 1:
        return r.err(f'{path}: effect must be an object with exactly one operator')
    (op, arg), = node.items()
    if op not in EFFECTS:
        return r.err(f'{path}: effect "{op}" not allowlisted')
    check_args(op, arg, EFFECTS[op], path, r)
    if not isinstance(arg, dict): return
    if op == 'setFlag' and isinstance(arg.get('key'), str):
        if arg['key'].startswith('input.'): r.err(f'{path}: setFlag cannot write transient key {arg["key"]}')
        ctx['flags_written'].add(arg['key'])
    if op == 'emitEvent': ctx['events_emitted'].add(arg.get('type'))
    if op == 'queueDialogue': ctx['dialogue'].add((path, arg.get('key')))
    if op == 'adjustMetric': ctx['metrics'].add(arg.get('key'))


def validate(root=ROOT):
    root = Path(root)
    r = Report()
    puzzles = load(root, 'content/puzzles/puzzles.json')
    events = load(root, 'content/events/catalog.json')
    en = load(root, 'content/localization/en.json')
    vi = load(root, 'content/localization/vi.json')
    schema = load(root, 'schemas/puzzle.schema.json')
    world = load(root, 'content/world/world.json')
    chapters = [load(root, f'content/chapters/ch0{i}.json') for i in range(1, 6)]
    manifest_path = root / 'content/assets/manifest.csv'
    manifest = set()
    if manifest_path.exists():
        with manifest_path.open(encoding='utf-8') as fh:
            manifest = {row['asset_id'] for row in csv.DictReader(fh)}

    ctx = {k: set() for k in ('flags_read', 'flags_written', 'events_emitted', 'events_read', 'dialogue', 'metrics', 'scenes_read')}
    event_types = [e.get('type') for e in events]
    ev = set(event_types)
    if len(ev) != len(event_types): r.err('Duplicate event IDs')
    for t in event_types:
        if not isinstance(t, str) or not EVENT_ID.match(t): r.err(f'catalog: bad event id {t!r}')

    if len(puzzles) != EXPECTED_PUZZLES: r.err(f'Expected {EXPECTED_PUZZLES} puzzle designs, found {len(puzzles)}')
    required, allowed = set(schema['required']), set(schema['properties'])
    seen, rule_ids, prereqs_by_puzzle = set(), set(), {}

    if not isinstance(puzzles, list):
        r.err('puzzles.json: root must be an array')
        return r
    for idx, p in enumerate(puzzles):
        if not isinstance(p, dict):
            r.err(f'puzzle #{idx}: must be an object')
            continue
        pid = p.get('id', f'#{idx}')
        missing = required - set(p)
        if missing:
            r.err(f'{pid}: missing required field(s) {sorted(missing)}')
            continue
        for extra in sorted(set(p) - allowed): r.err(f'{pid}: unknown field {extra}')
        if not PUZZLE_ID.match(pid): r.err(f'{pid}: bad puzzle id')
        if pid in seen: r.err(f'Duplicate puzzle: {pid}')
        seen.add(pid)
        if p['chapter'] != pid[:4]: r.err(f'{pid}: chapter {p["chapter"]} does not match id')
        if not SCENE_ID.match(p['scene']): r.err(f'{pid}: bad scene id {p["scene"]}')
        if not p['scene'].startswith(f'scene.{p["chapter"]}.'): r.err(f'{pid}: scene {p["scene"]} outside chapter')

        for h in p['hotspots']:
            if not h.isascii(): r.warn(f'{pid}: non-ASCII hotspot id {h}')
        item = p.get('inventoryItem')
        if item and manifest and item not in manifest: r.err(f'{pid}: inventoryItem {item} not in asset manifest')

        if len(p['solutions']) < 2: r.err(f'{pid}: fewer than 2 solutions')
        if len({s['steps'][-1]['targetId'] for s in p['solutions'] if s.get('steps')}) < 2:
            r.err(f'{pid}: alternative routes not distinct')
        if len({tuple((st.get('verb'), st.get('targetId')) for st in s.get('steps', [])[:-1]) for s in p['solutions']}) < 2:
            r.warn(f'{pid}: routes share identical setup steps (option aliases, see INTERACTION_IMPLEMENTATION_NOTES)')

        pr_ids = set()
        for i, rule in enumerate(p['rules']):
            path = f'{pid}.rules[{i}]'
            rid = rule.get('id')
            for f in ('id', 'priority', 'when', 'effects', 'once'):
                if f not in rule: r.err(f'{path}: missing {f}')
            if not isinstance(rid, str) or not rid.startswith(f'rule.{pid}.'): r.err(f'{path}: rule id {rid!r} must start with rule.{pid}.')
            if rid in rule_ids: r.err(f'{path}: duplicate rule id {rid}')
            rule_ids.add(rid); pr_ids.add(rid)
            if not isinstance(rule.get('priority'), int) or isinstance(rule.get('priority'), bool): r.err(f'{path}: priority must be integer')
            if not isinstance(rule.get('once'), bool): r.err(f'{path}: once must be boolean')
            if 'when' in rule: check_predicate(rule['when'], f'{path}.when', r, ctx)
            for j, e in enumerate(rule.get('effects', [])): check_effect(e, f'{path}.effects[{j}]', r, ctx)

        hotspots = set(p['hotspots'])
        for s in p['solutions']:
            for f in ('id', 'strategy', 'steps', 'ruleId', 'eventId'):
                if f not in s: r.err(f'{pid}: solution missing {f}')
            if s.get('ruleId') not in pr_ids: r.err(f'{pid}: missing rule {s.get("ruleId")}')
            if s.get('eventId') not in ev: r.err(f'{pid}: missing event {s.get("eventId")}')
            if not s.get('steps'): r.err(f'{pid}: empty recipe'); continue
            for st in s['steps']:
                if st.get('verb') not in VERBS: r.err(f'{pid}.{s.get("id")}: unknown verb {st.get("verb")!r}')
                if st.get('targetId') not in hotspots: r.err(f'{pid}.{s.get("id")}: step target {st.get("targetId")} not a hotspot')
            setup = s.get('inputSetup')
            if setup and not str(setup.get('flag', '')).startswith('input.'): r.err(f'{pid}.{s.get("id")}: inputSetup flag must be input.*')

        for e in p['consequenceEvents']:
            if e not in ev: r.err(f'{pid}: consequence event {e} not in catalog')
        keys = p['hints'] + [x.get('dialogueKey') for x in p['failureResponses']] \
            + [f'dlg.{pid}.success.{letter}' for letter in 'ab'] + [f'txt.{pid}.{f}' for f in ('title', 'goal', 'ruleTwist')]
        for key in keys:
            if key not in en or key not in vi: r.err(f'{pid}: missing localization {key}')
        if len(p['testCases']) < 3: r.err(f'{pid}: missing test cases')
        if not p['failureResponses']: r.err(f'{pid}: no failure feedback')
        prereqs_by_puzzle[pid] = p.get('prerequisites', [])
        for prereq in prereqs_by_puzzle[pid]:
            if not isinstance(prereq, str) or not FLAG_ID.match(prereq): r.err(f'{pid}: invalid prereq {prereq}')

    for pid, prereqs in prereqs_by_puzzle.items():
        for f in prereqs:
            if f not in ctx['flags_written']: r.err(f'{pid}: prerequisite {f} is never set by any rule (unsatisfiable)')
    for key in sorted(ctx['flags_read']):
        if not key.startswith('input.') and not FLAG_ID.match(key): r.err(f'flag {key}: bad flag id')
    for t in sorted(x for x in ctx['events_emitted'] if x not in ev): r.err(f'emitEvent {t} not in catalog')
    for t in sorted(x for x in ctx['events_read'] if x not in ev): r.err(f'eventOccurred {t} not in catalog')
    for path, key in sorted(ctx['dialogue'], key=lambda x: x[0]):
        if key not in en or key not in vi: r.err(f'{path}: dialogue {key} not localized')
    metric_keys = {m['key'] for m in world.get('metrics', [])}
    for m in sorted(x for x in ctx['metrics'] if x not in metric_keys): r.err(f'metric {m} not defined in world.json')
    for t in ev - ctx['events_emitted']: r.warn(f'catalog event {t} is never emitted')
    puzzle_ids_in_events = {e.get('puzzleId') for e in events}
    for orphan in sorted(x for x in puzzle_ids_in_events if x not in seen): r.err(f'catalog references unknown puzzle {orphan}')

    owner = {}
    for ch in chapters:
        if not str(ch.get('chapterCompleteFlag', '')).startswith('flag.'): r.err(f'{ch.get("id")}: bad chapterCompleteFlag')
        for pid in ch.get('puzzles', []):
            if pid not in seen: r.err(f'{ch["id"]}: unknown puzzle {pid}')
            if pid in owner: r.err(f'{pid}: listed in {owner[pid]} and {ch["id"]}')
            owner[pid] = ch['id']
            if not pid.startswith(ch['id'] + '.'): r.err(f'{ch["id"]}: puzzle {pid} belongs to another chapter')
    for pid in sorted(seen - set(owner)): r.err(f'{pid}: not listed in any chapter')

    if set(en) != set(vi):
        r.err('VI/EN localization key sets differ: ' + ', '.join(sorted(set(en) ^ set(vi))[:10]))
    placeholder = re.compile(r'\{[0-9a-zA-Z_]+\}')
    for key in sorted(set(en) & set(vi)):
        if not KEY_ID.match(key): r.err(f'localization key {key}: bad format')
        for lang, table in (('en', en), ('vi', vi)):
            if not isinstance(table[key], str) or not table[key].strip(): r.err(f'{lang}:{key}: empty text')
        if sorted(placeholder.findall(str(en[key]))) != sorted(placeholder.findall(str(vi[key]))):
            r.err(f'{key}: placeholder mismatch between en and vi')
        if len(str(en[key])) > 110 or len(str(vi[key])) > 110:
            r.warn(f'{key}: longer than 110 characters (split bubble)')
    dupes = {}
    for k, v in en.items():
        if '.success.' in k: dupes.setdefault(v, []).append(k)
    for text, ks in dupes.items():
        if len(ks) > 2: r.warn(f'{len(ks)} success lines share placeholder text "{text}"')
    return r


def main(argv):
    root = ROOT
    if '--root' in argv: root = Path(argv[argv.index('--root') + 1])
    report = validate(root)
    for w in report.warnings[:20]: print('WARN: ' + w)
    if len(report.warnings) > 20: print(f'WARN: ... {len(report.warnings) - 20} more warning(s)')
    if report.errors:
        print('\n'.join('ERROR: ' + e for e in report.errors))
        print(f'FAIL: {len(report.errors)} error(s), {len(report.warnings)} warning(s)')
        return 1
    if '--strict-warnings' in argv and report.warnings:
        print(f'FAIL: {len(report.warnings)} warning(s) with --strict-warnings')
        return 1
    print(f'PASS: {EXPECTED_PUZZLES} puzzles; DSL allowlist, IDs, events, chapters, prerequisites, localization parity '
          f'({len(report.warnings)} warning(s))')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
