namespace EgitimUssu.Modules.Study.Application;

/// <summary>
/// Hedef sınav türüne göre net ceza bölenini (yanlış / böleni) türeten saf yardımcı.
/// LGS → 3, TYT/AYT/YDS/diğer → 4, School → null (okul denemesi yanlış götürmez).
/// Study modülü Students'a referans vermez; hedef sınav istemci tarafından string olarak geçer.
/// </summary>
public static class ExamPenalty
{
    /// <summary>
    /// Verilen hedef sınav için net ceza bölenini döndürür. School için null (yanlış götürmez);
    /// null/None/bilinmeyen değerler için varsayılan 4.
    /// </summary>
    public static int? DivisorFor(string? targetExam)
    {
        if (string.IsNullOrWhiteSpace(targetExam))
        {
            return 4;
        }

        return targetExam.Trim().ToUpperInvariant() switch
        {
            "LGS" => 3,
            "SCHOOL" => null,
            _ => 4
        };
    }
}
