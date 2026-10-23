"""Writes Docs/Balance.md: a fight against a monster of your own level, level by level, with this project's numbers.

    python3 -I Tools/Balance/model.py

A model, not the game: one player with gear of its level (the weapon and armor material most often found there, plain,
no affixes, no attributes bought), auto-attacks only (spells come on top), a normal monster standing still. Keep the
constants below in step with the code they name when that code changes, then run it again.
"""
import math
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Docs', 'Balance.md')

# LevelCurve: the source game's experience tables.
EXPERIENCE = [100, 100, 123, 150, 195, 267, 373, 531, 760, 1087, 1548, 2194, 3091, 4331, 6031, 8352, 11510, 15788,
              21564, 29339, 39780, 53762, 72451, 97380]
KILL_EXPERIENCE = [20, 20, 24, 30, 39, 44, 62, 88, 126, 155, 186, 223, 268, 322, 386, 463, 556, 667, 800, 960, 1152,
                   1382, 1659, 1991, 2389]


def health_growth(level):  # LevelCurve.HealthGrowth
    return level * 1.5 if level > 20 else level


def damage_growth(level):  # LevelCurve.DamageGrowth
    return 2.0 * level ** 0.656


def monster_health(level):  # MonsterAI.SourceHealthAt
    return 60 * level ** (math.log(7340 / 60) / math.log(25))


# TestSceneBuilder monster tiers: (top level, average hit, seconds between hits, prefab level).
TIERS = [(2, 8, 2.0, 2), (5, 16, 2.2, 4), (99, 25, 2.0, 6)]


def monster_dps(level):  # tier damage x MonsterAI.DamageScaleBetween
    _, hit, interval, prefab = next(t for t in TIERS if level <= t[0])
    return hit * health_growth(level) / health_growth(prefab) / interval


def monster_gold(level):  # MonsterAI.GoldAt
    return (round(3.2 * level) + round(4.8 * level)) / 2


GEAR_CHANCE = 0.2        # LootDrop.GearChance
CLASS_SHARE = 0.6        # LootDrop.ClassGearShare
JUNK_CHANCE = 0.6        # TestSceneBuilder.JunkChance; junk sells for about 1.5 gold a level
REST_PER_HEALTH = 20     # HealthRegeneration: 5 % a second out of combat, after 5 seconds
WALK = 8                 # seconds from one monster to the next


def weapon_damage(level):  # TestSceneBuilder: 2 + (sell - 50) x 0.035, sell = GearData.WeaponSellPrice
    tier = min(7, 1 + (level - 1) // 4)
    sell = 50 + 10 * level + 2 * level * (tier - 1)
    return round(2 + (sell - 50) * 0.035), sell * 3


def armor(level, shield):  # TestSceneBuilder.ArmorPiece: 3 + power x 0.06 by slot share, power = 2.5 x tier x level
    piece = 3 + 2.5 * min(11, 2 + (level - 1) // 3) * level * 0.06
    total = sum(max(1, round(piece * share)) for share in (0.7, 1, 0.5, 0.8, 0.5))
    return total + (max(1, round(3 + 10 * level * 0.06)) if shield else 0)


# TestSceneBuilder.CreateClasses: health, min and max damage, damage a level, seconds between hits, critical chance,
# primary attribute and vitality at level 1, shield.
CLASSES = {
    'Warrior': (75, 5, 9, 1.0, 1.5, 0.06, 15, 15, True),
    'Ranger': (60, 3, 6, 0.8, 1.7, 0.09, 15, 10, False),
}
POTIONS = [(1, 15), (5, 75), (12, 180), (18, 270), (24, 360)]  # Life Potion tiers: level, price


def main():
    lines = ['# Balance model', '',
             'Written by `Tools/Balance/model.py` (run it again after changing the numbers it names). A model, not the',
             'game: a player with plain gear of its level, auto-attacks only (spells come on top), against a normal monster',
             'of its own level standing still. Elites have 3 times the health, hit 1.5 times as hard and give 3 times the',
             'experience and gold.', '',
             'Targets: a fight lasts about 10 s (warrior) to 13 s (ranger) and costs a quarter to a third of your health,',
             'so a rest every few monsters; a level takes a few minutes early and 15-20 minutes at the end (the source',
             'game\'s experience tables); a piece of gear about every five kills, most of them for your class; gold buys a',
             'piece of gear of your level every 5-10 minutes and potions easily.', '',
             '| Level | Monster HP | Monster DPS | Warrior HP / armor | Warrior DPS | Fight | HP lost | Ranger HP | Ranger DPS |'
             ' Fight | HP lost | Kills a level | Minutes a level | Gear a level (for you) | Gold a kill | Gold a minute |'
             ' Weapon price | Potion price |',
             '|' + '---|' * 18]
    total = 0
    for level in range(1, 26):
        cells = [str(level), f'{monster_health(level):.0f}', f'{monster_dps(level):.1f}']
        warrior_fight = warrior_lost = 0
        for name, (health, low, high, per_level, interval, crit, primary, vitality, shield) in CLASSES.items():
            hp = health * health_growth(level) + vitality * 4 + (primary if name == 'Warrior' else 10)
            weapon, price = weapon_damage(level)
            hit = (low + high) / 2 + per_level * (level - 1) + primary // 4 + weapon
            dps = hit * damage_growth(level) / interval * (1 + crit)
            fight = monster_health(level) / dps
            worn = armor(level, shield)
            lost = monster_dps(level) * 100 / (100 + worn) * fight / hp
            if name == 'Warrior':
                cells += [f'{hp:.0f} / {worn}', f'{dps:.0f}', f'{fight:.0f} s', f'{lost:.0%}']
                warrior_fight, warrior_lost = fight, lost
            else:
                cells += [f'{hp:.0f}', f'{dps:.0f}', f'{fight:.0f} s', f'{lost:.0%}']
        kills = EXPERIENCE[level - 1] / KILL_EXPERIENCE[level - 1] if level < 25 else 0
        seconds = warrior_fight + WALK + (5 + warrior_lost * REST_PER_HEALTH if warrior_lost > 0.05 else 0)
        minutes = kills * seconds / 60
        total += minutes
        gold = monster_gold(level) + JUNK_CHANCE * 1.5 * level
        gear = kills * GEAR_CHANCE
        potion = max(p for l, p in POTIONS if l <= level)
        cells += [f'{kills:.0f}' if kills else '–', f'{minutes:.0f}' if kills else '–',
                  f'{gear:.1f} ({gear * (CLASS_SHARE + (1 - CLASS_SHARE) / 3):.1f})' if kills else '–',
                  f'{gold:.0f}', f'{gold * 60 / seconds:.0f}', str(weapon_damage(level)[1]), str(potion)]
        lines.append('| ' + ' | '.join(cells) + ' |')
    lines += ['', f'Level 1 to 25: about {total / 60:.1f} hours of fighting (more with walking between areas and deaths).', '']
    with open(OUT, 'w', newline='\n') as f:
        f.write('\n'.join(lines))
    print(f'{OUT}: {total / 60:.1f} hours')


if __name__ == '__main__':
    main()
