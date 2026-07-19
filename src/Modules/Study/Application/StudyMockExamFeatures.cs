using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>Çok dersli deneme içindeki tek bir ders satırı (Türkçe, Matematik, ...).</summary>
public sealed record MockExamSubjectInput(
    string Subject,
    string? Topic,
    string? TestName,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    int? PenaltyDivisor,
    string? TargetExam);

public sealed record CreateMockExamCommand(
    Guid StudentId,
    string ExamType,
    DateTime TakenOnUtc,
    IReadOnlyCollection<MockExamSubjectInput> Subjects) : ICommand<Result<MockExamResponse>>, IStudentScopedRequest;

public sealed record MockExamResponse(
    Guid Id,
    Guid StudentId,
    string ExamType,
    DateTime TakenOnUtc,
    decimal TotalNet,
    int? EstimatedRank,
    IReadOnlyCollection<TestResultResponse> Subjects);

public sealed class CreateMockExamCommandHandler
    : ICommandHandler<CreateMockExamCommand, Result<MockExamResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateMockExamCommandHandler(
        IStudyRepository repository,
        StudyLinkResolver linkResolver,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<MockExamResponse>> Handle(CreateMockExamCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExamType) || command.Subjects.Count == 0)
        {
            return Result<MockExamResponse>.Failure(StudyErrors.InvalidRequest);
        }

        if (command.TakenOnUtc > _clock.UtcNow.AddMinutes(1))
        {
            return Result<MockExamResponse>.Failure(StudyErrors.InvalidRequest);
        }

        foreach (var subject in command.Subjects)
        {
            if (subject.Correct < 0 || subject.Wrong < 0 || subject.Blank < 0
                || subject.TotalQuestions <= 0
                || subject.Correct + subject.Wrong + subject.Blank != subject.TotalQuestions
                || string.IsNullOrWhiteSpace(subject.Subject))
            {
                return Result<MockExamResponse>.Failure(StudyErrors.InvalidTest);
            }
        }

        var link = await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        var now = _clock.UtcNow;
        var takenOn = DateTime.SpecifyKind(command.TakenOnUtc, DateTimeKind.Utc);
        var examType = command.ExamType.Trim();

        var mockExam = new MockExam(_idGenerator.New(), command.StudentId, examType, takenOn, now);
        var testResults = new List<TestResult>(command.Subjects.Count);

        foreach (var subject in command.Subjects)
        {
            // Ceza böleni: açık verilmişse o, yoksa ders satırının hedef sınavından, o da yoksa denemenin tipinden türetilir.
            var penaltyDivisor = subject.PenaltyDivisor is > 0
                ? subject.PenaltyDivisor.Value
                : ExamPenalty.DivisorFor(subject.TargetExam ?? examType) ?? int.MaxValue; // School → yanlış götürmez

            var testResult = new TestResult(
                _idGenerator.New(),
                command.StudentId,
                subject.Subject.Trim(),
                string.IsNullOrWhiteSpace(subject.Topic) ? null : subject.Topic.Trim(),
                string.IsNullOrWhiteSpace(subject.TestName) ? null : subject.TestName.Trim(),
                TestType.Subject,
                subject.TotalQuestions,
                subject.Correct,
                subject.Wrong,
                subject.Blank,
                penaltyDivisor,
                durationMinutes: null,
                takenOn,
                link.ShareTestsWithParent,
                link.ShareTestsWithTeacher,
                now);

            mockExam.AddSubject(testResult);
            testResults.Add(testResult);
        }

        await _repository.AddMockExamAsync(mockExam, cancellationToken);
        foreach (var testResult in testResults)
        {
            await _repository.AddTestAsync(testResult, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        var response = new MockExamResponse(
            mockExam.Id,
            mockExam.StudentId,
            mockExam.ExamType,
            mockExam.TakenOnUtc,
            mockExam.TotalNet,
            mockExam.EstimatedRank,
            testResults.Select(t => t.ToResponse()).ToArray());

        return Result<MockExamResponse>.Success(response);
    }
}
