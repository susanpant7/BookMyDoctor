using System.Collections.Concurrent;
using BookMyDoctor.Features.Appointments;

namespace BookMyDoctor.Features.Chats.Tools;

// Free times most recently shown to the patient for one doctor and day.
public sealed record SlotOffer(Guid DoctorId, string DoctorName, DateOnly Date, IReadOnlyList<TimeSlot> Slots);

// A booking the assistant has proposed but the patient hasn't confirmed yet.
public sealed record PendingBooking(Guid DoctorId, DateTimeOffset StartsAt, string Reason, Guid ProposedInMessageId);

// The booking each patient is in the middle of, kept by the server so the model doesn't have to remember
// the doctor, day and time across messages (small models lose track of them).
public sealed class BookingDrafts
{
    private readonly ConcurrentDictionary<Guid, SlotOffer> _offers = new();
    private readonly ConcurrentDictionary<Guid, PendingBooking> _pending = new();

    public SlotOffer? GetOffer(Guid patientId) => _offers.GetValueOrDefault(patientId);

    public void SetOffer(Guid patientId, SlotOffer offer) => _offers[patientId] = offer;

    public PendingBooking? GetPending(Guid patientId) => _pending.GetValueOrDefault(patientId);

    public void SetPending(Guid patientId, PendingBooking booking) => _pending[patientId] = booking;

    public void Clear(Guid patientId)
    {
        _offers.TryRemove(patientId, out _);
        _pending.TryRemove(patientId, out _);
    }

    public void ClearAll()
    {
        _offers.Clear();
        _pending.Clear();
    }
}
