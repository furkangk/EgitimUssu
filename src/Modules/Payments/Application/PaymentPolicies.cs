using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Application;

public sealed class CreatePaymentRecordCommandValidator : ICommandValidator<CreatePaymentRecordCommand>
{
    private static readonly Error InvalidRequest = new("payments.invalid_request", "Odeme kaydi bilgileri eksik veya hatali.");

    public Task<Result> Validate(CreatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsValid(command.TeacherUserId, command.StudentId, command.Description, command.Currency, command.ExpectedAmount, command.CollectedAmount, command.DueDateUtc)
            ? Result.Success()
            : Result.Failure(InvalidRequest));
    }

    private static bool IsValid(Guid teacherUserId, Guid studentId, string description, string currency, decimal expectedAmount, decimal collectedAmount, DateTime dueDateUtc)
    {
        return teacherUserId != Guid.Empty
            && studentId != Guid.Empty
            && !string.IsNullOrWhiteSpace(description)
            && !string.IsNullOrWhiteSpace(currency)
            && expectedAmount >= 0
            && collectedAmount >= 0
            && collectedAmount <= expectedAmount
            && dueDateUtc != default;
    }
}

public sealed class UpdatePaymentRecordCommandValidator : ICommandValidator<UpdatePaymentRecordCommand>
{
    private static readonly Error InvalidRequest = new("payments.invalid_request", "Odeme kaydi guncelleme bilgileri eksik veya hatali.");

    public Task<Result> Validate(UpdatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.PaymentRecordId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Description)
            && !string.IsNullOrWhiteSpace(command.Currency)
            && command.ExpectedAmount >= 0
            && command.CollectedAmount >= 0
            && command.CollectedAmount <= command.ExpectedAmount
            && command.DueDateUtc != default;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class PaymentQueryValidator :
    IQueryValidator<GetPaymentRecordByIdQuery>,
    IQueryValidator<ListPaymentRecordsForTeacherQuery>,
    IQueryValidator<ListFilteredPaymentRecordsForTeacherQuery>,
    IQueryValidator<SearchPaymentRecordsForTeacherQuery>,
    IQueryValidator<GetTeacherPaymentSummaryQuery>
{
    private static readonly Error InvalidRequest = new("payments.invalid_request", "Odeme sorgu bilgileri eksik veya hatali.");

    public Task<Result> Validate(GetPaymentRecordByIdQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.PaymentRecordId != Guid.Empty ? Result.Success() : Result.Failure(InvalidRequest));
    }

    public Task<Result> Validate(ListPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.TeacherUserId != Guid.Empty ? Result.Success() : Result.Failure(InvalidRequest));
    }

    public Task<Result> Validate(ListFilteredPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var dateRangeValid = !query.DateFromUtc.HasValue || !query.DateToUtc.HasValue || query.DateFromUtc <= query.DateToUtc;
        var ok = query.TeacherUserId != Guid.Empty && dateRangeValid;
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }

    public Task<Result> Validate(SearchPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var dateRangeValid = !query.DateFromUtc.HasValue || !query.DateToUtc.HasValue || query.DateFromUtc <= query.DateToUtc;
        var ok = query.TeacherUserId != Guid.Empty && query.Take >= 0 && query.Skip >= 0 && dateRangeValid;
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }

    public Task<Result> Validate(GetTeacherPaymentSummaryQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.TeacherUserId != Guid.Empty ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class PaymentRecordAuthorizer :
    ICommandAuthorizer<CreatePaymentRecordCommand>,
    ICommandAuthorizer<UpdatePaymentRecordCommand>,
    IQueryAuthorizer<GetPaymentRecordByIdQuery>,
    IQueryAuthorizer<ListPaymentRecordsForTeacherQuery>,
    IQueryAuthorizer<ListFilteredPaymentRecordsForTeacherQuery>,
    IQueryAuthorizer<SearchPaymentRecordsForTeacherQuery>,
    IQueryAuthorizer<GetTeacherPaymentSummaryQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu islemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("payments.record_not_found", "Odeme kaydi bulunamadi.");
    private readonly ICurrentUser _currentUser;
    private readonly IPaymentRecordRepository _repository;
    private readonly IRequestEntityCache _entityCache;

    public PaymentRecordAuthorizer(
        ICurrentUser currentUser,
        IPaymentRecordRepository repository,
        IRequestEntityCache entityCache)
    {
        _currentUser = currentUser;
        _repository = repository;
        _entityCache = entityCache;
    }

    public Task<Result> Authorize(CreatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(UpdatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        var paymentRecord = await _entityCache.LoadPaymentRecordAsync(_repository, command.PaymentRecordId, cancellationToken);
        return paymentRecord is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(paymentRecord.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(GetPaymentRecordByIdQuery query, CancellationToken cancellationToken)
    {
        var paymentRecord = await _entityCache.LoadPaymentRecordAsync(_repository, query.PaymentRecordId, cancellationToken);
        return paymentRecord is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(paymentRecord.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(ListPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(GetTeacherPaymentSummaryQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(ListFilteredPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(SearchPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
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

/// <summary>
/// O2: Ödeme kaydını istek kapsamında bir kez yükler; authorizer ve handler aynı örneği paylaşır.
/// </summary>
internal static class PaymentCacheKeys
{
    public static Task<PaymentRecord?> LoadPaymentRecordAsync(
        this IRequestEntityCache cache,
        IPaymentRecordRepository repository,
        Guid paymentRecordId,
        CancellationToken cancellationToken)
        => cache.GetOrLoadAsync(
            ("payments.record", paymentRecordId),
            ct => repository.GetByIdAsync(paymentRecordId, ct),
            cancellationToken);

    public static Task<ParentPaymentDeclaration?> LoadDeclarationAsync(
        this IRequestEntityCache cache,
        IParentPaymentDeclarationRepository repository,
        Guid declarationId,
        CancellationToken cancellationToken)
        => cache.GetOrLoadAsync(
            ("payments.declaration", declarationId),
            ct => repository.GetByIdAsync(declarationId, ct),
            cancellationToken);
}
