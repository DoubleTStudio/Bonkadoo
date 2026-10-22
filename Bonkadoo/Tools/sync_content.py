#!/usr/bin/env python3
"""Copy the kit's authoritative content into Unity Resources. --check exits 1 on drift (CI)."""
import filecmp, shutil, sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
KIT = PROJECT.parent / 'docs' / 'BONKADOO_Complete_Game_Development_Kit' / 'content'
DEST = PROJECT / 'Assets' / 'Bonkadoo' / 'Content' / 'Resources' / 'BonkadooContent'
CHAPTERS = [f'ch0{i}' for i in range(1, 6)]
# source -> destination; yarn ships as .txt so Unity's Yarn importer leaves it alone (compiled at runtime)
FILES = {rel: rel for rel in ['world/world.json', 'puzzles/puzzles.json', 'events/catalog.json', 'scenes/scenes.json',
                              'scenes/ambient.json', 'items/items.json', 'dialogue/lines.json',
                              'localization/en.json', 'localization/vi.json'] + [f'chapters/{c}.json' for c in CHAPTERS]}
FILES.update({f'dialogue/{c}.yarn': f'dialogue/{c}.txt' for c in CHAPTERS})

def main():
    check = '--check' in sys.argv
    drift = []
    for rel, out in FILES.items():
        src, dst = KIT / rel, DEST / out
        if not src.exists():
            sys.exit(f'missing source {src}')
        if dst.exists() and filecmp.cmp(src, dst, shallow=False):
            continue
        drift.append(rel)
        if not check:
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(src, dst)
    if check and drift:
        print('DRIFT: ' + ', '.join(drift) + ' (run Tools/sync_content.py)')
        sys.exit(1)
    print(('in sync' if check else f'synced {len(drift)} file(s)'))

if __name__ == '__main__':
    main()
