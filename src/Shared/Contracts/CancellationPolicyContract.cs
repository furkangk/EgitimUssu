namespace EgitimUssu.Shared.Contracts;

public sealed record TeacherCancellationPolicy(int NoticeHours, decimal ChargePercent)
{
    public static TeacherCancellationPolicy Disabled { get; } = new(0, 0m);
    public decimal Charge(DateTime startAtUtc, DateTime cancelledOnUtc, decimal lessonRate, bool studentCancelled, bool allowCharge)
        => studentCancelled && allowCharge && NoticeHours > 0 && ChargePercent > 0m && lessonRate > 0m
            && startAtUtc > cancelledOnUtc && startAtUtc - cancelledOnUtc < TimeSpan.FromHours(NoticeHours)
            ? decimal.Round(lessonRate * ChargePercent / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
}

public interface ICancellationPolicyDirectory
{
    Task<TeacherCancellationPolicy> GetCancellationPolicyAsync(Guid teacherUserId, CancellationToken cancellationToken);
}
