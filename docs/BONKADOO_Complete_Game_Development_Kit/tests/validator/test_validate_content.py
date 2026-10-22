"""Each case mutates a temp copy of the real content and asserts the validator rejects it.

Run: python3 -m unittest discover -s tests/validator
"""
import json, shutil, sys, tempfile, unittest
from pathlib import Path

KIT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(KIT / 'tools'))
import validate_content  # noqa: E402


class Fixture:
    def __init__(self):
        self.dir = Path(tempfile.mkdtemp(prefix='bonkadoo-validator-'))
        shutil.copytree(KIT / 'content', self.dir / 'content')
        shutil.copytree(KIT / 'schemas', self.dir / 'schemas')

    def edit(self, rel, fn):
        path = self.dir / rel
        data = json.loads(path.read_text(encoding='utf-8'))
        fn(data)  # mutates in place
        path.write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')

    def errors(self):
        return validate_content.validate(self.dir).errors

    def close(self):
        shutil.rmtree(self.dir, ignore_errors=True)


def first_rule(puzzles):
    return puzzles[0]['rules'][0]


class ValidatorRejectsInvalidContent(unittest.TestCase):
    def setUp(self):
        self.f = Fixture()

    def tearDown(self):
        self.f.close()

    def assertRejected(self, needle):
        errors = self.f.errors()
        self.assertTrue(any(needle in e for e in errors), f'expected error containing {needle!r}, got {errors[:5]}')

    def test_real_catalog_passes(self):
        self.assertEqual([], self.f.errors())

    def test_duplicate_puzzle_id(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[1].__setitem__('id', p[0]['id']))
        self.assertRejected('Duplicate puzzle')

    def test_bad_puzzle_id(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0].__setitem__('id', 'Chapter1-Puzzle1'))
        self.assertRejected('bad puzzle id')

    def test_missing_required_field(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0].pop('hints'))
        self.assertRejected('missing required field')

    def test_unknown_field(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0].__setitem__('secretBackdoor', True))
        self.assertRejected('unknown field secretBackdoor')

    def test_non_allowlisted_predicate(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: first_rule(p).__setitem__('when', {'eval': '1==1'}))
        self.assertRejected('not allowlisted')

    def test_non_allowlisted_effect(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: first_rule(p)['effects'].append({'runScript': {'code': 'x'}}))
        self.assertRejected('not allowlisted')

    def test_write_to_transient_input(self):
        self.f.edit('content/puzzles/puzzles.json',
                    lambda p: first_rule(p)['effects'].append({'setFlag': {'key': 'input.ch01.p01.choice', 'value': 'a'}}))
        self.assertRejected('cannot write transient key')

    def test_emitted_event_not_in_catalog(self):
        self.f.edit('content/puzzles/puzzles.json',
                    lambda p: first_rule(p)['effects'].append({'emitEvent': {'type': 'evt.ch01.p01.ghost'}}))
        self.assertRejected('evt.ch01.p01.ghost not in catalog')

    def test_solution_rule_missing(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0]['solutions'][0].__setitem__('ruleId', 'rule.nope'))
        self.assertRejected('missing rule rule.nope')

    def test_single_solution(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0].__setitem__('solutions', p[0]['solutions'][:1]))
        self.assertRejected('fewer than 2 solutions')

    def test_step_target_not_hotspot(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0]['solutions'][0]['steps'][0].__setitem__('targetId', 'prop.nowhere'))
        self.assertRejected('not a hotspot')

    def test_unknown_verb(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0]['solutions'][0]['steps'][0].__setitem__('verb', 'Lick'))
        self.assertRejected('unknown verb')

    def test_unsatisfiable_prerequisite(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[1].__setitem__('prerequisites', ['flag.never.set']))
        self.assertRejected('never set by any rule')

    def test_missing_localization(self):
        self.f.edit('content/localization/vi.json', lambda t: t.pop('dlg.ch01.p01.hint1'))
        self.assertRejected('missing localization dlg.ch01.p01.hint1')

    def test_empty_translation(self):
        self.f.edit('content/localization/vi.json', lambda t: t.__setitem__('dlg.ch01.p01.hint1', '  '))
        self.assertRejected('empty text')

    def test_placeholder_mismatch(self):
        self.f.edit('content/localization/en.json', lambda t: t.__setitem__('dlg.ch01.p01.hint1', 'Hi {name}'))
        self.assertRejected('placeholder mismatch')

    def test_puzzle_missing_from_chapters(self):
        self.f.edit('content/chapters/ch01.json', lambda c: c['puzzles'].remove('ch01.p09'))
        self.assertRejected('ch01.p09: not listed in any chapter')

    def test_chapter_lists_unknown_puzzle(self):
        self.f.edit('content/chapters/ch01.json', lambda c: c['puzzles'].append('ch01.p10'))
        self.assertRejected('unknown puzzle ch01.p10')

    def test_duplicate_event_in_catalog(self):
        self.f.edit('content/events/catalog.json', lambda e: e.append(dict(e[0])))
        self.assertRejected('Duplicate event IDs')

    def test_undefined_metric(self):
        self.f.edit('content/puzzles/puzzles.json',
                    lambda p: first_rule(p)['effects'].append({'adjustMetric': {'key': 'happiness', 'delta': 1}}))
        self.assertRejected('metric happiness not defined')

    def test_metric_delta_must_be_integer(self):
        self.f.edit('content/puzzles/puzzles.json',
                    lambda p: first_rule(p)['effects'].append({'adjustMetric': {'key': 'trust_bop', 'delta': '1'}}))
        self.assertRejected('must be integer')

    def test_malformed_entry_is_an_error_not_a_crash(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p.append('not a puzzle'))
        self.assertRejected('must be an object')

    def test_scene_outside_chapter(self):
        self.f.edit('content/puzzles/puzzles.json', lambda p: p[0].__setitem__('scene', 'scene.ch02.01'))
        self.assertRejected('outside chapter')


if __name__ == '__main__':
    unittest.main()
