using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.Teachers.Infrastructure;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherProfileUpdateMergeTests
{
    private static TeachersDbContext NewContext(string name)
    {
        var options = new DbContextOptionsBuilder<TeachersDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new TeachersDbContext(options, new NoOpDomainEventMapper());
    }

    private static TeacherProfile NewProfile()
    {
        var id = Guid.NewGuid();
        var profile = new TeacherProfile(
            id, Guid.NewGuid(), "Ayse Yilmaz", "Matematik", "Istanbul", "Kadikoy",
            null, null, TeacherLessonFormat.Online, 3, "Lisans", 1000m, "TRY",
            isVerified: false, profilePhotoUrl: null, createdOnUtc: new DateTime(2026, 1, 1));
        profile.Subjects.Add(new TeacherSubject(Guid.NewGuid(), id, "Matematik"));
        return profile;
    }

    [Fact]
    public async Task Update_With_Same_Subject_Should_Persist_Without_Error()
    {
        var dbName = $"teachers-merge-{Guid.NewGuid()}";
        var profile = NewProfile();

        await using (var context = NewContext(dbName))
        {
            context.Add(profile);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(dbName))
        {
            var loaded = await context.Set<TeacherProfile>()
                .Include(p => p.Subjects)
                .Include(p => p.Certificates)
                .Include(p => p.AvailabilitySlots)
                .SingleAsync();

            loaded.Update(
                "Ayse Yilmaz Guncel", "Matematik", "Istanbul", "Kadikoy", null, null,
                TeacherLessonFormat.Online, 4, "Lisans", 1100m, "TRY", null,
                availabilitySlots: [],
                subjects: [new TeacherSubject(Guid.NewGuid(), loaded.Id, "Matematik")],
                certificates: [],
                updatedOnUtc: new DateTime(2026, 2, 1));

            await context.SaveChangesAsync(); // A-01: burada patlıyordu
        }

        await using (var verify = NewContext(dbName))
        {
            var loaded = await verify.Set<TeacherProfile>().Include(p => p.Subjects).SingleAsync();
            Assert.Equal("Ayse Yilmaz Guncel", loaded.FullName);
            Assert.Single(loaded.Subjects);
            Assert.Equal("Matematik", loaded.Subjects[0].Subject);
        }
    }

    [Fact]
    public async Task Update_Should_Add_New_And_Remove_Missing_Subjects()
    {
        var dbName = $"teachers-merge-{Guid.NewGuid()}";
        var profile = NewProfile();

        await using (var context = NewContext(dbName))
        {
            context.Add(profile);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(dbName))
        {
            var loaded = await context.Set<TeacherProfile>().Include(p => p.Subjects).SingleAsync();
            loaded.Update(
                "Ayse Yilmaz", "Fizik", "Istanbul", "Kadikoy", null, null,
                TeacherLessonFormat.Online, 3, "Lisans", 1000m, "TRY", null,
                availabilitySlots: [],
                subjects:
                [
                    new TeacherSubject(Guid.NewGuid(), loaded.Id, "Fizik"),
                    new TeacherSubject(Guid.NewGuid(), loaded.Id, "Kimya")
                ],
                certificates: [],
                updatedOnUtc: new DateTime(2026, 2, 1));
            await context.SaveChangesAsync();
        }

        await using (var verify = NewContext(dbName))
        {
            var loaded = await verify.Set<TeacherProfile>().Include(p => p.Subjects).SingleAsync();
            Assert.Equal(2, loaded.Subjects.Count);
            Assert.DoesNotContain(loaded.Subjects, s => s.Subject == "Matematik");
        }
    }
}
