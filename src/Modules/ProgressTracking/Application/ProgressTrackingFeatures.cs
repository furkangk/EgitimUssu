using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Application;

// ---- Sorgu / komut ----

public sealed record ListTopicMasteryQuery(Guid StudentId, string? Subject)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record ListWeakSpotsQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record ListStrengthsQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record GetProgressOverviewQuery(Guid StudentId)
    : IQuery<Result<ProgressOverviewResponse>>, IStudentScopedProgressRequest;

public sealed record ListTopicGoalsQuery(Guid StudentId, string? Status)
    : IQuery<Result<IReadOnlyCollection<TopicGoalResponse>>>, IStudentScopedProgressRequest;

public sealed record CreateTopicGoalCommand(
    Guid StudentId, string Subject, string Topic, string TargetMasteryLevel, decimal? TargetNetRatio, DateOnly? TargetDate)
    : ICommand<Result<TopicGoalResponse>>, IStudentScopedProgressRequest;

public sealed record CancelTopicGoalCommand(Guid GoalId) : ICommand<Result<bool>>;

internal static class ProgressErrors
{
    public static readonly Error GoalNotFound = new("progress.goal_not_found", "Konu hedefi bulunamadı.");
    public static readonly Error InvalidRequest = new("progress.invalid_request", "Gelişim/hedef bilgisi eksik veya hatalı.");
}

internal static class ProgressMappings
{
    public static TopicMasteryResponse ToResponse(this TopicMastery m) => new(
        m.Id, m.StudentId, m.Subject, m.Topic, m.MasteryLevel.ToString(), m.MasteryScore,
        m.TotalStudyMinutes, m.TestAttemptCount, m.AverageNetRatio, m.Trend.ToString(),
        m.IsWeakSpot, m.IsStrength, m.LastEvaluatedOnUtc);

    public static TopicGoalResponse ToResponse(this TopicGoal g) => new(
        g.Id, g.StudentId, g.Subject, g.Topic, g.TargetMasteryLevel.ToString(), g.TargetNetRatio,
        g.SetByRole.ToString(), g.TargetDate, g.Status.ToString(), g.AchievedOnUtc, g.CreatedOnUtc);
}

// ---- Hâkimiyet servisi (consumer'ların çağırdığı ortak upsert + hedef değerlendirme) ----

public sealed class MasteryService
{
    public const string GeneralTopic = "(Genel)";
    private readonly IProgressRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public MasteryService(IProgressRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task ApplyStudyAsync(Guid studentId, string subject, string? topic, int effectiveMinutes, CancellationToken cancellationToken)
    {
        var mastery = await ResolveAsync(studentId, subject, topic, cancellationToken);
        mastery.RegisterStudy(effectiveMinutes, _clock.UtcNow);
        await EvaluateGoalsAsync(mastery, cancellationToken);
    }

    public async Task ApplyTestAsync(Guid studentId, string subject, string? topic, int totalQuestions, decimal net, CancellationToken cancellationToken)
    {
        var mastery = await ResolveAsync(studentId, subject, topic, cancellationToken);
        mastery.RegisterTest(totalQuestions, net, _clock.UtcNow);
        await EvaluateGoalsAsync(mastery, cancellationToken);
    }

    private async Task<TopicMastery> ResolveAsync(Guid studentId, string subject, string? topic, CancellationToken cancellationToken)
    {
        var normalizedTopic = string.IsNullOrWhiteSpace(topic) ? GeneralTopic : topic.Trim();
        var mastery = await _repository.GetMasteryAsync(studentId, subject.Trim(), normalizedTopic, cancellationToken);
        if (mastery is null)
        {
            mastery = new TopicMastery(_idGenerator.New(), studentId, subject, normalizedTopic, _clock.UtcNow);
            await _repository.AddMasteryAsync(mastery, cancellationToken);
        }

        return mastery;
    }

    private async Task EvaluateGoalsAsync(TopicMastery mastery, CancellationToken cancellationToken)
    {
        var goals = await _repository.ListActiveGoalsForTopicAsync(mastery.StudentId, mastery.Subject, mastery.Topic, cancellationToken);
        foreach (var goal in goals)
        {
            if (mastery.MasteryLevel >= goal.TargetMasteryLevel)
            {
                goal.MarkAchieved(_clock.UtcNow);
            }
        }
    }
}

// ---- Handler'lar ----

public sealed class ListTopicMasteryQueryHandler
    : IQueryHandler<ListTopicMasteryQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListTopicMasteryQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListTopicMasteryQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, query.Subject, cancellationToken);
        var payload = list
            .OrderBy(m => m.Subject)
            .ThenByDescending(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

public sealed class ListWeakSpotsQueryHandler
    : IQueryHandler<ListWeakSpotsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListWeakSpotsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListWeakSpotsQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, null, cancellationToken);
        var payload = list
            .Where(m => m.IsWeakSpot)
            .OrderBy(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

public sealed class ListStrengthsQueryHandler
    : IQueryHandler<ListStrengthsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListStrengthsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListStrengthsQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, null, cancellationToken);
        var payload = list
            .Where(m => m.IsStrength)
            .OrderByDescending(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

public sealed class GetProgressOverviewQueryHandler
    : IQueryHandler<GetProgressOverviewQuery, Result<ProgressOverviewResponse>>
{
    private readonly IProgressRepository _repository;

    public GetProgressOverviewQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<ProgressOverviewResponse>> Handle(GetProgressOverviewQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, null, cancellationToken);
        var activeGoals = await _repository.ListGoalsAsync(query.StudentId, TopicGoalStatus.Active, cancellationToken);

        var overview = new ProgressOverviewResponse(
            query.StudentId,
            list.Count(m => m.MasteryLevel == MasteryLevel.Mastered),
            list.Count(m => m.MasteryLevel == MasteryLevel.Proficient),
            list.Count(m => m.MasteryLevel == MasteryLevel.Developing),
            list.Count(m => m.MasteryLevel == MasteryLevel.Weak),
            list.Count(m => m.MasteryLevel == MasteryLevel.NotStarted),
            activeGoals.Count,
            list.Where(m => m.IsWeakSpot).OrderBy(m => m.MasteryScore).Take(5).Select(m => m.ToResponse()).ToArray(),
            list.Where(m => m.IsStrength).OrderByDescending(m => m.MasteryScore).Take(5).Select(m => m.ToResponse()).ToArray());

        return Result<ProgressOverviewResponse>.Success(overview);
    }
}

public sealed class ListTopicGoalsQueryHandler
    : IQueryHandler<ListTopicGoalsQuery, Result<IReadOnlyCollection<TopicGoalResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListTopicGoalsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicGoalResponse>>> Handle(ListTopicGoalsQuery query, CancellationToken cancellationToken)
    {
        TopicGoalStatus? status = Enum.TryParse<TopicGoalStatus>(query.Status, ignoreCase: true, out var parsed) ? parsed : null;
        var list = await _repository.ListGoalsAsync(query.StudentId, status, cancellationToken);
        return Result<IReadOnlyCollection<TopicGoalResponse>>.Success(list.Select(g => g.ToResponse()).ToArray());
    }
}

public sealed class CreateTopicGoalCommandHandler : ICommandHandler<CreateTopicGoalCommand, Result<TopicGoalResponse>>
{
    private readonly IProgressRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateTopicGoalCommandHandler(IProgressRepository repository, ICurrentUser currentUser, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TopicGoalResponse>> Handle(CreateTopicGoalCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Subject) || string.IsNullOrWhiteSpace(command.Topic)
            || !Enum.TryParse<MasteryLevel>(command.TargetMasteryLevel, ignoreCase: true, out var level))
        {
            return Result<TopicGoalResponse>.Failure(ProgressErrors.InvalidRequest);
        }

        var setBy = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : Guid.Empty;
        var goal = new TopicGoal(
            _idGenerator.New(), command.StudentId, command.Subject, command.Topic, level,
            command.TargetNetRatio, setBy, TopicGoalSetterRole.Student, command.TargetDate, _clock.UtcNow);

        await _repository.AddGoalAsync(goal, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TopicGoalResponse>.Success(goal.ToResponse());
    }
}

public sealed class CancelTopicGoalCommandHandler : ICommandHandler<CancelTopicGoalCommand, Result<bool>>
{
    private readonly IProgressRepository _repository;

    public CancelTopicGoalCommandHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<bool>> Handle(CancelTopicGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetGoalAsync(command.GoalId, cancellationToken);
        if (goal is null)
        {
            return Result<bool>.Failure(ProgressErrors.GoalNotFound);
        }

        goal.Cancel();
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ---- Yetkilendirme ----

public sealed class ProgressOwnershipGuard
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu gelişim verisine erişim yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _studentDirectory;

    public ProgressOwnershipGuard(ICurrentUser currentUser, IStudentDirectory studentDirectory)
    {
        _currentUser = currentUser;
        _studentDirectory = studentDirectory;
    }

    public async Task<Result> AuthorizeAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(Forbidden);
        }

        var owner = await _studentDirectory.GetOwnerUserIdAsync(studentId, cancellationToken);
        return owner == userId ? Result.Success() : Result.Failure(Forbidden);
    }
}

public sealed class ProgressOwnershipQueryAuthorizer<TQuery> : IQueryAuthorizer<TQuery>
    where TQuery : IStudentScopedProgressRequest
{
    private readonly ProgressOwnershipGuard _guard;

    public ProgressOwnershipQueryAuthorizer(ProgressOwnershipGuard guard) => _guard = guard;

    public Task<Result> Authorize(TQuery query, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(query.StudentId, cancellationToken);
}

public sealed class ProgressOwnershipCommandAuthorizer<TCommand> : ICommandAuthorizer<TCommand>
    where TCommand : IStudentScopedProgressRequest
{
    private readonly ProgressOwnershipGuard _guard;

    public ProgressOwnershipCommandAuthorizer(ProgressOwnershipGuard guard) => _guard = guard;

    public Task<Result> Authorize(TCommand command, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(command.StudentId, cancellationToken);
}

/// <summary>Konu hedefi iptali (kimlik = goalId) için sahiplik yetkilendiricisi.</summary>
public sealed class CancelTopicGoalAuthorizer : ICommandAuthorizer<CancelTopicGoalCommand>
{
    private readonly IProgressRepository _repository;
    private readonly ProgressOwnershipGuard _guard;

    public CancelTopicGoalAuthorizer(IProgressRepository repository, ProgressOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public async Task<Result> Authorize(CancelTopicGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetGoalAsync(command.GoalId, cancellationToken);
        if (goal is null)
        {
            return Result.Failure(ProgressErrors.GoalNotFound);
        }

        return await _guard.AuthorizeAsync(goal.StudentId, cancellationToken);
    }
}
