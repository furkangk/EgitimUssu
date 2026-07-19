using EgitimUssu.Modules.Payments.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentPaymentDeclarationTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static PaymentRecord NewPending()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            BillingItemType.LessonFee, "Ders", "TRY", expectedAmount: 500m, collectedAmount: 0m,
            dueDateUtc: Now, collectedOnUtc: null, PaymentStatus.Pending,
            billingPeriodStartUtc: null, billingPeriodEndUtc: null, notes: null, createdOnUtc: Now);

    [Fact]
    public void MarkCollectedByParentConfirmation_SetsPaidFull()
    {
        var p = NewPending();
        p.MarkCollectedByParentConfirmation(Now.AddDays(1));
        Assert.Equal(PaymentStatus.Paid, p.Status);
        Assert.Equal(p.ExpectedAmount, p.CollectedAmount);
        Assert.Equal(Now.AddDays(1), p.CollectedOnUtc);
    }

    [Fact]
    public void Declare_RaisesDeclaredEvent_AndPending()
    {
        var d = new ParentPaymentDeclaration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "havale", Now);
        Assert.Equal(ParentPaymentDeclarationStatus.Declared, d.Status);
        Assert.Contains(d.DomainEvents, e => e is ParentPaymentDeclaredDomainEvent);
    }

    [Fact]
    public void Confirm_OnlyFromDeclared()
    {
        var d = new ParentPaymentDeclaration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, null, Now);
        d.Reject(Now);
        Assert.Throws<InvalidOperationException>(() => d.Confirm(Now));
    }
}
