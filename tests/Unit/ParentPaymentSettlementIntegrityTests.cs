using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentPaymentSettlementIntegrityTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(499)]
    [InlineData(501)]
    public async Task Declaration_must_cover_exact_outstanding_balance(decimal amount)
    {
        var payment = NewPayment();
        var declarations = new DeclarationRepository();
        var handler = new DeclarePaymentPaidCommandHandler(new PaymentRepository(payment), declarations, new IdGenerator(), new Clock());

        var result = await handler.Handle(new DeclarePaymentPaidCommand(Guid.NewGuid(), payment.Id, amount, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("payments.declaration_amount_mismatch", result.Error.Code);
        Assert.Null(declarations.Item);
        Assert.Equal(0, declarations.SaveCount);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Cancelled)]
    public async Task Closed_payment_rejects_new_declaration(PaymentStatus status)
    {
        var payment = NewPayment(status);
        var declarations = new DeclarationRepository();
        var handler = new DeclarePaymentPaidCommandHandler(new PaymentRepository(payment), declarations, new IdGenerator(), new Clock());

        var result = await handler.Handle(new DeclarePaymentPaidCommand(Guid.NewGuid(), payment.Id, 500m, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("payments.record_not_outstanding", result.Error.Code);
        Assert.Equal(0, declarations.SaveCount);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, 0, 500)]
    [InlineData(PaymentStatus.PartiallyPaid, 200, 300)]
    [InlineData(PaymentStatus.Overdue, 200, 300)]
    public async Task Confirmation_settles_current_remaining_balance(PaymentStatus status, decimal collected, decimal declared)
    {
        var payment = NewPayment(status, collected);
        var declaration = NewDeclaration(payment, declared);
        var declarations = new DeclarationRepository(declaration);
        var handler = new ConfirmPaymentDeclarationCommandHandler(declarations, new PaymentRepository(payment), new Clock());

        var result = await handler.Handle(new ConfirmPaymentDeclarationCommand(declaration.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParentPaymentDeclarationStatus.Confirmed, declaration.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(500m, payment.CollectedAmount);
        Assert.Equal(Now, payment.CollectedOnUtc);
        Assert.Equal(1, declarations.SaveCount);
    }

    [Fact]
    public async Task Missing_payment_cannot_confirm_or_emit_resolution_event()
    {
        var declaration = NewDeclaration(NewPayment(), 500m);
        declaration.ClearDomainEvents();
        var declarations = new DeclarationRepository(declaration);
        var handler = new ConfirmPaymentDeclarationCommandHandler(declarations, new PaymentRepository(null), new Clock());

        var result = await handler.Handle(new ConfirmPaymentDeclarationCommand(declaration.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal("payments.record_not_found", result.Error.Code);
        AssertUnchanged(declaration, declarations);
    }

    [Theory]
    [InlineData(PaymentStatus.Cancelled, 0, "payments.record_not_outstanding")]
    [InlineData(PaymentStatus.Paid, 500, "payments.record_not_outstanding")]
    [InlineData(PaymentStatus.PartiallyPaid, 200, "payments.declaration_amount_mismatch")]
    public async Task Payment_changed_after_declaration_does_not_get_overwritten(PaymentStatus status, decimal collected, string error)
    {
        var payment = NewPayment(status, collected);
        var declaration = NewDeclaration(payment, 500m);
        payment.ClearDomainEvents();
        declaration.ClearDomainEvents();
        var declarations = new DeclarationRepository(declaration);
        var handler = new ConfirmPaymentDeclarationCommandHandler(declarations, new PaymentRepository(payment), new Clock());

        var result = await handler.Handle(new ConfirmPaymentDeclarationCommand(declaration.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error.Code);
        Assert.Equal(status, payment.Status);
        Assert.Equal(collected, payment.CollectedAmount);
        Assert.Empty(payment.DomainEvents);
        AssertUnchanged(declaration, declarations);
    }

    private static void AssertUnchanged(ParentPaymentDeclaration declaration, DeclarationRepository repository)
    {
        Assert.Equal(ParentPaymentDeclarationStatus.Declared, declaration.Status);
        Assert.Null(declaration.ResolvedOnUtc);
        Assert.Empty(declaration.DomainEvents);
        Assert.Equal(0, repository.SaveCount);
    }

    private static PaymentRecord NewPayment(PaymentStatus status = PaymentStatus.Pending, decimal collected = 0m)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, BillingItemType.LessonFee, "Ders", "TRY",
            500m, collected, Now.AddDays(1), null, status, null, null, null, Now);

    private static ParentPaymentDeclaration NewDeclaration(PaymentRecord payment, decimal amount)
        => new(Guid.NewGuid(), payment.Id, Guid.NewGuid(), payment.TeacherUserId, payment.StudentId, amount, null, Now);

    private sealed class DeclarationRepository(ParentPaymentDeclaration? item = null) : IParentPaymentDeclarationRepository
    {
        public ParentPaymentDeclaration? Item { get; private set; } = item;
        public int SaveCount { get; private set; }
        public Task AddAsync(ParentPaymentDeclaration declaration, CancellationToken cancellationToken)
        {
            Item = declaration;
            return Task.CompletedTask;
        }
        public Task<ParentPaymentDeclaration?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Item);
        public Task<IReadOnlyCollection<ParentPaymentDeclaration>> ListForTeacherAsync(Guid teacher, bool onlyPending, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentPaymentDeclaration>>(Item is null ? [] : [Item]);
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class PaymentRepository(PaymentRecord? payment) : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(payment);
        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacher, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<PaymentRecord>>(payment is null ? [] : [payment]);
        public Task AddAsync(PaymentRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
    private sealed class IdGenerator : IIdGenerator { public Guid New() => Guid.NewGuid(); }
}
