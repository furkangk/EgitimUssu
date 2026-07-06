// M14: Integration testleri süreç-geneli ortam değişkenlerini (ConnectionStrings__Postgres vb.) değiştiriyor;
// paralel çalışma bu değişkenlerde yarışa yol açar. Bu yüzden bu assembly'de test paralelliği kapatılır.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
