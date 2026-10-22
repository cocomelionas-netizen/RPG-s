"""This project's own names for the source game's: places, people, monsters, spells, items and affixes.

The game takes its world from the source game's data but must not carry its names. Every distinctive word of the source
game's names is mapped to a word of ours (case-sensitive: the capitalized word in names; lowercase words of ordinary
text are left alone), and plain words that any game uses (Sword, Boots, Potion, Valley...) stay.

    from names import rename      # in a generator: every name and text it writes goes through rename()
    python3 -I Tools/GameData/names.py <file.cs>...   # rewrites the string literals of C# files with the same map

The map is applied once per word (a replacement is never replaced again), so a name always becomes the same new name,
wherever it is written: generated data, the scene builder's literals, the runtime and the tests stay in step.
"""
import re
import sys

WORDS = {
    # ---------- Places ----------
    'Revere': 'Aurelis', 'Fanadin': 'Halvaren', 'Kandorys': 'Karthmoor', 'Kandoryrs': 'Karthmoor', 'Atmar': 'Ostrimar',
    'Atamr': 'Ostrimar', 'Fechmar': 'Wendmark', 'Hillsfar': 'Brackenmere', 'Salmaster': 'Corvalis', 'Telamont': 'Mirethane',
    'Fireshear': 'Emberreach', 'Morgall': 'Grulmak', 'Eversorrow': 'Mourndeep', 'Silveroak': 'Silverbirch',
    'Cheryb': 'Moonpetal', 'Pilgrim': 'Wayfarer', 'Refuge': 'Haven', 'Merchant': 'Trader', 'Way': 'Road',
    'Dreams': 'Slumber', 'Wanderer': 'Drifter', 'Hollow': 'Barren', 'Burial': 'Barrow', 'Decrepit': 'Crumbling',
    'Abandoned': 'Forsaken', 'Desecrated': 'Defiled', 'Lower': 'Sunken', 'Den': 'Lair', 'Backwoods': 'Wildwood',
    'Deceit': 'Treachery', 'Demiplane': 'Netherplane', 'Demiplanar': 'Netherplanar', 'Demiplanes': 'Netherplanes', 'Undercourse': 'Underhalls',
    'Forecourt': 'Outerward', 'Passage': 'Crossing', 'Thicket': 'Copse', 'Wyrms': 'Basilisks', 'Wyrm': 'Basilisk',
    'Deserter': 'Turncoat', 'Deserters': 'Turncoats', 'Zorn': 'Thaal', 'Anauroch': 'Sandmere',

    # ---------- People ----------
    'Aedis': 'Maelis', 'Alaria': 'Corenna', 'Argas': 'Brannoc', 'Argha': 'Durgha', 'Arjay': 'Ellroy',
    'Azralus': 'Velmorian', 'Azurex': 'Cyranox', 'Balagos': 'Morvagos', 'Baliel': 'Varion', 'Barrett': 'Corwin',
    'Berith': 'Malphor', 'Billings': 'Pennywhit', 'Boltar': 'Garrand', 'Bongo': 'Tumbo', 'Briggs': 'Hollis',
    'Calder': 'Tamsin', 'Cassi': 'Lissa', 'Cinaed': 'Breda', 'Clive': 'Edric', 'Cragen': 'Rennick', 'Dabbuz': 'Gorrak',
    'Dante': 'Severin', 'Darius': 'Tobin', 'Donal': 'Fergus', 'Dragos': 'Vasil', 'Drauglyen': 'Fenrask',
    'Earnest': 'Aldous', 'Emitt': 'Bram', 'Falgur': 'Torvald', 'Fasthorn': 'Swifthorn', 'Frederic': 'Ambrose',
    'Garett': 'Wendel', 'Goresh': 'Mordek', 'Gordon': 'Percival', 'Grover': 'Pip', 'Gwendolyn': 'Rosalind',
    'Gyldan': 'Ithrin', 'Halfrid': 'Osric', 'Harold': 'Gilbert', 'Helbred': 'Wenna', 'Helenia': 'Isolde',
    'Herdith': 'Marla', 'Herekin': 'Fizwick', 'Hezakoth': 'Zarrimon', 'Hissabal': 'Skrell', 'Huldria': 'Morwen',
    'Jacques': 'Anselm', 'Jamil': 'Rafiq', 'Jerrick': 'Dobbin', 'Jipples': 'Nibbets', 'John': 'Hal', 'Jordy': 'Cass',
    'Julius': 'Cassian', 'Julya': 'Elsie', 'Junkies': 'Grizzik', 'Karl': 'Otto', 'Kelda': 'Sigrun', 'Kent': 'Wilbur',
    'Kerbogh': 'Grumthar', 'Klauth': 'Vhorlax', 'Lamuel': 'Benedek', 'Laudrus': 'Halvard', 'Lawrence': 'Godfrey',
    'Leif': 'Rurik', 'Leon': 'Corin', 'Loglan': 'Grimbald', 'Lostral': 'Vesperin', 'Lothanmaarg': 'Ghorlamuth',
    'Lowe': 'Bertram', 'Malte': 'Ansel', 'Marc': 'Dorian', 'Mastemia': 'Vexaria', 'Maximus': 'Valerian',
    'Melchon': 'Thalgor', 'Miriam': 'Agnes', 'Montagar': 'Brakhar', 'Nina': 'Petra', 'Nybras': 'Quorath',
    'Oscar': 'Tibbet', 'Osraed': 'Faelan', 'Padger': 'Hobb', 'Patriel': 'Lucan', 'Petram': 'Ossian', 'Pietro': 'Lorenzo',
    'Piotr': 'Stanis', 'Ragnakk': 'Throgg', 'Regal': 'Royce', 'Reginald': 'Mortimer', 'Robin': 'Willa', 'Ruth': 'Greta',
    'Samel': 'Tobiah', 'Sarah': 'Edith', 'Sarloch': 'Vorlash', 'Selena': 'Liora', 'Shav': 'Skeev', 'Sneegro': 'Snarvik',
    'Stefan': 'Anton', 'Tantorus': 'Gorvanth', 'Tchazzar': 'Xalthuun', 'Tessa': 'Ivy', 'Thaddeus': 'Erasmus',
    'Torb': 'Grumm', 'Tybalt': 'Roderick', 'Tyrel': 'Gareth', 'Urzul': 'Vazhul', 'Valacar': 'Domitian',
    'Vikram': 'Ishan', 'Vincent': 'Aldric', 'Vivien': 'Elodie', 'Vobul': 'Gozrak', 'Xanice': 'Seraphine',
    'Yarrik': 'Dunstan', 'Yoan': 'Elric', 'Zardonic': 'Morthaxis', 'Zargan': 'Valdrek', 'Zedek': 'Malchus',
    'Shriekevil': 'Wailgrim', 'Tombshiver': 'Gravechill', 'Tombskull': 'Cryptskull',

    # ---------- Monsters ----------
    'Antipede': 'Crawlspine', 'Antling': 'Mandiblet', 'Antlings': 'Mandiblets', 'Antwig': 'Burrowling',
    'Bilewig': 'Gallgrub', 'Blackwing': 'Duskwing', 'Blightchosen': 'Rotsworn', 'Bonecracker': 'Skullsplitter',
    'Cokatrice': 'Gorgonet', 'Ebondeath': 'Nightbane', 'Esperite': 'Glimmerkin', 'Esperling': 'Glimmerling',
    'Eyescratch': 'Gougeclaw', 'Firemite': 'Cinderling', 'Firewig': 'Ashgrub', 'Fleshstomper': 'Gorestomper',
    'Frostmite': 'Rimeling', 'Howler': 'Gloomhound', 'Howlers': 'Gloomhounds', 'Stormhowler': 'Stormhound',
    'Plaguepox': 'Blightboil', 'Quazit': 'Impling', 'Rustweaver': 'Rotweaver', 'Shadowclaw': 'Gloomclaw',
    'Spiderite': 'Silkfang', 'Spiritmurk': 'Soulmire', 'Stormwig': 'Galegrub', 'Tasloi': 'Skreeg', 'Trog': 'Grulk',
    'Trogs': 'Grulks', 'Warpterror': 'Riftdread', 'Webslash': 'Silkreaver', 'Yargol': 'Vargul', 'Zhendale': 'Korthian',

    # ---------- Items ----------
    'Aberrant': 'Errant', 'Abnormal': 'Unnatural', 'Atypical': 'Offbeat', 'Bizarre': 'Outlandish',
    'Curious': 'Intriguing', 'Different': 'Distinct', 'Eccentric': 'Whimsical', 'Funny': 'Droll', 'Irregular': 'Uneven',
    'Mystifying': 'Baffling', 'Newfangled': 'Novel', 'Noteworthy': 'Notable', 'Noticeable': 'Conspicuous',
    'Odd': 'Quirky', 'Peculiar': 'Unwonted', 'Perplexing': 'Puzzling', 'Quaint': 'Antique', 'Queer': 'Wayward',
    'Singular': 'Unique', 'Strange': 'Eldritch', 'Uncanny': 'Eerie', 'Unremarkable': 'Plain', 'Unusual': 'Exotic',
    'Weird': 'Freakish', 'Shiny': 'Gleaming', 'Inactive': 'Dormant', 'Depleted': 'Spent', 'Destroyed': 'Ruined',
    'Broken': 'Cracked', 'Fractured': 'Splintered', 'Frayed': 'Tattered', 'Slicer': 'Hewer', 'Treasury': 'Vault',
    'Draconic': 'Dragonforged',

    # ---------- Spells ----------
    'Aegis': 'Rampart', 'Antimagic': 'Spellward', 'Apocalypse': 'Doomfall', 'Blindside': 'Ambush',
    'Champions': 'Heroes', 'Clairvoyance': 'Foresight', 'Dampening': 'Muting', 'Devotion': 'Fervor',
    'Disintegrate': 'Unmake', 'Forcecage': 'Forceprison', 'Fortification': 'Bastion', 'Gouge': 'Rake',
    'Hamstring': 'Hobble', 'Illusion': 'Mirage', 'Indomitability': 'Steadfastness', 'Inevitable': 'Unstoppable',
    'Kidney': 'Spine', 'Lazarus': 'the Anointed', 'Netherverse': 'Voidreach', 'Penance': 'Atonement',
    'Radiance': 'Luminance', 'Redemption': 'Deliverance', 'Reincarnation': 'Rebirth', 'Retribution': 'Reprisal',
    'Righteous': 'Hallowed', 'Sabotage': 'Mischief', 'Satanic': 'Infernal', 'Scorch': 'Sear', 'Sinister': 'Treacherous',
    'Smite': 'Chastise', 'Tactical': 'Strategic', 'Tranquile': 'Serene', 'Tranquility': 'Serenity', 'Vanish': 'Fade',
    'Vengeance': 'Reckoning', 'Warmth': 'Kindling', 'Wings': 'Pinions', 'Freedom': 'Liberty', 'Wrath': 'Ire',
    'Ethereal': 'Spectral', 'Flurry': 'Barrage', 'Entangling': 'Ensnaring', 'Bind': 'Shackle', 'Boon': 'Gift',
    'Blessing': 'Benediction', 'Amplification': 'Empowerment', 'Demoralizing': 'Dispiriting', 'Harrowing': 'Haunting',
    'Might': 'Prowess', 'Determination': 'Tenacity',
}

# The words of the source game's affix names ("of Magical Vim", "of the Protective Donkey"). Many are plain words that
# the game itself uses (Health, Attack, Dodge, Frost...), so these are applied to affix names only (rename(name, affix=True)).
AFFIX_WORDS = {
    'Abjurer': 'Warder', 'Absorption': 'Soaking', 'Abundant': 'Lavish', 'Accurate': 'Unerring', 'Adept': 'Practiced',
    'Agile': 'Lithe', 'Agony': 'Torment', 'Alluring': 'Captivating', 'Ample': 'Generous', 'Armored': 'Plated',
    'Assassin': 'Killer', 'Assault': 'Onrush', 'Atlas': 'Behemoth', 'Attack': 'Strike', 'Avalanche': 'Landslide',
    'Avoidance': 'Evasion', 'Baboon': 'Mandrill', 'Badger': 'Wolverine', 'Balance': 'Poise', 'Banishing': 'Exiling',
    'Barricade': 'Palisade', 'Barrier': 'Screen', 'Bashing': 'Battering', 'Bat': 'Moth', 'Bear': 'Grizzly',
    'Beaver': 'Marmot', 'Beguiling': 'Bewitching', 'Berserker': 'Rager', 'Blinding': 'Dazzling', 'Blizzard': 'Whiteout',
    'Blocking': 'Parrying', 'Blood': 'Gore', 'Bloodshed': 'Slaughter', 'Bold': 'Daring', 'Bombarding': 'Shelling',
    'Boulder': 'Monolith', 'Bountiful': 'Fruitful', 'Bowman': 'Longbowman', 'Bowyer': 'Arrowsmith',
    'Brave': 'Fearless', 'Brilliance': 'Radiance', 'Bullseye': 'Deadeye', 'Bulwark': 'Rampart', 'Butchering': 'Hacking',
    'Calming': 'Soothing', 'Carnage': 'Havoc', 'Cataclysm': 'Upheaval', 'Cerebral': 'Brainy', 'Chaotic': 'Wild',
    'Charming': 'Winsome', 'Chimpanzee': 'Orangutan', 'Chopping': 'Hewing', 'Clairvoyant': 'Farseeing',
    'Cleaving': 'Sundering', 'Collosus': 'Juggernaut', 'Combat': 'Warfare', 'Concentrating': 'Focusing',
    'Conflict': 'Strife', 'Consecrating': 'Hallowing', 'Constitution': 'Endurance', 'Consuming': 'Devouring',
    'Contemplative': 'Pensive', 'Copious': 'Profuse', 'Courageous': 'Valiant', 'Covering': 'Sheltering',
    'Coyote': 'Jackal', 'Crab': 'Lobster', 'Crafty': 'Wily', 'Crow': 'Rook', 'Crusader': 'Templar',
    'Crushing': 'Pulverizing', 'Crystalline': 'Glassy', 'Cunning': 'Shrewd', 'Curative': 'Mending', 'Cutthroat': 'Brigand',
    'Darting': 'Dashing', 'Deadly': 'Lethal', 'Dedication': 'Commitment', 'Deer': 'Elk', 'Defiant': 'Unbowed',
    'Deflection': 'Glancing', 'Deft': 'Nimble-handed', 'Devastating': 'Ruinous', 'Dexterity': 'Finesse',
    'Diabolic': 'Fiendish', 'Dilligent': 'Industrious', 'Disembowler': 'Gutter', 'Dispatching': 'Dispensing',
    'Diversion': 'Feint', 'Divine': 'Sacred', 'Diving': 'Plunging', 'Dodge': 'Sidestep', 'Dog': 'Hound',
    'Dolphin': 'Porpoise', 'Dominating': 'Commanding', 'Donkey': 'Burro', 'Doom': 'Ruin', 'Dreadful': 'Ghastly',
    'Duck': 'Goose', 'Ducking': 'Weaving', 'Dusk': 'Twilight', 'Eagle': 'Falcon', 'Elder': 'Ancestral',
    'Elephant': 'Mammoth', 'Eluding': 'Escaping', 'Enchanting': 'Spellbinding', 'Encompassing': 'Enveloping',
    'Equilibrium': 'Steadiness', 'Essence': 'Quintessence', 'Evading': 'Slipping', 'Eviscerating': 'Gutting',
    'Evoker': 'Conjurer', 'Exactness': 'Accuracy', 'Excellence': 'Mastery', 'Expert': 'Virtuoso', 'Fabled': 'Legendary',
    'Fending': 'Warding', 'Fierce': 'Ferocious', 'Fist': 'Knuckle', 'Flaying': 'Skinning', 'Fletcher': 'Featherer',
    'Formation': 'Array', 'Fortitude': 'Hardiness', 'Fortress': 'Citadel', 'Fox': 'Vixen', 'Frost': 'Rime',
    'Frostbitten': 'Icebound', 'Frozen': 'Gelid', 'Gallant': 'Chivalrous', 'Giant': 'Jotun', 'Gibbon': 'Lemur',
    'Glacial': 'Boreal', 'Glim': 'Gleam', 'Gloomy': 'Somber', 'Goat': 'Ibex', 'Gore': 'Carnage', 'Gorilla': 'Silverback',
    'Grit': 'Mettle', 'Guarding': 'Shielding', 'Guiding': 'Leading', 'Harmony': 'Concord', 'Healing': 'Restoring',
    'Health': 'Wellness', 'Hearty': 'Robust', 'Herculean': 'Mighty', 'Heroic': 'Epic', 'Honor': 'Glory',
    'Illumination': 'Enlightenment', 'Imperishable': 'Everlasting', 'Impregnability': 'Invincibility',
    'Incandescence': 'Glow', 'Incantation': 'Chant', 'Inferno': 'Firestorm', 'Infiltration': 'Stealth',
    'Initiate': 'Neophyte', 'Injuring': 'Harming', 'Intuitive': 'Instinctive', 'Invulnerability': 'Imperviousness',
    'King': 'Monarch', 'Lacerating': 'Gashing', 'Light': 'Lumen', 'Lion': 'Panther', 'Lizard': 'Gecko',
    'Luminescence': 'Glimmer', 'Magical': 'Arcane', 'Magma': 'Lava', 'Magus': 'Mystic', 'Maiming': 'Crippling',
    'Marksman': 'Sharpeye', 'Martyr': 'Saint', 'Masterful': 'Consummate', 'Mauling': 'Rending', 'Meditative': 'Serene',
    'Merciless': 'Pitiless', 'Mess': 'Muddle', 'Mighty': 'Potent', 'Mind': 'Intellect', 'Mindful': 'Heedful',
    'Mink': 'Sable', 'Mountain': 'Peak', 'Mule': 'Yak', 'Muskrat': 'Wombat', 'Mutilating': 'Mangling', 'Mystical': 'Occult',
    'Mythical': 'Fabulous', 'Negating': 'Nullifying', 'Nether': 'Abyssal', 'Nightfall': 'Gloaming', 'Nimble': 'Spry',
    'Novice': 'Apprentice', 'Occultism': 'Esoterica', 'Octopus': 'Kraken', 'Offense': 'Aggression', 'Ogre': 'Troll',
    'Omnipotence': 'Supremacy', 'Onslaught': 'Barrage', 'Opossum': 'Bandicoot', 'Ostrich': 'Emu', 'Otter': 'Platypus',
    'Owl': 'Nighthawk', 'Penetrating': 'Piercing', 'Perfection': 'Flawlessness', 'Perseverance': 'Persistence',
    'Phalanx': 'Shieldwall', 'Piercing': 'Impaling', 'Plentiful': 'Teeming', 'Polar': 'Arctic', 'Potent': 'Forceful',
    'Pounding': 'Hammering', 'Powerful': 'Mighty', 'Precision': 'Exactitude', 'Protecting': 'Defending',
    'Protective': 'Guarding', 'Puncturing': 'Perforating', 'Rabbit': 'Hare', 'Rat': 'Mole', 'Rationale': 'Logic',
    'Ravaging': 'Despoiling', 'Raven': 'Magpie', 'Ready': 'Primed', 'Reason': 'Sense', 'Recovery': 'Recuperation',
    'Regenerating': 'Renewing', 'Rejuvenating': 'Reviving', 'Reliant': 'Dependable', 'Relxation': 'Repose',
    'Remorseless': 'Relentless', 'Resilient': 'Enduring', 'Resolve': 'Willpower', 'Respose': 'Calm', 'Rest': 'Slumber',
    'Robust': 'Stalwart', 'Rock': 'Crag', 'Rumination': 'Musing', 'Rupturing': 'Bursting', 'Ruthless': 'Callous',
    'Sadistic': 'Vicious', 'Salvation': 'Mercy', 'Savage': 'Feral', 'Scalding': 'Searing', 'Scorching': 'Blistering',
    'Severe': 'Harsh', 'Severing': 'Cutting', 'Shades': 'Specters', 'Shadows': 'Umbra', 'Shark': 'Barracuda',
    'Shielding': 'Screening', 'Shining': 'Beaming', 'Shocking': 'Jolting', 'Shrew': 'Vole', 'Sidestep': 'Dodge',
    'Sighting': 'Spotting', 'Skillful': 'Adroit', 'Slashing': 'Raking', 'Slaughtering': 'Massacring', 'Slayer': 'Butcher',
    'Slaying': 'Killing', 'Smart': 'Clever', 'Smashing': 'Shattering', 'Snake': 'Viper', 'Sniper': 'Sharpshot',
    'Soldier': 'Trooper', 'Solid': 'Stout', 'Sorceror': 'Warlock', 'Sparkling': 'Glittering', 'Spellcraft': 'Arcana',
    'Splitting': 'Riving', 'Squid': 'Cuttlefish', 'Squire': 'Page', 'Stabbing': 'Piercing', 'Stability': 'Firmness',
    'Stabilizing': 'Steadying', 'Stone': 'Granite', 'Stout': 'Sturdy', 'Striking': 'Smiting', 'Strong': 'Brawny',
    'Sturdy': 'Rugged', 'Superior': 'Supreme', 'Sweeping': 'Scything', 'Swinging': 'Swaying', 'Tamarin': 'Marmoset',
    'Targeting': 'Aiming', 'Tempting': 'Enticing', 'Tetsudo': 'Testudo', 'Thief': 'Pickpocket', 'Thought': 'Reflection',
    'Thrusting': 'Lunging', 'Titan': 'Goliath', 'Tough': 'Hardy', 'Transcendent': 'Sublime', 'Trickery': 'Guile',
    'Tundra': 'Steppe', 'Turtle': 'Tortoise', 'Twisted': 'Warped', 'Undying': 'Deathless', 'Unfading': 'Unwaning',
    'Unrelenting': 'Unceasing', 'Unyielding': 'Adamant', 'Valor': 'Courage', 'Vigor': 'Vitality', 'Vim': 'Verve',
    'Vitality': 'Lifeblood', 'Vivacity': 'Liveliness', 'Volcanic': 'Molten', 'Vollying': 'Volleying',
    'Volunteering': 'Offering', 'Voodoo': 'Hex', 'Vortex': 'Maelstrom', 'Warm': 'Balmy', 'Weasel': 'Ferret',
    'Whale': 'Leviathan', 'Willing': 'Eager', 'Wisdom': 'Insight', 'Witch': 'Hag', 'Witchcraft': 'Sorcery',
    'Wizard': 'Archmage', 'Wounding': 'Bloodletting',
}

TOKEN = re.compile(r"[A-Za-z][A-Za-z]*")


def rename(text, affix=False):
    """The text with every mapped word (as written: capitalized) replaced by ours, each word once; an affix name
    (<paramref name="affix"/>) also gets the affix words."""
    if not text:
        return text
    if affix:
        return TOKEN.sub(lambda m: AFFIX_WORDS.get(m.group(0), WORDS.get(m.group(0), m.group(0))), text)
    return TOKEN.sub(lambda m: WORDS.get(m.group(0), m.group(0)), text)


LITERAL = re.compile(r'(\$@|@\$|\$|@)?"((?:[^"\\\n]|\\.)*)"')


def rename_literal(match):
    prefix, body = match.group(1) or '', match.group(2)
    if '$' in prefix:
        # Interpolated: only the text outside the {expressions}.
        parts = re.split(r'(\{[^{}]*\})', body)
        body = ''.join(p if p.startswith('{') else rename(p) for p in parts)
    else:
        body = rename(body)
    return f'{prefix}"{body}"'


def rewrite_cs(path):
    """Renames the words in a C# file's string literals (comments and code are left as they are)."""
    out = []
    changed = False
    with open(path, encoding='utf-8') as f:
        for line in f:
            stripped = line.lstrip()
            if stripped.startswith('//'):
                out.append(line)
                continue
            new = LITERAL.sub(rename_literal, line)
            changed |= new != line
            out.append(new)
    if changed:
        with open(path, 'w', encoding='utf-8', newline='') as f:
            f.writelines(out)
    return changed


if __name__ == '__main__':
    for name in sys.argv[1:]:
        if rewrite_cs(name):
            print('renamed in', name)
