using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class KnowledgeSourcesTests
{
    [Fact]
    public void SplitMarkdown_MakesOneDocumentPerSectionWithItsHeading()
    {
        const string markdown = """
            # Booking

            Intro text that is not in a section.

            ## How to book
            Open the app and pick a doctor.

            ## Rescheduling an appointment
            Reschedule from My Appointments.
            Rescheduling is free 12 hours ahead.
            """;

        var documents = KnowledgeSources.SplitMarkdown("booking.md", markdown).ToList();

        Assert.Equal(2, documents.Count);
        Assert.Equal("booking.md#how-to-book", documents[0].Id);
        Assert.Equal("How to book", documents[0].Title);
        Assert.Equal("## How to book\nOpen the app and pick a doctor.", documents[0].Text);
        Assert.Equal("booking.md#rescheduling-an-appointment", documents[1].Id);
        Assert.EndsWith("Rescheduling is free 12 hours ahead.", documents[1].Text);
    }

    [Fact]
    public void DoctorProfile_IncludesBioAndLanguagesButNotFees()
    {
        var doctor = new DoctorsRepo().GetById(SeedIds.DermatologyDoctorId)!;

        var document = KnowledgeSources.DoctorProfile(doctor);

        Assert.Equal($"doctor:{doctor.Id}", document.Id);
        Assert.Contains("Spanish", document.Text);
        Assert.Contains(doctor.Bio, document.Text);
        Assert.DoesNotContain("$", document.Text);
    }

    [Fact]
    public void GetDocuments_ReadsTheRealKnowledgeFolder()
    {
        var documents = new KnowledgeSources(new DoctorsRepo()).GetDocuments();

        Assert.True(documents.Count > 90);
        Assert.Equal(documents.Count, documents.Select(document => document.Id).Distinct().Count());
        Assert.All(documents, document => Assert.StartsWith("## ", document.Text));
        Assert.Equal(5, documents.Count(document => document.Source == "doctors"));
    }
}
