using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

public sealed record RecordTestResultCommand(
    Guid StudentId,
    string Subject,
    string? Topic,
    string TestType,
    string? TestName,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    int? PenaltyDivisor,
    int? DurationMinutes,
    DateTime TakenOnUtc,
    string? TargetExam = null) : ICommand<Result<TestResultResponse>>, IStudentScopedRequest;

public sealed record EditTestResultCommand(
    Guid TestResultId,
    string Subject,
    string? Topic,
    string TestType,
    string? TestName,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    int? PenaltyDivisor,
    int? DurationMinutes,
    DateTime TakenOnUtc) : ICommand<Result<TestResultResponse>>;

public sealed record DeleteTestResultCommand(Guid TestResultId) : ICommand<Result<bool>>;

public sealed record GetTestResultQuery(Guid TestResultId) : IQuery<Result<TestResultResponse>>;

public sealed record ListTestResultsQuery(Guid StudentId, string? Subject, string? Topic, DateTime? FromUtc, DateTime? ToUtc)
    : IQuery<Result<IReadOnlyCollection<TestResultResponse>>>, IStudentScopedRequest;

public sealed record NetTrendQuery(Guid StudentId, string? Subject, string? Topic)
    : IQuery<Result<NetTrendResponse>>, IStudentScopedRequest;

public sealed class RecordTestResultCommandHandler
    : ICommandHandler<RecordTestResultCommand, Result<TestResultResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly AchievementEvaluator _evaluator;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RecordTestResultCommandHandler(
        IStudyRepository repository,
        StudyLinkResolver linkResolver,
        AchievementEvaluator evaluator,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _evaluator = evaluator;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TestResultResponse>> Handle(RecordTestResultCommand command, CancellationToken cancellationToken)
    {
        if (command.Correct < 0 || command.Wrong < 0 || command.Blank < 0
            || command.Correct + command.Wrong + command.Blank != command.TotalQuestions
            || command.TotalQuestions <= 0)
        {
            return Result<TestResultResponse>.Failure(StudyErrors.InvalidTest);
        }

        if (command.TakenOnUtc > _clock.UtcNow.AddMinutes(1))
        {
            return Result<TestResultResponse>.Failure(StudyErrors.InvalidRequest);
        }

        var link = await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        var penaltyDivisor = command.PenaltyDivisor is > 0
            ? command.PenaltyDivisor.Value
            : ExamPenalty.DivisorFor(command.TargetExam) ?? int.MaxValue; // School → int.MaxValue: yanlış götürmez (Net ≈ Correct)

        var testResult = new TestResult(
            _idGenerator.New(),
            command.StudentId,
            command.Subject.Trim(),
            string.IsNullOrWhiteSpace(command.Topic) ? null : command.Topic.Trim(),
            string.IsNullOrWhiteSpace(command.TestName) ? null : command.TestName.Trim(),
            ParseTestType(command.TestType),
            command.TotalQuestions,
            command.Correct,
            command.Wrong,
            command.Blank,
            penaltyDivisor,
            command.DurationMinutes,
            DateTime.SpecifyKind(command.TakenOnUtc, DateTimeKind.Utc),
            link.ShareTestsWithParent,
            link.ShareTestsWithTeacher,
            _clock.UtcNow);

        await _repository.AddTestAsync(testResult, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var streak = await _repository.GetStreakAsync(command.StudentId, cancellationToken);
        var metrics = new StudyMetrics(
            streak?.CurrentStreakDays ?? 0,
            await _repository.SumEffectiveMinutesAsync(command.StudentId, cancellationToken),
            await _repository.CountCompletedSessionsAsync(command.StudentId, cancellationToken),
            await _repository.CountTestsAsync(command.StudentId, cancellationToken));

        await _evaluator.EvaluateAsync(command.StudentId, metrics, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<TestResultResponse>.Success(testResult.ToResponse());
    }

    private static TestType ParseTestType(string value) =>
        Enum.TryParse<TestType>(value, ignoreCase: true, out var parsed) ? parsed : TestType.General;
}

public sealed class EditTestResultCommandHandler
    : ICommandHandler<EditTestResultCommand, Result<TestResultResponse>>
{
    private static readonly Error NotFound = new("study.test_not_found", "Deneme sonucu bulunamadı.");
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public EditTestResultCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<TestResultResponse>> Handle(EditTestResultCommand command, CancellationToken cancellationToken)
    {
        if (command.Correct < 0 || command.Wrong < 0 || command.Blank < 0
            || command.Correct + command.Wrong + command.Blank != command.TotalQuestions
            || command.TotalQuestions <= 0)
        {
            return Result<TestResultResponse>.Failure(StudyErrors.InvalidTest);
        }

        if (command.TakenOnUtc > _clock.UtcNow.AddMinutes(1))
        {
            return Result<TestResultResponse>.Failure(StudyErrors.InvalidRequest);
        }

        var test = await _repository.GetTestAsync(command.TestResultId, cancellationToken);
        if (test is null)
        {
            return Result<TestResultResponse>.Failure(NotFound);
        }

        var penaltyDivisor = command.PenaltyDivisor is > 0 ? command.PenaltyDivisor.Value : 4;
        test.Edit(
            command.Subject.Trim(),
            command.Topic,
            command.TestName,
            ParseTestType(command.TestType),
            command.TotalQuestions,
            command.Correct,
            command.Wrong,
            command.Blank,
            penaltyDivisor,
            command.DurationMinutes,
            DateTime.SpecifyKind(command.TakenOnUtc, DateTimeKind.Utc),
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TestResultResponse>.Success(test.ToResponse());
    }

    private static TestType ParseTestType(string value) =>
        Enum.TryParse<TestType>(value, ignoreCase: true, out var parsed) ? parsed : TestType.General;
}

public sealed class DeleteTestResultCommandHandler : ICommandHandler<DeleteTestResultCommand, Result<bool>>
{
    private static readonly Error NotFound = new("study.test_not_found", "Deneme sonucu bulunamadı.");
    private readonly IStudyRepository _repository;

    public DeleteTestResultCommandHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteTestResultCommand command, CancellationToken cancellationToken)
    {
        var test = await _repository.GetTestAsync(command.TestResultId, cancellationToken);
        if (test is null)
        {
            return Result<bool>.Failure(NotFound);
        }

        _repository.RemoveTest(test);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetTestResultQueryHandler
    : IQueryHandler<GetTestResultQuery, Result<TestResultResponse>>
{
    private readonly IStudyRepository _repository;

    public GetTestResultQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TestResultResponse>> Handle(GetTestResultQuery query, CancellationToken cancellationToken)
    {
        var testResult = await _repository.GetTestAsync(query.TestResultId, cancellationToken);
        return testResult is null
            ? Result<TestResultResponse>.Failure(new Error("study.test_not_found", "Deneme sonucu bulunamadı."))
            : Result<TestResultResponse>.Success(testResult.ToResponse());
    }
}

public sealed class ListTestResultsQueryHandler
    : IQueryHandler<ListTestResultsQuery, Result<IReadOnlyCollection<TestResultResponse>>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyMembershipResolver _membership;
    private readonly IClock _clock;

    public ListTestResultsQueryHandler(IStudyRepository repository, StudyMembershipResolver membership, IClock clock)
    {
        _repository = repository;
        _membership = membership;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyCollection<TestResultResponse>>> Handle(ListTestResultsQuery query, CancellationToken cancellationToken)
    {
        // Ö-D: Free deneme geçmişi son 30 güne kısılır; Premium sınırsız.
        var tier = await _membership.CurrentTierAsync(cancellationToken);
        var fromUtc = MembershipGate.ClampFrom(tier, query.FromUtc, _clock.UtcNow);

        var tests = await _repository.ListTestsAsync(query.StudentId, query.Subject, query.Topic, fromUtc, query.ToUtc, cancellationToken);
        var payload = tests.Select(t => t.ToResponse()).ToArray();
        return Result<IReadOnlyCollection<TestResultResponse>>.Success(payload);
    }
}

public sealed class NetTrendQueryHandler
    : IQueryHandler<NetTrendQuery, Result<NetTrendResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyMembershipResolver _membership;
    private readonly IClock _clock;

    public NetTrendQueryHandler(IStudyRepository repository, StudyMembershipResolver membership, IClock clock)
    {
        _repository = repository;
        _membership = membership;
        _clock = clock;
    }

    public async Task<Result<NetTrendResponse>> Handle(NetTrendQuery query, CancellationToken cancellationToken)
    {
        // Ö-D: Free net trendi son 30 güne kısılır; Premium sınırsız.
        var tier = await _membership.CurrentTierAsync(cancellationToken);
        var fromUtc = MembershipGate.ClampFrom(tier, null, _clock.UtcNow);

        var tests = await _repository.ListTestsAsync(query.StudentId, query.Subject, query.Topic, fromUtc, null, cancellationToken);
        var points = tests
            .OrderBy(t => t.TakenOnUtc)
            .Select(t => new NetTrendPointResponse(t.TakenOnUtc, t.Net, t.TestName, t.TotalQuestions))
            .ToArray();

        return Result<NetTrendResponse>.Success(new NetTrendResponse(query.Subject, query.Topic, points));
    }
}
