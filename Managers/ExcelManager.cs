using ClosedXML.Excel;
using KutuBarkodWPF.Models;
using System;
using System.Collections.Generic;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace KutuBarkodWPF.Managers
{
    public class ExcelManager
    {
        public int ToplamVeriSayisi { get; private set; } = 0;
        public string SecilenExcelYolu { get; private set; } = ""; 

        public List<ExcelSutunModel> ExceldenSutunlariGetir()
        {
            var sutunListesi = new List<ExcelSutunModel>();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Kutu Etiketleri İçin Excel Dosyanızı Seçin";
            openFileDialog.Filter = "Excel Dosyaları|*.xlsx;*.xls";

            if (openFileDialog.ShowDialog() != true) return null;

            try
            {
                SecilenExcelYolu = openFileDialog.FileName;
                using (var workbook = new XLWorkbook(SecilenExcelYolu))
                {
                    var worksheet = workbook.Worksheet(1);
                    var baslikSatiri = worksheet.Row(1).CellsUsed();
                    var ornekVeriSatiri = worksheet.Row(2);

                    int sonDoluSatir = worksheet.LastRowUsed().RowNumber();
                    ToplamVeriSayisi = sonDoluSatir > 1 ? sonDoluSatir - 1 : 0;

                    foreach (var hucre in baslikSatiri)
                    {
                        string sutunAdi = hucre.Value.ToString().Trim();
                        string ornekVeri = "";

                        if (ornekVeriSatiri != null)
                        {
                            var hucreVeri = ornekVeriSatiri.Cell(hucre.Address.ColumnNumber);
                            if (!hucreVeri.IsEmpty()) ornekVeri = hucreVeri.Value.ToString().Trim();
                        }

                        if (!string.IsNullOrEmpty(sutunAdi))
                        {
                            sutunListesi.Add(new ExcelSutunModel { AlanAdi = sutunAdi, OrnekVeri = ornekVeri, EtiketteMi = false, QrDaMi = false });
                        }
                    }
                }
                return sutunListesi;
            }
            catch (Exception ex)
            {
                throw new Exception("Excel okunurken hata oluştu!\nHata detayı: " + ex.Message);
            }
        }

        public List<Dictionary<string, string>> TumVerileriGetir()
        {
            var tumVeriler = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(SecilenExcelYolu)) return tumVeriler;

            using (var workbook = new XLWorkbook(SecilenExcelYolu))
            {
                var worksheet = workbook.Worksheet(1);
                var basliklar = new Dictionary<int, string>();

                foreach (var hucre in worksheet.Row(1).CellsUsed())
                {
                    basliklar[hucre.Address.ColumnNumber] = hucre.Value.ToString().Trim();
                }

                int sonSatir = worksheet.LastRowUsed().RowNumber();
                for (int i = 2; i <= sonSatir; i++)
                {
                    var satirVerisi = new Dictionary<string, string>();
                    foreach (var baslik in basliklar)
                    {
                        var hcr = worksheet.Cell(i, baslik.Key);
                        satirVerisi[baslik.Value] = hcr.IsEmpty() ? "" : hcr.Value.ToString().Trim();
                    }
                    tumVeriler.Add(satirVerisi);
                }
            }
            return tumVeriler;
        }
    }
}