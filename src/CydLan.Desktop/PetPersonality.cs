namespace CydLan.Desktop;

/// <summary>Companion moods the visual knows how to draw.</summary>
public static class PetMood
{
    public const string Neutral = "", Happy = "happy", Love = "love", Angry = "angry", Sad = "sad", Bored = "bored",
        Alert = "alert", Shy = "shy", Dizzy = "dizzy", Hungry = "hungry", Fainted = "fainted", Celebrate = "celebrate",
        Drowsy = "drowsy", Sleeping = "sleeping", Curious = "curious", Smug = "smug",
        Crying = "crying", Laughing = "laughing", Surprised = "surprised", Eating = "eating", Yawning = "yawning",
        Nervous = "nervous", Fired = "fired", Hurt = "hurt";
}

/// <summary>Personality traits for the companion. Traits stack: a pet can be needy, sleepy and grumpy at once.</summary>
public static class PetTraits
{
    public const string Needy = "needy", Sleepy = "sleepy", Grumpy = "grumpy", Cheerful = "cheerful", Shy = "shy",
        Clingy = "clingy", Dramatic = "dramatic", Chatty = "chatty", Foodie = "foodie", Curious = "curious",
        Playful = "playful", Bossy = "bossy", Sassy = "sassy", Zen = "zen";

    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new(Needy, "Needy"),
        new(Sleepy, "Sleepy"),
        new(Grumpy, "Grumpy"),
        new(Cheerful, "Cheerful"),
        new(Shy, "Shy"),
        new(Clingy, "Clingy"),
        new(Dramatic, "Dramatic"),
        new(Chatty, "Chatty"),
        new(Foodie, "Foodie"),
        new(Curious, "Curious"),
        new(Playful, "Playful"),
        new(Bossy, "Bossy"),
        new(Sassy, "Sassy"),
        new(Zen, "Zen")
    ];

    private static readonly HashSet<string> Known = All.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);

    public static List<string> Normalize(IEnumerable<string>? traits) =>
        (traits ?? []).Select(t => (t ?? "").Trim().ToLowerInvariant()).Where(Known.Contains).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<(string Name, string[] Traits)> Presets { get; } =
    [
        ("Clingy baby", [Needy, Clingy, Shy]),
        ("Sleepy grump", [Sleepy, Grumpy]),
        ("Drama queen", [Dramatic, Needy, Sassy]),
        ("Hype buddy", [Cheerful, Playful, Chatty]),
        ("Office boss", [Bossy, Grumpy, Chatty]),
        ("Snack goblin", [Foodie, Needy, Playful]),
        ("Monk", [Zen, Sleepy]),
        ("Chaos", [Needy, Sleepy, Grumpy, Dramatic, Playful, Foodie, Clingy]),
        ("Golden retriever", [Cheerful, Clingy, Playful]),
        ("Cat energy", [Sassy, Sleepy, Curious]),
        ("Tiny tyrant", [Bossy, Needy, Dramatic]),
        ("Lazy chef", [Foodie, Sleepy, Zen]),
        ("Wallflower", [Shy, Curious, Zen]),
        ("Gossip", [Chatty, Sassy, Curious]),
        ("Gym coach", [Bossy, Cheerful, Playful]),
        ("Goth teen", [Grumpy, Sassy, Shy]),
        ("Hangry", [Foodie, Grumpy, Dramatic]),
        ("Puppy love", [Needy, Clingy, Cheerful, Chatty])
    ];
}

public enum PetPoke { Hover, Pet, Click, DragStart, Drop, Shake, Feed, Play, Wake, Tuck }

/// <summary>What the companion window should do this tick.</summary>
public readonly record struct PetMindOutput(string Mood, bool Asleep, bool FollowCursor, bool FaceCursor, bool Zoomies, bool AngryHop, string? Line);

/// <summary>
/// The companion's inner life: affection, boredom, hunger, sleepiness, and temper, driven by traits and how the
/// user treats it. Pure logic with no UI types so it can be tested with a fake clock.
/// </summary>
public sealed class PetMind
{
    private readonly Random random;
    private HashSet<string> traits = [];

    public double IdleSeconds { get; private set; }
    public double Affection { get; private set; } = 70;
    public double Hunger { get; private set; }
    public double Temper { get; private set; }
    public bool Asleep { get; private set; }
    public string Mood { get; private set; } = PetMood.Neutral;
    public int ReminderNag { get; set; } = -1;
    public bool ReminderUrgent { get; set; }

    private string transientMood = PetMood.Neutral;
    private double transientLeft;
    private double lineCooldown = 8;
    private double ambientLineIn;
    private double zoomiesLeft;
    private double angryHopIn;
    private readonly Queue<string> lines = new();
    private readonly List<double> recentClicks = [];
    private double clock;
    private bool lonelySaid, angrySaid, drowsySaid, hungrySaid;
    private int lastTimeCueHour = -1;
    private bool greeted;

    public PetMind(Random? random = null)
    {
        this.random = random ?? Random.Shared;
        ambientLineIn = 240 + this.random.NextDouble() * 360;
    }

    public IReadOnlySet<string> Traits => traits;
    public void SetTraits(IEnumerable<string> value) => traits = PetTraits.Normalize(value).ToHashSet();
    public bool Has(string trait) => traits.Contains(trait);

    private double NeedyLonelyAfter => Has(PetTraits.Clingy) ? 90 : 150;
    private double NeedyAngryAfter => Has(PetTraits.Clingy) ? 210 : 330;

    /// <summary>Queues a line, respecting the cooldown unless forced.</summary>
    public void Say(string line, bool force = false)
    {
        if (!force && lineCooldown > 0) return;
        lines.Clear();
        lines.Enqueue(line);
        lineCooldown = 22;
    }

    private void Flash(string mood, double seconds) { transientMood = mood; transientLeft = seconds; }

    public void Poke(PetPoke kind)
    {
        bool wasAsleep = Asleep;
        bool wasLonely = Mood is PetMood.Sad or PetMood.Angry && ReminderNag < 0;
        double idleBefore = IdleSeconds;
        switch (kind)
        {
            case PetPoke.Hover:
                IdleSeconds = Math.Min(IdleSeconds, 2);
                break;
            case PetPoke.Tuck:
                Asleep = true;
                IdleSeconds = 9999;
                Say(Pick("Goodnight…", "*snuggles in*", "Zzz… finally."), true);
                return;
            default:
                IdleSeconds = 0;
                break;
        }

        if (wasAsleep && kind != PetPoke.Hover || wasAsleep && kind == PetPoke.Hover && !Has(PetTraits.Sleepy))
        {
            Asleep = false;
            drowsySaid = false;
            Flash(PetMood.Dizzy, 1.6);
            Say(Has(PetTraits.Grumpy) ? Pick("I was SLEEPING.", "Five more minutes…", "Rude.") : Pick("Huh?! I'm up!", "*yawn* …hi.", "Was I asleep? No."), true);
        }

        switch (kind)
        {
            case PetPoke.Hover:
                if (wasAsleep && Has(PetTraits.Sleepy)) break; // sleepy pets need more than a hover
                if (Asleep) break;
                if (wasLonely)
                {
                    Flash(PetMood.Love, 3);
                    Say(Has(PetTraits.Grumpy) || Has(PetTraits.Sassy) ? Pick("…fine. You're forgiven.", "Took you long enough.") : Pick("You came back!", "There you are!", "Missed you!"), true);
                }
                else if (Has(PetTraits.Shy) && idleBefore > 45)
                {
                    Flash(PetMood.Shy, 2.6);
                    Say(Pick("Eep!", "O-oh, hi…", "*hides*"));
                }
                else if (Has(PetTraits.Curious) && random.Next(4) == 0)
                {
                    Flash(PetMood.Curious, 1.8);
                }
                lonelySaid = angrySaid = false;
                Temper = Math.Max(0, Temper - 8);
                break;
            case PetPoke.Pet:
                Affection = Math.Min(100, Affection + 6);
                Temper = Math.Max(0, Temper - 25);
                lonelySaid = angrySaid = false;
                if (Has(PetTraits.Grumpy) && random.Next(3) == 0)
                {
                    Flash(PetMood.Shy, 2.2);
                    Say(Pick("…don't get used to it.", "Hmph. Okay. One more.", "I'm not purring. You're purring."));
                }
                else
                {
                    Flash(PetMood.Love, 2.4);
                    Say(Has(PetTraits.Dramatic) ? Pick("My hero!", "I feel SO loved!") : Has(PetTraits.Needy) ? Pick("More!", "Don't stop!", "Best human.") : Pick("Hehe~", "♥", "That's nice."));
                }
                break;
            case PetPoke.Click:
                recentClicks.Add(clock);
                recentClicks.RemoveAll(t => clock - t > 4);
                if (recentClicks.Count >= 4)
                {
                    recentClicks.Clear();
                    Temper = Math.Min(100, Temper + 40);
                    Flash(Has(PetTraits.Grumpy) || Has(PetTraits.Sassy) ? PetMood.Angry : PetMood.Dizzy, 3);
                    Say(Has(PetTraits.Grumpy) ? Pick("STOP. POKING.", "Do that again. I dare you.") : Has(PetTraits.Dramatic) ? Pick("Assault! ASSAULT!", "I'm telling IT!") : Pick("Hey! That tickles!", "Okay okay okay!"), true);
                }
                break;
            case PetPoke.DragStart:
                if (Has(PetTraits.Dramatic)) Say(Pick("AAAH! I'm flying!", "Wheee— no wait AAAH!", "Put me down gently!!"), true);
                else if (Has(PetTraits.Grumpy)) { Flash(PetMood.Angry, 2); Say(Pick("Put me down!", "Unhand me.", "Hey!"), true); }
                else if (Has(PetTraits.Playful)) Say(Pick("Wheee!", "Higher!", "Again!"), true);
                break;
            case PetPoke.Shake:
                Flash(PetMood.Dizzy, 3.5);
                Say(Pick("@_@ the room is spinning", "Bleh… dizzy…", "Earthquake?!"), true);
                break;
            case PetPoke.Drop:
                break;
            case PetPoke.Feed:
                Hunger = 0;
                hungrySaid = false;
                Affection = Math.Min(100, Affection + 10);
                Flash(PetMood.Love, 3);
                Say(Has(PetTraits.Foodie) ? Pick("YUM! More?", "Om nom nom!", "Best. Snack. Ever.") : Pick("Thanks!", "Yum!", "Tasty~"), true);
                break;
            case PetPoke.Play:
                zoomiesLeft = 5.5;
                Affection = Math.Min(100, Affection + 8);
                Flash(PetMood.Happy, 5.5);
                Say(Pick("Zoomies!!", "Catch me!", "Wheee!"), true);
                break;
            case PetPoke.Wake:
                break;
        }
    }

    /// <summary>Advances time. Call about once a second with the real local time.</summary>
    public PetMindOutput Tick(double dt, DateTime now, bool hovered, bool userStatusDnd = false)
    {
        clock += dt;
        lineCooldown = Math.Max(0, lineCooldown - dt);
        if (!hovered) IdleSeconds += dt;
        if (transientLeft > 0) { transientLeft -= dt; if (transientLeft <= 0) transientMood = PetMood.Neutral; }
        if (zoomiesLeft > 0) zoomiesLeft -= dt;
        Temper = Math.Max(0, Temper - dt * (Has(PetTraits.Zen) ? 2.5 : .6));
        Affection = Math.Max(0, Affection - dt * (Has(PetTraits.Needy) ? .05 : .015));

        bool night = now.Hour >= 22 || now.Hour < 6;
        double sleepScale = (Has(PetTraits.Sleepy) ? .55 : 1.0) * (night ? .5 : 1.0) * (Has(PetTraits.Playful) ? 1.4 : 1.0);
        double drowsyAfter = 300 * sleepScale, sleepAfter = 480 * sleepScale;

        if (!greeted)
        {
            greeted = true;
            if (Has(PetTraits.Chatty) || Has(PetTraits.Cheerful))
                Say(now.Hour switch { < 5 => "Up late, huh?", < 12 => "Good morning!", < 18 => "Good afternoon!", _ => "Good evening!" }, true);
        }

        // Reminders take priority over everything else.
        if (ReminderNag >= 0)
        {
            Asleep = false;
            bool hotHead = Has(PetTraits.Grumpy) || Has(PetTraits.Needy) || Has(PetTraits.Bossy) || ReminderUrgent;
            string mood = Has(PetTraits.Dramatic) && ReminderNag >= 3 ? PetMood.Fainted
                : hotHead && ReminderNag >= (ReminderUrgent ? 1 : 2) ? PetMood.Angry
                : ReminderNag >= 2 && !Has(PetTraits.Zen) ? PetMood.Sad
                : PetMood.Alert;
            if (transientLeft > 0 && transientMood is PetMood.Love or PetMood.Celebrate) mood = transientMood;
            Mood = mood;
            angryHopIn -= dt;
            bool hop = false;
            if (mood == PetMood.Angry && angryHopIn <= 0) { hop = true; angryHopIn = ReminderUrgent ? 2.2 : 4.5; }
            return new(Mood, false, Has(PetTraits.Clingy), true, false, hop, TakeLine());
        }

        // Sleep.
        if (!Asleep && userStatusDnd && IdleSeconds > 60) Asleep = true;
        bool hasPersonality = traits.Count > 0;
        if (!Asleep && hasPersonality && IdleSeconds > sleepAfter && !(Has(PetTraits.Needy) && !Has(PetTraits.Sleepy)))
        {
            Asleep = true;
            Say(Pick("Zzz…", "*curls up*", "Nap time…"));
        }
        else if (!Asleep && IdleSeconds > drowsyAfter && !drowsySaid && Has(PetTraits.Sleepy))
        {
            drowsySaid = true;
            Say(Pick("*yaaawn*", "So… sleepy…", "Eyes… heavy…"));
        }

        // Hunger.
        if (Has(PetTraits.Foodie))
        {
            Hunger = Math.Min(1.5, Hunger + dt / (75 * 60));
            if (Hunger >= 1 && !Asleep && (!hungrySaid || random.Next(900) == 0))
            {
                hungrySaid = true;
                Say(Pick("I'm hungry…", "Snack? Please?", "My tummy is rumbling.", "Feed me!"));
            }
        }

        // Loneliness and temper for needy pets.
        bool needy = Has(PetTraits.Needy) || Has(PetTraits.Clingy);
        if (needy && !Asleep)
        {
            if (IdleSeconds > NeedyAngryAfter)
            {
                Temper = Math.Min(100, Temper + dt * 4);
                if (!angrySaid)
                {
                    angrySaid = true;
                    Say(Has(PetTraits.Dramatic) ? Pick("You've FORGOTTEN me!", "Nobody loves me!!") : Pick("HEY! Pet me!", "Hmph! Ignoring me?!", "I'm right HERE!"), true);
                }
                angryHopIn -= dt;
            }
            else if (IdleSeconds > NeedyLonelyAfter && !lonelySaid)
            {
                lonelySaid = true;
                Say(Pick("Hey… you there?", "Pay attention to me?", "*pokes you*", "I'm bored…"));
            }
        }

        // Time-of-day cues.
        if ((Has(PetTraits.Chatty) || Has(PetTraits.Bossy) || Has(PetTraits.Foodie)) && now.Minute == 0 && now.Hour != lastTimeCueHour && !Asleep)
        {
            lastTimeCueHour = now.Hour;
            string? cue = now.Hour switch
            {
                10 => Has(PetTraits.Bossy) ? "Status update, please." : "Water break?",
                12 => Has(PetTraits.Foodie) ? "LUNCH! LUNCH! LUNCH!" : "Lunch time!",
                15 => Has(PetTraits.Foodie) ? "Snack o'clock." : "Stretch break!",
                17 => "Almost done for the day!",
                _ => null
            };
            if (cue is not null) Say(cue, true);
        }

        // Ambient chatter.
        ambientLineIn -= dt;
        if (ambientLineIn <= 0 && !Asleep)
        {
            ambientLineIn = (Has(PetTraits.Chatty) ? 300 : 720) + random.NextDouble() * 600;
            string? line = AmbientLine();
            if (line is not null) Say(line);
        }

        // Pick the mood.
        string m;
        if (transientLeft > 0) m = transientMood;
        else if (Asleep) m = PetMood.Sleeping;
        else if (needy && IdleSeconds > NeedyAngryAfter) m = PetMood.Angry;
        else if (needy && IdleSeconds > NeedyLonelyAfter) m = PetMood.Sad;
        else if (Has(PetTraits.Foodie) && Hunger >= 1) m = PetMood.Hungry;
        else if (Has(PetTraits.Sleepy) && IdleSeconds > drowsyAfter) m = PetMood.Drowsy;
        else if (Temper > 55) m = PetMood.Angry;
        else if (hasPersonality && IdleSeconds > 900 && !Has(PetTraits.Zen)) m = PetMood.Bored;
        else if (Has(PetTraits.Sassy) && Affection > 60) m = PetMood.Smug;
        else if (Has(PetTraits.Cheerful) || Affection > 85) m = PetMood.Happy;
        else m = PetMood.Neutral;
        Mood = m;

        bool angryHop = false;
        if (m == PetMood.Angry && angryHopIn <= 0) { angryHop = true; angryHopIn = 6 + random.NextDouble() * 6; }
        bool zoomies = zoomiesLeft > 0 || (Has(PetTraits.Playful) && !Asleep && random.Next(1800) == 0 && (zoomiesLeft = 4) > 0);

        return new(Mood, Asleep, Has(PetTraits.Clingy) && !Asleep, (Has(PetTraits.Curious) || Has(PetTraits.Clingy)) && !Asleep, zoomies, angryHop, TakeLine());
    }

    public string? TakeLine() => lines.Count > 0 ? lines.Dequeue() : null;

    private string? AmbientLine()
    {
        var pool = new List<string>();
        if (Has(PetTraits.Cheerful)) pool.AddRange(["You're doing great!", "Smile! :)", "Proud of you!", "Today's a good day."]);
        if (Has(PetTraits.Grumpy)) pool.AddRange(["Hmph.", "Mondays. Ugh.", "Don't talk to me.", "*grumbles*"]);
        if (Has(PetTraits.Bossy)) pool.AddRange(["Back to work.", "Did you reply to that message?", "Posture!", "Hydrate. Now."]);
        if (Has(PetTraits.Sassy)) pool.AddRange(["I'm the main character.", "Cute screen. Mine's cuter.", "Not now, I'm being adorable."]);
        if (Has(PetTraits.Zen)) pool.AddRange(["Breathe in… breathe out.", "Be like water.", "Ommm…"]);
        if (Has(PetTraits.Curious)) pool.AddRange(["What's that tab?", "Ooh, what are you typing?", "What does this button do?"]);
        if (Has(PetTraits.Chatty)) pool.AddRange(["I could go for a walk.", "Talk to me!", "So… what's new?"]);
        if (Has(PetTraits.Playful)) pool.AddRange(["Play with me!", "Bet you can't catch me.", "Tag! You're it!"]);
        if (Has(PetTraits.Dramatic)) pool.AddRange(["Life is SO hard.", "*sighs dramatically*", "Nobody understands me."]);
        if (Has(PetTraits.Needy)) pool.AddRange(["Pet me?", "Do you love me?", "Notice me!"]);
        return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
    }

    // ── Reminder & event lines ─────────────────────────────────────────────

    public string ReminderLine(string title, int nag, bool overdue, string priority)
    {
        string t = string.IsNullOrWhiteSpace(title) ? "your reminder" : title.Trim();
        if (overdue) return Pick($"You missed this: {t}", $"Psst… {t} didn't happen.", $"Still on the list: {t}");
        if (nag <= 0)
        {
            if (Has(PetTraits.Bossy)) return Pick($"{t}. Now, please.", $"Agenda item: {t}.");
            if (Has(PetTraits.Grumpy)) return Pick($"Ugh. {t}. Go.", $"{t}. Don't make me ask twice.");
            if (Has(PetTraits.Dramatic)) return Pick($"It's TIME! {t}!", $"The moment has come: {t}!");
            if (Has(PetTraits.Sleepy)) return Pick($"*yawn* …{t}?", $"Mmh… {t} time…");
            if (Has(PetTraits.Needy)) return Pick($"Do {t} with me?", $"Hey hey hey! {t}!");
            if (Has(PetTraits.Shy)) return Pick($"Um… {t}…?", $"S-sorry… it's {t} time.");
            if (Has(PetTraits.Foodie) && LooksLikeFood(t)) return Pick($"FOOD! I mean… {t}!", $"{t}? Save me some!");
            if (Has(PetTraits.Cheerful)) return Pick($"Yay! {t} time!", $"Let's go: {t}!");
            if (Has(PetTraits.Zen)) return Pick($"When you're ready: {t}.", $"A gentle nudge: {t}.");
            return priority == "urgent" ? $"Important: {t}!" : Pick($"Time for {t}", $"Reminder: {t}");
        }
        if (nag == 1)
        {
            if (Has(PetTraits.Grumpy) || Has(PetTraits.Bossy)) return Pick($"I said {t}.", $"Still waiting. {t}.");
            if (Has(PetTraits.Needy)) return Pick($"Helloooo? {t}!", $"You're ignoring me AND {t}?");
            if (Has(PetTraits.Dramatic)) return Pick($"Why won't you {t}?!", $"{t}… please… for me…");
            return Pick($"Still waiting on {t}…", $"Don't forget: {t}");
        }
        if (nag == 2)
        {
            if (Has(PetTraits.Grumpy) || Has(PetTraits.Bossy) || Has(PetTraits.Needy)) return $"{t.ToUpperInvariant()}. NOW.";
            if (Has(PetTraits.Sassy)) return Pick($"Oh, we're just not doing {t}? Cool.", $"I'll wait. ({t})");
            return Pick($"Pretty please: {t}", $"{t}! Third time!");
        }
        if (Has(PetTraits.Dramatic)) return Pick($"*faints* …{t}…", $"I can't go on without {t}!");
        if (Has(PetTraits.Grumpy) || Has(PetTraits.Needy) || Has(PetTraits.Bossy)) return Pick($"{t.ToUpperInvariant()}!!!", $"I WILL NOT STOP. {t.ToUpperInvariant()}.");
        if (Has(PetTraits.Zen)) return $"Still here when you're ready: {t}.";
        return Pick($"{t}! Again!", $"Please, {t}!");
    }

    public string DoneLine(int streak)
    {
        Flash(PetMood.Celebrate, 4);
        ReminderNag = -1;
        string streakText = streak >= 2 ? $" {streak} in a row!" : "";
        if (Has(PetTraits.Grumpy)) return Pick("Finally.", "About time.", "Hmph. Good.") + streakText;
        if (Has(PetTraits.Dramatic)) return Pick("A TRIUMPH!", "Legendary!", "I could cry!") + streakText;
        if (Has(PetTraits.Bossy)) return Pick("Good. Next.", "Noted.", "Productive.") + streakText;
        if (Has(PetTraits.Sassy)) return Pick("Look at you go.", "Okay, show-off.") + streakText;
        if (Has(PetTraits.Shy)) return Pick("Y-yay…!", "Good job… ♥") + streakText;
        return Pick("Nice!", "Yay, done!", "Great job!", "Woohoo!") + streakText;
    }

    public string SnoozeLine(int minutes)
    {
        ReminderNag = -1;
        if (Has(PetTraits.Grumpy) || Has(PetTraits.Bossy)) { Flash(PetMood.Angry, 1.8); return Pick($"{minutes} minutes. Not one more.", "Fine. I'm counting."); }
        if (Has(PetTraits.Needy)) { Flash(PetMood.Sad, 2); return Pick("Okay… I'll wait…", "Don't forget me…"); }
        if (Has(PetTraits.Sleepy)) return Pick("Snooze? Love that.", "Good idea. Nap time.");
        if (Has(PetTraits.Dramatic)) { Flash(PetMood.Sad, 2); return Pick("You BETRAY me… for now.", "The suspense!"); }
        return Pick($"Okay, {minutes} more minutes.", "I'll remind you again.");
    }

    public string? PeerOnlineLine(string name)
    {
        if (!Has(PetTraits.Chatty) && !Has(PetTraits.Cheerful) && !Has(PetTraits.Curious)) return null;
        Flash(PetMood.Happy, 2);
        return Has(PetTraits.Curious) ? $"Ooh, {name} is online!" : Pick($"{name} is here!", $"Say hi to {name}!");
    }

    public string? MessageLine(string sender)
    {
        if (Has(PetTraits.Needy) && random.Next(3) == 0) return $"{sender} again? What about ME?";
        if (Has(PetTraits.Curious) && random.Next(2) == 0) return "Ooh, what did they say?";
        return null;
    }

    private static bool LooksLikeFood(string t)
    {
        string s = t.ToLowerInvariant();
        return new[] { "lunch", "eat", "food", "snack", "coffee", "tea", "breakfast", "dinner", "drink", "meal", "water" }.Any(s.Contains);
    }

    private string Pick(params string[] options) => options[random.Next(options.Length)];
}
