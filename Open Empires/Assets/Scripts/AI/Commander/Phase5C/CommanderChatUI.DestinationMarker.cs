using System;
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

        internal bool OwnsDestinationInput => placingDestination || consumeDestinationRelease || markerInputFrame == Time.frameCount;

        public void BeginDestinationPlacement()
        {
            if (submitting || semanticGoalManager == null || semanticGoalManager.IsDisposed) return;
            placingDestination = true;
            markerInputFrame = Time.frameCount;
            MinimizeCommander();
            AppendLine("Commander", "Click a visible destination. This does not issue an order.", false);
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
        }

        private bool HandleRetainedLocationReply(string reply)
        {
            if (retainedLocationResult == null || pendingActionPlan != null) return false;
            if (runtimeGeneration != retainedLocationGeneration || retainedLocationLease?.Invoke() != true)
            { retainedLocationResult = null; retainedLocationLease = null; return false; }
            string form = NormalizeWholeForm(reply);
            if (form == "cancel" || form == "never mind")
            { retainedLocationResult = null; retainedLocationLease = null; SetLatestRequestState("Cancelled", "Request cancelled."); return true; }
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
                        retainedLocationTicket, strategicPipeline, true);
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
                        RemoveDestinationVisual();
                    }
                    return;
                }
                pendingActionPlan = semanticGoalManager.PrepareActionPlan(retainedLocationResult,
                    retainedLocationText, runtimeGeneration, strategicPipeline, retainedLocationLease, retainedLocationTicket);
                SetLatestRequestState("Awaiting approval", "Destination set. Review and approve the plan.");
                AppendLine("Commander", pendingActionPlan.Preview);
                UpdateStrategicControls();
            }
            catch (ArgumentException)
            {
                SetLatestRequestState("Rejected", "That destination cannot be used for this request. Choose another marker.");
                AppendLine("Commander", "That destination cannot be used for this request. Choose another marker.");
            }
        }

        public bool TryPlaceDestinationMarker(int tileX, int tileZ)
        {
            if (submitting || semanticGoalManager == null || semanticGoalManager.IsDisposed
                || !semanticGoalManager.TrySetPlayerMarkerSnapshot(tileX, tileZ))
            {
                SetLatestRequestState("Needs clarification", "Choose a visible point inside the map.");
                return false;
            }
            placingDestination = false;
            markerInputFrame = Time.frameCount;
            consumeDestinationRelease = Mouse.current?.leftButton.isPressed == true;
            ShowDestinationVisual(tileX, tileZ);
            if (pendingActionPlan?.UsesPlayerMarker == true) ClearActionPlanPreview();
            FillPendingMarkerLocation();
            PrepareRetainedDestinationPreview();
            return true;
        }

        private void FillPendingMarkerLocation()
        {
            var pending = pendingClarification;
            if (pending?.GraphDraft == null || pending.RuntimeGeneration != runtimeGeneration
                || pending.RequestTicket?.IsCurrent() != true) return;
            int locationSlots = 0;
            foreach (var slot in pending.GraphDraft.MissingSlots)
                if (slot.Type == "Location") locationSlots++;
            if (locationSlots != 1) return; // A marker never chooses between unrelated slots.
            var completed = pending.GraphDraft.Resolve("PlayerMarker");
            if (completed?.IsValid != true) return;
            if (completed.Outcome == CommanderSemanticOutcome.Clarify)
            {
                pendingClarification = new CommanderPendingClarification(completed.PendingGraphDraft,
                    pending.OriginalText, completed.SafeExplanation, runtimeGeneration,
                    pending.Sequence, pending.Turns, pending.RequestTicket);
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
                out RaycastHit hit, 200)) point.y = hit.point.y;
            destinationMarkerVisual.transform.position = point + Vector3.up * .15f;
            destinationMarkerVisual.transform.localScale = new Vector3(.8f, .07f, .8f);
            var collider = destinationMarkerVisual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
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
            if (ContainsScreenPoint(point) || EventSystem.current?.IsPointerOverGameObject() == true) return;
            markerInputFrame = Time.frameCount;
            Camera camera = Camera.main;
            if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(point), out RaycastHit hit, 1000,
                LayerMask.GetMask("Ground"))) return;
            Vector2Int tile = semanticSimulation.MapData.WorldToTile(FixedVector3.FromVector3(hit.point));
            TryPlaceDestinationMarker(tile.x, tile.y);
        }
    }
}
