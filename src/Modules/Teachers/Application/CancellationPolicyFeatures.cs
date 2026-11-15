using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Teachers.Application;

public sealed record SetCancellationPolicyCommand(Guid UserId, int NoticeHours, decimal ChargePercent)
    : ICommand<Result<TeacherProfileResponse>>;

public sealed class SetCancellationPolicyCommandHandler(ITeacherProfileRepository repository, IClock clock)
    : ICommandHandler<SetCancellationPolicyCommand, Result<TeacherProfileResponse>>
{
    public async Task<Result<TeacherProfileResponse>> Handle(SetCancellationPolicyCommand command, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is null) return Result<TeacherProfileResponse>.Failure(new("teachers.profile_not_found", "Öğretmen profili bulunamadı."));
        var result = profile.SetCancellationPolicy(new CancellationPolicy(command.NoticeHours, command.ChargePercent), clock.UtcNow);
        if (result.IsFailure) return Result<TeacherProfileResponse>.Failure(result.Error);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class SetCancellationPolicyCommandAuthorizer(ICurrentUser currentUser) : ICommandAuthorizer<SetCancellationPolicyCommand>
{
    public Task<Result> Authorize(SetCancellationPolicyCommand command, CancellationToken cancellationToken)
        => Task.FromResult(currentUser.IsAuthenticated && currentUser.Roles.Contains("Teacher") &&
            Guid.TryParse(currentUser.UserId, out var id) && id == command.UserId ? Result.Success() :
            Result.Failure(new Error("shared.forbidden", "Yalnızca kendi iptal politikanızı değiştirebilirsiniz.")));
}
