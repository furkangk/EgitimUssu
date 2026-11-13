using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

/// <summary>
/// E-07: Ödeme kayıtlarındaki kişisel veriyi siler — <b>tutarları koruyarak</b>.
/// <para>
/// Bu modül, silme akışının en hassas yeridir: ödeme kayıtları ticari kayıttır ve yasal saklama
/// süresi boyunca (TTK m.82: 10 yıl) durmak zorundadır. Bu yüzden satırlar <b>silinmez</b>; yalnız
/// serbest metin alanları (açıklama, not) temizlenir, çünkü oraya kişi adı yazılmış olabilir.
/// Tutar, para birimi, vade ve tahsilat durumu olduğu gibi kalır.
/// (Dayanak: <c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §2.3.)
/// </para>
/// </summary>
public sealed class PaymentsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly PaymentsDbContext _dbContext;

    public PaymentsPersonalDataAnonymizer(PaymentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "Payments";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var records = await _dbContext.PaymentRecords
            .Where(record => record.TeacherUserId == subject.UserId || studentIds.Contains(record.StudentId))
            .ToArrayAsync(cancellationToken);

        var declarations = await _dbContext.ParentPaymentDeclarations
            .Where(declaration => declaration.ParentUserId == subject.UserId
                || declaration.TeacherUserId == subject.UserId
                || studentIds.Contains(declaration.StudentId))
            .ToArrayAsync(cancellationToken);

        var affected = records.Count(record => record.AnonymizePersonalData())
            + declarations.Count(declaration => declaration.AnonymizePersonalData());

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
