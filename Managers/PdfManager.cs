using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using QRCoder;

namespace KutuBarkodWPF.Managers
{
    public class WindowsFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            string fontKlasoru = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (faceName.Contains("Arial-Bold")) return File.ReadAllBytes(Path.Combine(fontKlasoru, "arialbd.ttf"));
            return File.ReadAllBytes(Path.Combine(fontKlasoru, "arial.ttf"));
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (familyName.Equals("Arial", StringComparison.CurrentCultureIgnoreCase))
            {
                if (isBold) return new FontResolverInfo("Arial-Bold");
                return new FontResolverInfo("Arial");
            }
            return new FontResolverInfo("Arial");
        }
    }

    public class SablonElemani
    {
        public string Tur { get; set; }
        public string Tag { get; set; }
        public string Icerik { get; set; }
        public string OrnekVeri { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Genislik { get; set; }
        public double Yukseklik { get; set; }
        public double Punto { get; set; }
        public bool KalinMi { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string Hizalama { get; set; }
    }

    public class SablonKayitModel
    {
        public List<SablonElemani> Elemanlar { get; set; }
        public List<string> QrSutunlari { get; set; }
        public string QrAyirici { get; set; }
    }

    public class PdfManager
    {
        public void PdfOlustur(string kayitYolu, List<Dictionary<string, string>> veriler, List<SablonElemani> sablon, List<string> qrSutunlari, string qrAyirici, bool sayfaNoOlsun)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            if (GlobalFontSettings.FontResolver == null) GlobalFontSettings.FontResolver = new WindowsFontResolver();

            PdfDocument document = new PdfDocument();
            document.Info.Title = "MEB Kutu Etiketleri";

            double a4Width = 595.28;
            double a4Height = 841.89;

            double mmToPt = 2.83465;

            // ETICA 3176 DIŞ FİZİKSEL KAĞIT ÖLÇÜLERİ
            double etiketEnMM = 99.1;
            double etiketBoyMM = 93.1;
            double yatayBoslukMM = 0.0;
            double dikeyBoslukMM = 0.0;

            double solKenarBosluguMM = 5.9;
            double ustKenarBosluguMM = 8.8;

            // --- YENİ İSTEK: İÇ GÜVENLİK BOŞLUĞU 3 MM (0.3 CM) OLARAK AYARLANDI ---
            double icBoslukMM = 3.0;

            double etiketEn = etiketEnMM * mmToPt;
            double etiketBoy = etiketBoyMM * mmToPt;
            double yatayBosluk = yatayBoslukMM * mmToPt;
            double dikeyBosluk = dikeyBoslukMM * mmToPt;
            double solKenar = solKenarBosluguMM * mmToPt;
            double ustKenar = ustKenarBosluguMM * mmToPt;
            double icBosluk = icBoslukMM * mmToPt;

            // Etiketin içindeki GÜVENLİ ÇİZİM ALANI (3 mm içeriden)
            double cizimEn = etiketEn - (icBosluk * 2);
            double cizimBoy = etiketBoy - (icBosluk * 2);

            // Ölçekleme Güvenli Alana Göre
            double scale = Math.Min(cizimEn / 380.0, cizimBoy / 360.0);

            double slackX = cizimEn - (380 * scale);
            double slackY = cizimBoy - (360 * scale);

            Dictionary<string, int> kurumKutuToplam = new Dictionary<string, int>();
            Dictionary<string, int> kurumKutuSayac = new Dictionary<string, int>();
            string kurumSutunu = veriler.Count > 0 ? veriler[0].Keys.FirstOrDefault(k => k.ToLower().Replace("_", " ").Contains("kurum kodu")) : null;

            if (kurumSutunu != null)
            {
                foreach (var row in veriler)
                {
                    string kk = row[kurumSutunu];
                    if (!kurumKutuToplam.ContainsKey(kk)) kurumKutuToplam[kk] = 0;
                    kurumKutuToplam[kk]++;
                    kurumKutuSayac[kk] = 0;
                }
            }

            PdfPage page = null;
            XGraphics gfx = null;

            for (int i = 0; i < veriler.Count; i++)
            {
                int indexInPage = i % 6;
                if (indexInPage == 0)
                {
                    page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);

                    if (sayfaNoOlsun)
                    {
                        int totalPages = (int)Math.Ceiling(veriler.Count / 6.0);
                        int currentPage = (i / 6) + 1;
                        XFont sfFont = new XFont("Arial", 8, XFontStyleEx.Bold);
                        gfx.DrawString($"Sayfa {currentPage:D2} / {totalPages:D2}", sfFont, XBrushes.Gray, new XRect(0, a4Height - 20, a4Width, 20), XStringFormats.Center);
                    }
                }

                int col = indexInPage % 2;
                int row = indexInPage / 2;

                // KOORDİNATLAR
                double cellX = solKenar + (col * (etiketEn + yatayBosluk));
                double cellY = ustKenar + (row * (etiketBoy + dikeyBosluk));

                gfx.Save();

                // ÇİZİM NOKTASI: Kesim yeri + İç Boşluk (3mm) eklendi!
                gfx.TranslateTransform(cellX + icBosluk - (10 * scale) + (slackX / 2.0), cellY + icBosluk - (10 * scale) + (slackY / 2.0));
                gfx.ScaleTransform(scale, scale);

                var satirVerisi = veriler[i];

                foreach (var eleman in sablon)
                {
                    XColor elemanRengi = XColor.FromArgb(eleman.R, eleman.G, eleman.B);

                    if (eleman.Tur == "KUTU")
                    {
                        XPen pen = new XPen(elemanRengi, 4);
                        gfx.DrawRectangle(pen, eleman.X, eleman.Y, eleman.Genislik, eleman.Yukseklik);
                    }
                    else if (eleman.Tur == "YAZI")
                    {
                        string metin = eleman.Icerik;
                        double xPos = eleman.X;
                        double yPos = eleman.Y;
                        double genislik = eleman.Genislik;

                        XParagraphAlignment hizalama = eleman.Hizalama == "Orta" ? XParagraphAlignment.Center : (eleman.Hizalama == "Sağ" ? XParagraphAlignment.Right : XParagraphAlignment.Left);

                        // KELİME KIRILMASINI (Aşağı kaymayı) ÖNLEYEN GÖRÜNMEZ ESNEME PAYI
                        double buffer = 20.0;
                        if (hizalama == XParagraphAlignment.Left)
                        {
                            genislik += buffer;
                        }
                        else if (hizalama == XParagraphAlignment.Right)
                        {
                            xPos -= buffer;
                            genislik += buffer;
                        }
                        else if (hizalama == XParagraphAlignment.Center)
                        {
                            xPos -= (buffer / 2);
                            genislik += buffer;
                        }

                        if (!string.IsNullOrEmpty(eleman.Tag) && satirVerisi.ContainsKey(eleman.Tag))
                        {
                            string deger = satirVerisi[eleman.Tag];

                            // KURUM KODUNU İSTİSNASIZ 8 HANEYE TAMAMLA (Alt tire ve büyük-küçük harf duyarsız)
                            if (eleman.Tag.ToLower().Replace("_", " ").Contains("kurum kodu"))
                            {
                                deger = deger.PadLeft(8, '0');
                            }

                            if (!string.IsNullOrEmpty(eleman.OrnekVeri) && metin.Contains(eleman.OrnekVeri))
                            {
                                int sonIndex = metin.LastIndexOf(eleman.OrnekVeri);
                                if (sonIndex != -1)
                                {
                                    metin = metin.Remove(sonIndex, eleman.OrnekVeri.Length).Insert(sonIndex, deger);
                                }
                            }
                            else
                            {
                                metin = deger;
                            }
                        }
                        else if (eleman.Tag == "ADET_SIRASI")
                        {
                            int hane = Math.Max(3, veriler.Count.ToString().Length);
                            metin = $"Etiket No: {(i + 1).ToString($"D{hane}")} / {veriler.Count.ToString($"D{hane}")}";
                        }
                        else if (eleman.Tag == "SABLON_KUTUNO" && kurumSutunu != null)
                        {
                            string kk = satirVerisi[kurumSutunu];
                            kurumKutuSayac[kk]++;
                            metin = $"KUTU NO\n{kurumKutuSayac[kk]:D2} / {kurumKutuToplam[kk]:D2}";
                        }
                        else if (eleman.Tag == "SAYFA_NO") continue;

                        XFont font = new XFont("Arial", eleman.Punto, eleman.KalinMi ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                        XBrush brush = new XSolidBrush(elemanRengi);

                        double guvenliYukseklik = eleman.Yukseklik == 0 ? 100 : eleman.Yukseklik + 5;
                        XRect rect = new XRect(xPos, yPos, genislik, guvenliYukseklik);

                        XTextFormatter tf = new XTextFormatter(gfx);
                        tf.Alignment = hizalama;
                        tf.DrawString(metin, font, brush, rect);
                    }
                    else if (eleman.Tur == "QR")
                    {
                        List<string> qrParcalari = new List<string>();
                        foreach (var qrSutunAdi in qrSutunlari)
                        {
                            string val = satirVerisi.ContainsKey(qrSutunAdi) ? satirVerisi[qrSutunAdi] : "";

                            // QR İÇİNDEKİ KURUM KODUNU DA 8 HANEYE TAMAMLA
                            if (qrSutunAdi.ToLower().Replace("_", " ").Contains("kurum kodu"))
                            {
                                val = val.PadLeft(8, '0');
                            }
                            qrParcalari.Add(val);
                        }
                        string qrMetni = string.Join(qrAyirici, qrParcalari);

                        if (!string.IsNullOrEmpty(qrMetni))
                        {
                            QRCodeGenerator qrGenerator = new QRCodeGenerator();
                            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrMetni, QRCodeGenerator.ECCLevel.Q);
                            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                            byte[] qrBytes = qrCode.GetGraphic(20);

                            string tempDosya = Path.Combine(Path.GetTempPath(), "qr_" + Guid.NewGuid().ToString() + ".jpg");

                            using (var ms = new MemoryStream(qrBytes))
                            {
                                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(ms, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                                encoder.Frames.Add(decoder.Frames[0]);
                                using (var fs = new FileStream(tempDosya, FileMode.Create))
                                {
                                    encoder.Save(fs);
                                }
                            }

                            using (XImage xImg = XImage.FromFile(tempDosya))
                            {
                                gfx.DrawImage(xImg, eleman.X, eleman.Y, eleman.Genislik, eleman.Yukseklik);
                            }

                            File.Delete(tempDosya);
                        }
                    }
                }
                gfx.Restore();
            }
            document.Save(kayitYolu);
        }
    }
}