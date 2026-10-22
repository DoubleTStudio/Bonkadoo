#!/usr/bin/env python3
"""Compile and play ONE chapter spec in isolation (no files written). For authors.

Usage: check_chapter.py ch03
Checks: compiles, every route of every puzzle completes in order, Yarn nodes compile-shaped,
line lengths, required chapter beats (intro on first scene, at least one choice, outro).
"""
import json, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build  # noqa: E402
import simulate  # noqa: E402
from bonk import Choice, If, Option  # noqa: E402


def walk(body):
    for item in body:
        yield item
        if isinstance(item, Choice):
            for o in item.options:
                yield o
                yield from walk(o.lines)
        elif isinstance(item, If):
            yield from walk(item.then)
            yield from walk(item.otherwise)


def main(cid):
    n = int(cid[2:])
    ch = build.load_chapter(cid)
    if ch is None:
        print(f'no spec for {cid}')
        return 1
    comp = build.Compiler()
    prev = f'flag.ch0{n - 1}.complete' if n > 1 else None
    try:
        comp.compile_chapter(ch, n - 1, prev)
        comp.finish()
    except build.BuildError as e:
        print(f'BUILD ERROR: {e}')
        return 1

    problems, warnings = [], []
    world_def = json.loads((build.CONTENT / 'world' / 'world.json').read_text(encoding='utf-8'))
    engine = simulate.Engine(comp.puzzles, comp.ambient)
    hotspots = simulate.scene_hotspots(comp.scenes)
    by_id = {p['id']: p for p in comp.puzzles}
    chapter = comp.chapters[0]
    for route in ('a', 'b'):
        w = simulate.World(world_def)
        if prev: w.flags[prev] = True
        _, errs = simulate.play_chapter(engine, world_def, chapter, by_id, route, hotspots, w)
        problems += errs

    if cid == 'ch05':
        problems += simulate.check_endings(engine, world_def, by_id['ch05.p09']['scene'])
    if len(ch.puzzles) != 9: problems.append(f'{cid}: expected 9 puzzles, found {len(ch.puzzles)}')
    if not ch.scenes[0].enter: problems.append(f'{cid}: first scene needs an enter conversation (chapter intro)')
    nodes = {c.node for c in ch.conversations}
    referenced = {s.enter for s in ch.scenes if s.enter}
    for p in ch.puzzles:
        for e in p.after:
            if 'startConversation' in e: referenced.add(e['startConversation']['node'])
    for r in ch.extra_rules:
        for e in r.get('effects', []):
            if 'startConversation' in e: referenced.add(e['startConversation']['node'])
    for node in sorted(referenced - nodes): problems.append(f'conversation {node} referenced but not defined')
    for node in sorted(nodes - referenced): warnings.append(f'conversation {node} is never started')
    if not any(isinstance(i, Option) and i.act for c in ch.conversations for i in walk(c.body)):
        problems.append(f'{cid}: needs at least one dialogue choice with act= (relationship choice)')

    for key in comp.s.en:
        for lang, table in (('en', comp.s.en), ('vi', comp.s.vi)):
            if len(table[key]) > 110: warnings.append(f'{lang}:{key} is {len(table[key])} chars (>110)')
    items_used = {st.src for p in ch.puzzles for r in p.routes for st in r.steps if st.src}
    items_given = {g for p in ch.puzzles for r in p.routes for st in r.steps for g in st.gives}
    for i in sorted(items_used - items_given): warnings.append(f'item {i} used but never given in this chapter')

    for w_ in warnings: print('WARN: ' + w_)
    for p in problems: print('ERROR: ' + p)
    print(f'{cid}: {len(comp.puzzles)} puzzles, {sum(len(p["rules"]) for p in comp.puzzles)} step rules, '
          f'{len(comp.ambient)} ambient rules, {len(comp.s.en)} strings, {len(comp.yarn)} conversations')
    print('FAIL' if problems else 'PASS')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
