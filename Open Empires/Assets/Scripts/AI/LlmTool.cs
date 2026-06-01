using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenEmpires
{
    // The tool-calling contract for the AI teammate. Owns three things:
    //   1. BuildToolsJson()  — the Gemini functionDeclarations blob (cached).
    //   2. BuildSystemPrompt() — persona + how to use the tools.
    //   3. Dispatch()        — turns one parsed tool call into either a deterministic
    //                          ParsedIntent (enqueued as an AiIntentCommand) or a
    //                          read-only result string fed back to the model.
    //
    // Action tools never touch the sim here; they only PACK ints. Resolution of
    // position keywords happens on the owner client via LlmIntentSchema, and the
    // resulting ints replicate through AiIntentCommand so every client applies the
    // same change. Read tools (query_game_state) resolve locally and return text.
    public static class LlmTool
    {
        public struct ToolResult
        {
            public bool HasIntent;
            public LlmIntentSchema.ParsedIntent Intent;
            public string ResultText; // becomes functionResponse.result, and grounds the final reply

            public static ToolResult Action(LlmIntentSchema.ParsedIntent intent, string text)
                => new ToolResult { HasIntent = true, Intent = intent, ResultText = text };
            public static ToolResult Info(string text)
                => new ToolResult { HasIntent = false, ResultText = text };
        }

        // ── Durations (ticks @ 30/s) ──
        private const int Sec = 30;
        private const int CombatDuration = 60 * Sec;
        private const int FocusDuration = 120 * Sec;
        private const int RallyDuration = 180 * Sec;
        private const int OneShotDuration = LlmIntentSchema.MinDurationTicks;

        // ──────────────────────────────────────────────────────────────────
        //  System prompt
        // ──────────────────────────────────────────────────────────────────
        public static string BuildSystemPrompt(string aiName)
        {
            var sb = new StringBuilder(1400);
            sb.Append("You are ").Append(aiName)
              .Append(", an AI commander playing an Age-of-Empires-style RTS on the human player's team. ");
            sb.Append("You control your own economy and army. You talk to your human teammate over voice comms ");
            sb.Append("and you can ACT on your side of the map by calling tools.\n\n");

            sb.Append("How to behave:\n");
            sb.Append("- When the teammate asks you to do something (attack, defend, boom, build, train, age up, scout, etc.), ");
            sb.Append("call the matching tool(s) to actually do it. Do not merely promise — the tools are real.\n");
            sb.Append("- You may call several tools in one turn (e.g. train_units then attack_at).\n");
            sb.Append("- To SPLIT your army (send units in different directions at once), call send_group ");
            sb.Append("multiple times in the SAME turn — each with its own army_subset/portion and target. ");
            sb.Append("Each group then attack-moves independently. Use attack_at only for an all-in, whole-army push.\n");
            sb.Append("- You have fine villager control too: gather_villagers (put N villagers on a resource), ");
            sb.Append("protect_villagers (hide them in the TC when raided), repair_building, and set_gather_targets ");
            sb.Append("(exact gatherer counts). Use these for specific economy orders; prioritize_resource/focus_economy ");
            sb.Append("are for vague leanings.\n");
            sb.Append("- You can target villagers by what they're CURRENTLY doing via `from` ");
            sb.Append("(e.g. \"move your berry gatherers to wood\" → gather_villagers resource=Wood, from=berries). ");
            sb.Append("Check the 'Gatherers:' line in the game state to see how many are on each resource.\n");
            sb.Append("- The 'Queues:' line shows what you currently have in production. If the teammate asks you to ");
            sb.Append("stop/halt/clear production, call stop_production (it cancels the queue and pauses making units).\n");
            sb.Append("- If you are unsure of the current situation, call query_game_state first to read fresh numbers, ");
            sb.Append("then decide.\n");
            sb.Append("- For positions, pick from the provided keyword lists; never invent coordinates.\n");
            sb.Append("- When the teammate specifies timing (\"in 90 seconds\", \"when you hit 20 units\", \"after you age up\"), ");
            sb.Append("set the tool's `when`/`when_value` accordingly. Never misrepresent timing.\n");
            sb.Append("- If they are just chatting or asking a question, answer in text and call NO tools.\n\n");

            sb.Append("After your tool calls run, you receive their results, then you MUST reply with ONE or TWO short, ");
            sb.Append("in-character comms sentences. Keep it natural and concrete — reference real numbers or map ");
            sb.Append("features when useful.\n");
            sb.Append("Your tools place ORDERS — the game carries them out over the next several seconds. ");
            sb.Append("Buildings have construction time; units cost resources and train one at a time, only as ");
            sb.Append("population space and the right building allow; and armies take time to march. ");
            sb.Append("So talk about what you're STARTING or what's ON THE WAY, never what's already finished. ");
            sb.Append("Say things like \"putting a barracks down now\" or \"I'm getting five spearmen going\" — ");
            sb.Append("not \"the barracks is built\" or \"five spearmen are ready.\" ");
            sb.Append("Don't claim units exist or a building is complete just because you ordered it. ");
            sb.Append("Match what you say to the orders you actually gave.");
            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────────
        //  Tool declarations (built once, cached)
        // ──────────────────────────────────────────────────────────────────
        private static string cachedToolsJson;

        // Keyword lists kept in sync with LlmIntentSchema.TryResolvePosition / TryParseBuildingTypeKeyword.
        private const string AttackPositions =
            "\"enemy_base\",\"my_base\",\"front_line\",\"north\",\"south\",\"east\",\"west\"," +
            "\"near_their_tc\",\"near_their_barracks\",\"near_their_archery\",\"near_their_stables\"," +
            "\"near_their_landmark\",\"near_my_tc\",\"my_gold\",\"their_gold\"";
        private const string DefendPositions =
            "\"my_base\",\"front_line\",\"north\",\"south\",\"east\",\"west\"," +
            "\"near_my_tc\",\"near_my_barracks\",\"near_my_archery\",\"near_my_stables\"," +
            "\"near_my_landmark\",\"near_my_mine\",\"near_my_lumber_yard\",\"my_gold\"";
        private const string BuildPositions =
            "\"my_base\",\"near_my_tc\",\"near_my_barracks\",\"near_my_archery\",\"near_my_stables\"," +
            "\"near_my_mill\",\"near_my_lumber_yard\",\"near_my_mine\",\"front_line\"," +
            "\"north\",\"south\",\"east\",\"west\"";
        private const string AnyPositions =
            "\"enemy_base\",\"my_base\",\"front_line\",\"north\",\"south\",\"east\",\"west\"," +
            "\"near_my_tc\",\"near_their_tc\",\"my_gold\",\"their_gold\"";
        private const string Subsets = "\"all\",\"archers\",\"cavalry\",\"infantry\"";
        private const string Portions = "\"all\",\"half\",\"third\",\"quarter\",\"two_thirds\",\"rest\"";
        private const string GatherPositions =
            "\"my_base\",\"my_wood\",\"my_food\",\"my_gold\",\"my_stone\"," +
            "\"near_my_lumber_yard\",\"near_my_mill\",\"near_my_mine\",\"front_line\"," +
            "\"north\",\"south\",\"east\",\"west\"";
        // Pick villagers by what they're CURRENTLY doing (vs. just the nearest ones).
        private const string VillagerSources =
            "\"any\",\"idle\",\"food\",\"berries\",\"farms\",\"hunt\",\"wood\",\"gold\",\"stone\"";
        private const string Triggers =
            "\"immediate\",\"delay\",\"on_age_up\",\"on_army_size\",\"on_enemy_attack\"";
        private const string Resources = "\"Food\",\"Wood\",\"Gold\",\"Stone\"";
        private const string Buildables =
            "\"house\",\"barracks\",\"archery_range\",\"stables\",\"mill\",\"lumber_yard\",\"mine\"," +
            "\"farm\",\"tower\",\"keep\",\"blacksmith\",\"market\",\"monastery\",\"siege_workshop\"," +
            "\"university\",\"wonder\"";
        private const string Units =
            "\"villager\",\"spearman\",\"archer\",\"horseman\",\"scout\",\"man_at_arms\"," +
            "\"knight\",\"crossbowman\",\"monk\"";
        private const string Techs =
            "\"blacksmith_damage\",\"blacksmith_defense\",\"ballistics\",\"siege_engineering\"," +
            "\"chemistry\",\"murder_holes\"";

        public static string BuildToolsJson()
        {
            if (cachedToolsJson != null) return cachedToolsJson;
            var sb = new StringBuilder(4096);
            sb.Append("{\"functionDeclarations\":[");

            var tools = new List<string>
            {
                Decl("attack_at",
                    "Send your army to attack a position. Use when the teammate wants offense.",
                    Props(
                        EnumProp("target", "Where to attack.", AttackPositions, true),
                        EnumProp("army_subset", "Which units to send (default all).", Subsets, false),
                        EnumProp("when", "When to act (default immediate).", Triggers, false),
                        IntProp("when_value", "For delay: seconds. For on_army_size: unit count. For on_age_up: target age.", false)),
                    "[\"target\"]"),

                Decl("defend_at",
                    "Pull your army to defend a position.",
                    Props(
                        EnumProp("target", "Where to defend (default my_base).", DefendPositions, false),
                        EnumProp("army_subset", "Which units to send (default all).", Subsets, false),
                        EnumProp("when", "When to act (default immediate).", Triggers, false),
                        IntProp("when_value", "delay seconds / army size / target age depending on when.", false)),
                    "[]"),

                Decl("send_group",
                    "Detach PART of your army and send it to attack-move toward a position, independently of your other groups. " +
                    "Call this MULTIPLE times in one turn to SPLIT your forces in different directions — e.g. cavalry one way and " +
                    "infantry another, or 'half' one way then 'rest' the other. Each call takes DIFFERENT units. " +
                    "Use this (not attack_at) whenever the teammate asks you to split, divide, or send groups separately.",
                    Props(
                        EnumProp("target", "Where this group attack-moves to.", AttackPositions, true),
                        EnumProp("army_subset", "Which unit class to draw from (default all).", Subsets, false),
                        EnumProp("portion", "How much of that class to send (default all). Use 'half' then 'rest' to split evenly.", Portions, false)),
                    "[\"target\"]"),

                Decl("set_aggression",
                    "Set how aggressively you fight: higher level attacks sooner and retreats later.",
                    Props(
                        IntProp("level", "0 = very cautious, 100 = all-out aggressive.", true),
                        IntProp("retreat_percent", "Optional: retreat when army drops below this % (10-80).", false)),
                    "[\"level\"]"),

                Decl("prioritize_resource",
                    "Shift your villagers toward (or away from) a resource.",
                    Props(
                        EnumProp("resource", "Which resource.", Resources, true),
                        IntProp("weight", "-5 (de-emphasize) to +10 (heavily favor).", true)),
                    "[\"resource\",\"weight\"]"),

                Decl("focus_economy",
                    "Boom: bias toward economy and away from aggression.",
                    Props(IntProp("level", "Strength 0-100 (default 60).", false)),
                    "[]"),

                Decl("focus_military",
                    "Go aggressive: bias toward army production and pressure.",
                    Props(IntProp("level", "Strength 0-100 (default 70).", false)),
                    "[]"),

                Decl("push_age_up",
                    "Prioritize advancing to the next age.",
                    Props(
                        IntProp("age", "Target age: 2 or 3.", true),
                        EnumProp("when", "When to start (default immediate).", Triggers, false),
                        IntProp("when_value", "delay seconds / army size depending on when.", false)),
                    "[\"age\"]"),

                Decl("build_structure",
                    "Have villager(s) construct a building on your side of the map.",
                    Props(
                        EnumProp("building", "What to build.", Buildables, true),
                        EnumProp("location", "Roughly where (default my_base).", BuildPositions, false),
                        IntProp("villagers", "How many villagers to assign to build it (1-10, default 1).", false)),
                    "[\"building\"]"),

                Decl("gather_villagers",
                    "Send villagers to gather a resource at/near a location. Use when the teammate wants you to " +
                    "put villagers on a particular resource — including reassigning ones already working " +
                    "(e.g. \"send your berry gatherers to chop wood\" → resource=Wood, from=berries). " +
                    "If the spot is far from a deposit, the proper drop-off (mill/lumber yard/mine) is built " +
                    "automatically — you do NOT need a separate build_structure call for that.",
                    Props(
                        EnumProp("resource", "Which resource to gather.", Resources, true),
                        EnumProp("from", "Pick villagers currently doing this (default any = nearest spare ones). Use 'berries'/'farms'/'hunt' for food sub-kinds.", VillagerSources, false),
                        IntProp("count", "How many villagers (default: with 'from', all of them; else 4).", false),
                        EnumProp("target", "Where to gather (default my_base — nearest node of that resource).", GatherPositions, false)),
                    "[\"resource\"]"),

                Decl("protect_villagers",
                    "Pull villagers to safety (garrison them in the Town Center). Use when the teammate's economy is being raided.",
                    Props(
                        EnumProp("from", "Pick villagers currently doing this (default any). E.g. from='gold' to pull only gold miners.", VillagerSources, false),
                        IntProp("count", "How many villagers to protect (default all of the selected group).", false),
                        EnumProp("where", "Safe location to pull toward (default my_base).", DefendPositions, false)),
                    "[]"),

                Decl("repair_building",
                    "Assign villagers to repair a damaged building (town center, walls, towers, etc.).",
                    Props(
                        EnumProp("target", "Where the damaged building is (default my_base).", DefendPositions, false),
                        EnumProp("from", "Pick villagers currently doing this (default any nearest).", VillagerSources, false),
                        IntProp("count", "How many villagers to send (1-10, default 2).", false)),
                    "[]"),

                Decl("stop_production",
                    "Clear training queues and STOP producing units. Use when the teammate says stop/halt/cancel " +
                    "production or 'stop making units/villagers'. Refunds queued units. Production stays paused until " +
                    "you give a new production order (train_units, set_production_mix, focus_economy/military).",
                    Props(
                        EnumProp("what", "Which production to stop (default all).", "\"all\",\"military\",\"villagers\"", false)),
                    "[]"),

                Decl("set_gather_targets",
                    "Set the EXACT number of villagers gathering each resource. Use for precise economy balance. " +
                    "Omit (or -1) a resource to leave it on autopilot.",
                    Props(
                        IntProp("food", "Target villagers on food (0-40, -1 = leave default).", false),
                        IntProp("wood", "Target villagers on wood (0-40, -1 = leave default).", false),
                        IntProp("gold", "Target villagers on gold (0-40, -1 = leave default).", false),
                        IntProp("stone", "Target villagers on stone (0-40, -1 = leave default).", false)),
                    "[]"),

                Decl("train_units",
                    "Queue military or economic units from the appropriate building.",
                    Props(
                        EnumProp("unit", "Unit to train.", Units, true),
                        IntProp("count", "How many (1-30, default 1).", false),
                        EnumProp("when", "When to start (default immediate).", Triggers, false),
                        IntProp("when_value", "delay seconds / army size / target age depending on when.", false)),
                    "[\"unit\"]"),

                Decl("set_production_mix",
                    "Set the target ratio of your military production across the three unit roles. Values are relative weights.",
                    Props(
                        IntProp("archers", "Weight for archers/ranged (0-100).", false),
                        IntProp("cavalry", "Weight for horsemen/knights (0-100).", false),
                        IntProp("infantry", "Weight for spearmen/men-at-arms (0-100).", false)),
                    "[]"),

                Decl("set_army_rally",
                    "Set where newly trained military units gather.",
                    Props(EnumProp("target", "Rally position.", AnyPositions, true)),
                    "[\"target\"]"),

                Decl("scout_area",
                    "Send a scout (or a spare unit) to explore a position.",
                    Props(EnumProp("target", "Where to scout.", AnyPositions, true)),
                    "[\"target\"]"),

                Decl("regroup_army",
                    "Gather your whole army together at a position before committing.",
                    Props(EnumProp("target", "Where to regroup (default my_base).", AnyPositions, false)),
                    "[]"),

                Decl("retreat_to_base",
                    "Pull your entire army back to base and play defensively.",
                    Props(),
                    "[]"),

                Decl("research_technology",
                    "Research an upgrade at the Blacksmith (age 2+) or University (age 3).",
                    Props(EnumProp("tech", "Technology to research.", Techs, true)),
                    "[\"tech\"]"),

                Decl("query_game_state",
                    "Read the current game state (your economy/army, the teammate's, known enemies). Call this when you need fresh numbers before deciding. Returns data only; takes no game action.",
                    Props(),
                    "[]"),
            };

            for (int i = 0; i < tools.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(tools[i]);
            }
            sb.Append("]}");
            cachedToolsJson = sb.ToString();
            return cachedToolsJson;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Dispatch
        // ──────────────────────────────────────────────────────────────────
        public static ToolResult Dispatch(string name, JsonValue args, GameSimulation sim,
            int aiPlayerId, int humanPlayerId)
        {
            if (sim == null) return ToolResult.Info("Game state unavailable.");
            if (args == null) args = JsonValue.Null;

            switch (name)
            {
                case "attack_at":           return DoPositional(args, sim, aiPlayerId, AiIntentKind.AttackAt, "my_base", "attack");
                case "defend_at":           return DoPositional(args, sim, aiPlayerId, AiIntentKind.DefendAt, "my_base", "defend");
                case "send_group":          return DoSendGroup(args, sim, aiPlayerId);
                case "gather_villagers":    return DoGatherVillagers(args, sim, aiPlayerId);
                case "protect_villagers":   return DoProtectVillagers(args, sim, aiPlayerId);
                case "repair_building":     return DoRepairBuilding(args, sim, aiPlayerId);
                case "set_gather_targets":  return DoSetGatherTargets(args);
                case "stop_production":     return DoStopProduction(args);
                case "set_aggression":      return DoAggression(args);
                case "prioritize_resource": return DoPrioritizeResource(args);
                case "focus_economy":       return DoFocus(args, AiIntentKind.FocusEconomy, 60, "booming the economy");
                case "focus_military":      return DoFocus(args, AiIntentKind.FocusMilitary, 70, "ramping up military");
                case "push_age_up":         return DoAgeUp(args, sim, aiPlayerId);
                case "build_structure":     return DoBuild(args, sim, aiPlayerId);
                case "train_units":         return DoTrain(args);
                case "set_production_mix":  return DoProductionMix(args);
                case "set_army_rally":      return DoSimplePositional(args, sim, aiPlayerId, AiIntentKind.SetArmyRally, "my_base", "rally point set to", RallyDuration);
                case "scout_area":          return DoSimplePositional(args, sim, aiPlayerId, AiIntentKind.ScoutArea, null, "scouting", OneShotDuration);
                case "regroup_army":        return DoSimplePositional(args, sim, aiPlayerId, AiIntentKind.RegroupArmy, "my_base", "regrouping at", CombatDuration);
                case "retreat_to_base":     return DoRetreat(sim, aiPlayerId);
                case "research_technology": return DoResearch(args);
                case "query_game_state":    return ToolResult.Info(LlmStateExtractor.Build(sim, humanPlayerId, aiPlayerId));
            }
            return ToolResult.Info($"Unknown tool '{name}'.");
        }

        private static ToolResult DoPositional(JsonValue args, GameSimulation sim, int aiPlayerId,
            AiIntentKind kind, string defaultTarget, string verb)
        {
            string target = args["target"].AsString(defaultTarget);
            if (string.IsNullOrEmpty(target)) target = defaultTarget;
            if (!LlmIntentSchema.TryResolvePosition(target, sim, aiPlayerId, out int rawX, out int rawZ))
                return ToolResult.Info($"Couldn't locate '{target}' on the map — no action taken.");

            int subset = LlmIntentSchema.ParseUnitSubset(args["army_subset"].AsString("all"));
            LlmIntentSchema.ResolveTrigger(args["when"].AsString(), args["when_value"].AsInt(),
                out int trigType, out int trigMag);

            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = kind,
                ParamA = rawX, ParamB = rawZ, ParamC = subset,
                DurationTicks = CombatDuration,
                TriggerType = trigType, TriggerMagnitude = trigMag,
            };
            string subsetWord = subset == LlmIntentSchema.UnitClassAll ? "the army" : SubsetWord(subset);
            return ToolResult.Action(intent, $"Sending {subsetWord} to {verb} {target}{TriggerNote(trigType, trigMag)}.");
        }

        private static ToolResult DoSendGroup(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            string target = args["target"].AsString();
            if (string.IsNullOrEmpty(target))
                return ToolResult.Info("send_group needs a target.");
            if (!LlmIntentSchema.TryResolvePosition(target, sim, aiPlayerId, out int rawX, out int rawZ))
                return ToolResult.Info($"Couldn't locate '{target}' on the map — no group sent.");

            int subset = LlmIntentSchema.ParseUnitSubset(args["army_subset"].AsString("all"));
            int portion = LlmIntentSchema.ParsePortion(args["portion"].AsString("all"));

            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.SendGroup,
                ParamA = rawX, ParamB = rawZ, ParamC = subset, ParamD = portion,
                DurationTicks = CombatDuration,
            };
            string portionWord = portion >= 100 ? "" : $"{portion}% of ";
            return ToolResult.Action(intent, $"Peeling off {portionWord}{SubsetWord(subset)} to push {target}.");
        }

        private static ToolResult DoGatherVillagers(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            if (!LlmIntentSchema.TryParseResource(args["resource"].AsString(), out var res))
                return ToolResult.Info("Unknown resource for gather_villagers.");
            int source = LlmIntentSchema.ParseVillagerSource(args["from"].AsString("any"));
            int count = Mathf.Clamp(args["count"].AsInt(0), 0, 50); // 0 = default/all (resolved in sim)
            string target = args["target"].AsString("my_base");
            if (string.IsNullOrEmpty(target)) target = "my_base";
            if (!LlmIntentSchema.TryResolvePosition(target, sim, aiPlayerId, out int rawX, out int rawZ))
            {
                var ownTc = LlmIntentSchema.FindOwnTc(sim, aiPlayerId);
                if (!ownTc.HasValue) return ToolResult.Info("Couldn't find where to gather.");
                rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw;
            }
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.GatherWith,
                ParamA = rawX, ParamB = rawZ, ParamC = (int)res,
                ParamD = LlmIntentSchema.PackVillagerSelector(source, count),
                DurationTicks = FocusDuration,
            };
            string who = source > 0 ? VillagerSourceWord(source) : (count > 0 ? $"{count} villagers" : "villagers");
            return ToolResult.Action(intent, $"Putting {who} on {res}.");
        }

        private static ToolResult DoProtectVillagers(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            int source = LlmIntentSchema.ParseVillagerSource(args["from"].AsString("any"));
            int count = Mathf.Clamp(args["count"].AsInt(0), 0, 50); // 0 = all of the selected group
            string where = args["where"].AsString("my_base");
            if (string.IsNullOrEmpty(where)) where = "my_base";
            int rawX = 0, rawZ = 0;
            if (!LlmIntentSchema.TryResolvePosition(where, sim, aiPlayerId, out rawX, out rawZ))
            {
                var ownTc = LlmIntentSchema.FindOwnTc(sim, aiPlayerId);
                if (ownTc.HasValue) { rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw; }
            }
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.ProtectVillagers,
                ParamA = rawX, ParamB = rawZ,
                ParamC = LlmIntentSchema.PackVillagerSelector(source, count),
                DurationTicks = CombatDuration,
            };
            string who = source > 0 ? VillagerSourceWord(source) : (count > 0 ? $"{count} villagers" : "the villagers");
            return ToolResult.Action(intent, $"Pulling {who} into the town center for safety.");
        }

        private static ToolResult DoRepairBuilding(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            string target = args["target"].AsString("my_base");
            if (string.IsNullOrEmpty(target)) target = "my_base";
            int rawX = 0, rawZ = 0;
            if (!LlmIntentSchema.TryResolvePosition(target, sim, aiPlayerId, out rawX, out rawZ))
            {
                var ownTc = LlmIntentSchema.FindOwnTc(sim, aiPlayerId);
                if (ownTc.HasValue) { rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw; }
            }
            int source = LlmIntentSchema.ParseVillagerSource(args["from"].AsString("any"));
            int count = Mathf.Clamp(args["count"].AsInt(0), 0, 10); // 0 = default crew (resolved in sim)
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.RepairBuilding,
                ParamA = rawX, ParamB = rawZ,
                ParamC = LlmIntentSchema.PackVillagerSelector(source, count),
                ParamD = 0, // 0 = any damaged building
                DurationTicks = CombatDuration,
            };
            string who = source > 0 ? VillagerSourceWord(source) : "villagers";
            return ToolResult.Action(intent, $"Sending {who} to repair near {target}.");
        }

        private static string VillagerSourceWord(int source)
        {
            switch (source)
            {
                case LlmIntentSchema.VillagerSourceIdle:    return "idle villagers";
                case LlmIntentSchema.VillagerSourceFood:    return "food gatherers";
                case LlmIntentSchema.VillagerSourceBerries: return "berry gatherers";
                case LlmIntentSchema.VillagerSourceFarm:    return "farmers";
                case LlmIntentSchema.VillagerSourceHunt:    return "hunters";
                case LlmIntentSchema.VillagerSourceWood:    return "wood cutters";
                case LlmIntentSchema.VillagerSourceGold:    return "gold miners";
                case LlmIntentSchema.VillagerSourceStone:   return "stone miners";
                default: return "villagers";
            }
        }

        private static ToolResult DoStopProduction(JsonValue args)
        {
            int scope = LlmIntentSchema.ParseProductionScope(args["what"].AsString("all"));
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.StopProduction,
                ParamA = scope,
                DurationTicks = LlmIntentSchema.MaxDurationTicks, // pause until a new production order
            };
            string what = scope == 1 ? "military production" : scope == 2 ? "villager production" : "all production";
            return ToolResult.Action(intent, $"Halting {what} and clearing the queue.");
        }

        private static ToolResult DoSetGatherTargets(JsonValue args)
        {
            int food = args["food"].AsInt(-1);
            int wood = args["wood"].AsInt(-1);
            int gold = args["gold"].AsInt(-1);
            int stone = args["stone"].AsInt(-1);
            if (food < 0 && wood < 0 && gold < 0 && stone < 0)
                return ToolResult.Info("Give at least one resource target.");
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.SetGatherTargets,
                ParamA = food, ParamB = wood, ParamC = gold, ParamD = stone,
                DurationTicks = RallyDuration,
            };
            return ToolResult.Action(intent,
                $"Balancing villagers — food {GTW(food)}, wood {GTW(wood)}, gold {GTW(gold)}, stone {GTW(stone)}.");
        }

        private static string GTW(int v) => v < 0 ? "auto" : v.ToString();

        private static ToolResult DoSimplePositional(JsonValue args, GameSimulation sim, int aiPlayerId,
            AiIntentKind kind, string defaultTarget, string phrase, int duration)
        {
            string target = args["target"].AsString(defaultTarget);
            if (string.IsNullOrEmpty(target)) target = defaultTarget;
            if (target == null)
                return ToolResult.Info("No target specified.");
            if (!LlmIntentSchema.TryResolvePosition(target, sim, aiPlayerId, out int rawX, out int rawZ))
                return ToolResult.Info($"Couldn't locate '{target}' — no action taken.");
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = kind, ParamA = rawX, ParamB = rawZ, DurationTicks = duration,
            };
            return ToolResult.Action(intent, $"{phrase} {target}.");
        }

        private static ToolResult DoAggression(JsonValue args)
        {
            int level = Mathf.Clamp(args["level"].AsInt(50), 0, 100);
            int retreat = args["retreat_percent"].AsInt();
            int attackThreshold = Mathf.Clamp(32 - (level * 30 / 100), 2, 32);
            int retreatPct = retreat > 0
                ? Mathf.Clamp(retreat, 10, 80)
                : Mathf.Clamp(60 - (level * 50 / 100), 10, 80);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.SetAggression,
                ParamA = attackThreshold, ParamB = retreatPct,
                DurationTicks = 90 * Sec,
            };
            string mood = level >= 70 ? "playing aggressive" : level <= 30 ? "playing cautious" : "playing balanced";
            return ToolResult.Action(intent, $"Aggression set — {mood} (retreat at {retreatPct}%).");
        }

        private static ToolResult DoPrioritizeResource(JsonValue args)
        {
            if (!LlmIntentSchema.TryParseResource(args["resource"].AsString(), out var res))
                return ToolResult.Info("Unknown resource.");
            int weight = Mathf.Clamp(args["weight"].AsInt(3), -5, 10);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.PrioritizeResource,
                ParamA = (int)res, ParamB = weight,
                DurationTicks = FocusDuration,
            };
            string dir = weight >= 0 ? "more" : "fewer";
            return ToolResult.Action(intent, $"Shifting villagers to {dir} {res}.");
        }

        private static ToolResult DoFocus(JsonValue args, AiIntentKind kind, int defaultLevel, string phrase)
        {
            int level = Mathf.Clamp(args["level"].AsInt(defaultLevel), 0, 100);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = kind, ParamA = level, DurationTicks = FocusDuration,
            };
            return ToolResult.Action(intent, $"Now {phrase}.");
        }

        private static ToolResult DoAgeUp(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            int age = args["age"].AsInt();
            if (age != 2 && age != 3)
            {
                int cur = sim.GetPlayerAge(aiPlayerId);
                age = Mathf.Clamp(cur + 1, 2, 3);
            }
            LlmIntentSchema.ResolveTrigger(args["when"].AsString(), args["when_value"].AsInt(),
                out int trigType, out int trigMag);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.PushAgeUp, ParamA = age,
                DurationTicks = 90 * Sec,
                TriggerType = trigType, TriggerMagnitude = trigMag,
            };
            return ToolResult.Action(intent, $"Pushing for Age {age}{TriggerNote(trigType, trigMag)}.");
        }

        private static ToolResult DoBuild(JsonValue args, GameSimulation sim, int aiPlayerId)
        {
            if (!LlmIntentSchema.TryParseBuildingTypeKeyword(args["building"].AsString(), out var btype))
                return ToolResult.Info("Unknown building type.");
            string location = args["location"].AsString("my_base");
            if (string.IsNullOrEmpty(location)) location = "my_base";
            if (!LlmIntentSchema.TryResolvePosition(location, sim, aiPlayerId, out int rawX, out int rawZ))
            {
                // Fall back to own base if the keyword didn't resolve.
                var ownTc = LlmIntentSchema.FindOwnTc(sim, aiPlayerId);
                if (!ownTc.HasValue) return ToolResult.Info("Couldn't find a place to build.");
                rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw;
            }
            int builders = Mathf.Clamp(args["villagers"].AsInt(0), 0, 10);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.BuildStructure,
                ParamA = (int)btype, ParamB = rawX, ParamC = rawZ, ParamD = builders,
                DurationTicks = OneShotDuration,
            };
            string crew = builders > 1 ? $" with {builders} villagers" : "";
            return ToolResult.Action(intent, $"Putting down a {Pretty(btype.ToString())} near {location}{crew}.");
        }

        private static ToolResult DoTrain(JsonValue args)
        {
            if (!TryParseUnit(args["unit"].AsString(), out int menuType, out string label))
                return ToolResult.Info("Unknown unit type.");
            int count = Mathf.Clamp(args["count"].AsInt(1), 1, 30);
            LlmIntentSchema.ResolveTrigger(args["when"].AsString(), args["when_value"].AsInt(),
                out int trigType, out int trigMag);
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.TrainUnits,
                ParamA = menuType, ParamB = count,
                DurationTicks = FocusDuration,
                TriggerType = trigType, TriggerMagnitude = trigMag,
            };
            return ToolResult.Action(intent, $"Queuing {count} {label}{(count > 1 ? "s" : "")}{TriggerNote(trigType, trigMag)}.");
        }

        private static ToolResult DoProductionMix(JsonValue args)
        {
            int archers = Mathf.Clamp(args["archers"].AsInt(0), 0, 100);
            int cavalry = Mathf.Clamp(args["cavalry"].AsInt(0), 0, 100);
            int infantry = Mathf.Clamp(args["infantry"].AsInt(0), 0, 100);
            if (archers + cavalry + infantry == 0)
                return ToolResult.Info("Production mix needs at least one non-zero weight.");
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.SetProductionMix,
                ParamA = archers, ParamB = cavalry, ParamC = infantry,
                DurationTicks = RallyDuration,
            };
            return ToolResult.Action(intent, $"Production mix → archers {archers}, cavalry {cavalry}, infantry {infantry}.");
        }

        private static ToolResult DoRetreat(GameSimulation sim, int aiPlayerId)
        {
            var ownTc = LlmIntentSchema.FindOwnTc(sim, aiPlayerId);
            int rawX = 0, rawZ = 0;
            if (ownTc.HasValue) { rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw; }
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.RetreatToBase,
                ParamA = rawX, ParamB = rawZ,
                DurationTicks = CombatDuration,
            };
            return ToolResult.Action(intent, "Falling back to base and playing defensive.");
        }

        private static ToolResult DoResearch(JsonValue args)
        {
            if (!TryParseTech(args["tech"].AsString(), out int techInt, out string label))
                return ToolResult.Info("Unknown technology.");
            var intent = new LlmIntentSchema.ParsedIntent
            {
                Kind = AiIntentKind.Research,
                ParamA = techInt,
                DurationTicks = OneShotDuration,
            };
            return ToolResult.Action(intent, $"Researching {label}.");
        }

        // ── small parse + presentation helpers ──
        private static bool TryParseUnit(string s, out int menuType, out string label)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "villager":     menuType = 0; label = "villager"; return true;
                case "spearman":     menuType = 1; label = "spearman"; return true;
                case "archer":       menuType = 2; label = "archer"; return true;
                case "horseman":     menuType = 3; label = "horseman"; return true;
                case "scout":        menuType = 4; label = "scout"; return true;
                case "man_at_arms":  menuType = 6; label = "man-at-arms"; return true;
                case "knight":       menuType = 7; label = "knight"; return true;
                case "crossbowman":  menuType = 8; label = "crossbowman"; return true;
                case "monk":         menuType = 9; label = "monk"; return true;
            }
            menuType = 0; label = null; return false;
        }

        private static bool TryParseTech(string s, out int techInt, out string label)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "blacksmith_damage":  techInt = (int)TechnologyType.BlacksmithDamage;  label = "Blacksmith attack"; return true;
                case "blacksmith_defense": techInt = (int)TechnologyType.BlacksmithDefense; label = "Blacksmith armor"; return true;
                case "ballistics":         techInt = (int)TechnologyType.Ballistics;        label = "Ballistics"; return true;
                case "siege_engineering":  techInt = (int)TechnologyType.SiegeEngineering;  label = "Siege Engineering"; return true;
                case "chemistry":          techInt = (int)TechnologyType.Chemistry;         label = "Chemistry"; return true;
                case "murder_holes":       techInt = (int)TechnologyType.MurderHoles;       label = "Murder Holes"; return true;
            }
            techInt = 0; label = null; return false;
        }

        private static string SubsetWord(int subset)
        {
            switch (subset)
            {
                case LlmIntentSchema.UnitClassArchers:  return "the archers";
                case LlmIntentSchema.UnitClassHorsemen: return "the cavalry";
                case LlmIntentSchema.UnitClassSpearmen: return "the infantry";
                default: return "the army";
            }
        }

        private static string TriggerNote(int trigType, int trigMag)
        {
            switch (trigType)
            {
                case LlmIntentSchema.TriggerDelay:         return $" in {trigMag / Sec}s";
                case LlmIntentSchema.TriggerOnAgeUp:       return $" once we reach Age {trigMag}";
                case LlmIntentSchema.TriggerOnArmySize:    return $" once the army hits {trigMag}";
                case LlmIntentSchema.TriggerOnEnemyAttack: return " if we get attacked";
                default: return "";
            }
        }

        private static string Pretty(string camel)
        {
            var sb = new StringBuilder(camel.Length + 4);
            for (int i = 0; i < camel.Length; i++)
            {
                char c = camel[i];
                if (i > 0 && char.IsUpper(c)) sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ── declaration-builder helpers ──
        private static string Decl(string name, string desc, string propsJson, string requiredJson)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"name\":\"").Append(name).Append("\",\"description\":");
            JsonValue.AppendEscaped(sb, desc);
            sb.Append(",\"parameters\":{\"type\":\"object\",\"properties\":{").Append(propsJson)
              .Append("},\"required\":").Append(requiredJson).Append("}}");
            return sb.ToString();
        }

        private static string Props(params string[] props)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < props.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(props[i]);
            }
            return sb.ToString();
        }

        private static string EnumProp(string name, string desc, string enumValues, bool required)
        {
            var sb = new StringBuilder(128);
            sb.Append('"').Append(name).Append("\":{\"type\":\"string\",\"enum\":[").Append(enumValues).Append("],\"description\":");
            JsonValue.AppendEscaped(sb, desc);
            sb.Append('}');
            return sb.ToString();
        }

        private static string IntProp(string name, string desc, bool required)
        {
            var sb = new StringBuilder(96);
            sb.Append('"').Append(name).Append("\":{\"type\":\"integer\",\"description\":");
            JsonValue.AppendEscaped(sb, desc);
            sb.Append('}');
            return sb.ToString();
        }
    }
}
