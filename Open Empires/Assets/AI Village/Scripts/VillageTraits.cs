using System.Collections.Generic;

namespace OpenEmpires.Village
{
    public enum Gender { Male, Female }

    public enum Trait
    {
        // Personality (rolled at birth)
        Introvert,      // company drains social; solitude restores it
        Extrovert,      // company restores social twice as fast; solitude drains it
        Fast,           // walks 30% faster
        Slow,           // walks 30% slower
        Glutton,        // gets hungry 50% faster; meals give extra fun
        LightSleeper,   // sleep restores energy more slowly
        NightOwl,       // wakes and sleeps an hour later
        EarlyBird,      // wakes and sleeps an hour earlier
        Hardworking,    // +50% wages
        Lazy,           // −25% wages, tires faster
        Cheerful,       // fun drains slowly
        Grumpy,         // fun drains fast, company helps less
        Curious,        // very easily distracted by eccentrics
        Brave,          // stands and fights when wolves come

        // Acquired through life
        Misogynist,     // spurned by a woman: refuses to court women, women's company doesn't count
        Misandrist,     // spurned by a man: refuses to court men, men's company doesn't count
        BrokenLeg,      // temporary: hobbles at 40% speed until it heals
        Sick,           // temporary: food poisoning — tires quickly, no appetite bonus
        WeakStomach,    // after bad food: gets hungry faster for good
        Distractible    // after gawking too often
    }

    public static class VillageTraits
    {
        /// <summary>Traits a villager can be born with.</summary>
        public static readonly Trait[] Innate =
        {
            Trait.Introvert, Trait.Extrovert, Trait.Fast, Trait.Slow, Trait.Glutton, Trait.LightSleeper,
            Trait.NightOwl, Trait.EarlyBird, Trait.Hardworking, Trait.Lazy, Trait.Cheerful, Trait.Grumpy, Trait.Curious, Trait.Brave
        };

        /// <summary>Pairs that can't coexist (rolling one removes the other).</summary>
        private static readonly (Trait, Trait)[] Exclusive =
        {
            (Trait.Introvert, Trait.Extrovert), (Trait.Fast, Trait.Slow), (Trait.NightOwl, Trait.EarlyBird),
            (Trait.Hardworking, Trait.Lazy), (Trait.Cheerful, Trait.Grumpy)
        };

        public static bool Conflicts(Trait a, Trait b)
        {
            foreach (var (x, y) in Exclusive)
                if ((a == x && b == y) || (a == y && b == x)) return true;
            return false;
        }

        public static bool IsTemporary(Trait t) => t == Trait.BrokenLeg || t == Trait.Sick;

        public static string Name(Trait t)
        {
            switch (t)
            {
                case Trait.LightSleeper: return "Light sleeper";
                case Trait.NightOwl: return "Night owl";
                case Trait.EarlyBird: return "Early bird";
                case Trait.BrokenLeg: return "Broken leg";
                case Trait.WeakStomach: return "Weak stomach";
                default: return t.ToString();
            }
        }

        public static string Icon(Trait t)
        {
            switch (t)
            {
                case Trait.Introvert: return "◔";
                case Trait.Extrovert: return "☺";
                case Trait.Fast: return "»";
                case Trait.Slow: return "›";
                case Trait.Glutton: return "♨";
                case Trait.LightSleeper: return "☾";
                case Trait.NightOwl: return "☾";
                case Trait.EarlyBird: return "☀";
                case Trait.Hardworking: return "★";
                case Trait.Lazy: return "☁";
                case Trait.Cheerful: return "♪";
                case Trait.Grumpy: return "☹";
                case Trait.Curious: return "?";
                case Trait.Brave: return "⚔";
                case Trait.Misogynist: return "✕♀";
                case Trait.Misandrist: return "✕♂";
                case Trait.BrokenLeg: return "✚";
                case Trait.Sick: return "✚";
                case Trait.WeakStomach: return "♨";
                case Trait.Distractible: return "?";
                default: return "•";
            }
        }

        public static string Describe(Trait t)
        {
            switch (t)
            {
                case Trait.Introvert: return "company is draining; time alone recharges them";
                case Trait.Extrovert: return "thrives on company, wilts alone";
                case Trait.Fast: return "walks 30% faster";
                case Trait.Slow: return "walks 30% slower";
                case Trait.Glutton: return "always hungry; loves a good meal";
                case Trait.LightSleeper: return "sleep restores them slowly";
                case Trait.NightOwl: return "up late, sleeps in";
                case Trait.EarlyBird: return "up at dawn, early to bed";
                case Trait.Hardworking: return "earns 50% more";
                case Trait.Lazy: return "earns less and tires quickly";
                case Trait.Cheerful: return "rarely bored";
                case Trait.Grumpy: return "bores easily; company helps little";
                case Trait.Curious: return "very easily distracted";
                case Trait.Brave: return "stands and fights when the village is threatened";
                case Trait.Misogynist: return "spurned by a woman; wants nothing to do with women";
                case Trait.Misandrist: return "spurned by a man; wants nothing to do with men";
                case Trait.BrokenLeg: return "hobbling until it heals";
                case Trait.Sick: return "food poisoning";
                case Trait.WeakStomach: return "never quite recovered from bad stew";
                case Trait.Distractible: return "gawks at anything odd";
                default: return "";
            }
        }
    }
}
