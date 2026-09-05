using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicResourceReservationStatus
    {
        Active,
        Released,
        Cancelled
    }

    public sealed class StrategicResourceRequirement
    {
        public ResourceType ResourceType { get; }
        public int Amount { get; }

        internal StrategicResourceRequirement(ResourceType resourceType, int amount)
        {
            if (!Enum.IsDefined(typeof(ResourceType), resourceType))
                throw new ArgumentOutOfRangeException(nameof(resourceType));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            ResourceType = resourceType;
            Amount = amount;
        }
    }

    public sealed class StrategicResourceReservation
    {
        public int ReservationId { get; }
        public int PlanId { get; }
        public StrategicPlanType OwnerPlanType { get; }
        public ResourceType ResourceType { get; }
        public int Amount { get; }
        public StrategicResourceReservationStatus Status { get; private set; }

        internal StrategicResourceReservation(int reservationId, StrategicPlan plan,
            StrategicResourceRequirement requirement)
        {
            if (reservationId < 1) throw new ArgumentOutOfRangeException(nameof(reservationId));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (requirement == null) throw new ArgumentNullException(nameof(requirement));
            ReservationId = reservationId;
            PlanId = plan.StrategicPlanId;
            OwnerPlanType = plan.PlanType;
            ResourceType = requirement.ResourceType;
            Amount = requirement.Amount;
            Status = StrategicResourceReservationStatus.Active;
        }

        internal void Release(bool cancelled)
        {
            if (Status != StrategicResourceReservationStatus.Active) return;
            Status = cancelled
                ? StrategicResourceReservationStatus.Cancelled
                : StrategicResourceReservationStatus.Released;
        }
    }

    public sealed class StrategicReservationConflict
    {
        public int RequestingPlanId { get; }
        public StrategicPlanType RequestingPlanType { get; }
        public ResourceType ResourceType { get; }
        public int RequestedAmount { get; }
        public int CurrentAmount { get; }
        public int ReservedAmount { get; }
        public int AvailableAmount { get; }
        public int? OwnerReservationId { get; }
        public int? OwnerPlanId { get; }
        public StrategicPlanType? OwnerPlanType { get; }

        internal StrategicReservationConflict(StrategicPlan plan, ResourceType resourceType,
            int requestedAmount, int currentAmount, int reservedAmount, int availableAmount,
            StrategicResourceReservation owner)
        {
            RequestingPlanId = plan?.StrategicPlanId ?? 0;
            RequestingPlanType = plan?.PlanType ?? default;
            ResourceType = resourceType;
            RequestedAmount = requestedAmount;
            CurrentAmount = currentAmount;
            ReservedAmount = reservedAmount;
            AvailableAmount = availableAmount;
            OwnerReservationId = owner?.ReservationId;
            OwnerPlanId = owner?.PlanId;
            OwnerPlanType = owner?.OwnerPlanType;
        }

        public override string ToString()
        {
            string owner = OwnerPlanId.HasValue
                ? $"plan #{OwnerPlanId.Value} ({OwnerPlanType}), reservation #{OwnerReservationId.Value}"
                : "no active reservation owner";
            return $"Reservation conflict for {ResourceType}: plan #{RequestingPlanId} "
                + $"requested {RequestedAmount}; current {CurrentAmount}, reserved {ReservedAmount}, "
                + $"available {AvailableAmount}; owner: {owner}.";
        }
    }

    public sealed class StrategicResourceAvailability
    {
        public ResourceType ResourceType { get; }
        public int RequestedAmount { get; }
        public int CurrentAmount { get; }
        public int ReservedAmount { get; }
        public int AvailableAmount { get; }
        public bool IsAvailable => RequestedAmount <= AvailableAmount;

        internal StrategicResourceAvailability(ResourceType resourceType, int requestedAmount,
            int currentAmount, int reservedAmount)
        {
            ResourceType = resourceType;
            RequestedAmount = requestedAmount;
            CurrentAmount = currentAmount;
            ReservedAmount = reservedAmount;
            AvailableAmount = Math.Max(0, currentAmount - reservedAmount);
        }
    }

    // Planning-only accounting. This class never writes to PlayerResources.
    public sealed class StrategicResourceReservationManager
    {
        private readonly Func<ResourceType, int> currentAmountProvider;
        private readonly List<StrategicResourceReservation> reservations =
            new List<StrategicResourceReservation>();
        private int nextReservationId = 1;

        public IReadOnlyList<StrategicResourceReservation> Reservations => reservations;
        public event Action<StrategicResourceReservation> ReservationCreated;
        public event Action<StrategicResourceReservation> ReservationReleased;
        public event Action<StrategicReservationConflict> ReservationConflictDetected;

        public StrategicResourceReservationManager(Func<ResourceType, int> currentAmountProvider)
        {
            this.currentAmountProvider = currentAmountProvider
                ?? throw new ArgumentNullException(nameof(currentAmountProvider));
        }

        public int GetReservedAmount(ResourceType resourceType)
        {
            ValidateResourceType(resourceType);
            int total = 0;
            for (int i = 0; i < reservations.Count; i++)
                if (reservations[i].Status == StrategicResourceReservationStatus.Active
                    && reservations[i].ResourceType == resourceType)
                    total += reservations[i].Amount;
            return total;
        }

        public int GetReservedAmountForPlan(int planId, ResourceType resourceType)
        {
            ValidateResourceType(resourceType);
            int total = 0;
            for (int i = 0; i < reservations.Count; i++)
                if (reservations[i].Status == StrategicResourceReservationStatus.Active
                    && reservations[i].PlanId == planId
                    && reservations[i].ResourceType == resourceType)
                    total += reservations[i].Amount;
            return total;
        }

        public StrategicResourceAvailability CheckAvailability(ResourceType resourceType, int amount)
        {
            ValidateResourceType(resourceType);
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int current = Math.Max(0, currentAmountProvider(resourceType));
            return new StrategicResourceAvailability(resourceType, amount, current,
                GetReservedAmount(resourceType));
        }

        public bool CanAllocate(ResourceType resourceType, int amount)
        {
            return CheckAvailability(resourceType, amount).IsAvailable;
        }

        public IReadOnlyList<StrategicResourceReservation> GetReservationsForPlan(int planId)
        {
            var result = new List<StrategicResourceReservation>();
            for (int i = 0; i < reservations.Count; i++)
                if (reservations[i].PlanId == planId) result.Add(reservations[i]);
            return result.AsReadOnly();
        }

        internal bool TryReservePlan(StrategicPlan plan, out StrategicReservationConflict conflict)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.StrategicPlanId < 1)
                throw new ArgumentException("A plan must have a stable identity before reserving resources.", nameof(plan));
            if (HasActiveReservations(plan.StrategicPlanId))
                throw new InvalidOperationException("A plan cannot create duplicate active reservations.");

            // Validate every requirement before creating any reservation so plan claims are atomic.
            for (int i = 0; i < plan.RequiredResources.Count; i++)
            {
                StrategicResourceRequirement requirement = plan.RequiredResources[i];
                StrategicResourceAvailability availability = CheckAvailability(
                    requirement.ResourceType, requirement.Amount);
                if (availability.IsAvailable) continue;
                conflict = new StrategicReservationConflict(plan, requirement.ResourceType,
                    requirement.Amount, availability.CurrentAmount, availability.ReservedAmount,
                    availability.AvailableAmount, FindDeterministicOwner(requirement.ResourceType));
                ReservationConflictDetected?.Invoke(conflict);
                return false;
            }

            for (int i = 0; i < plan.RequiredResources.Count; i++)
            {
                var reservation = new StrategicResourceReservation(nextReservationId++, plan,
                    plan.RequiredResources[i]);
                reservations.Add(reservation);
                ReservationCreated?.Invoke(reservation);
            }
            conflict = null;
            return true;
        }

        internal void ReleasePlanReservations(int planId, bool cancelled)
        {
            for (int i = 0; i < reservations.Count; i++)
            {
                StrategicResourceReservation reservation = reservations[i];
                if (reservation.PlanId != planId
                    || reservation.Status != StrategicResourceReservationStatus.Active) continue;
                reservation.Release(cancelled);
                ReservationReleased?.Invoke(reservation);
            }
        }

        private bool HasActiveReservations(int planId)
        {
            for (int i = 0; i < reservations.Count; i++)
                if (reservations[i].PlanId == planId
                    && reservations[i].Status == StrategicResourceReservationStatus.Active)
                    return true;
            return false;
        }

        private StrategicResourceReservation FindDeterministicOwner(ResourceType resourceType)
        {
            // Reservations are stored in stable ID order; the first active claim is the owner reported.
            for (int i = 0; i < reservations.Count; i++)
                if (reservations[i].Status == StrategicResourceReservationStatus.Active
                    && reservations[i].ResourceType == resourceType)
                    return reservations[i];
            return null;
        }

        private static void ValidateResourceType(ResourceType resourceType)
        {
            if (!Enum.IsDefined(typeof(ResourceType), resourceType))
                throw new ArgumentOutOfRangeException(nameof(resourceType));
        }
    }
}
