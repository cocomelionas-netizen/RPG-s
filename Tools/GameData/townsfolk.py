"""Generates Assets/_Project/Scripts/Editor/TownsfolkData.cs from the source game's client data.

    python3 -I Tools/GameData/townsfolk.py <path to the source data checkout>

Townsfolk: every friendly NPC of the island (npc + npc_template, faction 1) that is not a vendor (those come from
world.py), where it stands (island.cell_of), its name, subname, model (npc_models: how it is dressed here) and gossip
menu. Animals (horses, pigs, boars) are left out: there is no art for them here.

Gossip: every gossip text (gossip: the menu, the text, up to three conditions), every option (gossip_option: the
menu, the text, the menu it opens, the script it runs and its conditions) and every script step (scripts). Conditions
are (type, value, must be true): 1 the player has the quest, 2 turned it in, 3 has it finished in the log, 7 has the
aura of a spell, 12 has or did the quest, 15 is above a level. Script commands: 1 cast a spell on the player, 2 talk
credit for an NPC (quests), 3 the NPC says a world text, 10 point the way to an NPC, 11 the bank, 12 respec, 13 the
arena. The spells, NPCs and world texts they name are written out by name.

The source game's own name is replaced by "the island" in every text.
"""
import os
import re
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import names  # noqa: E402
from island import cell_of, on_island  # noqa: E402
from world import read_vendors  # noqa: E402

ANIMALS = {'horse', 'horse_tack', 'Pig', 'boar'}


def number(value, default=0):
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def game_name(root):
    """The source game's name, read from its executable's file name (so it is never written here)."""
    return next(f[:-4] for f in os.listdir(os.path.join(root, 'bin')) if f.endswith('.exe'))


def cleaner(name):
    def clean(text):
        text = (text or '').replace('\r\n', '\n').replace('\r', '\n')
        text = re.sub(r'\bisland we call ' + name + r'\b', 'island of ours', text, flags=re.I)
        text = re.sub(r'\b' + name + r' Island\b', 'the island', text, flags=re.I)
        text = re.sub(r'\b' + name + r'\b', 'the island', text, flags=re.I)
        return text.strip('\n')
    return clean


def conditions(row):
    """The (type, value, must) conditions of a gossip or option row (condition1..3 and their _true flags)."""
    found = []
    for i in (1, 2, 3):
        kind = number(row[f'condition{i}'])
        if kind:
            found.append((kind, number(row[f'condition{i}_value1']), number(row[f'condition{i}_true']) == 1))
    return found


def main(root):
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor',
                       'TownsfolkData.cs')
    db = sqlite3.connect(os.path.join(root, 'game.db'))
    db.row_factory = sqlite3.Row
    clean = cleaner(game_name(root))
    vendors = {v['entry'] for v in read_vendors(db)}

    people = {}
    for row in db.execute('select t.entry, t.name, t.subname, t.gossip_menu_id, m.name as model, n.position_x, n.position_y '
                          'from npc n join npc_template t on t.entry = n.entry left join npc_models m on m.id = t.model_id '
                          'where n.map = 1 and t.faction = 1 order by n.guid'):
        if row['entry'] in vendors or row['model'] in ANIMALS or not on_island(row['position_x'], row['position_y']):
            continue
        person = people.setdefault(row['entry'], {
            'entry': row['entry'], 'name': row['name'], 'title': (row['subname'] or '').strip(), 'model': row['model'] or '',
            'gossip': number(row['gossip_menu_id']), 'cells': [],
        })
        person['cells'].append(cell_of(row['position_x'], row['position_y']))

    spell_names = {r['entry']: r['name'] for r in db.execute('select entry, name from spell_template')}
    spell_seconds = {r['entry']: number(r['duration']) / 1000 for r in db.execute('select entry, duration from spell_template')}
    npc_names = {r['entry']: r['name'] for r in db.execute('select entry, name from npc_template')}
    world_texts = {r['id']: clean(r['string']) for r in db.execute('select id, string from world_texts')}

    texts = []
    for row in db.execute('select * from gossip order by gossipId, entry'):
        texts.append((row['entry'], row['gossipId'], clean(row['text']), conditions(row)))
    options = []
    for row in db.execute('select * from gossip_option order by gossipId, entry'):
        options.append((row['entry'], row['gossipId'], clean(row['text']), number(row['click_new_gossip']),
                        number(row['click_script']), conditions(row)))
    steps = []
    for row in db.execute('select * from scripts order by scriptId, delay, entry'):
        command, data = number(row['command']), number(row['data1'])
        name = spell_names.get(data, '') if command == 1 else npc_names.get(data, '') if command in (2, 10) \
            else world_texts.get(data, '') if command == 3 else ''
        seconds = spell_seconds.get(data, 0) if command == 1 else 0
        steps.append((number(row['scriptId']), number(row['delay']), command, data, name, seconds))

    leftover = [t for t in [p['name'] for p in people.values()] + [t[2] for t in texts] + [o[2] for o in options]
                if game_name(root).lower() in t.lower()]
    assert not leftover, leftover
    write(out, sorted(people.values(), key=lambda p: p['name']), texts, options, steps, spell_names)
    print(f'townsfolk: {len(people)} ({sum(len(p["cells"]) for p in people.values())} placed); '
          f'gossip: {len(texts)} texts, {len(options)} options, {len(steps)} script steps')


def cs(text):
    # Every name and text written goes through this project's names (names.py).
    text = names.rename(text)
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\t', '\\t') + '"'


def write(path, people, texts, options, steps, spell_names):
    def conds(found):
        return ''.join(f', new GossipCondition({kind}, {value}, {"true" if must else "false"}'
                       + (f', {cs(spell_names.get(value, ""))}' if kind == 7 else '') + ')' for kind, value, must in found)

    with open(path, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/townsfolk.py from the source game\'s game.db (npc, npc_template, npc_models,\n')
        f.write('// gossip, gossip_option, scripts): the friendly NPCs of the island that are not vendors, and what everyone says.\n')
        f.write('using RPG.World;\nusing UnityEngine;\n\nnamespace RPG.EditorTools\n{\n    internal static class TownsfolkData\n    {\n')
        f.write('        public struct Person\n        {\n            public int entry, gossip;\n            public string name, title, model;\n')
        f.write('            public Vector3Int[] cells;\n        }\n\n')
        f.write('        public static readonly Person[] People =\n        {\n')
        for p in people:
            cells = ', '.join(f'new Vector3Int({x}, {y}, 0)' for x, y in p['cells'])
            f.write(f'            new Person {{ entry = {p["entry"]}, name = {cs(p["name"])}, title = {cs(p["title"])}, '
                    f'model = {cs(p["model"])}, gossip = {p["gossip"]}, cells = new[] {{ {cells} }} }},\n')
        f.write('        };\n\n')
        f.write('        public static readonly GossipText[] Texts =\n        {\n')
        for entry, menu, text, found in texts:
            f.write(f'            new GossipText({entry}, {menu}, {cs(text)}{conds(found)}),\n')
        f.write('        };\n\n')
        f.write('        public static readonly GossipOption[] Options =\n        {\n')
        for entry, menu, text, next_menu, script, found in options:
            f.write(f'            new GossipOption({entry}, {menu}, {cs(text)}, {next_menu}, {script}{conds(found)}),\n')
        f.write('        };\n\n')
        f.write('        public static readonly GossipScriptStep[] Scripts =\n        {\n')
        for script, delay, command, data, name, seconds in steps:
            f.write(f'            new GossipScriptStep({script}, {delay}, {command}, {data}, {cs(name)}, {seconds:g}f),\n')
        f.write('        };\n    }\n}\n')


if __name__ == '__main__':
    main(sys.argv[1])
