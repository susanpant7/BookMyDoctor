using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Features.Settings;

// Deletes everything, leaving the database as empty as on a fresh start. The admin signs in from configuration, so
// stays signed in, and loads the data again on Settings → Manage App Data.
public sealed class DataCleanup
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly BookingDrafts _drafts;
    private readonly ToolTranscripts _transcripts;

    public DataCleanup(IDbContextFactory<AppDbContext> factory, BookingDrafts drafts, ToolTranscripts transcripts)
    {
        _factory = factory;
        _drafts = drafts;
        _transcripts = transcripts;
    }

    public async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        await using (var db = await _factory.CreateDbContextAsync(cancellationToken))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Rows that point at others go first, so no foreign key is left dangling.
            await db.Appointments.ExecuteDeleteAsync(cancellationToken);
            await db.ChatMessages.ExecuteDeleteAsync(cancellationToken);
            await db.TimeOff.ExecuteDeleteAsync(cancellationToken);
            await db.DoctorSchedules.ExecuteDeleteAsync(cancellationToken);
            await db.Doctors.ExecuteDeleteAsync(cancellationToken);
            await db.Specialties.ExecuteDeleteAsync(cancellationToken);
            await db.Users.ExecuteDeleteAsync(cancellationToken);
            await db.KnowledgeChunks.ExecuteDeleteAsync(cancellationToken);
            await db.ServicePrices.ExecuteDeleteAsync(cancellationToken);
            await db.HospitalHours.ExecuteDeleteAsync(cancellationToken);
            await db.HospitalInfo.ExecuteDeleteAsync(cancellationToken);
            await db.Settings.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        _drafts.ClearAll();
        _transcripts.ClearAll();
    }
}
