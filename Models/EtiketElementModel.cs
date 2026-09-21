namespace KutuBarkodWPF.Models
{
    public class EtiketElementModel
    {
        public string Id { get; set; }

        public string Icerik { get; set; }

        public bool QrKoduMu { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public int Punto { get; set; }
        public string FontAilesi { get; set; }
        public bool KalinMi { get; set; }
        public string Hizalama { get; set; } // "Sol", "Orta", "Sağ"
    }
}