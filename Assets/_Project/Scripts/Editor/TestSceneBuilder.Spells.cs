using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Progression;
using UnityEngine;
using Attribute = RPG.Progression.Attribute;

namespace RPG.EditorTools
{
    /// <summary>
    /// The classes' spells up to level 20, the source game's (spell_template and the Tomes in item_template): our Warrior has the Paladin's,
    /// the Ranger the Ranger's, the Mage the Mage's, each learned from its Tome's level. Numbers are the source game's:
    /// cooldowns, cast times, mana (base + per level), weapon damage percents and spell formulas (base, spread,
    /// coefficient over two attributes, rank divisor), effect durations. Its ranges (yards, melee 130, spells 610) are
    /// divided by 120 for our units. Where its data leaves a duration empty, a short one is chosen.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const float Yards = 1f / 120f;

        private static readonly Color HolyColor = new Color(1f, 0.9f, 0.45f);
        private static readonly Color SteelColor = new Color(0.85f, 0.85f, 0.95f);
        private static readonly Color HealColor = new Color(0.4f, 1f, 0.5f);
        private static readonly Color NatureColor = new Color(0.45f, 0.95f, 0.4f);
        private static readonly Color ShadowColor = new Color(0.65f, 0.4f, 0.9f);
        private static readonly Color FireColor = new Color(1f, 0.5f, 0.15f);
        private static readonly Color FrostColor = new Color(0.5f, 0.85f, 1f);
        private static readonly Color ArcaneColor = new Color(0.7f, 0.55f, 1f);

        /// <summary>A spell asset, everything reset, with the basics; the caller sets the rest.</summary>
        private static AbilityDefinition Spell(string fileName, string displayName, string description, AbilityKind kind, int level,
            DamageSchool school, float cooldownMs, int manaBase, float manaPerLevel, float castMs, float rangeYards, Color color)
        {
            AbilityDefinition spell = LoadOrCreateAsset<AbilityDefinition>($"{AbilitiesFolder}/Generated/{fileName}.asset");
            spell.displayName = displayName;
            spell.description = description;
            spell.kind = kind;
            spell.learnLevel = level;
            spell.maxRank = 10;
            spell.school = school;
            spell.cooldown = cooldownMs / 1000f;
            spell.manaCost = manaBase;
            spell.manaPerLevel = manaPerLevel;
            spell.castTime = castMs / 1000f;
            spell.range = rangeYards * Yards;
            spell.radius = 1f;
            spell.maxTargets = 0;
            spell.damageMultiplier = 0f;
            spell.weaponPercent = 0f;
            spell.weaponPercentPerRank = 0f;
            spell.hits = 1;
            spell.ignoreDefenses = false;
            spell.isHeal = false;
            spell.spellAmount = 0f;
            spell.spellSpread = 0f;
            spell.spellCoefficient = 0f;
            spell.spellStat = Attribute.Intelligence;
            spell.spellStat2 = Attribute.Vitality;
            spell.spellStat2Factor = 1f;
            spell.rankDivisor = 5f;
            spell.healFraction = 0f;
            spell.effects = Array.Empty<SpellEffect>();
            spell.removesHarmful = false;
            spell.breaksControl = false;
            spell.requiresTargetEffect = false;
            spell.requiresTargetOutOfCombat = false;
            spell.requiresCasterOutOfCombat = false;
            spell.requiredEffectName = "";
            spell.restoreManaPercent = 0f;
            spell.restoreManaPerRank = 0f;
            spell.leapDistance = 2.5f;
            spell.reviveHealth = 0.4f;
            spell.reviveMana = 0.2f;
            spell.effectColor = color;
            UnityEditor.EditorUtility.SetDirty(spell);
            return spell;
        }

        /// <summary>
        /// Sleep (the source game's spell 245, every class starts with it): a 2.5 sec nap out of combat, then 40% of health
        /// and mana back; 3 min cooldown, no ranks.
        /// </summary>
        private static AbilityDefinition Sleep()
        {
            AbilityDefinition sleep = Spell("Sleep", "Sleep", "Rest your eyes for a quick nap: after it, 40% of your health and mana come back. Cannot be used in combat.",
                AbilityKind.Self, 1, DamageSchool.Physical, 180000, 0, 0f, 2500, 0, new Color(0.6f, 0.7f, 1f));
            sleep.maxRank = 1;
            sleep.healFraction = 0.4f;
            sleep.restoreManaPercent = 40f;
            sleep.requiresCasterOutOfCombat = true;
            return sleep;
        }

        private static AbilityDefinition Weapon(this AbilityDefinition spell, float percent, float perRank)
        {
            spell.weaponPercent = percent;
            spell.weaponPercentPerRank = perRank;
            return spell;
        }

        /// <summary>The source game's spell formula: rank + (stat + stat2 x factor) x coefficient / (105 - rank x divisor) + base..base+spread.</summary>
        private static AbilityDefinition Power(this AbilityDefinition spell, float amount, float spread, float coefficient,
            Attribute stat, Attribute stat2, float stat2Factor, float divisor = 5f, bool heal = false)
        {
            spell.spellAmount = amount;
            spell.spellSpread = spread;
            spell.spellCoefficient = coefficient;
            spell.spellStat = stat;
            spell.spellStat2 = stat2;
            spell.spellStat2Factor = stat2Factor;
            spell.rankDivisor = divisor;
            spell.isHeal = heal;
            return spell;
        }

        private static AbilityDefinition With(this AbilityDefinition spell, params SpellEffect[] effects)
        {
            spell.effects = effects;
            return spell;
        }

        private static SpellEffect Effect(StatusKind kind, float value, float durationMs, float perRank = 0f, float intervalMs = 0f,
            int maxStacks = 1, DamageSchool school = DamageSchool.Physical, bool anySchool = true, bool onCaster = false, bool magicOnly = false)
        {
            return new SpellEffect
            {
                kind = kind, value = value, valuePerRank = perRank, duration = durationMs / 1000f, interval = intervalMs / 1000f,
                maxStacks = maxStacks, school = school, anySchool = anySchool && !magicOnly, onCaster = onCaster, magicOnly = magicOnly,
            };
        }

        private static SpellEffect ScaledBy(this SpellEffect effect, Attribute stat, float factor)
        {
            effect.scalesWithStat = true;
            effect.stat = stat;
            effect.statFactor = factor;
            return effect;
        }

        /// <summary>The Paladin's spells (the source game's class 1), for our Warrior.</summary>
        private static AbilityDefinition[] CreateWarriorSpells()
        {
            const Attribute Courage = Attribute.Vitality;
            return new[]
            {
                Spell("MightyBlow", "Mighty Blow", "An instant strike that causes 134% weapon damage.", AbilityKind.TargetDamage, 1,
                    DamageSchool.Physical, 7000, 1, 0.5f, 0, 130, SteelColor).Weapon(134f, 1f),
                Spell("HolyWrath", "Holy Ire", "A bolt of divine energy from the sky: Holy damage, and more over 3 sec.", AbilityKind.TargetDamage, 1,
                    DamageSchool.Holy, 10000, 2, 1f, 1000, 610, HolyColor)
                    .Power(12f, 5f, 115f, Courage, Attribute.Intelligence, 1f)
                    .With(Effect(StatusKind.DamageOverTime, 5f, 3000, 1f, 1000, school: DamageSchool.Holy)),
                Spell("Radiance", "Luminance", "Heals a friendly target (or you), and more over 6 sec.", AbilityKind.FriendlyTarget, 1,
                    DamageSchool.Holy, 10000, 2, 1f, 1000, 610, HealColor)
                    .Power(10f, 8f, 100f, Courage, Attribute.Intelligence, 1f, heal: true)
                    .With(Effect(StatusKind.HealOverTime, 7f, 6000, 1f, 2000)),
                Spell("HammerOfMight", "Hammer of Prowess", "Hurls a magical hammer: Holy damage and a 2 sec stun.", AbilityKind.TargetDamage, 2,
                    DamageSchool.Holy, 30000, 2, 1.5f, 0, 510, HolyColor)
                    .Power(15f, 20f, 85f, Courage, Attribute.Strength, 0.1f)
                    .With(Effect(StatusKind.Stun, 0f, 2000)),
                Spell("Charge", "Charge", "Rush towards an enemy and stun it for 1.5 sec.", AbilityKind.Charge, 3,
                    DamageSchool.Physical, 12000, 1, 0.5f, 0, 500, SteelColor)
                    .With(Effect(StatusKind.Stun, 0f, 1500)),
                Spell("BlessingOfChampions", "Benediction of Heroes", "Raises a friendly target's attributes by 5% (+1% a rank) for 5 min.",
                        AbilityKind.FriendlyTarget, 4, DamageSchool.Holy, 10000, 1, 0.5f, 0, 610, HolyColor)
                    .With(Effect(StatusKind.Attributes, 5f, 300000, 1f)),
                Spell("DivineStrike", "Divine Strike", "82% weapon damage as Holy; the target takes 1% more damage, up to 10 times.",
                        AbilityKind.TargetDamage, 5, DamageSchool.Holy, 3000, 1, 0.5f, 0, 130, HolyColor)
                    .Weapon(82f, 3f)
                    .With(Effect(StatusKind.DamageTaken, 1f, 10000, maxStacks: 10)),
                MarkCleanse(Spell("Cleanse", "Cleanse", "Cleanses a friendly target (or you) of one harmful effect.", AbilityKind.FriendlyTarget, 6,
                    DamageSchool.Holy, 3000, 1, 0.5f, 0, 610, HealColor)),
                Spell("VengeanceAura", "Reckoning Aura", "Friends near you deal 2% (+1% a rank) more damage for 15 sec.", AbilityKind.FriendlyArea, 7,
                        DamageSchool.Holy, 60000, 2, 1.5f, 0, 0, HolyColor)
                    .WithRadius(4f)
                    .With(Effect(StatusKind.DamageDealt, 2f, 15000, 1f)),
                Spell("BlessingOfDefense", "Benediction of Defense", "A friendly target dodges 4% (+1% a rank) more often for 5 min.",
                        AbilityKind.FriendlyTarget, 8, DamageSchool.Holy, 10000, 1, 0.5f, 0, 610, SteelColor)
                    .With(Effect(StatusKind.Dodge, 4f, 300000, 1f)),
                HealToFull(Spell("TouchOfSalvation", "Touch of Salvation", "Heals a friendly target (or you) to full health.",
                    AbilityKind.FriendlyTarget, 10, DamageSchool.Holy, 180000, 4, 3f, 500, 300, HealColor)),
                Spell("SalvationAura", "Salvation Aura", "Friends near you take 10% (+1% a rank) less magic damage for 15 sec.", AbilityKind.FriendlyArea, 11,
                        DamageSchool.Holy, 60000, 2, 1.5f, 0, 0, HolyColor)
                    .WithRadius(4f)
                    .With(Effect(StatusKind.DamageTaken, -10f, 15000, -1f, magicOnly: true)),
                Spell("Retribution", "Reprisal", "107% weapon damage; the target cannot swing its weapon for 3 sec.", AbilityKind.TargetDamage, 12,
                        DamageSchool.Physical, 30000, 2, 1f, 0, 130, SteelColor)
                    .Weapon(107f, 3f)
                    .With(Effect(StatusKind.Pacify, 0f, 3000)),
                Spell("RighteousStorm", "Hallowed Storm", "All your damage 20% higher for 9 sec.", AbilityKind.Self, 13,
                        DamageSchool.Holy, 60000, 2, 1.5f, 0, 0, HolyColor)
                    .With(Effect(StatusKind.DamageDealt, 20f, 9000)),
                Spell("FortificationAura", "Bastion Aura", "Friends near you take 18% (+2% a rank) less physical damage for 15 sec.",
                        AbilityKind.FriendlyArea, 14, DamageSchool.Holy, 60000, 2, 1.5f, 0, 0, SteelColor)
                    .WithRadius(4f)
                    .With(Effect(StatusKind.DamageTaken, -18f, 15000, -2f, school: DamageSchool.Physical, anySchool: false)),
                FreeFromControl(Spell("WingsOfFreedom", "Pinions of Liberty", "Frees a friendly target (or you) from stuns, roots and slows, immune to stuns for 1 sec.",
                        AbilityKind.FriendlyTarget, 15, DamageSchool.Holy, 60000, 2, 1f, 0, 610, HolyColor)
                    .With(Effect(StatusKind.StunImmunity, 1f, 1000))),
                Spell("AegisOfValor", "Rampart of Valor", "Take 32% (+3% a rank) less damage for 15 sec.", AbilityKind.Self, 16,
                        DamageSchool.Holy, 120000, 3, 2.5f, 0, 0, SteelColor)
                    .With(Effect(StatusKind.DamageTaken, -32f, 15000, -3f)),
                Spell("Redemption", "Deliverance", "Brings the nearest fallen friend back where they fell, with 40% health and 20% mana.",
                    AbilityKind.Revive, 18, DamageSchool.Holy, 1800000, 6, 4.5f, 3000, 370, HealColor),
                Spell("ShieldBlock", "Shield Block", "Block rating higher by your Strength for 10 sec.", AbilityKind.Self, 19,
                        DamageSchool.Physical, 10000, 2, 1f, 400, 0, SteelColor)
                    .With(Effect(StatusKind.BlockRating, 10f, 10000, 2f).ScaledBy(Attribute.Strength, 1.5f)),
                Spell("DivineProtection", "Divine Protection", "Immune to all damage for 4 sec, but deal 75% less damage.", AbilityKind.Self, 20,
                        DamageSchool.Holy, 180000, 5, 3.5f, 0, 0, HolyColor)
                    .With(Effect(StatusKind.Immune, 1f, 4000), Effect(StatusKind.DamageDealt, -75f, 4000)),
                Sleep(),
            };
        }

        /// <summary>The Ranger's spells (the source game's class 3).</summary>
        private static AbilityDefinition[] CreateRangerSpells()
        {
            return new[]
            {
                Spell("SinisterStrike", "Treacherous Strike", "A strong melee attack: 175% weapon damage.", AbilityKind.TargetDamage, 1,
                    DamageSchool.Physical, 24000, 1, 1f, 0, 130, SteelColor).Weapon(175f, 5f),
                Spell("EntanglingShot", "Ensnaring Shot", "117% ranged damage and 40% slower movement for 3 sec.", AbilityKind.TargetDamage, 1,
                        DamageSchool.Physical, 10000, 2, 2f, 300, 610, NatureColor)
                    .Weapon(117f, 3f)
                    .With(Effect(StatusKind.Slow, 40f, 3000)),
                Spell("MarkTarget", "Mark Target", "The marked enemy takes 9% (+1% a rank) more damage for 10 sec.", AbilityKind.TargetDamage, 1,
                        DamageSchool.Physical, 20000, 1, 1f, 0, 610, new Color(1f, 0.4f, 0.3f))
                    .With(Effect(StatusKind.DamageTaken, 9f, 10000, 1f)),
                Spell("MultiShot", "Multi-Shot", "Fires three arrows in rapid succession (70% damage each).", AbilityKind.TargetDamage, 2,
                        DamageSchool.Physical, 8000, 2, 2f, 500, 610, NatureColor)
                    .Weapon(70f, 2f).WithHits(3),
                Spell("StunningShot", "Stunning Shot", "130% ranged damage and a 1 sec stun.", AbilityKind.TargetDamage, 3,
                        DamageSchool.Physical, 12000, 2, 2f, 400, 610, NatureColor)
                    .Weapon(130f, 5f)
                    .With(Effect(StatusKind.Stun, 0f, 1000)),
                Spell("FocusedEvasion", "Focused Evasion", "Evade the next hostile attack within 5 sec.", AbilityKind.Self, 4,
                        DamageSchool.Physical, 10000, 2, 2f, 0, 0, SteelColor)
                    .With(Effect(StatusKind.Evade, 1f, 5000)),
                Spell("Blindside", "Ambush", "Hurls a dagger, incapacitating the enemy for 4 sec. Any damage ends it.", AbilityKind.TargetDamage, 5,
                        DamageSchool.Physical, 15000, 2, 3f, 0, 260, ShadowColor)
                    .With(Effect(StatusKind.Incapacitate, 0f, 4000)),
                Spell("KidneyShot", "Spine Shot", "195% ranged damage and a 2 sec stun. Only on incapacitated targets.", AbilityKind.TargetDamage, 6,
                        DamageSchool.Physical, 10000, 4, 6f, 1000, 610, ShadowColor)
                    .Weapon(195f, 5f)
                    .Requires(StatusKind.Incapacitate)
                    .With(Effect(StatusKind.Stun, 0f, 2000)),
                Spell("Vanish", "Fade", "Sneak around unseen by monsters for 20 sec, 30% slower. Attacking or being hit ends it.",
                        AbilityKind.Self, 7, DamageSchool.Physical, 60000, 4, 6f, 500, 0, ShadowColor)
                    .With(Effect(StatusKind.Stealth, 1f, 20000), Effect(StatusKind.Slow, 30f, 20000)),
                OutOfCombat(Spell("Sabotage", "Mischief", "Incapacitates a monster not yet fighting for 6 sec. Any damage ends it.",
                        AbilityKind.TargetDamage, 8, DamageSchool.Physical, 6000, 1, 1f, 0, 160, ShadowColor)
                    .With(Effect(StatusKind.Incapacitate, 0f, 6000))),
                Unavoidable(Spell("AimedShot", "Aimed Shot", "115% ranged damage that cannot be dodged or blocked and ignores armor.",
                        AbilityKind.TargetDamage, 9, DamageSchool.Physical, 8000, 2, 2f, 800, 610, NatureColor)
                    .Weapon(115f, 5f)),
                Spell("Devotion", "Fervor", "Physical damage 28% (+2% a rank) higher for 5 sec; then 20% mana back.", AbilityKind.Self, 10,
                        DamageSchool.Holy, 30000, 2, 2f, 0, 0, HolyColor)
                    .With(Effect(StatusKind.DamageDealt, 28f, 5000, 2f, school: DamageSchool.Physical, anySchool: false),
                        Effect(StatusKind.ManaOnExpire, 20f, 5000)),
                Spell("SleepArrow", "Sleep Arrow", "The enemy falls asleep for 8 sec. Any damage wakes it.", AbilityKind.TargetDamage, 11,
                        DamageSchool.Physical, 30000, 5, 8f, 300, 610, ShadowColor)
                    .With(Effect(StatusKind.Incapacitate, 0f, 8000)),
                Spell("Hamstring", "Hobble", "148% melee damage and 29% (+1% a rank) slower movement for 8 sec.", AbilityKind.TargetDamage, 12,
                        DamageSchool.Physical, 12000, 1, 1f, 0, 130, SteelColor)
                    .Weapon(148f, 2f)
                    .With(Effect(StatusKind.Slow, 29f, 8000, 1f)),
                FreeFromControl(Spell("TacticalRetreat", "Strategic Retreat", "Leap away from the enemy, free of stuns and slows, 34% faster for 5 sec.",
                        AbilityKind.Retreat, 14, DamageSchool.Physical, 45000, 2, 3f, 0, 130, NatureColor)
                    .With(Effect(StatusKind.Haste, 34f, 5000, 1f))),
                Spell("EtherealSting", "Spectral Sting", "Burns the enemy's mana every 2 sec for 8 sec; it receives 50% less healing.", AbilityKind.TargetDamage, 15,
                        DamageSchool.Physical, 18000, 2, 2f, 300, 610, ArcaneColor)
                    .With(Effect(StatusKind.ManaBurn, 20f, 8000, 4f, 2000).ScaledBy(Attribute.Intelligence, 0.5f),
                        Effect(StatusKind.HealingTaken, -50f, 8000)),
                Spell("FlameArrow", "Flame Arrow", "49% ranged damage as Fire; the target takes 19% (+1% a rank) more Fire damage for 10 sec.",
                        AbilityKind.TargetDamage, 16, DamageSchool.Fire, 18000, 2, 2f, 300, 610, FireColor)
                    .Weapon(49f, 1f)
                    .With(Effect(StatusKind.DamageTaken, 19f, 10000, 1f, school: DamageSchool.Fire, anySchool: false)),
                Unavoidable(Spell("ExplosiveArrow", "Explosive Arrow", "97% ranged damage as Fire that cannot be dodged or blocked. Needs Flame Arrow on the target.",
                        AbilityKind.TargetDamage, 18, DamageSchool.Fire, 3000, 2, 2f, 300, 610, FireColor)
                    .Weapon(97f, 3f)
                    .Requires(StatusKind.DamageTaken, "Flame Arrow")),
                Spell("EyeGouge", "Eye Rake", "Blinds the enemy: it deals 30% (+5% a rank) less damage for 10 sec.", AbilityKind.TargetDamage, 19,
                        DamageSchool.Physical, 120000, 3, 5f, 0, 500, ShadowColor)
                    .With(Effect(StatusKind.DamageDealt, -30f, 10000, -5f)),
                Spell("ArrowFlurry", "Arrow Barrage", "Your attacks come twice as fast for 10 sec.", AbilityKind.Self, 20,
                        DamageSchool.Holy, 60000, 0, 1f, 0, 0, NatureColor)
                    .With(Effect(StatusKind.AttackSpeed, 50f, 10000)),
                Sleep(),
            };
        }

        /// <summary>The Mage's spells (the source game's class 2).</summary>
        private static AbilityDefinition[] CreateMageSpells()
        {
            const Attribute Courage = Attribute.Vitality;
            return new[]
            {
                Spell("Fireball", "Fireball", "Hurls a fiery ball that causes Fire damage.", AbilityKind.TargetDamage, 1,
                        DamageSchool.Fire, 6000, 3, 3f, 1500, 610, FireColor)
                    .Power(6f, 8f, 110f, Attribute.Intelligence, Courage, 0.5f),
                Spell("Ignite", "Ignite", "Fire damage, more over 4 sec, and the target takes 10% more Fire damage.", AbilityKind.TargetDamage, 1,
                        DamageSchool.Fire, 10000, 2, 2f, 1000, 610, FireColor)
                    .Power(0f, 8f, 85f, Attribute.Intelligence, Courage, 0.5f)
                    .With(Effect(StatusKind.DamageOverTime, 3f, 4000, 1f, 2000, school: DamageSchool.Fire).ScaledBy(Attribute.Intelligence, 0.15f),
                        Effect(StatusKind.DamageTaken, 10f, 4000, school: DamageSchool.Fire, anySchool: false)),
                Spell("ChainsOfIce", "Chains of Ice", "Frost damage and 40% slower movement for 10 sec.", AbilityKind.TargetDamage, 1,
                        DamageSchool.Frost, 8000, 2, 2f, 1000, 610, FrostColor)
                    .Power(0f, 8f, 85f, Attribute.Intelligence, Attribute.Willpower, 0.5f)
                    .With(Effect(StatusKind.Slow, 40f, 10000)),
                Spell("Scorch", "Sear", "Inflicts Fire damage.", AbilityKind.TargetDamage, 2,
                        DamageSchool.Fire, 1000, 2, 2f, 1500, 610, FireColor)
                    .Power(4f, 8f, 95f, Attribute.Intelligence, Courage, 0.5f),
                Spell("IceShard", "Ice Shard", "Frost damage and a 2 sec stun. Only on targets slowed by Chains of Ice.", AbilityKind.TargetDamage, 3,
                        DamageSchool.Frost, 8000, 2, 2f, 1000, 610, FrostColor)
                    .Power(0f, 8f, 100f, Attribute.Intelligence, Attribute.Willpower, 0.5f)
                    .Requires(StatusKind.Slow)
                    .With(Effect(StatusKind.Stun, 0f, 2000)),
                Spell("BoonOfClairvoyance", "Gift of Foresight", "Spell critical rating up by Willpower x 5 for 8 sec.", AbilityKind.Self, 4,
                        DamageSchool.Holy, 40000, 3, 5f, 0, 0, ArcaneColor)
                    .With(Effect(StatusKind.SpellCritical, 0f, 8000).ScaledBy(Attribute.Willpower, 5f)),
                Spell("IceBlast", "Ice Blast", "Frost damage to up to five enemies around the target, frozen in place for 3 sec.", AbilityKind.TargetArea, 5,
                        DamageSchool.Frost, 24000, 2, 3f, 0, 610, FrostColor)
                    .Power(5f, 15f, 30f, Attribute.Intelligence, Attribute.Willpower, 0.5f)
                    .WithRadius(1.6f).WithMaxTargets(5)
                    .With(Effect(StatusKind.Root, 0f, 3000)),
                Spell("VimOfWisdom", "Vim of Wisdom", "Raises a friendly target's Intelligence (by Willpower) for 5 min.", AbilityKind.FriendlyTarget, 6,
                        DamageSchool.Holy, 10000, 2, 2f, 0, 610, ArcaneColor)
                    .With(Effect(StatusKind.Intellect, 1f, 300000, 1f).ScaledBy(Attribute.Willpower, 0.3f)),
                Spell("BindSpirit", "Shackle Spirit", "Banishes an enemy for 6 sec. Any damage ends it.", AbilityKind.TargetDamage, 7,
                        DamageSchool.Shadow, 10000, 2, 2f, 1500, 500, ShadowColor)
                    .With(Effect(StatusKind.Incapacitate, 0f, 6000)),
                Spell("BoonOfProtection", "Gift of Protection", "Absorbs damage (Willpower x 30) and grants immunity to stuns for 2 sec.",
                        AbilityKind.Self, 8, DamageSchool.Holy, 10000, 1, 1f, 0, 0, ArcaneColor)
                    .With(Effect(StatusKind.Absorb, 0f, 2000).ScaledBy(Attribute.Willpower, 30f), Effect(StatusKind.StunImmunity, 1f, 2000)),
                Spell("Warmth", "Kindling", "Fire damage dealt 9% (+1% a rank) higher and Fire damage taken lower for 15 sec.", AbilityKind.Self, 9,
                        DamageSchool.Fire, 50000, 2, 5f, 0, 0, FireColor)
                    .With(Effect(StatusKind.DamageDealt, 9f, 15000, 1f, school: DamageSchool.Fire, anySchool: false),
                        Effect(StatusKind.DamageTaken, -9f, 15000, -1f, school: DamageSchool.Fire, anySchool: false)),
                FreeFromControl(Spell("Teleport", "Teleport", "Instantly moves you to the pointer (in reach), free of stuns and roots.", AbilityKind.Teleport, 10,
                    DamageSchool.Shadow, 15000, 1, 1f, 0, 610, ArcaneColor)),
                Spell("DeepFreeze", "Deep Freeze", "Encases the enemy in ice for 8 sec: it cannot act, and nothing can hurt it.", AbilityKind.TargetDamage, 11,
                        DamageSchool.Frost, 45000, 2, 2f, 500, 610, FrostColor)
                    .With(Effect(StatusKind.Stun, 0f, 8000), Effect(StatusKind.Immune, 1f, 8000)),
                Spell("MagicalAmplification", "Magical Empowerment", "A friendly target deals 2% (+1% a rank) more magic damage for 5 min.",
                        AbilityKind.FriendlyTarget, 12, DamageSchool.Holy, 10000, 2, 2f, 0, 610, ArcaneColor)
                    .With(Effect(StatusKind.DamageDealt, 2f, 300000, 1f, magicOnly: true)),
                RestoresMana(Spell("WisdomOfLazarus", "Wisdom of the Anointed", "Restores 39% (+1% a rank) of your mana at once, and more over 3 sec.",
                        AbilityKind.Self, 14, DamageSchool.Holy, 300000, 0, 0f, 0, 0, ArcaneColor)
                    .With(Effect(StatusKind.ManaOverTime, 10f, 3000, 2f, 1000).ScaledBy(Attribute.Willpower, 2f)), 39f, 1f),
                Spell("IllusionGate", "Mirage Gate", "After 10 sec of casting, takes you to St. Aurelis.", AbilityKind.Recall, 15,
                    DamageSchool.Shadow, 1200000, 6, 10f, 10000, 0, ArcaneColor),
                MarkCleanse(Spell("RemoveCurse", "Remove Curse", "Removes one harmful effect from a friendly target (or you).", AbilityKind.FriendlyTarget, 16,
                    DamageSchool.Holy, 1000, 1, 1f, 0, 610, ArcaneColor)),
                Spell("MagicalDampening", "Magical Muting", "A friendly target takes 5% (+1% a rank) less magic damage for 5 min.",
                        AbilityKind.FriendlyTarget, 18, DamageSchool.Holy, 10000, 2, 2f, 0, 610, ArcaneColor)
                    .With(Effect(StatusKind.DamageTaken, -5f, 300000, -1f, magicOnly: true)),
                Spell("Antimagic", "Spellward", "Interrupts the enemy and keeps it from casting for 4 sec.", AbilityKind.TargetDamage, 19,
                        DamageSchool.Shadow, 24000, 1, 1f, 0, 610, ShadowColor)
                    .With(Effect(StatusKind.Silence, 0f, 4000)),
                Spell("Inferno", "Inferno", "Hurls an immense fiery boulder for great Fire damage.", AbilityKind.TargetDamage, 20,
                        DamageSchool.Fire, 60000, 3, 5f, 2500, 610, FireColor)
                    .Power(10f, 8f, 200f, Attribute.Intelligence, Courage, 0.5f),
                Sleep(),
            };
        }

        private static AbilityDefinition WithRadius(this AbilityDefinition spell, float radius)
        {
            spell.radius = radius;
            return spell;
        }

        private static AbilityDefinition WithMaxTargets(this AbilityDefinition spell, int maxTargets)
        {
            spell.maxTargets = maxTargets;
            return spell;
        }

        private static AbilityDefinition WithHits(this AbilityDefinition spell, int hits)
        {
            spell.hits = hits;
            return spell;
        }

        private static AbilityDefinition Requires(this AbilityDefinition spell, StatusKind effect, string effectName = "")
        {
            spell.requiresTargetEffect = true;
            spell.requiredEffect = effect;
            spell.requiredEffectName = effectName;
            return spell;
        }

        private static AbilityDefinition RestoresMana(AbilityDefinition spell, float percent, float perRank)
        {
            spell.restoreManaPercent = percent;
            spell.restoreManaPerRank = perRank;
            return spell;
        }

        private static AbilityDefinition OutOfCombat(AbilityDefinition spell)
        {
            spell.requiresTargetOutOfCombat = true;
            return spell;
        }

        private static AbilityDefinition Unavoidable(AbilityDefinition spell)
        {
            spell.ignoreDefenses = true;
            return spell;
        }

        private static AbilityDefinition MarkCleanse(AbilityDefinition spell)
        {
            spell.removesHarmful = true;
            return spell;
        }

        private static AbilityDefinition HealToFull(AbilityDefinition spell)
        {
            spell.healFraction = 1f;
            return spell;
        }

        private static AbilityDefinition FreeFromControl(AbilityDefinition spell)
        {
            spell.breaksControl = true;
            return spell;
        }

        /// <summary>Tomes for the spells above level 1 (the source game's: level of the spell, sold by the class's trainer).</summary>
        /// <summary>The Tomes of a class's spells from level 2 (or, <paramref name="firstLevel"/>, of its level 1 spells).</summary>
        private static List<RPG.Items.ItemDefinition> CreateTomes(AbilityDefinition[] spells, int classIndex, bool firstLevel = false)
        {
            var tomes = new List<RPG.Items.ItemDefinition>();
            foreach (AbilityDefinition spell in spells)
            {
                // Sleep is every class's own: no Tome.
                if ((spell.learnLevel <= 1) != firstLevel || spell.name == "Sleep") continue;
                RPG.Items.ItemDefinition tome = CreateItem($"Tomes/TomeOf{spell.name}", $"Tome of {spell.displayName}", RPG.Items.ItemSlot.None,
                    RPG.Items.ItemRarity.Uncommon, 0, 0, 0, spell.effectColor * 0.7f, 5 * spell.learnLevel * spell.learnLevel, classIndex,
                    spell.learnLevel);
                // The source game's Tomes sell for 5 x their level and cost that times their level (Holy Wrath 5, Divine
                // Protection at 20: 2000).
                tome.sellValue = 5 * spell.learnLevel;
                tome.use = RPG.Items.ItemUse.LearnSpell;
                tome.teachesSpell = spell;
                tome.canHaveAffix = false;
                tome.dropWeight = 0f;
                tomes.Add(tome);
            }
            return tomes;
        }
    }
}
