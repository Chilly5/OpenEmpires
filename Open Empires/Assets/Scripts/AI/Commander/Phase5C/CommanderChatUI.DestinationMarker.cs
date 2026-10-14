using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private bool placingDestination;
        private bool consumeDestinationRelease;
        private int markerInputFrame = -1;
        private GameObject destinationMarkerVisual;
        private CommanderSemanticResult retainedLocationResult;
        private string retainedLocationText;
        private int retainedLocationGeneration;
        private Func<bool> retainedLocationLease;
        private CommanderRequestTicket retainedLocationTicket;
        private CommanderPendingClarification retainedLocationPending;
        private int? selectedMarkerLocationNode;
        private Material destinationMarkerMaterial;
        private CommanderRequestTicket markerLocationsTicket;
        private readonly Dictionary<int,CommanderBoundLocation> pendingMarkerLocations=new Dictionary<int,CommanderBoundLocation>();

        private void ClearPendingMarkerLocations()
        {pendingMarkerLocations.Clear();markerLocationsTicket=null;}

        private IReadOnlyDictionary<int,CommanderBoundLocation> RetainedMarkerLocations()
        {
            if(!ReferenceEquals(markerLocationsTicket,retainedLocationTicket)||retainedLocationResult?.Nodes==null)return null;
            // An explicit typed text destination supersedes its prior marker selection.
            return pendingMarkerLocations.Where(pair=>pair.Key<retainedLocationResult.Nodes.Count
                &&(retainedLocationResult.Nodes[pair.Key].LocationSelector==CommanderSemanticLocationSelector.PlayerMarker
                    ||retainedLocationResult.Nodes[pair.Key].PlacementAnchorSelector==CommanderSemanticAnchorSelector.PlayerMarker))
                .ToDictionary(pair=>pair.Key,pair=>pair.Value);
        }

        internal bool OwnsDestinationInput => placingDestination || consumeDestinationRelease || markerInputFrame == Time.frameCount;

        public void BeginDestinationPlacement()
        {
            if (submitting || semanticGoalManager == null || semanticGoalManager.IsDisposed) return;
            placingDestination = true;
            currentRequestGoalId = 0;
            markerInputFrame = Time.frameCount;
            MinimizeCommander();
            string detail = pendingClarification?.GraphDraft?.MissingSlots.Count(s => s.Type == "Location") > 1
                ? "Click a visible destination, then choose which step uses it."
                : retainedLocationResult != null || pendingClarification?.GraphDraft?.MissingSlots.Any(s => s.Type == "Location") == true
                    ? "Click a visible destination for the pending request."
                    : "Click a visible destination to save it for the next applicable request.";
            SetLatestRequestState("Select destination", detail);
            AppendLine("Commander", detail, false);
        }

        public void ClearDestinationMarker()
        {
            placingDestination = false;
            consumeDestinationRelease = false;
            markerInputFrame = -1;
            semanticGoalManager?.ClearPlayerMarkerSnapshot();
            if (pendingActionPlan?.UsesPlayerMarker == true) ClearActionPlanPreview();
            RemoveDestinationVisual();
        }

        private void ResetDestinationState()
        {
            ClearDestinationMarker();
            retainedLocationResult = null;
            retainedLocationText = null;
            retainedLocationLease = null;
            retainedLocationTicket = null;
            retainedLocationPending = null;
            selectedMarkerLocationNode = null;
            ClearPendingMarkerLocations();
        }

        private bool HandleRetainedLocationReply(string reply)
        {
            if (retainedLocationResult == null || pendingActionPlan != null) return false;
            if (runtimeGeneration != retainedLocationGeneration || retainedLocationLease?.Invoke() != true)
            { retainedLocationResult = null; retainedLocationLease = null; return false; }
            string form = NormalizeWholeForm(reply);
            if (form == "cancel" || form == "never mind")
            { retainedLocationResult = null; retainedLocationLease = null; ClearPendingMarkerLocations(); SetLatestRequestState("Cancelled", "Request cancelled."); return true; }
            CommanderSemanticLocationSelector location;
            ResourceType? resource = null;
            switch (form)
            {
                case "my town center": case "at my town center": case "near my town center":
                case "my tc": case "playerbase": location = CommanderSemanticLocationSelector.PlayerBase; break;
                case "worked gold": case "gold worked by my villagers":
                    location = CommanderSemanticLocationSelector.WorkedResource; resource = ResourceType.Gold; break;
                case "worked wood": case "wood worked by my villagers":
                    location = CommanderSemanticLocationSelector.WorkedResource; resource = ResourceType.Wood; break;
                default:
                    foreach (string start in new[] { "build ", "train ", "move ", "gather ", "send ", "make ", "produce ", "research " })
                        if (form.StartsWith(start, StringComparison.Ordinal)) return false;
                    AppendLine("Commander", "Where? Place a marker or name your Town Center or a worked resource.");
                    SetLatestRequestState("Needs clarification", "Where? Place a marker or describe the location.");
                    return true;
            }
            var completed = retainedLocationResult.WithMarkerDestination(location, resource);
            if (completed == null)
            {
                AppendLine("Commander", "That location cannot fill this complete request. Place a marker instead.");
                return true;
            }
            AppendLine("Player", reply);
            retainedLocationResult = completed;
            PrepareRetainedDestinationPreview();
            return true;
        }

        private static bool UsesMarker(CommanderSemanticResult result)
        {
            if (result?.Nodes != null)
                foreach (var node in result.Nodes)
                    if (node.LocationSelector == CommanderSemanticLocationSelector.PlayerMarker
                        || node.PlacementAnchorSelector == CommanderSemanticAnchorSelector.PlayerMarker) return true;
            if (result?.DynamicPlan != null)
                foreach (var node in result.DynamicPlan.Nodes)
                    if (node.Parameters.TryGetValue("anchor", out object anchor)
                        && (string)anchor == "PlayerMarker") return true;
            return false;
        }

        private bool PrepareMarkerRequest(CommanderSemanticResult result, string original,
            int generation, Func<bool> lease, CommanderRequestTicket ticket)
        {
            if (!UsesMarker(result)) return false;
            retainedLocationResult = result;
            retainedLocationText = original;
            retainedLocationGeneration = generation;
            retainedLocationLease = lease;
            retainedLocationTicket = ticket;
            if (!semanticGoalManager.TryGetPlayerMarkerSnapshot(out _, out _))
            {
                SetLatestRequestState("Needs clarification", "Where? Place a marker or describe the location.");
                AppendLine("Commander", "Where? Place a marker or describe the location.");
                return true;
            }
            PrepareRetainedDestinationPreview();
            return true;
        }

        private void PrepareRetainedDestinationPreview()
        {
            if (retainedLocationResult == null || runtimeGeneration != retainedLocationGeneration
                || retainedLocationLease?.Invoke() != true || semanticGoalManager == null
                || semanticGoalManager.IsDisposed) return;
            try
            {
                ClearActionPlanPreview();
                // A location answer is semantic slot completion. The ordinary one-node
                // automatic authority gate still applies; compound/strategic approval does not change.
                if (retainedLocationResult.Outcome == CommanderSemanticOutcome.Request
                    && retainedLocationResult.Nodes.Count == 1
                    && retainedLocationResult.Nodes[0].Type != CommanderSemanticNodeType.WatchFutureUnits
                    && retainedLocationResult.Nodes[0].Type != CommanderSemanticNodeType.StrategicObjective)
                {
                    var context = new CommanderContextBuilder().Build(semanticSimulation, semanticGoalManager);
                    if (!CommanderSemanticAdmission.TryCreateTacticalIntent(retainedLocationResult.Nodes[0], context,
                        out CommanderIntent intent, out string reason)) throw new ArgumentException(reason);
                    var scope = semanticGoalManager.PrepareKnownIntentScope(retainedLocationResult,
                        retainedLocationTicket, strategicPipeline, true,RetainedMarkerLocations());
                    var submission = semanticGoalManager.SubmitKnownScopedIntent(scope, intent,
                        () => semanticDispatcher.SubmitIntent(intent));
                    TrackCurrentRequest(submission);
                    SetLatestRequestState(submission?.CreatedGoal == true ? "Accepted" : "Failed",
                        submission?.Response ?? "The destination request could not be admitted safely.");
                    AppendLine("Commander", submission?.Response ?? "The destination request could not be admitted safely.");
                    if (submission?.CreatedGoal == true)
                    {
                        RecordAcceptedSemanticNodes(retainedLocationResult.Nodes);
                        if (ReferenceEquals(pendingClarification, retainedLocationPending)) pendingClarification = null;
                        retainedLocationResult = null;
                        retainedLocationLease = null;
                        retainedLocationTicket = null;
                        retainedLocationPending = null;
                        ClearPendingMarkerLocations();
                        RemoveDestinationVisual();
                    }
                    return;
                }
                pendingActionPlan = semanticGoalManager.PrepareActionPlan(retainedLocationResult,
                    retainedLocationText, runtimeGeneration, strategicPipeline, retainedLocationLease, retainedLocationTicket,RetainedMarkerLocations());
                SetLatestRequestState("Awaiting approval", "Destination set. Review and approve the plan.");
                AppendLine("Commander", pendingActionPlan.Preview);
                UpdateStrategicControls();
            }
            catch (CommanderMovementAdmissionException error)
            {
                if (this != null && runtimeGeneration == retainedLocationGeneration
                    && retainedLocationLease?.Invoke() == true)
                {
                    string rejection = error.Message + " No goal was created; your destination marker is retained.";
                    SetLatestRequestState("Rejected", rejection);
                    AppendLine("Commander", rejection);
                }
            }
            catch (ArgumentException)
            {
                SetLatestRequestState("Rejected", "That destination cannot be used for this request. Choose another marker.");
                AppendLine("Commander", "That destination cannot be used for this request. Choose another marker.");
            }
        }

        public bool TryPlaceDestinationMarker(int tileX, int tileZ)
        {
            if (!submitting && !placingDestination && semanticGoalManager?.IsDisposed == false
                && semanticGoalManager.TryGetPlayerMarkerSnapshot(out int previousX, out int previousZ)
                && previousX == tileX && previousZ == tileZ) return true;
            bool hadPendingLocation = retainedLocationResult != null
                || pendingClarification?.GraphDraft?.MissingSlots.Any(s => s.Type == "Location") == true;
            if (submitting || semanticGoalManager == null || semanticGoalManager.IsDisposed
                || !semanticGoalManager.TrySetPlayerMarkerSnapshot(tileX, tileZ))
            {
                DestinationClickFailure("Choose a visible point inside the map. The destination was not changed.");
                return false;
            }
            placingDestination = false;
            markerInputFrame = Time.frameCount;
            consumeDestinationRelease = Mouse.current?.leftButton.isPressed == true;
            ShowDestinationVisual(tileX, tileZ);
            if (pendingActionPlan?.UsesPlayerMarker == true) ClearActionPlanPreview();
            FillPendingMarkerLocation();
            PrepareRetainedDestinationPreview();
            if (pendingActionPlan != null && pendingActionPlan.UsesPlayerMarker)
            {
                AppendLine("Commander", "Destination selected for: " + retainedLocationText + ". Review and approve the complete plan.", false);
                OpenCommander();
            }
            else if (!hadPendingLocation)
            {
                currentRequestGoalId = 0;
                string acknowledgement = "Destination saved for the next applicable request. Existing tasks keep their approved destination.";
                SetLatestRequestState("Destination saved", acknowledgement);
                AppendLine("Commander", acknowledgement, false);
            }
            return true;
        }

        private bool HandleMarkerStepReply(string reply)
        {
            var pending = pendingClarification;
            string form = NormalizeWholeForm(reply);
            if (pending?.GraphDraft == null || pending.RuntimeGeneration != runtimeGeneration
                || pending.RequestTicket?.IsCurrent() != true || !form.StartsWith("step ", StringComparison.Ordinal)
                || !int.TryParse(form.Substring(5), out int step)) return false;
            if (!pending.GraphDraft.MissingSlots.Any(s => s.Type == "Location" && s.Node == step - 1))
            { AppendLine("Commander", "That step has no unresolved destination."); return true; }
            selectedMarkerLocationNode = step - 1;
            AppendLine("Player", reply);
            if (semanticGoalManager.TryGetPlayerMarkerSnapshot(out _, out _))
            { FillPendingMarkerLocation(); PrepareRetainedDestinationPreview(); }
            else AppendLine("Commander", "Place a destination for step " + step + ".");
            return true;
        }

        private void DestinationClickFailure(string reason)
        {
            markerInputFrame = Time.frameCount;
            consumeDestinationRelease = Mouse.current?.leftButton.isPressed == true;
            SetLatestRequestState("Destination not selected", reason);
            AppendLine("Commander", reason, false);
        }

        private void FillPendingMarkerLocation()
        {
            var pending = pendingClarification;
            if (pending?.GraphDraft == null || pending.RuntimeGeneration != runtimeGeneration
                || pending.RequestTicket?.IsCurrent() != true) return;
            int locationSlots = 0;
            foreach (var slot in pending.GraphDraft.MissingSlots)
                if (slot.Type == "Location") locationSlots++;
            if (locationSlots == 0) return;
            int node;
            if (locationSlots == 1) node = pending.GraphDraft.MissingSlots.Single(s => s.Type == "Location").Node;
            else if (selectedMarkerLocationNode.HasValue) node = selectedMarkerLocationNode.Value;
            else
            {
                string question = "Which step should use this destination? Reply " + string.Join(" or ",
                    pending.GraphDraft.MissingSlots.Where(s => s.Type == "Location").Select(s => "step " + (s.Node + 1))) + ".";
                SetLatestRequestState("Choose destination step", question);
                AppendLine("Commander", question, false);
                OpenCommander();
                return;
            }
            var completed = pending.GraphDraft.ResolveMarkerLocation(node);
            if (completed?.IsValid != true) return;
            if(!semanticGoalManager.TryGetPlayerMarkerSnapshot(out int markerX,out int markerZ))return;
            if(!ReferenceEquals(markerLocationsTicket,pending.RequestTicket))
            {ClearPendingMarkerLocations();markerLocationsTicket=pending.RequestTicket;}
            pendingMarkerLocations[node]=CommanderBoundLocation.FromPlayerMarker(semanticSimulation,semanticGoalManager,
                semanticGoalManager.PlayerId,markerX,markerZ,CommanderSemanticPlacementRelation.Near,1);
            selectedMarkerLocationNode = null;
            AppendLine("Commander", "Destination selected for step " + (node + 1) + " of: " + pending.OriginalText + ".", false);
            if (completed.Outcome == CommanderSemanticOutcome.Clarify)
            {
                pendingClarification = new CommanderPendingClarification(completed.PendingGraphDraft,
                    pending.OriginalText, completed.SafeExplanation, runtimeGeneration,
                    pending.Sequence, pending.Turns, pending.RequestTicket);
                SetLatestRequestState("Needs clarification", completed.SafeExplanation);
                AppendLine("Commander", completed.SafeExplanation);
                return;
            }
            retainedLocationResult = completed;
            retainedLocationText = pending.OriginalText;
            retainedLocationGeneration = runtimeGeneration;
            retainedLocationTicket = pending.RequestTicket;
            retainedLocationLease = pending.RequestTicket.IsCurrent;
            retainedLocationPending = pending;
        }

        private void RemoveDestinationVisual()
        {
            if (destinationMarkerVisual == null) return;
            if (Application.isPlaying) Destroy(destinationMarkerVisual);
            else DestroyImmediate(destinationMarkerVisual);
            destinationMarkerVisual = null;
            if (destinationMarkerMaterial != null)
            {
                if (Application.isPlaying) Destroy(destinationMarkerMaterial); else DestroyImmediate(destinationMarkerMaterial);
                destinationMarkerMaterial = null;
            }
        }

        private void ShowDestinationVisual(int x, int z)
        {
            RemoveDestinationVisual();
            if (!Application.isPlaying) return;
            destinationMarkerVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            destinationMarkerVisual.name = "Commander player destination";
            destinationMarkerVisual.transform.SetParent(transform, false);
            Vector3 point = semanticSimulation.MapData.TileToWorldFixed(x, z).ToVector3();
            if (Physics.Raycast(new Vector3(point.x, 100, point.z), Vector3.down,
                out RaycastHit hit, 200, LayerMask.GetMask("Ground"))) point.y = hit.point.y;
            destinationMarkerVisual.transform.position = point + Vector3.up * .15f;
            destinationMarkerVisual.transform.localScale = new Vector3(.8f, .07f, .8f);
            var collider = destinationMarkerVisual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            destinationMarkerMaterial = destinationMarkerVisual.GetComponent<Renderer>().material;
            destinationMarkerMaterial.color = Color.cyan;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Destination pin";
            head.transform.SetParent(destinationMarkerVisual.transform, false);
            head.transform.localPosition = new Vector3(0, 12, 0);
            head.transform.localScale = new Vector3(.6f, 6, .6f);
            head.GetComponent<Renderer>().sharedMaterial = destinationMarkerMaterial;
            Destroy(head.GetComponent<Collider>());
        }

        private void PollDestinationPlacement()
        {
            if (consumeDestinationRelease)
            {
                if (Mouse.current?.leftButton.isPressed != true)
                { consumeDestinationRelease = false; markerInputFrame = Time.frameCount; }
                return;
            }
            if (!placingDestination || markerInputFrame == Time.frameCount) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            { placingDestination = false; markerInputFrame = Time.frameCount; return; }
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 point = Cursor.lockState == CursorLockMode.Locked ? VirtualCursor.Position : mouse.position.ReadValue();
            if (ContainsScreenPoint(point) || EventSystem.current?.IsPointerOverGameObject() == true)
            { DestinationClickFailure("That click hit a UI control. Click visible terrain outside the panels."); return; }
            markerInputFrame = Time.frameCount;
            Camera camera = Camera.main;
            if (camera == null)
            { DestinationClickFailure("The game camera is unavailable. No destination was selected."); return; }
            if (!camera.pixelRect.Contains(point))
            { DestinationClickFailure("That click is outside the game camera view. Click visible terrain."); return; }
            if (!Physics.Raycast(camera.ScreenPointToRay(point), out RaycastHit hit, 1000, LayerMask.GetMask("Ground")))
            { DestinationClickFailure("That click did not hit terrain. Click a visible ground tile."); return; }
            Vector2Int tile = semanticSimulation.MapData.WorldToTile(FixedVector3.FromVector3(hit.point));
            TryPlaceDestinationMarker(tile.x, tile.y);
        }
    }
}
