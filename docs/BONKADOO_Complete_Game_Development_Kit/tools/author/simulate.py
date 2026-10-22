#!/usr/bin/env python3
"""Plays every authored route through a Python mirror of the C# rule engine (ADR-0002 semantics).

Catches unreachable steps, rule collisions and missing items before Unity tests run.
The C# test ContentRouteTests is the authority; this is the fast author-side check.
Usage: simulate.py [chNN ...]
"""
import json, sys
from pathlib import Path

KIT = Path(__file__).resolve().parents[2]


class World:
    def __init__(self, world_def):
        self.flags, self.items, self.events, self.fired, self.exits = {}, set(), set(), set(), set()
        self.metric_def = {m['key']: m for m in world_def['metrics']}
        self.metrics = {k: m['initial'] for k, m in self.metric_def.items()}
        self.npc_def = world_def['npcDisposition']
        self.npcs = {}
        self.scene = None


def evaluate(p, w, req):
    (op, arg), = p.items()
    if op == 'all': return all(evaluate(c, w, req) for c in arg)
    if op == 'any': return any(evaluate(c, w, req) for c in arg)
    if op == 'not': return not evaluate(arg, w, req)
    if op == 'flagEquals':
        key, want = arg['key'], arg['value']
        actual = req.get('input', {}).get(key) if key.startswith('input.') else w.flags.get(key)
        if actual is None: return want is False
        return type(actual) is type(want) and actual == want
    if op == 'hasItem': return arg['itemId'] in w.items
    if op == 'metricAtLeast': return w.metrics.get(arg['key'], 0) >= arg['value']
    if op == 'npcDispositionAtLeast': return w.npcs.get(arg['npcId'], w.npc_def['initial']) >= arg['value']
    if op == 'eventOccurred': return arg['type'] in w.events
    if op == 'currentScene': return w.scene == arg
    if op == 'requestVerb': return req['verb'] == arg
    if op == 'requestTarget': return req['target'] == arg
    if op == 'requestSource': return req.get('source') == arg
    raise ValueError(f'unknown predicate {op}')


class Engine:
    def __init__(self, puzzles, ambient):
        rules = []
        for p in puzzles:
            gates = [{'flagEquals': {'key': f, 'value': True}} for f in p.get('prerequisites', [])]
            for r in p['rules']:
                when = {'all': gates + [r['when']]} if gates else r['when']
                rules.append(dict(r, when=when))
        rules += ambient
        self.rules = sorted(rules, key=lambda r: (-r['priority'], r['id']))

    def interact(self, w, req):
        for r in self.rules:
            if r['once'] and r['id'] in w.fired: continue
            if evaluate(r['when'], w, req):
                return r, self.apply(r, w)
        return None, []

    def apply(self, rule, w):
        out = []
        for e in rule['effects']:
            (op, arg), = e.items()
            if op == 'setFlag': w.flags[arg['key']] = arg['value']
            elif op == 'adjustMetric':
                d = w.metric_def[arg['key']]
                w.metrics[arg['key']] = max(d['min'], min(d['max'], w.metrics[arg['key']] + arg['delta']))
            elif op == 'addItem': w.items.add(arg['itemId'])
            elif op == 'removeItem':
                if arg['itemId'] not in w.items: raise AssertionError(f'{rule["id"]}: removes unheld {arg["itemId"]}')
                w.items.discard(arg['itemId'])
            elif op == 'emitEvent': w.events.add(arg['type'])
            elif op == 'unlockExit': w.exits.add(arg['exitId'])
            elif op == 'setNpcDisposition': w.npcs[arg['npcId']] = arg['value']
            elif op in ('queueDialogue', 'queueAnimation', 'startConversation'): out.append(e)
        if rule['once']: w.fired.add(rule['id'])
        return out


def load(rel):
    return json.loads((KIT / 'content' / rel).read_text(encoding='utf-8'))


def scene_hotspots(scenes):
    return {s['id']: {h['id'] for h in s['hotspots']} for s in scenes}


def play_chapter(engine, world_def, chapter, puzzles_by_id, route, hotspots, carry=None):
    w = carry or World(world_def)
    errors = []
    for pid in chapter['puzzles']:
        p = puzzles_by_id[pid]
        w.scene = p['scene']
        sol = next((s for s in p['solutions'] if s['id'].endswith('.' + route)), p['solutions'][0])
        if pid == 'ch05.p09':
            # pick an always-available ending before the finale (route a: PATCH, b: WALK_AWAY)
            door = 'prop.ch05.door_patch' if route == 'a' else 'prop.ch05.door_walk'
            rule, _ = engine.interact(w, {'verb': 'Inspect', 'target': door})
            if not rule or not rule['id'].startswith('rule.ending.choose.'):
                errors.append(f'ch05.p09.{route}: choosing {door} matched {rule and rule["id"]}')
        for i, st in enumerate(sol['steps']):
            if st['targetId'] not in hotspots.get(p['scene'], set()):
                errors.append(f'{pid}.{route} step {i + 1}: target {st["targetId"]} not in {p["scene"]}')
            req = {'verb': st['verb'], 'target': st['targetId'], 'source': st.get('sourceId')}
            if req['source'] and req['source'] not in w.items:
                errors.append(f'{pid}.{route} step {i + 1}: {req["source"]} not held')
            rule, _ = engine.interact(w, req)
            want = sol['ruleId'] if i == len(sol['steps']) - 1 else f'{sol["ruleId"]}.s{i + 1}'
            got = rule['id'] if rule else None
            if got != want:
                errors.append(f'{pid}.{route} step {i + 1} {st["verb"]} {st["targetId"]}: matched {got}, expected {want}')
                break
        if not w.flags.get(f'flag.{pid}.complete'):
            errors.append(f'{pid}.{route}: not completed')
            break
    if not w.flags.get(chapter['chapterCompleteFlag']):
        errors.append(f'{chapter["id"]}.{route}: chapter complete flag never set')
    return w, errors


def check_endings(engine, world_def, final_scene):
    """Each ending must be choosable from a low-meter state via its corrective (ENDING_MATRIX, TC-008)."""
    errors = []
    for door, value, corrective in (('patch', 'PATCH', None), ('walk', 'WALK_AWAY', None),
                                    ('rewrite', 'REWRITE', ('Talk', 'npc.mai_ch05')),
                                    ('reveal', 'REVEAL', ('Inspect', 'prop.ch05.audit_terminal'))):
        w = World(world_def)
        w.scene = final_scene
        w.flags['flag.ch05.p08.complete'] = True
        w.metrics['public_trust'], w.metrics['evidence_count'] = -3, 0
        target = f'prop.ch05.door_{door}'
        engine.interact(w, {'verb': 'Inspect', 'target': target})
        if corrective:
            if w.flags.get('flag.ending') == value:
                errors.append(f'{value}: selectable without corrective at low meters')
            engine.interact(w, {'verb': corrective[0], 'target': corrective[1]})
            engine.interact(w, {'verb': 'Inspect', 'target': target})
        if w.flags.get('flag.ending') != value:
            errors.append(f'{value}: not reachable (flag.ending={w.flags.get("flag.ending")!r})')
    return errors


def run(chapter_ids=None):
    world_def = load('world/world.json')
    puzzles = load('puzzles/puzzles.json')
    ambient = load('scenes/ambient.json')
    hotspots = scene_hotspots(load('scenes/scenes.json'))
    by_id = {p['id']: p for p in puzzles}
    engine = Engine(puzzles, ambient)
    errors = []
    chapters = [load(f'chapters/ch0{i}.json') for i in range(1, 6)]
    for route in ('a', 'b'):
        carry = World(world_def)
        for ch in chapters:
            if chapter_ids and ch['id'] not in chapter_ids:
                continue
            carry, errs = play_chapter(engine, world_def, ch, by_id, route, hotspots, carry)
            errors += errs
    if not chapter_ids or 'ch05' in chapter_ids:
        errors += check_endings(engine, world_def, by_id['ch05.p09']['scene'])
    return errors


if __name__ == '__main__':
    errs = run(sys.argv[1:] or None)
    for e in errs: print('ROUTE ERROR: ' + e)
    print('FAIL' if errs else 'PASS: every route of every puzzle completes in chapter order')
    sys.exit(1 if errs else 0)
