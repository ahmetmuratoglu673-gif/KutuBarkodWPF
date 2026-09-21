namespace KutuBarkodWPF.Models
{
    public class EtiketElementModel
    {
        // Hangi eleman olduğunu bilmemiz için benzersiz bir ID (Güncelleme ve Silme işlemleri için şart)
        public string Id { get; set; }

        // Ekranda yazan metin (Örn: "Mardin Derik Lisesi")
        public string Icerik { get; set; }

        // Bu eleman bir QR Kod mu yoksa normal metin mi?
        public bool QrKoduMu { get; set; }

        // Özellikler Menüsü (Alt Panel) için veriler
        public double X { get; set; }
        public double Y { get; set; }
        public int Punto { get; set; }
        public string FontAilesi { get; set; }
        public bool KalinMi { get; set; }
        public string Hizalama { get; set; } // "Sol", "Orta", "Sağ"
    }
}