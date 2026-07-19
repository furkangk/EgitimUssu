using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Application;

// ---------------------------------------------------------------------------
// Commands + response
// ---------------------------------------------------------------------------

public sealed record DeclarePaymentPaidCommand(Guid ParentUserId, Guid PaymentRecordId, decimal DeclaredAmount, string? Note)
    : ICommand<Result<ParentPaymentDeclarationResponse>>;

public sealed record ConfirmPaymentDeclarationCommand(Guid DeclarationId) : ICommand<Result<ParentPaymentDeclarationResponse>>;

public sealed record RejectPaymentDeclarationCommand(Guid DeclarationId) : ICommand<Result<ParentPaymentDeclarationResponse>>;

public sealed record ListPaymentDeclarationsForTeacherQuery(Guid TeacherUserId, bool OnlyPending = false)
    : IQuery<Result<IReadOnlyCollection<ParentPaymentDeclarationResponse>>>;

public sealed record ParentPaymentDeclarationResponse(
    Guid Id,
    Guid PaymentRecordId,
    Guid ParentUserId,
    Guid TeacherUserId,
    Guid StudentId,
    decimal DeclaredAmount,
    string? Note,
    string Status,
    DateTime CreatedOnUtc,
    DateTime? ResolvedOnUtc);

// ---------------------------------------------------------------------------
// Repository abstraction
// ---------------------------------------------------------------------------

public interface IParentPaymentDeclarationRepository
{
    Task AddAsync(ParentPaymentDeclaration declaration, CancellationToken cancellationToken);

    Task<ParentPaymentDeclaration?> GetByIdAsync(Guid declarationId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParentPaymentDeclaration>> ListForTeacherAsync(Guid teacherUserId, bool onlyPending, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------

public static class ParentPaymentDeclarationErrors
{
    public static readonly Error RecordNotFound = new("payments.record_not_found", "Ödeme kaydı bulunamadı.");
    public static readonly Error DeclarationNotFound = new("payments.declaration_not_found", "Ödeme beyanı bulunamadı.");
    public static readonly Error DeclarationNotPending = new("payments.declaration_not_pending", "Bu ödeme beyanı zaten sonuçlandırılmış.");
    public static readonly Error InvalidRequest = new("payments.invalid_request", "Ödeme beyanı bilgileri eksik veya hatalı.");
}

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class DeclarePaymentPaidCommandValidator : ICommandValidator<DeclarePaymentPaidCommand>
{
    public Task<Result> Validate(DeclarePaymentPaidCommand command, CancellationToken cancellationToken)
        => Task.FromResult(
            command.ParentUserId == Guid.Empty || command.PaymentRecordId == Guid.Empty || command.DeclaredAmount <= 0m
                ? Result.Failure(ParentPaymentDeclarationErrors.InvalidRequest)
                : Result.Success());
}

// ---------------------------------------------------------------------------
// Command handlers
// ---------------------------------------------------------------------------

public sealed class DeclarePaymentPaidCommandHandler : ICommandHandler<DeclarePaymentPaidCommand, Result<ParentPaymentDeclarationResponse>>
{
    private readonly IPaymentRecordRepository _paymentRepository;
    private readonly IParentPaymentDeclarationRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public DeclarePaymentPaidCommandHandler(
        IPaymentRecordRepository paymentRepository,
        IParentPaymentDeclarationRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _paymentRepository = paymentRepository;
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ParentPaymentDeclarationResponse>> Handle(DeclarePaymentPaidCommand command, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(command.PaymentRecordId, cancellationToken);
        if (payment is null)
        {
            return Result<ParentPaymentDeclarationResponse>.Failure(ParentPaymentDeclarationErrors.RecordNotFound);
        }

        var declaration = new ParentPaymentDeclaration(
            _idGenerator.New(),
            payment.Id,
            command.ParentUserId,
            payment.TeacherUserId,
            payment.StudentId,
            command.DeclaredAmount,
            command.Note,
            _clock.UtcNow);

        await _repository.AddAsync(declaration, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ParentPaymentDeclarationResponse>.Success(declaration.ToResponse());
    }
}

public sealed class ConfirmPaymentDeclarationCommandHandler : ICommandHandler<ConfirmPaymentDeclarationCommand, Result<ParentPaymentDeclarationResponse>>
{
    private readonly IParentPaymentDeclarationRepository _repository;
    private readonly IPaymentRecordRepository _paymentRepository;
    private readonly IClock _clock;

    public ConfirmPaymentDeclarationCommandHandler(
        IParentPaymentDeclarationRepository repository,
        IPaymentRecordRepository paymentRepository,
        IClock clock)
    {
        _repository = repository;
        _paymentRepository = paymentRepository;
        _clock = clock;
    }

    public async Task<Result<ParentPaymentDeclarationResponse>> Handle(ConfirmPaymentDeclarationCommand command, CancellationToken cancellationToken)
    {
        var declaration = await _repository.GetByIdAsync(command.DeclarationId, cancellationToken);
        if (declaration is null)
        {
            return Result<ParentPaymentDeclarationResponse>.Failure(ParentPaymentDeclarationErrors.DeclarationNotFound);
        }

        if (declaration.Status != ParentPaymentDeclarationStatus.Declared)
        {
            return Result<ParentPaymentDeclarationResponse>.Failure(ParentPaymentDeclarationErrors.DeclarationNotPending);
        }

        var now = _clock.UtcNow;
        declaration.Confirm(now);

        var payment = await _paymentRepository.GetByIdAsync(declaration.PaymentRecordId, cancellationToken);
        payment?.MarkCollectedByParentConfirmation(now);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ParentPaymentDeclarationResponse>.Success(declaration.ToResponse());
    }
}

public sealed class RejectPaymentDeclarationCommandHandler : ICommandHandler<RejectPaymentDeclarationCommand, Result<ParentPaymentDeclarationResponse>>
{
    private readonly IParentPaymentDeclarationRepository _repository;
    private readonly IClock _clock;

    public RejectPaymentDeclarationCommandHandler(IParentPaymentDeclarationRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ParentPaymentDeclarationResponse>> Handle(RejectPaymentDeclarationCommand command, CancellationToken cancellationToken)
    {
        var declaration = await _repository.GetByIdAsync(command.DeclarationId, cancellationToken);
        if (declaration is null)
        {
            return Result<ParentPaymentDeclarationResponse>.Failure(ParentPaymentDeclarationErrors.DeclarationNotFound);
        }

        if (declaration.Status != ParentPaymentDeclarationStatus.Declared)
        {
            return Result<ParentPaymentDeclarationResponse>.Failure(ParentPaymentDeclarationErrors.DeclarationNotPending);
        }

        declaration.Reject(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ParentPaymentDeclarationResponse>.Success(declaration.ToResponse());
    }
}

// ---------------------------------------------------------------------------
// Query handler
// ---------------------------------------------------------------------------

public sealed class ListPaymentDeclarationsForTeacherQueryHandler
    : IQueryHandler<ListPaymentDeclarationsForTeacherQuery, Result<IReadOnlyCollection<ParentPaymentDeclarationResponse>>>
{
    private readonly IParentPaymentDeclarationRepository _repository;

    public ListPaymentDeclarationsForTeacherQueryHandler(IParentPaymentDeclarationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<ParentPaymentDeclarationResponse>>> Handle(ListPaymentDeclarationsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var items = await _repository.ListForTeacherAsync(query.TeacherUserId, query.OnlyPending, cancellationToken);
        return Result<IReadOnlyCollection<ParentPaymentDeclarationResponse>>.Success(
            items.Select(d => d.ToResponse()).ToArray());
    }
}

// ---------------------------------------------------------------------------
// Authorizers
// ---------------------------------------------------------------------------

// Veli beyanı: yalnızca ödeme kaydındaki öğrencinin ONAYLI velisi (veya Admin) beyan verebilir.
public sealed class DeclarePaymentPaidCommandAuthorizer : ICommandAuthorizer<DeclarePaymentPaidCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ödeme için beyan verme yetkiniz yok.");
    private static readonly Error NotFound = new("payments.record_not_found", "Ödeme kaydı bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IPaymentRecordRepository _paymentRepository;
    private readonly IParentAccessDirectory _parentAccess;

    public DeclarePaymentPaidCommandAuthorizer(ICurrentUser currentUser, IPaymentRecordRepository paymentRepository, IParentAccessDirectory parentAccess)
    {
        _currentUser = currentUser;
        _paymentRepository = paymentRepository;
        _parentAccess = parentAccess;
    }

    public async Task<Result> Authorize(DeclarePaymentPaidCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !Guid.TryParse(_currentUser.UserId, out var userId) || userId != command.ParentUserId)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var payment = await _paymentRepository.GetByIdAsync(command.PaymentRecordId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(NotFound);
        }

        var isApprovedParent = await _parentAccess.IsApprovedParentOfStudentAsync(command.ParentUserId, payment.StudentId, cancellationToken);
        return isApprovedParent ? Result.Success() : Result.Failure(Forbidden);
    }
}

// Teyit/red: yalnızca beyanın ait olduğu öğretmen (veya Admin).
public sealed class PaymentDeclarationResolveAuthorizer :
    ICommandAuthorizer<ConfirmPaymentDeclarationCommand>,
    ICommandAuthorizer<RejectPaymentDeclarationCommand>,
    IQueryAuthorizer<ListPaymentDeclarationsForTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ödeme beyanını yönetme yetkiniz yok.");
    private static readonly Error NotFound = new("payments.declaration_not_found", "Ödeme beyanı bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IParentPaymentDeclarationRepository _repository;

    public PaymentDeclarationResolveAuthorizer(ICurrentUser currentUser, IParentPaymentDeclarationRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(ConfirmPaymentDeclarationCommand command, CancellationToken cancellationToken)
        => AuthorizeTeacherOfDeclarationAsync(command.DeclarationId, cancellationToken);

    public Task<Result> Authorize(RejectPaymentDeclarationCommand command, CancellationToken cancellationToken)
        => AuthorizeTeacherOfDeclarationAsync(command.DeclarationId, cancellationToken);

    public Task<Result> Authorize(ListPaymentDeclarationsForTeacherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private async Task<Result> AuthorizeTeacherOfDeclarationAsync(Guid declarationId, CancellationToken cancellationToken)
    {
        var declaration = await _repository.GetByIdAsync(declarationId, cancellationToken);
        if (declaration is null)
        {
            return Result.Failure(NotFound);
        }

        return CanManageTeacher(declaration.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden);
    }

    private bool CanManageTeacher(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == teacherUserId);
    }
}

internal static class ParentPaymentDeclarationMappings
{
    public static ParentPaymentDeclarationResponse ToResponse(this ParentPaymentDeclaration d)
        => new(
            d.Id,
            d.PaymentRecordId,
            d.ParentUserId,
            d.TeacherUserId,
            d.StudentId,
            d.DeclaredAmount,
            d.Note,
            d.Status.ToString(),
            d.CreatedOnUtc,
            d.ResolvedOnUtc);
}
