/// M14: Rapor ekranlarının ortak durum kümesi.
///
/// [premiumRequired] bilinçli olarak [failure]'dan **ayrıdır**: sunucu `402`
/// (üyelik yetersiz) ya da `403` (paylaşım/yetki kapalı) döndüğünde ekran hata
/// balonu değil "kilitli" kartı gösterir — kullanıcı yapabileceği bir şey görür.
enum ReportingStatus { initial, loading, loaded, failure, premiumRequired }
