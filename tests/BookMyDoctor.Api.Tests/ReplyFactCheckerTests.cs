using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class ReplyFactCheckerTests
{
    private readonly ReplyFactChecker _checker = new(new DoctorService(new DoctorsRepo()), new HospitalRepo());

    [Fact]
    public void FlagsInventedDoctorAndPrice()
    {
        // The reply from the real conversation.
        var problems = _checker.FindProblems(
            "The fee for a Cardiology consultation is typically $150. One of them, Dr. Ravi Kumar, specializes in high blood pressure.",
            []);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, problem => problem.Contains("\"Dr. Ravi Kumar\" is not a doctor at this hospital"));
        Assert.Contains(problems, problem => problem.Contains("\"$150\" is not a real fee or price"));
    }

    [Fact]
    public void FlagsTimesNobodyLookedUp()
    {
        // From the real conversation: no slots had been looked up, and Cardiology doesn't work on that day.
        var problems = _checker.FindProblems(
            "Let's check the details of the Cardiologist who is available tomorrow at 09:00.",
            ["i am having high BP", "Doctor Cardiology | Cardiology | 12 years | new visit $180, follow-up $120"]);

        Assert.Contains("\"09:00\" is not a time you looked up", Assert.Single(problems));
    }

    [Theory]
    [InlineData("Doctor Dermatology is free at 12:20 and 12:40.")]   // exactly as the tool wrote them
    [InlineData("How about 12:20 pm?")]                              // same time, 12-hour style
    [InlineData("Shall I book 2:20 PM for you?")]                     // 14:20 written in 12-hour style
    [InlineData("You can come at 9 am.")]                             // 09:00 without minutes
    [InlineData("Doctor Cardiology has 12 years of experience.")]    // a plain number isn't a time
    public void AcceptsTimesTheModelWasGiven(string reply)
    {
        string[] seen = ["Doctor Dermatology has free times on Tuesday 27 October 2026 (2026-10-27): 09:00, 12:20, 12:40, 14:20 (each 20 minutes)."];

        Assert.Empty(_checker.FindProblems(reply, seen));
    }

    [Theory]
    [InlineData("Doctor Cardiology charges $180 for a new visit and $120 for a follow-up.")]
    [InlineData("Dr. Cardiology is free on Monday.")]                        // short form of "Doctor Cardiology"
    [InlineData("Doctor General Practice is the cheapest at $90.")]           // two-word name
    [InlineData("Doctor Dermatology's fee is $140.")]                         // possessive
    [InlineData("Doctor Cardiology Monday slots are full.")]                  // next capitalised word
    [InlineData("An ECG costs $45 and an X-ray is $60.")]                     // service prices
    [InlineData("Please see a doctor today.")]                                // lower-case "doctor" isn't a name
    public void AcceptsRealDoctorsAndPrices(string reply)
    {
        Assert.Empty(_checker.FindProblems(reply, []));
    }
}
