using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed record CreateTimeOffBlockCommand(
    Guid TeacherUserId,
    TimeOffType Type,
    string Title,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    bool IsAllDay) : ICommand<Result<CreateTimeOffResponse>>;

public sealed record DeleteTimeOffBlockCommand(Guid TimeOffId) : ICommand<Result>;

public sealed record ListTimeOffBlocksForTeacherQuery(
    Guid TeacherUserId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<TimeOffBlockResponse>>>;

public sealed record TimeOffBlockResponse(
    Guid Id, Guid TeacherUserId, string Type, string Title,
    DateTime StartAtUtc, DateTime EndAtUtc, bool IsAllDay, DateTime CreatedOnUtc);

public sealed record CreateTimeOffResponse(
    TimeOffBlockResponse Block,
    IReadOnlyCollection<LessonScheduleResponse> ConflictingLessons);

public interface ITimeOffBlockRepository
{
    Task AddAsync(TimeOffBlock block, CancellationToken cancellationToken);
    Task<TimeOffBlock?> GetByIdAsync(Guid timeOffId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TimeOffBlock>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);
    void Remove(TimeOffBlock block);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateTimeOffBlockCommandHandler : ICommandHandler<CreateTimeOffBlockCommand, Result<CreateTimeOffResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Tatil baslangic ve bitis araligi gecersiz.");
    private readonly ITimeOffBlockRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateTimeOffBlockCommandHandler(
        ITimeOffBlockRepository repository,
        ILessonScheduleRepository lessonRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CreateTimeOffResponse>> Handle(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<CreateTimeOffResponse>.Failure(InvalidRange);
        }

        var block = new TimeOffBlock(
            _idGenerator.New(), command.TeacherUserId, command.Type, command.Title.Trim(),
            command.StartAtUtc, command.EndAtUtc, command.IsAllDay, _clock.UtcNow);

        await _repository.AddAsync(block, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var lessons = await _lessonRepository.ListForTeacherAsync(
            command.TeacherUserId, command.StartAtUtc, command.EndAtUtc, cancellationToken);
        var conflicting = lessons
            .Where(l => l.Status != LessonScheduleStatus.Cancelled && l.StartAtUtc < command.EndAtUtc && l.EndAtUtc > command.StartAtUtc)
            .OrderBy(l => l.StartAtUtc)
            .Select(l => l.ToResponse())
            .ToArray();

        return Result<CreateTimeOffResponse>.Success(new CreateTimeOffResponse(block.ToResponse(), conflicting));
    }
}

public sealed class DeleteTimeOffBlockCommandHandler : ICommandHandler<DeleteTimeOffBlockCommand, Result>
{
    private static readonly Error NotFound = new("scheduling.timeoff_not_found", "Tatil blogu bulunamadi.");
    private readonly ITimeOffBlockRepository _repository;

    public DeleteTimeOffBlockCommandHandler(ITimeOffBlockRepository repository) => _repository = repository;

    public async Task<Result> Handle(DeleteTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var block = await _repository.GetByIdAsync(command.TimeOffId, cancellationToken);
        if (block is null)
        {
            return Result.Failure(NotFound);
        }

        _repository.Remove(block);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListTimeOffBlocksForTeacherQueryHandler : IQueryHandler<ListTimeOffBlocksForTeacherQuery, Result<IReadOnlyCollection<TimeOffBlockResponse>>>
{
    private readonly ITimeOffBlockRepository _repository;

    public ListTimeOffBlocksForTeacherQueryHandler(ITimeOffBlockRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TimeOffBlockResponse>>> Handle(ListTimeOffBlocksForTeacherQuery query, CancellationToken cancellationToken)
    {
        var blocks = await _repository.ListForTeacherAsync(query.TeacherUserId, query.StartAtUtc, query.EndAtUtc, cancellationToken);
        var payload = blocks.OrderBy(b => b.StartAtUtc).Select(b => b.ToResponse()).ToArray();
        return Result<IReadOnlyCollection<TimeOffBlockResponse>>.Success(payload);
    }
}

internal static class TimeOffBlockMappings
{
    public static TimeOffBlockResponse ToResponse(this TimeOffBlock block)
        => new(block.Id, block.TeacherUserId, block.Type.ToString(), block.Title,
            block.StartAtUtc, block.EndAtUtc, block.IsAllDay, block.CreatedOnUtc);
}
