using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentDeclarationUniquenessTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Second_parent_cannot_leave_another_pending_declaration_and_rejection_allows_a_new_one()
    {
        await using var services = Services();
        var db = services.GetRequiredService<PaymentsDbContext>();
        var repo = services.GetRequiredService<IParentPaymentDeclarationRepository>();
        var payment = Payment();
        db.Add(payment);
        var existing = Declaration(payment);
        db.Add(existing);
        await db.SaveChangesAsync();
        var handler = new DeclarePaymentPaidCommandHandler(services.GetRequiredService<IPaymentRecordRepository>(), repo, new Ids(), new Clock());
        var denied = await handler.Handle(new(Guid.NewGuid(), payment.Id, 500m, null), default);
        Assert.True(denied.IsFailure);
        Assert.Equal("payments.declaration_pending", denied.Error.Code);
        Assert.Single(await db.ParentPaymentDeclarations.ToArrayAsync());
        await new RejectPaymentDeclarationCommandHandler(repo, new Clock()).Handle(new(existing.Id), default);
        var allowed = await handler.Handle(new(Guid.NewGuid(), payment.Id, 500m, null), default);
        Assert.True(allowed.IsSuccess);
        Assert.Equal(2, await db.ParentPaymentDeclarations.CountAsync());
        Assert.Single(await db.ParentPaymentDeclarations.Where(x => x.Status == ParentPaymentDeclarationStatus.Declared).ToArrayAsync());
    }

    [Fact]
    public async Task Confirm_rejects_legacy_other_pending_declarations_in_same_save_and_emits_resolution_for_each()
    {
        await using var services = Services();
        var db = services.GetRequiredService<PaymentsDbContext>();
        var payment = Payment();
        var selected = Declaration(payment);
        var other = Declaration(payment);
        var unrelated = Declaration(Payment());
        db.AddRange(payment, selected, other, unrelated);
        await db.SaveChangesAsync();
        var result = await new ConfirmPaymentDeclarationCommandHandler(
            services.GetRequiredService<IParentPaymentDeclarationRepository>(), services.GetRequiredService<IPaymentRecordRepository>(), new Clock())
            .Handle(new(selected.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(ParentPaymentDeclarationStatus.Confirmed, selected.Status);
        Assert.Equal(ParentPaymentDeclarationStatus.Rejected, other.Status);
        Assert.Equal(Now, other.ResolvedOnUtc);
        Assert.Equal(ParentPaymentDeclarationStatus.Declared, unrelated.Status);
        Assert.Equal(500m, payment.CollectedAmount);
        Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.Type == nameof(ParentPaymentDeclarationResolvedDomainEvent)));
    }

    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:Postgres"] = $"InMemory:declaration-{Guid.NewGuid()}" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddPaymentsModule(config);
        return services.BuildServiceProvider();
    }
    private static PaymentRecord Payment() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, BillingItemType.LessonFee,
        "Ders", "TRY", 500m, 0m, Now.AddDays(1), null, PaymentStatus.Pending, null, null, null, Now);
    private static ParentPaymentDeclaration Declaration(PaymentRecord payment) => new(Guid.NewGuid(), payment.Id, Guid.NewGuid(), payment.TeacherUserId, payment.StudentId, 500m, null, Now);
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
    private sealed class Ids : IIdGenerator { public Guid New() => Guid.NewGuid(); }
}
