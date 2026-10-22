#!/usr/bin/env python3
"""Compile tools/author/chapters/*.py into runtime content (ADR-0003).

Usage: build.py [--check]   (--check exits 1 if generated files would change)
"""
import csv, importlib.util, io, json, re, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
KIT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import bonk  # noqa: E402
from bonk import Line, Choice, If, flag  # noqa: E402

CONTENT = KIT / 'content'
CHAPTER_IDS = ['ch01', 'ch02', 'ch03', 'ch04', 'ch05']


class BuildError(Exception):
    pass


class Strings:
    def __init__(self):
        self.en, self.vi = {}, {}

    def add(self, key, pair):
        en, vi = pair
        for table, text, lang in ((self.en, en, 'en'), (self.vi, vi, 'vi')):
            if not isinstance(text, str) or not text.strip():
                raise BuildError(f'{key}: empty {lang} text')
            if key in table and table[key] != text:
                raise BuildError(f'{key}: conflicting {lang} text')
            table[key] = text
        return key


def load_chapter(cid):
    path = HERE / 'chapters' / f'{cid}.py'
    if not path.exists():
        return None
    spec = importlib.util.spec_from_file_location(f'chapter_{cid}', path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod.CHAPTER


def say_effects(strings, key_prefix, lines, speakers_used):
    out = []
    for n, line in enumerate(lines, 1):
        key = key_prefix if n == 1 else f'{key_prefix}.{n}'
        strings.add(key, (line.en, line.vi))
        speakers_used.add(line.speaker)
        eff = {'queueDialogue': {'key': key}}
        if line.speaker and line.speaker != 'narrator':
            eff['queueDialogue']['speaker'] = line.speaker
        out.append(eff)
    return out


def yarn_text(s):
    s = re.sub(r'[#{}<>\[\]\\]', '', s).replace('//', '/').replace(':', ' -').strip()
    return s or '...'


class Compiler:
    def __init__(self):
        self.s = Strings()
        self.puzzles, self.events, self.scenes, self.ambient, self.items = [], [], [], [], []
        self.chapters, self.yarn, self.line_speakers = [], {}, {}
        self.speakers_used = set()
        self.hotspot_ids, self.rule_ids, self.item_ids = set(), set(), set()

    def rule(self, rule, bucket):
        if rule['id'] in self.rule_ids:
            raise BuildError(f'duplicate rule {rule["id"]}')
        self.rule_ids.add(rule['id'])
        bucket.append(rule)

    def event(self, etype, puzzle_id, description):
        if any(e['type'] == etype for e in self.events):
            raise BuildError(f'duplicate event {etype}')
        entry = {'type': etype, 'description': description}
        if puzzle_id:
            entry = {'type': etype, 'puzzleId': puzzle_id, 'description': description}
        self.events.append(entry)

    def compile_chapter(self, ch, index, prev_complete_flag):
        s = self.s
        s.add(f'txt.chapter.{ch.id}.title', ch.title)
        s.add(f'txt.chapter.{ch.id}.objective', ch.objective)
        s.add(f'txt.chapter.{ch.id}.reveal', ch.reveal)
        complete_flag = f'flag.{ch.id}.complete'

        for item in ch.items:
            if item.id in self.item_ids:
                raise BuildError(f'duplicate item {item.id}')
            self.item_ids.add(item.id)
            self.items.append({'id': item.id, 'chapter': ch.id, 'labelKey': s.add(f'txt.item.{item.id}', item.label),
                               'descKey': s.add(f'txt.item.{item.id}.desc', item.desc), 'color': item.color})

        for scene in ch.scenes:
            self.compile_scene(ch, scene)

        puzzle_ids = [p.id for p in ch.puzzles]
        for i, p in enumerate(ch.puzzles):
            prereqs = p.prereqs if p.prereqs is not None else (
                [f'flag.{puzzle_ids[i - 1]}.complete'] if i > 0 else ([prev_complete_flag] if prev_complete_flag else []))
            last = i == len(ch.puzzles) - 1
            self.compile_puzzle(ch, p, prereqs, complete_flag if last else None)

        for conv in ch.conversations:
            self.compile_conversation(ch, conv)
        for etype, desc in ch.extra_events:
            self.event(etype, None, desc)
        for key, pair in ch.extra_strings.items():
            s.add(key, pair)
        for rule in ch.extra_rules:
            self.rule(rule, self.ambient)
            for e in rule.get('effects', []):
                if 'queueDialogue' in e:
                    self.speakers_used.add(e['queueDialogue'].get('speaker', 'narrator'))

        self.chapters.append({
            'id': ch.id, 'title': ch.title[0], 'locations': [sc.id for sc in ch.scenes], 'objective': ch.objective[0],
            'reveal': ch.reveal[0], 'puzzles': puzzle_ids,
            'requiredCallbacks': ['method-dependent NPC reaction', 'Tí/Bốp quiet beat', 'clue to next chapter'],
            'chapterCompleteFlag': complete_flag, 'startScene': ch.scenes[0].id,
            'titleKey': f'txt.chapter.{ch.id}.title', 'objectiveKey': f'txt.chapter.{ch.id}.objective',
            'revealKey': f'txt.chapter.{ch.id}.reveal',
        })
        return complete_flag

    def compile_scene(self, ch, scene):
        s = self.s
        hotspots = []
        for h in scene.hotspots:
            if h.id in self.hotspot_ids:
                raise BuildError(f'duplicate hotspot {h.id}')
            if not h.id.isascii():
                raise BuildError(f'hotspot id must be ASCII: {h.id}')
            self.hotspot_ids.add(h.id)
            entry = {'id': h.id, 'kind': h.kind, 'labelKey': s.add(f'txt.hotspot.{h.id}', h.label),
                     'x': h.x, 'y': h.y, 'w': h.w, 'h': h.h}
            if h.color: entry['color'] = h.color
            if h.character: entry['character'] = h.character
            if h.visible: entry['visibleWhen'] = h.visible
            if h.kind == 'Exit':
                if not h.exit_to: raise BuildError(f'{h.id}: exit without exit_to')
                entry['exitTo'] = h.exit_to
                entry['startUnlocked'] = h.start_unlocked
            hotspots.append(entry)

            base = [{'currentScene': scene.id}, {'requestTarget': h.id}]
            if h.look:
                verbs = {'any': [{'requestVerb': 'Inspect'}, {'requestVerb': 'Tap'}]}
                self.rule({'id': f'rule.look.{h.id}', 'priority': 1, 'when': {'all': base + [verbs]},
                           'effects': say_effects(s, f'dlg.look.{h.id}', [h.look], self.speakers_used), 'once': False},
                          self.ambient)
            elif h.kind != 'Npc':
                raise BuildError(f'{h.id}: props and exits need a look line (every touch gives feedback)')
            if h.kind == 'Npc' and not any(tk.when is None for tk in h.talk):
                raise BuildError(f'{h.id}: NPC needs a default talk entry (when=None)')
            for n, tk in enumerate(h.talk):
                when = base + [{'requestVerb': 'Talk'}] + ([tk.when] if tk.when else [])
                effects = say_effects(s, f'dlg.talk.{h.id}.{n + 1}', tk.lines, self.speakers_used) + list(tk.effects)
                self.rule({'id': f'rule.talk.{h.id}.{n + 1:02d}', 'priority': 40 - n, 'when': {'all': when},
                           'effects': effects, 'once': tk.once}, self.ambient)

        if scene.enter:
            self.rule({'id': f'rule.enter.{scene.id}', 'priority': 50,
                       'when': {'all': [{'currentScene': scene.id}, {'requestVerb': 'Enter'}]},
                       'effects': [{'startConversation': {'node': scene.enter}}], 'once': True}, self.ambient)
        self.scenes.append({'id': scene.id, 'chapter': ch.id, 'nameKey': s.add(f'txt.scene.{scene.id}', scene.name),
                            'background': scene.bg, 'music': scene.music, 'hotspots': hotspots})

    def compile_puzzle(self, ch, p, prereqs, chapter_complete_flag):
        s, pid = self.s, p.id
        complete = f'flag.{pid}.complete'
        s.add(f'txt.{pid}.title', p.title)
        s.add(f'txt.{pid}.goal', p.goal)
        s.add(f'txt.{pid}.ruleTwist', p.twist)
        if len(p.hints) != 3: raise BuildError(f'{pid}: exactly 3 hints required')
        hints = [s.add(f'dlg.{pid}.hint{i + 1}', h) for i, h in enumerate(p.hints)]
        fail_key = s.add(f'dlg.{pid}.fail', (p.fail.en, p.fail.vi))
        self.speakers_used.add(p.fail.speaker)

        rules, solutions, targets = [], [], []
        first_signatures = set()
        for route in p.routes:
            r = route.key
            if not route.steps: raise BuildError(f'{pid}.{r}: no steps')
            sig = (route.steps[0].verb, route.steps[0].target, route.steps[0].src)
            if sig in first_signatures: raise BuildError(f'{pid}: routes start with the same action {sig}')
            first_signatures.add(sig)
            trace = []
            for i, st in enumerate(route.steps):
                final = i == len(route.steps) - 1
                step_flag = f'flag.{pid}.{r}{i + 1}'
                when = [{'currentScene': p.scene}, flag(complete, False), {'requestVerb': st.verb},
                        {'requestTarget': st.target}]
                if st.src:
                    when += [{'requestSource': st.src}, bonk.has(st.src)]
                if i > 0: when.append(flag(f'flag.{pid}.{r}{i}'))
                if not final: when.append(flag(step_flag, False))
                if st.requires: when.append(st.requires)
                effects = [] if final else [set_flag_eff(step_flag)]
                effects += [bonk.give(x) for x in st.gives] + [bonk.take(x) for x in st.takes] + list(st.effects)
                effects += say_effects(s, f'dlg.{pid}.{r}.s{i + 1}', st.say, self.speakers_used)
                if final:
                    effects = [set_flag_eff(complete), bonk.emit(f'evt.{pid}.solved.{r}')] + effects + list(route.effects)
                    effects += say_effects(s, f'dlg.{pid}.success.{r}', route.success, self.speakers_used)
                    effects += [bonk.unlock(x) for x in p.unlocks] + list(p.after)
                    if chapter_complete_flag: effects.append(set_flag_eff(chapter_complete_flag))
                rid = f'rule.{pid}.{r}' if final else f'rule.{pid}.{r}.s{i + 1}'
                rule = {'id': rid, 'priority': 100 + 10 * i, 'when': {'all': when}, 'effects': effects, 'once': True}
                self.rule(rule, rules)
                step = {'verb': st.verb, 'targetId': st.target}
                if st.src: step['sourceId'] = st.src
                trace.append(step)
                if st.target not in targets: targets.append(st.target)
            solutions.append({'id': f'{pid}.{r}', 'strategy': route.strategy[0], 'steps': trace,
                              'ruleId': f'rule.{pid}.{r}', 'eventId': f'evt.{pid}.solved.{r}'})
            s.add(f'txt.{pid}.strategy.{r}', route.strategy)
            self.event(f'evt.{pid}.solved.{r}', pid, f'{p.title[0]}: solved by {route.strategy[0]}')

        puzzle = {
            'id': pid, 'chapter': ch.id, 'scene': p.scene, 'title': p.title[0], 'goal': p.goal[0], 'ruleTwist': p.twist[0],
            'featuredNPC': p.npc, 'hotspots': targets, 'prerequisites': prereqs, 'rules': rules, 'solutions': solutions,
            'failureResponses': [{'condition': 'unmatched', 'dialogueKey': fail_key, 'speaker': p.fail.speaker}],
            'hints': hints, 'consequenceEvents': [f'evt.{pid}.solved.{r.key}' for r in p.routes],
            'callbacks': [f'callback.{pid}.method'],
            'testCases': [{'name': f'{pid}.solution.{r.key}', 'expectEvent': f'evt.{pid}.solved.{r.key}'} for r in p.routes]
                         + [{'name': f'{pid}.fallback', 'expectFeedback': fail_key}],
        }
        if p.item: puzzle['inventoryItem'] = p.item
        self.puzzles.append(puzzle)

    def compile_conversation(self, ch, conv):
        if conv.node in self.yarn: raise BuildError(f'duplicate conversation {conv.node}')
        if not re.match(r'^[A-Za-z][A-Za-z0-9_]*$', conv.node): raise BuildError(f'bad node name {conv.node}')
        counter = {'n': 0}
        out = [f'title: {conv.node}', 'tags: ' + ch.id, '---']
        self.emit_body(conv.node, conv.body, out, 0, counter)
        out.append('===')
        self.yarn[conv.node] = (ch.id, '\n'.join(out))

    def next_id(self, node, counter, kind='l'):
        counter['n'] += 1
        return f'{node}.{kind}{counter["n"]:03d}'

    def emit_body(self, node, body, out, depth, counter):
        pad = '    ' * depth
        for item in body:
            if isinstance(item, Line):
                lid = self.next_id(node, counter)
                self.s.add(f'yarn.{lid}', (item.en, item.vi))
                self.line_speakers[f'yarn.{lid}'] = item.speaker
                self.speakers_used.add(item.speaker)
                prefix = '' if item.speaker == 'narrator' else f'{item.speaker}: '
                out.append(f'{pad}{prefix}{yarn_text(item.en)} #line:{lid}')
            elif isinstance(item, Choice):
                for opt in item.options:
                    oid = self.next_id(node, counter, 'o')
                    self.s.add(f'yarn.{oid}', opt.label)
                    self.line_speakers[f'yarn.{oid}'] = 'ti'
                    out.append(f'{pad}-> {yarn_text(opt.label[0])} #line:{oid}')
                    if opt.act:
                        out.append(f'{pad}    <<act {opt.act}>>')
                        self.compile_choice(opt)
                    self.emit_body(node, opt.lines, out, depth + 1, counter)
            elif isinstance(item, If):
                out.append(f'{pad}<<if {item.condition}>>')
                self.emit_body(node, item.then, out, depth + 1, counter)
                if item.otherwise:
                    out.append(f'{pad}<<else>>')
                    self.emit_body(node, item.otherwise, out, depth + 1, counter)
                out.append(f'{pad}<<endif>>')
            else:
                raise BuildError(f'{node}: unsupported body item {item!r}')

    def compile_choice(self, opt):
        etype = f'evt.{opt.act}'
        self.event(etype, None, f'Dialogue choice {opt.act}')
        self.rule({'id': f'rule.{opt.act}', 'priority': 200,
                   'when': {'all': [{'requestVerb': 'Talk'}, {'requestTarget': opt.act}]},
                   'effects': [bonk.emit(etype)] + list(opt.effects), 'once': True}, self.ambient)

    def finish(self):
        s = self.s
        for sp in sorted(self.speakers_used):
            if sp == 'narrator': continue
            if sp not in bonk.SPEAKERS: raise BuildError(f'unknown speaker {sp}')
            s.add(f'ui.speaker.{sp}', bonk.SPEAKERS[sp])
        ui_path = HERE / 'ui_strings.py'
        if ui_path.exists():
            spec = importlib.util.spec_from_file_location('ui_strings', ui_path)
            mod = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(mod)
            for key, pair in mod.UI.items():
                s.add(key, pair)


def set_flag_eff(key, value=True):
    return {'setFlag': {'key': key, 'value': value}}


def dump(obj):
    return json.dumps(obj, ensure_ascii=False, indent=2) + '\n'


def manifest_text(comp):
    path = CONTENT / 'assets' / 'manifest.csv'
    rows = list(csv.DictReader(path.open(encoding='utf-8'))) if path.exists() else []
    known = {r['asset_id'] for r in rows}
    wanted = [(i['id'], 'prop', i['chapter']) for i in comp.items]
    wanted += [(sc['id'], 'background', sc['chapter']) for sc in comp.scenes]
    wanted += [(f'chr.{sp}', 'character', 'all') for sp in sorted(comp.speakers_used) if sp != 'narrator']
    for asset_id, kind, used_by in wanted:
        if asset_id not in known:
            rows.append({'asset_id': asset_id, 'type': kind, 'used_by': used_by, 'status': 'concept-needed',
                         'license': 'unlicensed/unproduced'})
            known.add(asset_id)
    buf = io.StringIO()
    w = csv.DictWriter(buf, fieldnames=['asset_id', 'type', 'used_by', 'status', 'license'], lineterminator='\n')
    w.writeheader()
    w.writerows(rows)
    return buf.getvalue()


def build():
    comp = Compiler()
    prev = None
    for i, cid in enumerate(CHAPTER_IDS):
        ch = load_chapter(cid)
        if ch is None:
            raise BuildError(f'missing chapter spec {cid}')
        if ch.id != cid: raise BuildError(f'{cid}.py defines {ch.id}')
        prev = comp.compile_chapter(ch, i, prev)
    comp.finish()
    files = {
        'puzzles/puzzles.json': dump(comp.puzzles),
        'events/catalog.json': dump(comp.events),
        'scenes/scenes.json': dump(comp.scenes),
        'scenes/ambient.json': dump(comp.ambient),
        'items/items.json': dump(comp.items),
        'dialogue/lines.json': dump(dict(sorted(comp.line_speakers.items()))),
        'localization/en.json': dump(dict(sorted(comp.s.en.items()))),
        'localization/vi.json': dump(dict(sorted(comp.s.vi.items()))),
        'assets/manifest.csv': manifest_text(comp),
    }
    for ch in comp.chapters:
        files[f'chapters/{ch["id"]}.json'] = dump(ch)
    for cid in CHAPTER_IDS:
        nodes = [src for (owner, src) in comp.yarn.values() if owner == cid]
        files[f'dialogue/{cid}.yarn'] = '\n\n'.join(nodes) + '\n'
    return files


def main(argv):
    try:
        files = build()
    except BuildError as e:
        print(f'BUILD ERROR: {e}')
        return 1
    changed = [rel for rel, text in files.items()
               if not (CONTENT / rel).exists() or (CONTENT / rel).read_text(encoding='utf-8') != text]
    if '--check' in argv:
        if changed:
            print('STALE: ' + ', '.join(changed) + ' (run tools/author/build.py)')
            return 1
        print('generated content up to date')
        return 0
    for rel in changed:
        (CONTENT / rel).parent.mkdir(parents=True, exist_ok=True)
        (CONTENT / rel).write_text(files[rel], encoding='utf-8')
    print(f'wrote {len(changed)} file(s)')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
