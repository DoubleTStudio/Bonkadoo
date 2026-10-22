"""BONKADOO authoring DSL (ADR-0003).

Chapter specs in tools/author/chapters/chNN.py build these objects; build.py compiles them into
content/*.json, content/dialogue/*.yarn and the VI/EN tables. Generated files are committed and
are what the game and validator read. Edit the specs, never the generated JSON.
"""
from dataclasses import dataclass, field
from typing import List, Optional, Tuple, Union

SPEAKERS = {
    'ti': ('Tí', 'Tí'), 'bop': ('Bốp', 'Bốp'), 'vuong': ('Director Vượng', 'Giám đốc Vượng'),
    'coc': ('Officer Cộc', 'Cán bộ Cộc'), 'mai': ('Mai', 'Chị Mai'), 'lan': ('Archivist Lan', 'Lưu trữ viên Lan'),
    'logic': ('LOGIC', 'LOGIC'), 'narrator': ('', ''), 'clerk': ('Clerk', 'Nhân viên'),
    'janitor': ('Janitor Tư', 'Chú lao công Tư'), 'guard': ('Guard', 'Bảo vệ'), 'crowd': ('Crowd', 'Đám đông'),
    'inspector': ('Inspector', 'Thanh tra'), 'mayor': ('Mayor', 'Thị trưởng'), 'curator': ('Curator', 'Quản lý bảo tàng'),
    'printer': ('Printer', 'Máy in'), 'baker': ('Baker Hùng', 'Thợ bánh Hùng'), 'sign': ('Sign', 'Biển báo'),
}

T = Tuple[str, str]  # (english, vietnamese)


def t(en: str, vi: str) -> T:
    return (en, vi)


@dataclass
class Line:
    speaker: str
    en: str
    vi: str


def L(speaker, en, vi):
    return Line(speaker, en, vi)


# ---- predicates (allowlisted DSL, ADR-0002) ----
def ev(event_type): return {'eventOccurred': {'type': event_type}}
def flag(key, value=True): return {'flagEquals': {'key': key, 'value': value}}
def has(item): return {'hasItem': {'itemId': item}}
def metric_at_least(key, value): return {'metricAtLeast': {'key': key, 'value': value}}
def npc_at_least(npc, value): return {'npcDispositionAtLeast': {'npcId': npc, 'value': value}}
def all_(*ps): return {'all': list(ps)}
def any_(*ps): return {'any': list(ps)}
def not_(p): return {'not': p}
def done(pid): return flag(f'flag.{pid}.complete')
def solved(pid, route): return ev(f'evt.{pid}.solved.{route}')


# ---- effects ----
def metric(key, delta): return {'adjustMetric': {'key': key, 'delta': delta}}
def npc(npc_id, value): return {'setNpcDisposition': {'npcId': npc_id, 'value': value}}
def set_flag(key, value=True): return {'setFlag': {'key': key, 'value': value}}
def emit(event_type): return {'emitEvent': {'type': event_type}}
def give(item): return {'addItem': {'itemId': item}}
def take(item): return {'removeItem': {'itemId': item}}
def unlock(exit_id): return {'unlockExit': {'exitId': exit_id}}
def converse(node): return {'startConversation': {'node': node}}
def anim(cue): return {'queueAnimation': {'cue': cue}}


INSPECT, TALK, USE, COMBINE, TAP = 'Inspect', 'Talk', 'UseItem', 'CombineItems', 'Tap'


@dataclass
class Talk:
    """NPC talk line used when `when` holds. Listed first = checked first; one entry should have when=None."""
    lines: List[Line]
    when: Optional[dict] = None
    effects: List[dict] = field(default_factory=list)
    once: bool = False


@dataclass
class Hotspot:
    id: str
    kind: str  # Prop | Npc | Exit
    label: T
    x: float
    y: float
    w: float = 0.12
    h: float = 0.2
    look: Optional[Line] = None
    talk: List[Talk] = field(default_factory=list)
    visible: Optional[dict] = None
    exit_to: Optional[str] = None
    start_unlocked: bool = False
    color: Optional[str] = None
    character: Optional[str] = None  # speaker id drawn for Npc hotspots


@dataclass
class Item:
    id: str
    label: T
    desc: T
    color: str = '#d9b26f'


@dataclass
class Step:
    verb: str
    target: str
    src: Optional[str] = None
    gives: List[str] = field(default_factory=list)
    takes: List[str] = field(default_factory=list)
    say: List[Line] = field(default_factory=list)
    effects: List[dict] = field(default_factory=list)
    requires: Optional[dict] = None


@dataclass
class Route:
    key: str  # 'a' | 'b'
    strategy: T
    steps: List[Step]
    success: List[Line]
    effects: List[dict] = field(default_factory=list)


@dataclass
class Puzzle:
    id: str
    scene: str
    npc: str
    title: T
    goal: T
    twist: T
    routes: List[Route]
    fail: Line
    hints: List[T]
    item: Optional[str] = None
    unlocks: List[str] = field(default_factory=list)
    after: List[dict] = field(default_factory=list)  # extra effects on completion, e.g. converse('ch01_beat')
    prereqs: Optional[List[str]] = None  # default: previous puzzle in chapter


@dataclass
class Option:
    label: T
    act: Optional[str] = None  # choice target id; its rule applies `effects` through the engine
    effects: List[dict] = field(default_factory=list)
    lines: List[Union[Line, 'If']] = field(default_factory=list)


@dataclass
class Choice:
    options: List[Option]


@dataclass
class If:
    condition: str  # Yarn expression over read-only functions: metric("x"), has_event("evt"), flag("k"), flag_text("k")
    then: List[Union[Line, 'If', Choice]]
    otherwise: List[Union[Line, 'If', Choice]] = field(default_factory=list)


@dataclass
class Conversation:
    node: str
    body: List[Union[Line, Choice, If]]


@dataclass
class Scene:
    id: str
    name: T
    bg: str
    hotspots: List[Hotspot]
    music: str = 'calm'
    enter: Optional[str] = None  # conversation node on first entry


@dataclass
class Chapter:
    id: str
    title: T
    objective: T
    reveal: T
    scenes: List[Scene]
    items: List[Item]
    puzzles: List[Puzzle]
    conversations: List[Conversation]
    extra_events: List[Tuple[str, str]] = field(default_factory=list)  # (type, description)
    extra_rules: List[dict] = field(default_factory=list)  # raw ambient rules for special cases (endings)
    extra_strings: dict = field(default_factory=dict)  # key -> (en, vi) used by extra_rules


ENDINGS = [('patch', 'PATCH'), ('rewrite', 'REWRITE'), ('reveal', 'REVEAL'), ('walk', 'WALK_AWAY')]


def ending_rules(final_scene, chosen_lines, locked_lines, corrective_lines):
    """Chapter 5 ending choice + correctives (AUTHORING_GUIDE, ENDING_MATRIX).

    chosen_lines: {'patch': Line, ...} said when a door is chosen.
    locked_lines: {'rewrite': Line, 'reveal': Line} said when a gated door is still locked (explain the corrective).
    corrective_lines: {'testimony': Line, 'audit': Line} said when a corrective is completed.
    Returns (rules, strings) for Chapter(extra_rules=..., extra_strings=...).
    """
    rules, strings = [], {}

    def say(key, line):
        strings[key] = (line.en, line.vi)
        eff = {'queueDialogue': {'key': key}}
        if line.speaker != 'narrator':
            eff['queueDialogue']['speaker'] = line.speaker
        return eff

    ready = [{'currentScene': final_scene}, {'requestVerb': 'Inspect'},
             flag('flag.ch05.p08.complete'), flag('flag.ch05.p09.complete', False)]
    gates = {
        'rewrite': any_(metric_at_least('public_trust', 0), flag('flag.ch05.corrective.testimony')),
        'reveal': any_(metric_at_least('evidence_count', 4), flag('flag.ch05.corrective.audit')),
    }
    for short, value in ENDINGS:
        door = f'prop.ch05.door_{short}'
        when = ready + [{'requestTarget': door}] + ([gates[short]] if short in gates else [])
        rules.append({'id': f'rule.ending.choose.{short}', 'priority': 300, 'when': {'all': when},
                      'effects': [set_flag('flag.ending', value), say(f'dlg.ending.choose.{short}', chosen_lines[short])],
                      'once': False})
        if short in gates:
            rules.append({'id': f'rule.ending.locked.{short}', 'priority': 290,
                          'when': {'all': ready + [{'requestTarget': door}]},
                          'effects': [say(f'dlg.ending.locked.{short}', locked_lines[short])], 'once': False})
    after_p08 = [{'currentScene': final_scene}, flag('flag.ch05.p08.complete')]
    rules.append({'id': 'rule.ending.corrective.testimony', 'priority': 250,
                  'when': {'all': after_p08 + [{'requestVerb': 'Talk'}, {'requestTarget': 'npc.mai_ch05'}]},
                  'effects': [set_flag('flag.ch05.corrective.testimony'),
                              say('dlg.ending.corrective.testimony', corrective_lines['testimony'])], 'once': True})
    rules.append({'id': 'rule.ending.corrective.audit', 'priority': 250,
                  'when': {'all': after_p08 + [{'requestVerb': 'Inspect'}, {'requestTarget': 'prop.ch05.audit_terminal'}]},
                  'effects': [set_flag('flag.ch05.corrective.audit'),
                              say('dlg.ending.corrective.audit', corrective_lines['audit'])], 'once': True})
    return rules, strings


ENDING_CHOSEN = any_(*[flag('flag.ending', v) for _, v in ENDINGS])
