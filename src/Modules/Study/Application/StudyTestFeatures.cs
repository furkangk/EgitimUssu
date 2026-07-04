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
    DateTime TakenOnUtc) : ICommand<Result<TestResultResponse>>, IStudentScopedRequest;

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
        var penaltyDivisor = command.PenaltyDivisor is > 0 ? command.PenaltyDivisor.Value : 4;

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

    public ListTestResultsQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<TestResultResponse>>> Handle(ListTestResultsQuery query, CancellationToken cancellationToken)
    {
        var tests = await _repository.ListTestsAsync(query.StudentId, query.Subject, query.Topic, query.FromUtc, query.ToUtc, cancellationToken);
        var payload = tests.Select(t => t.ToResponse()).ToArray();
        return Result<IReadOnlyCollection<TestResultResponse>>.Success(payload);
    }
}

public sealed class NetTrendQueryHandler
    : IQueryHandler<NetTrendQuery, Result<NetTrendResponse>>
{
    private readonly IStudyRepository _repository;

    public NetTrendQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<NetTrendResponse>> Handle(NetTrendQuery query, CancellationToken cancellationToken)
    {
        var tests = await _repository.ListTestsAsync(query.StudentId, query.Subject, query.Topic, null, null, cancellationToken);
        var points = tests
            .OrderBy(t => t.TakenOnUtc)
            .Select(t => new NetTrendPointResponse(t.TakenOnUtc, t.Net, t.TestName, t.TotalQuestions))
            .ToArray();

        return Result<NetTrendResponse>.Success(new NetTrendResponse(query.Subject, query.Topic, points));
    }
}
