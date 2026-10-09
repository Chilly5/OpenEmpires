using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    [JsonObject(MemberSerialization.OptOut)]
    public sealed class CommanderIntentDTO
    {
        public string intentCategory;
        public string intentType;
        public string action;
        public string location;
        public JObject target;
        public string technology;
        public int? unitType;
        public string objectiveType;
        public int? priority;
        public Dictionary<string, string> parameters;
        public string unit;
        public string structure;
        public string resource;
        public string mode;
        public string targetAge;
        public string countMode;
        public string quantityMode;
        public string workerState;
        public string currentResource;
        public string sourceKind;
        public int? amount;
        public int? resourceAmount;
        public string resourceAmountMode;
        public List<CommanderConstraintDTO> constraints = new List<CommanderConstraintDTO>();
    }

    [JsonObject(MemberSerialization.OptOut)]
    public sealed class CommanderConstraintDTO
    {
        public string type;
        public string mode;
        public string resource;
        public string sourceKind;
        public int? amount;
    }

    // Untrusted JSON is parsed as data, never polymorphically deserialized into runtime intents.
    public static class CommanderIntentDtoCodec
    {
        public const int MaximumResponseCharacters = 16384;
        private static readonly Regex JsonNumber = new Regex(@"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static string Serialize(CommanderIntentDTO dto) => JsonConvert.SerializeObject(dto, Settings);
        public static string ToJson(CommanderIntentDTO dto) => Serialize(dto);

        public static CommanderIntentInterpretation InterpretJson(string json, CommanderContext context)
        {
            if (context == null) return Reject(CommanderIntentErrorCode.InvalidPlayer, "context", "Missing trusted player context.");
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumResponseCharacters)
                    throw new JsonException("Response is empty or exceeds the size limit.");
                CheckJsonSyntax(json);
                JObject root;
                using (var reader = new StrictJsonReader(new StringReader(json)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                    if (reader.Read()) throw new JsonException("Trailing JSON content is not allowed.");
                }
                NormalizeExternalJson(root);
                CheckFields(root, "intentCategory", "intentType", "action", "location", "technology", "unitType", "objectiveType", "priority", "parameters", "unit", "structure", "resource", "mode", "targetAge", "amount", "constraints", "countMode", "workerState", "currentResource", "sourceKind", "target", "quantityMode", "resourceAmount", "resourceAmountMode");
                var dto = new CommanderIntentDTO
                {
                    intentCategory = ReadString(root, "intentCategory"),
                    intentType = ReadString(root, "intentType"),
                    action = ReadString(root, "action"),
                    location = ReadString(root, "location"),
                    technology = ReadString(root, "technology"),
                    unitType = ReadAmount(root, "unitType"),
                    objectiveType = ReadString(root, "objectiveType"),
                    priority = ReadAmount(root, "priority"),
                    unit = ReadString(root, "unit"),
                    structure = ReadString(root, "structure"),
                    resource = ReadString(root, "resource"),
                    mode = ReadString(root, "mode"),
                    targetAge = ReadString(root, "targetAge"),
                    countMode = ReadString(root, "countMode"),
                    quantityMode = ReadString(root, "quantityMode"),
                    workerState = ReadString(root, "workerState"),
                    currentResource = ReadString(root, "currentResource"),
                    sourceKind = ReadString(root, "sourceKind"),
                    amount = ReadAmount(root, "amount"),
                    resourceAmount = ReadAmount(root, "resourceAmount"),
                    resourceAmountMode = ReadString(root, "resourceAmountMode")
                };
                if (root.TryGetValue("target", out var targetToken))
                {
                    if (!(targetToken is JObject targetObject)) throw new JsonException();
                    dto.target = targetObject;
                }
                if (root.TryGetValue("parameters", out JToken paramsToken))
                {
                    if (!(paramsToken is JObject paramsObj))
                        throw new JsonException("parameters must be an object.");
                    dto.parameters = new Dictionary<string, string>();
                    foreach (var prop in paramsObj.Properties())
                    {
                        dto.parameters[prop.Name] = prop.Value?.ToString() ?? string.Empty;
                    }
                }
                if (root.TryGetValue("constraints", out JToken constraints))
                {
                    if (!(constraints is JArray array) || array.Count > 4) throw new JsonException("constraints must be an array of at most four objects.");
                    foreach (var item in array)
                    {
                        if (!(item is JObject constraint)) throw new JsonException("Each constraint must be an object.");
                        CheckFields(constraint, "type", "mode", "resource", "amount", "sourceKind");
                        dto.constraints.Add(new CommanderConstraintDTO { type = ReadString(constraint, "type"),
                            mode = ReadString(constraint, "mode"), resource = ReadString(constraint, "resource"), amount = ReadAmount(constraint, "amount"), sourceKind = ReadString(constraint, "sourceKind") });
                    }
                }
                return ValidateAndConvert(dto, context);
            }
            catch (Exception error) when (error is JsonException || error is OverflowException || error is FormatException)
            { return Reject(CommanderIntentErrorCode.InvalidJson, "response", error.Message); }
        }

        public static CommanderIntentInterpretation ValidateAndConvert(CommanderIntentDTO dto, CommanderContext context)
        {
            if (dto?.target != null && (dto.intentType != nameof(CommanderIntentType.CapabilityAction)
                || string.Equals(dto.intentCategory, "Strategic", StringComparison.OrdinalIgnoreCase))) return UnexpectedFields();
            if (dto?.quantityMode != null && (dto.intentType != nameof(CommanderIntentType.EnsureUnitCount)
                || string.Equals(dto.intentCategory, "Strategic", StringComparison.OrdinalIgnoreCase))) return UnexpectedFields();
            if (dto != null && dto.intentType != nameof(CommanderIntentType.AllocateWorkers)
                && (dto.countMode != null || dto.workerState != null || dto.currentResource != null || dto.sourceKind != null
                    || dto.resourceAmount.HasValue || dto.resourceAmountMode != null))
                return UnexpectedFields();
            if (context == null) return Reject(CommanderIntentErrorCode.InvalidPlayer, "context", "Missing trusted player context.");
            if (dto == null) return Reject(CommanderIntentErrorCode.UnknownCommand, "intentType", "No intent DTO supplied.");

            bool isStrategic = string.Equals(dto.intentCategory, "Strategic", StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(dto.intentCategory) && !string.IsNullOrEmpty(dto.objectiveType) && string.IsNullOrEmpty(dto.intentType));

            if (isStrategic)
            {
                string rawObjective = !string.IsNullOrWhiteSpace(dto.objectiveType) ? dto.objectiveType : dto.intentType;
                if (string.IsNullOrWhiteSpace(rawObjective))
                    return Reject(CommanderIntentErrorCode.UnknownCommand, "objectiveType", "Missing strategic objective type.");
                if (!NamedEnum(rawObjective, out StrategicObjectiveType objectiveType)
                    && !Enum.TryParse(rawObjective, true, out objectiveType)
                    && !TryResolveObjectiveAlias(rawObjective, out objectiveType))
                    return Reject(CommanderIntentErrorCode.UnknownCommand, "objectiveType", "Unknown strategic objective type.");
                if (dto.action != null || dto.location != null || dto.technology != null || dto.unitType.HasValue
                    || dto.unit != null || dto.structure != null || dto.resource != null || dto.mode != null || dto.targetAge != null)
                    return UnexpectedFields();

                var parameters = dto.parameters != null
                    ? new Dictionary<string, string>(dto.parameters)
                    : new Dictionary<string, string>();
                if (dto.amount.HasValue && !parameters.ContainsKey("targetCount"))
                {
                    parameters["targetCount"] = dto.amount.Value.ToString();
                }

                var strategic = StrategicIntent.FromPlayerInterpretation(
                    context.PlayerId,
                    objectiveType,
                    context.SnapshotTick,
                    parameters,
                    dto.priority);

                return CommanderIntentInterpretation.AcceptedStrategic(strategic);
            }

            if (!NamedEnum(dto.intentType, out CommanderIntentType type))
                return Reject(CommanderIntentErrorCode.UnknownCommand, "intentType", "Unknown intent type.");
            if (type != CommanderIntentType.CapabilityAction
                && (dto.action != null || dto.location != null || dto.technology != null || dto.unitType.HasValue))
                return UnexpectedFields();
            var constraints = new List<CommanderConstraint>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (dto.constraints == null || dto.constraints.Count > 4)
                return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "Invalid constraint collection.");
            foreach (var constraint in dto.constraints)
            {
                if (constraint == null || constraint.type == null || !seen.Add(constraint.type))
                    return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "Empty or duplicate constraint.");
                if (constraint.sourceKind != null && constraint.type != "ResourceSource")
                    return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "SourceKind belongs only to ResourceSource.");
                switch (constraint.type)
                {
                    case "ResourceSource":
                        if ((type != CommanderIntentType.EnsureUnitCount && type != CommanderIntentType.BuildStructure
                                && type != CommanderIntentType.ReachAge)
                            || constraint.mode != null || constraint.amount.HasValue
                            || !NamedEnum(constraint.resource, out ResourceType sourceResource)
                            || !NamedEnum(constraint.sourceKind, out ResourceSourceKind sourceKind)
                            || !ResourceSourceRules.IsCompatible(sourceResource, sourceKind))
                            return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "ResourceSource requires a compatible resource/source pair for preparation.");
                        constraints.Add(new ResourceSourceConstraint(sourceResource, sourceKind)); break;
                    case "NoConstruction":
                        if ((type != CommanderIntentType.EnsureUnitCount && type != CommanderIntentType.ReachAge)
                            || constraint.mode != null || constraint.resource != null || constraint.amount.HasValue)
                            return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "NoConstruction forbids new and resumed construction and takes no parameters.");
                        constraints.Add(new NoConstructionConstraint()); break;
                    case "PreferredWorkers":
                        if (constraint.mode != "IdleOnly" || constraint.resource != null || constraint.amount.HasValue)
                            return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "PreferredWorkers requires only mode IdleOnly.");
                        constraints.Add(new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly)); break;
                    case "MaximumQueue":
                        if (type != CommanderIntentType.EnsureUnitCount || constraint.mode != null || constraint.resource != null
                            || !constraint.amount.HasValue || constraint.amount < 1 || constraint.amount > CommanderIntentValidator.MaximumQueuePolicy)
                            return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "MaximumQueue needs a valid unit-production queue limit.");
                        constraints.Add(new MaximumQueueConstraint(constraint.amount.Value)); break;
                    case "ProtectedResource":
                        if (!NamedEnum(constraint.resource, out ResourceType protectedResource) || constraint.mode != null
                            || constraint.amount < 0 || constraint.amount > context.MaximumPopulation)
                            return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "Invalid protected resource or worker floor.");
                        constraints.Add(new ProtectedResourceConstraint(protectedResource, constraint.amount)); break;
                    default: return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "constraints", "Unknown constraint type.");
                }
            }
            CommanderIntent intent;
            switch (type)
            {
                case CommanderIntentType.EnsureUnitCount:
                    int unitType;
                    switch (dto.unit)
                    {
                        case "Villager": unitType = 0; break;
                        case "Spearman": unitType = 1; break;
                        case "Archer": unitType = 2; break;
                        case "Knight": unitType = 7; break;
                        default:
                            if (CommanderIntentCatalog.TryResolveUnit(dto.unit, out int resolvedUnit) && CommanderIntentCatalog.IsSupportedUnit(resolvedUnit))
                                unitType = resolvedUnit;
                            else
                                return Reject(CommanderIntentErrorCode.UnknownUnit, "unit", "Unknown unit type.");
                            break;
                    }
                    if (dto.structure != null || dto.resource != null || dto.mode != null || dto.targetAge != null) return UnexpectedFields();
                    if (!InRange(dto.amount, 1, context.MaximumPopulation)) return InvalidAmount();
                    if (dto.quantityMode != null && dto.quantityMode != "TargetTotal" && dto.quantityMode != "New") return UnexpectedFields();
                    intent = new EnsureUnitCountIntent(context.PlayerId, unitType, dto.amount.Value, constraints,
                        dto.quantityMode == "New" ? dto.amount : (int?)null); break;
                case CommanderIntentType.BuildStructure:
                    BuildingType structure;
                    if (!NamedEnum(dto.structure, out structure) && !CommanderIntentCatalog.TryResolveStructure(dto.structure, out structure))
                        return Reject(CommanderIntentErrorCode.UnknownStructure, "structure", "Unknown structure type.");
                    if (!CommanderIntentCatalog.IsSupportedStructure(structure))
                        return Reject(CommanderIntentErrorCode.UnknownStructure, "structure", "Unknown structure type.");
                    if (dto.unit != null || dto.resource != null || dto.mode != null || dto.targetAge != null) return UnexpectedFields();
                    if (!InRange(dto.amount, 1, CommanderIntentValidator.MaximumStructureCount)) return InvalidAmount();
                    intent = new BuildStructureIntent(context.PlayerId, structure, dto.amount.Value, constraints); break;
                case CommanderIntentType.SetResourceAllocation:
                    if (!NamedEnum(dto.resource, out ResourceType resource)) return Reject(CommanderIntentErrorCode.UnknownResource, "resource", "Unknown resource type.");
                    if (!NamedEnum(dto.mode, out ResourceAllocationMode mode)) return Reject(CommanderIntentErrorCode.UnknownCommand, "mode", "Unknown allocation mode.");
                    if (dto.unit != null || dto.structure != null || dto.targetAge != null) return UnexpectedFields();
                    if ((mode == ResourceAllocationMode.SetExact || dto.amount.HasValue) && !InRange(dto.amount, 0, context.MaximumPopulation)) return InvalidAmount();
                    intent = new SetResourceAllocationIntent(context.PlayerId, resource, mode, dto.amount, constraints); break;
                case CommanderIntentType.AllocateWorkers:
                    if (dto.unit != null || dto.structure != null || dto.targetAge != null || dto.parameters != null
                        || dto.objectiveType != null || dto.priority.HasValue)
                        return UnexpectedFields();
                    if (!NamedEnum(dto.mode, out CommanderWorkerAllocationMode workerMode)
                        || !NamedEnum(dto.countMode, out CommanderWorkerCountMode countMode)
                        || !NamedEnum(dto.workerState, out CommanderWorkerState workerState)
                        || !NamedEnum(dto.resource, out ResourceType destinationResource)
                        || !NamedEnum(dto.sourceKind ?? nameof(ResourceSourceKind.Any), out ResourceSourceKind sourceKind))
                        return Reject(CommanderIntentErrorCode.UnknownCommand, "allocation", "Unknown worker allocation criteria.");
                    ResourceType? previousResource = null;
                    if (dto.currentResource != null)
                    {
                        if (!NamedEnum(dto.currentResource, out ResourceType parsedPrevious)) return UnexpectedFields();
                        previousResource = parsedPrevious;
                    }
                    CommanderResourceAmountMode? amountMode = null;
                    if (dto.resourceAmountMode != null)
                    {
                        if (!NamedEnum(dto.resourceAmountMode, out CommanderResourceAmountMode parsedAmountMode)) return UnexpectedFields();
                        amountMode = parsedAmountMode;
                    }
                    var workerAllocation = new CommanderWorkerAllocation(workerMode, countMode, dto.amount,
                        new CommanderWorkerSelector(workerState, previousResource),
                        new CommanderResourceDestination(destinationResource, sourceKind), dto.resourceAmount, amountMode);
                    if (!workerAllocation.IsValid(context.MaximumPopulation)) return InvalidAmount();
                    intent = new AllocateWorkersIntent(context.PlayerId, workerAllocation, constraints); break;
                case CommanderIntentType.ReachAge:
                    if (dto.unit != null || dto.structure != null || dto.resource != null || dto.mode != null
                        || dto.amount.HasValue || string.IsNullOrWhiteSpace(dto.targetAge)) return UnexpectedFields();
                    if (!TryParseAgeTarget(dto.targetAge, out CommanderSemanticAgeTarget requestedTarget))
                        return Reject(CommanderIntentErrorCode.AmountOutOfRange, "targetAge", "Unknown age target.");
                    int targetAge = requestedTarget == CommanderSemanticAgeTarget.Next
                        ? context.Age + 1 : (int)requestedTarget;
                    if (targetAge < 2 || targetAge > 4)
                        return Reject(CommanderIntentErrorCode.AmountOutOfRange, "targetAge", "Age target is outside the supported range.");
                    intent = new ReachAgeIntent(context.PlayerId, requestedTarget, targetAge, constraints); break;
                case CommanderIntentType.CapabilityAction:
                    // Tactical selectors have a closed typed schema. A legacy strategic
                    // parameter bag cannot silently add (and then lose) restrictions.
                    if (dto.parameters != null && dto.parameters.Count != 0)
                        return Reject(CommanderIntentErrorCode.UnsupportedConstraint, "parameters",
                            "This action cannot preserve additional parameter restrictions. Use supported point selectors or clarify the request.");
                    if (!NamedEnum(dto.action, out CommanderCapabilityActionType action)
                        || !NamedEnum(dto.unit, out CommanderUnitSelectorKind unitKind)
                        || !NamedEnum(dto.location, out CommanderLocationSelectorKind locationKind))
                        return Reject(CommanderIntentErrorCode.UnknownCommand, "action", "Unknown or incomplete capability action selectors.");
                    if (unitKind == CommanderUnitSelectorKind.UnitType
                        && (!dto.unitType.HasValue || !CommanderIntentCatalog.IsSupportedUnit(dto.unitType.Value)))
                        return Reject(CommanderIntentErrorCode.UnknownUnit, "unitType", "The capability unit type is not supported.");
                    if (unitKind != CommanderUnitSelectorKind.UnitType && dto.unitType.HasValue)
                        return UnexpectedFields();
                    ResourceType? actionResource = null;
                    if (dto.resource != null)
                    {
                        if (!NamedEnum(dto.resource, out ResourceType parsedResource))
                            return Reject(CommanderIntentErrorCode.UnknownResource, "resource", "Unknown capability resource.");
                        actionResource = parsedResource;
                    }
                    if ((locationKind == CommanderLocationSelectorKind.WorkedResource
                        || locationKind == CommanderLocationSelectorKind.VisibleResource) && !actionResource.HasValue)
                        return Reject(CommanderIntentErrorCode.UnknownResource, "resource", "This location requires a resource selector.");
                    if (locationKind != CommanderLocationSelectorKind.WorkedResource
                        && locationKind != CommanderLocationSelectorKind.VisibleResource && actionResource.HasValue)
                        return UnexpectedFields();
                    TechnologyType? actionTechnology = null;
                    if (dto.technology != null)
                    {
                        if (!NamedEnum(dto.technology, out TechnologyType parsedTechnology))
                            return Reject(CommanderIntentErrorCode.UnknownCommand, "technology", "Unknown capability technology.");
                        actionTechnology = parsedTechnology;
                    }
                    BuildingType? actionStructure = null;
                    if (dto.structure != null)
                    {
                        if (!NamedEnum(dto.structure, out BuildingType parsedStructure))
                            return Reject(CommanderIntentErrorCode.UnknownStructure, "structure", "Unknown capability structure.");
                        actionStructure = parsedStructure;
                    }
                    if (action == CommanderCapabilityActionType.ResearchTechnology && !actionTechnology.HasValue)
                        return Reject(CommanderIntentErrorCode.UnknownCommand, "technology", "Research requires a technology selector.");
                    if (action != CommanderCapabilityActionType.ResearchTechnology && actionTechnology.HasValue)
                        return UnexpectedFields();
                    if (!InRange(dto.amount, 1, 50)) return InvalidAmount();
                    CommanderTargetSelector? actionTarget = null;
                    if (dto.target != null)
                    {
                        try { actionTarget = CommanderSemanticResult.ParseTargetSelector(dto.target); }
                        catch (JsonException) { return UnexpectedFields(); }
                        if (!actionTarget.Value.IsCompatible(action, locationKind)) return UnexpectedFields();
                    }
                    intent = new CapabilityActionIntent(context.PlayerId, action,
                        new CommanderUnitSelector(unitKind, dto.amount.Value, dto.unitType ?? -1),
                        new CommanderLocationSelector(locationKind, actionResource), actionTechnology,
                        actionStructure, constraints, actionTarget);
                    break;
                default: return Reject(CommanderIntentErrorCode.UnknownCommand, "intentType", "Unknown intent type.");
            }
            return CommanderIntentInterpretation.Accepted(intent);
        }

        private static void NormalizeExternalJson(JObject root)
        {
            if (root == null) return;

            // category -> intentCategory
            if (root.Property("category") != null)
            {
                if (root.Property("intentCategory") != null)
                    throw new JsonException("Duplicate intentCategory and category fields.");
                JProperty prop = root.Property("category");
                root.Add("intentCategory", prop.Value);
                prop.Remove();
            }

            // type -> intentCategory (or intentType if tactical type)
            if (root.Property("type") != null)
            {
                JProperty prop = root.Property("type");
                string val = prop.Value?.Type == JTokenType.String ? (string)prop.Value : null;
                if (val != null && Enum.TryParse(val, out CommanderIntentType _))
                {
                    if (root.Property("intentType") != null)
                        throw new JsonException("Duplicate intentType and type fields.");
                    root.Add("intentType", prop.Value);
                }
                else
                {
                    if (root.Property("intentCategory") != null)
                        throw new JsonException("Duplicate intentCategory and type fields.");
                    root.Add("intentCategory", prop.Value);
                }
                prop.Remove();
            }

            // objective -> objectiveType
            if (root.Property("objective") != null)
            {
                if (root.Property("objectiveType") != null)
                    throw new JsonException("Duplicate objectiveType and objective fields.");
                JProperty prop = root.Property("objective");
                root.Add("objectiveType", prop.Value);
                prop.Remove();
            }

            // count / targetCount -> amount
            JProperty countProp = root.Property("targetCount") ?? root.Property("count");
            if (countProp != null)
            {
                if (root.Property("amount") != null)
                    throw new JsonException("Duplicate amount and targetCount/count fields.");
                root.Add("amount", countProp.Value);
                root.Property("targetCount")?.Remove();
                root.Property("count")?.Remove();
            }

            // targetType -> unit, structure, or parameters["targetType"]
            if (root.Property("targetType") != null)
            {
                JProperty prop = root.Property("targetType");
                string val = prop.Value?.Type == JTokenType.String ? (string)prop.Value : null;
                string cat = root.Property("intentCategory")?.Value?.ToString();
                bool isCatStrategic = string.Equals(cat, "Strategic", StringComparison.OrdinalIgnoreCase);

                if (isCatStrategic)
                {
                    if (root.Property("parameters") == null)
                        root.Add("parameters", new JObject());
                    if (root["parameters"] is JObject pObj && pObj.Property("targetType") == null)
                    {
                        pObj.Add("targetType", prop.Value);
                    }
                }
                else
                {
                    if (val != null && (CommanderIntentCatalog.TryResolveStructure(val, out _) || string.Equals(root.Property("intentType")?.Value?.ToString(), "BuildStructure", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (root.Property("structure") != null)
                            throw new JsonException("Duplicate structure and targetType fields.");
                        root.Add("structure", prop.Value);
                    }
                    else
                    {
                        if (root.Property("unit") != null)
                            throw new JsonException("Duplicate unit and targetType fields.");
                        root.Add("unit", prop.Value);
                    }
                }
                prop.Remove();
            }
        }

        private static bool TryParseAgeTarget(string value, out CommanderSemanticAgeTarget target)
        {
            switch (value)
            {
                case "Next": target = CommanderSemanticAgeTarget.Next; return true;
                case "Feudal": target = CommanderSemanticAgeTarget.Feudal; return true;
                case "Castle": target = CommanderSemanticAgeTarget.Castle; return true;
                case "Imperial": target = CommanderSemanticAgeTarget.Imperial; return true;
                default: target = CommanderSemanticAgeTarget.Next; return false;
            }
        }

        public static CommanderIntentDTO FromIntent(CommanderIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var dto = new CommanderIntentDTO
            {
                intentCategory = "Tactical",
                intentType = intent.Type.ToString()
            };
            if (intent is EnsureUnitCountIntent ensure)
            {
                dto.unit = CommanderIntentCatalog.GetUnitDisplayName(ensure.UnitType);
                dto.amount = ensure.NewProductionCount ?? ensure.TargetTotal;
                dto.quantityMode = ensure.NewProductionCount.HasValue ? "New" : "TargetTotal";
            }
            else if (intent is BuildStructureIntent build) { dto.structure = build.StructureType.ToString(); dto.amount = build.Count; }
            else if (intent is ReachAgeIntent reachAge) { dto.targetAge = reachAge.RequestedTarget.ToString(); }
            else if (intent is SetResourceAllocationIntent allocation) { dto.resource = allocation.Resource.ToString(); dto.mode = allocation.Mode.ToString(); dto.amount = allocation.WorkerCount; }
            else if (intent is AllocateWorkersIntent workers)
            {
                if (workers.Allocation == null) throw new ArgumentException("Missing worker allocation.", nameof(intent));
                dto.mode = workers.Allocation.Mode.ToString();
                dto.countMode = workers.Allocation.CountMode.ToString();
                dto.amount = workers.Allocation.Count;
                dto.workerState = workers.Allocation.Workers?.State.ToString();
                dto.currentResource = workers.Allocation.Workers?.CurrentResource?.ToString();
                dto.resource = workers.Allocation.Destination?.Resource.ToString();
                dto.sourceKind = workers.Allocation.Destination?.SourceKind.ToString();
                dto.resourceAmount = workers.Allocation.ResourceAmount;
                dto.resourceAmountMode = workers.Allocation.ResourceAmountMode?.ToString();
            }
            else if (intent is CapabilityActionIntent capability)
            {
                if(capability.LocationSelector.RadiusTiles.HasValue)
                    throw new ArgumentException("An unsupported radius/perimeter cannot be omitted from the tactical DTO.",nameof(intent));
                dto.action = capability.ActionType.ToString();
                dto.unit = capability.UnitSelector.Kind.ToString();
                dto.unitType = capability.UnitSelector.Kind == CommanderUnitSelectorKind.UnitType
                    ? capability.UnitSelector.UnitType : (int?)null;
                dto.location = capability.LocationSelector.Kind.ToString();
                dto.target = capability.TargetSelector.HasValue
                    ? CommanderSemanticResult.TargetSelectorJson(capability.TargetSelector.Value) : null;
                dto.resource = capability.LocationSelector.ResourceType?.ToString();
                dto.technology = capability.Technology?.ToString();
                dto.structure = capability.StructureType?.ToString();
                dto.amount = capability.UnitSelector.Count;
            }
            else throw new ArgumentException("Unsupported intent implementation.", nameof(intent));
            foreach (var constraint in intent.Constraints)
            {
                dto.constraints.Add(FromConstraint(constraint));
            }
            return dto;
        }

        public static CommanderIntentDTO FromIntent(StrategicIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var dto = new CommanderIntentDTO
            {
                intentCategory = "Strategic",
                objectiveType = intent.ObjectiveType.ToString(),
                priority = intent.Priority,
                parameters = intent.Parameters != null ? new Dictionary<string, string>(intent.Parameters) : null
            };
            return dto;
        }

        internal static CommanderConstraintDTO FromConstraint(CommanderConstraint constraint)
        {
            var item = new CommanderConstraintDTO { type = constraint.Type.ToString() };
            if (constraint is PreferredWorkersConstraint preferred) item.mode = preferred.WorkerSource.ToString();
            else if (constraint is ResourceSourceConstraint source) { item.resource = source.Resource.ToString(); item.sourceKind = source.SourceKind.ToString(); }
            else if (constraint is ProtectedResourceConstraint resource) { item.resource = resource.Resource.ToString(); item.amount = resource.MinimumWorkers; }
            else if (constraint is MaximumQueueConstraint queue) item.amount = queue.MaximumQueue;
            else if (!(constraint is NoConstructionConstraint))
                throw new ArgumentException("Unsupported constraint implementation.", nameof(constraint));
            return item;
        }

        public static CommanderIntentDTO FromStrategicIntent(StrategicIntent intent) => FromIntent(intent);
        public static CommanderIntentDTO ToDTO(CommanderIntent intent) => FromIntent(intent);
        public static CommanderIntentDTO ToDTO(StrategicIntent intent) => FromIntent(intent);
        public static CommanderIntentDTO ToDTO(ICommanderIntentRequest request)
        {
            if (request is CommanderIntent tactical) return FromIntent(tactical);
            if (request is StrategicIntent strategic) return FromIntent(strategic);
            return null;
        }

        private static bool NamedEnum<T>(string text, out T value) where T : struct
        {
            value = default;
            // Numeric strings and aliases are deliberately not accepted at this boundary.
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] names = Enum.GetNames(typeof(T));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], text, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.TryParse(names[i], out value);
                }
            }
            return false;
        }

        private static bool TryResolveObjectiveAlias(string text, out StrategicObjectiveType objectiveType)
        {
            objectiveType = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string cleaned = text.Replace("-", "").Replace("_", "").Trim();
            if (Enum.TryParse(cleaned, true, out objectiveType)) return true;

            string lower = cleaned.ToLowerInvariant();
            if (lower.Contains("defen") || lower.Contains("guard"))
            {
                objectiveType = StrategicObjectiveType.DefensivePreparation;
                return true;
            }
            if (lower.Contains("cavalry") || lower.Contains("attack") || lower.Contains("pressure") || lower.Contains("strike"))
            {
                objectiveType = StrategicObjectiveType.AttackPreparation;
                return true;
            }
            if (lower.Contains("econ") || lower.Contains("boom") || lower.Contains("grow") || lower.Contains("villager"))
            {
                objectiveType = StrategicObjectiveType.EconomicExpansion;
                return true;
            }
            if (lower.Contains("reinforce") || lower.Contains("military") || lower.Contains("army"))
            {
                objectiveType = StrategicObjectiveType.MilitaryReinforcement;
                return true;
            }
            return false;
        }
        private static bool InRange(int? amount, int min, int max) => amount.HasValue && amount >= min && amount <= max;
        private static CommanderIntentInterpretation InvalidAmount() => Reject(CommanderIntentErrorCode.AmountOutOfRange, "amount", "Required integer amount is missing or outside the supported range.");
        private static CommanderIntentInterpretation UnexpectedFields() => Reject(CommanderIntentErrorCode.InvalidJson, "response", "Fields do not match the selected intent type.");
        private static CommanderIntentInterpretation Reject(CommanderIntentErrorCode code, string field, string reason) => CommanderIntentInterpretation.Rejected(code, reason, field);
        private static void CheckFields(JObject obj, params string[] allowed)
        {
            foreach (var property in obj.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0) throw new JsonException("Unknown field: " + property.Name);
        }
        private static string ReadString(JObject obj, string key)
        {
            if (!obj.TryGetValue(key, out JToken value)) return null;
            if (value.Type != JTokenType.String) throw new JsonException(key + " must be a string.");
            return (string)value;
        }
        private static int? ReadAmount(JObject obj, string key = "amount")
        {
            if (!obj.TryGetValue(key, out JToken value)) return null;
            if (value.Type != JTokenType.Integer) throw new JsonException(key + " must be an integer JSON number.");
            return checked((int)value);
        }

        // Json.NET accepts JavaScript extensions by default. The model boundary accepts JSON only.
        private sealed class StrictJsonReader : JsonTextReader
        {
            public StrictJsonReader(TextReader reader) : base(reader) { }
            public override bool Read()
            {
                bool read = base.Read();
                if (read && (TokenType == JsonToken.PropertyName || TokenType == JsonToken.String) && QuoteChar != '"')
                    throw new JsonException("JSON names and strings require double quotes.");
                return read;
            }
        }

        private static void CheckJsonSyntax(string json)
        {
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    bool closed = false;
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new JsonException("Unescaped control character in string.");
                        if (c != '\\') continue;
                        if (++i >= json.Length || "\"\\/bfnrtu".IndexOf(json[i]) < 0) throw new JsonException("Invalid JSON escape.");
                        if (json[i] != 'u') continue;
                        for (int digit = 0; digit < 4; digit++)
                            if (++i >= json.Length || !Uri.IsHexDigit(json[i])) throw new JsonException("Invalid Unicode escape.");
                    }
                    if (!closed) throw new JsonException("Unterminated JSON string.");
                }
                else if (c == ',')
                {
                    int next = i + 1;
                    while (next < json.Length && IsJsonWhitespace(json[next])) next++;
                    if (next == json.Length || json[next] == '}' || json[next] == ']') throw new JsonException("Trailing JSON comma.");
                }
                else if (IsJsonWhitespace(c) || "{}[]:".IndexOf(c) >= 0) continue;
                else
                {
                    int start = i;
                    while (i + 1 < json.Length && !IsJsonWhitespace(json[i + 1]) && "{}[]:,".IndexOf(json[i + 1]) < 0) i++;
                    string token = json.Substring(start, i - start + 1);
                    if (token != "true" && token != "false" && token != "null" && !JsonNumber.IsMatch(token))
                        throw new JsonException("Invalid JSON token.");
                }
            }
        }
        private static bool IsJsonWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';
    }
}
