using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KutuBarkodWPF.Managers;

using Application = System.Windows.Application;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Panel = System.Windows.Controls.Panel;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace KutuBarkodWPF
{
    public partial class MainWindow : Window
    {
        private ExcelManager _excelManager;
        private bool _isDragging = false;
        private Point _ilkFareNoktasi;
        private List<UIElement> _seciliElemanlar = new List<UIElement>();
        private Dictionary<UIElement, Point> _orijinalPozisyonlar = new Dictionary<UIElement, Point>();
        private Line _snapXLine = new Line { Stroke = Brushes.Red, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 2 }, Visibility = Visibility.Hidden };
        private Line _snapYLine = new Line { Stroke = Brushes.Red, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 2 }, Visibility = Visibility.Hidden };
        private bool _uiGuncelleniyor = false;
        private bool _sablonYukleniyor = false;

        public MainWindow()
        {
            InitializeComponent();
            _excelManager = new ExcelManager();
            EtiketCanvas.Children.Add(_snapXLine);
            EtiketCanvas.Children.Add(_snapYLine);
            Panel.SetZIndex(_snapXLine, 999);
            Panel.SetZIndex(_snapYLine, 999);

            SablonListesiniGuncelle();
            VarsayilanSablonuYukle();
        }

        private string SablonKlasoru => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sablonlar");

        private void SablonListesiniGuncelle()
        {
            if (!Directory.Exists(SablonKlasoru)) Directory.CreateDirectory(SablonKlasoru);

            cmbSablonlar.Items.Clear();
            cmbSablonlar.Items.Add(new ComboBoxItem { Content = "Şablon Seç...", IsSelected = true, Foreground = Brushes.Gray });

            string[] dosyalar = Directory.GetFiles(SablonKlasoru, "*.json");
            foreach (var dosya in dosyalar)
            {
                cmbSablonlar.Items.Add(new ComboBoxItem { Content = System.IO.Path.GetFileNameWithoutExtension(dosya) });
            }
        }

        private void SablonKaydet_Click(object sender, RoutedEventArgs e)
        {
            string isim = txtSablonAdi.Text.Trim();
            if (string.IsNullOrEmpty(isim))
            {
                MessageBox.Show("Lütfen şablona bir isim veriniz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SablonKayitModel kayit = new SablonKayitModel();
                kayit.Elemanlar = SablonuCikart();

                var kaynakSutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;
                if (kaynakSutunlar != null)
                {
                    kayit.QrSutunlari = kaynakSutunlar
                                          .Where(x => x.QrDaMi)
                                          .OrderBy(x => { int.TryParse(x.QrSira, out int sira); return sira == 0 ? 999 : sira; })
                                          .Select(x => x.AlanAdi).ToList();
                }
                kayit.QrAyirici = txtQrAyirici.Text;

                string jsonString = JsonSerializer.Serialize(kayit, new JsonSerializerOptions { WriteIndented = true });
                if (!Directory.Exists(SablonKlasoru)) Directory.CreateDirectory(SablonKlasoru);
                File.WriteAllText(System.IO.Path.Combine(SablonKlasoru, isim + ".json"), jsonString);

                MessageBox.Show("Şablon başarıyla kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                SablonListesiniGuncelle();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Şablon kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SablonSil_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSablonlar.SelectedItem is ComboBoxItem item && item.Content.ToString() != "Şablon Seç...")
            {
                string dosyaYolu = System.IO.Path.Combine(SablonKlasoru, item.Content.ToString() + ".json");
                if (File.Exists(dosyaYolu))
                {
                    MessageBoxResult result = MessageBox.Show($"'{item.Content}' şablonunu kalıcı olarak silmek istediğinize emin misiniz?", "Şablonu Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(dosyaYolu);
                            MessageBox.Show("Şablon başarıyla silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                            SablonListesiniGuncelle();
                            TuvaliTemizle_Click(null, null);
                        }
                        catch (Exception ex) { MessageBox.Show("Şablon silinirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
                    }
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek için listeden bir şablon seçiniz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void cmbSablonlar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSablonlar.SelectedItem is ComboBoxItem item && item.Content.ToString() != "Şablon Seç...")
            {
                string dosyaYolu = System.IO.Path.Combine(SablonKlasoru, item.Content.ToString() + ".json");
                if (File.Exists(dosyaYolu))
                {
                    try
                    {
                        _sablonYukleniyor = true;

                        string jsonString = File.ReadAllText(dosyaYolu);
                        var yuklenenSablon = JsonSerializer.Deserialize<SablonKayitModel>(jsonString);

                        TuvaliTemizle_Click(null, null);

                        txtQrAyirici.Text = yuklenenSablon.QrAyirici ?? "-";
                        var kaynakSutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;

                        foreach (var eleman in yuklenenSablon.Elemanlar)
                        {
                            SolidColorBrush firca = new SolidColorBrush(Color.FromRgb(eleman.R, eleman.G, eleman.B));

                            if (eleman.Tur == "KUTU")
                            {
                                foreach (UIElement child in EtiketCanvas.Children)
                                {
                                    if (child is Border brd && brd.Tag?.ToString() == "SABLON_CERCEVE")
                                        brd.BorderBrush = firca;
                                }
                            }
                            else if (eleman.Tur == "QR")
                            {
                                QrCodeEkle(eleman.X, eleman.Y, eleman.Genislik, eleman.Yukseklik);
                            }
                            else if (eleman.Tur == "YAZI")
                            {
                                FontWeight kalinlik = eleman.KalinMi ? FontWeights.Bold : FontWeights.Normal;
                                TuvaleYaziEkle(eleman.Icerik, eleman.X, eleman.Y, (int)eleman.Punto, kalinlik, eleman.Hizalama, firca, eleman.Tag);

                                var sonEklenen = EtiketCanvas.Children[EtiketCanvas.Children.Count - 1] as Grid;
                                if (sonEklenen != null)
                                {
                                    sonEklenen.Width = eleman.Genislik;
                                    if (sonEklenen.Children[0] is Border b && b.Child is TextBlock txt) txt.MaxWidth = eleman.Genislik;
                                }
                            }
                        }

                        if (yuklenenSablon.QrSutunlari != null && kaynakSutunlar != null)
                        {
                            foreach (var colName in yuklenenSablon.QrSutunlari)
                            {
                                var sutun = kaynakSutunlar.FirstOrDefault(x => x.AlanAdi == colName);
                                if (sutun != null)
                                {
                                    sutun.QrDaMi = true;
                                    sutun.QrSira = (yuklenenSablon.QrSutunlari.IndexOf(colName) + 1).ToString();
                                }
                            }
                        }

                        foreach (var eleman in yuklenenSablon.Elemanlar)
                        {
                            if (eleman.Tur == "YAZI" && !string.IsNullOrEmpty(eleman.Tag))
                            {
                                if (kaynakSutunlar != null)
                                {
                                    var sutun = kaynakSutunlar.FirstOrDefault(x => x.AlanAdi == eleman.Tag);
                                    if (sutun != null) sutun.EtiketteMi = true;
                                }

                                if (eleman.Tag == "SAYFA_NO") chkSayfaNo.IsChecked = true;
                                if (eleman.Tag == "ADET_SIRASI") chkAdetSirasi.IsChecked = true;
                            }
                        }

                        _sablonYukleniyor = false;
                    }
                    catch { MessageBox.Show("Şablon yüklenirken dosya okuma hatası oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); _sablonYukleniyor = false; }
                }
            }
        }

        private void YeniMetinEkle_Click(object sender, RoutedEventArgs e)
        {
            TuvaleYaziEkle("Yeni Metin", 150, 150, 14, FontWeights.Bold, "Sol", Brushes.Black, "SERBEST_METIN");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;

            if (e.Key == Key.Delete)
            {
                SeciliSil_Click(null, null);
            }
            else if (_seciliElemanlar.Count > 0)
            {
                double hiz = Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1;
                bool hareketEtti = false;

                foreach (var eleman in _seciliElemanlar)
                {
                    double x = Canvas.GetLeft(eleman);
                    double y = Canvas.GetTop(eleman);

                    if (e.Key == Key.Left) { Canvas.SetLeft(eleman, x - hiz); hareketEtti = true; }
                    else if (e.Key == Key.Right) { Canvas.SetLeft(eleman, x + hiz); hareketEtti = true; }
                    else if (e.Key == Key.Up) { Canvas.SetTop(eleman, y - hiz); hareketEtti = true; }
                    else if (e.Key == Key.Down) { Canvas.SetTop(eleman, y + hiz); hareketEtti = true; }
                }

                if (hareketEtti)
                {
                    e.Handled = true;
                    AltPaneliGuncelle();
                }
            }
        }

        private void RenkSec_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SolidColorBrush yeniRenk = new SolidColorBrush(Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                foreach (UIElement child in EtiketCanvas.Children)
                {
                    if (child is Border brd && brd.Tag?.ToString() == "SABLON_CERCEVE") brd.BorderBrush = yeniRenk;
                }
            }
        }

        private void MetinRenkSec_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliElemanlar.Count == 0) return;
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SolidColorBrush yeniRenk = new SolidColorBrush(Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                foreach (var eleman in _seciliElemanlar)
                {
                    if (eleman is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border srm && srm.Child is TextBlock txt)
                        txt.Foreground = yeniRenk;
                }
            }
        }

        private void btnSayfadaOrtala_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliElemanlar.Count == 1)
            {
                var eleman = _seciliElemanlar[0] as FrameworkElement;
                if (eleman != null)
                {
                    double genislik = double.IsNaN(eleman.Width) ? eleman.ActualWidth : eleman.Width;
                    double yeniX = 200 - (genislik / 2);
                    Canvas.SetLeft(eleman, yeniX);
                    AltPaneliGuncelle();
                }
            }
            else
            {
                MessageBox.Show("Sayfada ortalamak için lütfen tuvalden tek bir nesne seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GercekQrUret(string icerik)
        {
            try
            {
                Image qrResmi = null;
                foreach (UIElement child in EtiketCanvas.Children)
                {
                    if (child is Grid grid && grid.Tag?.ToString() == "QR_KOD")
                    {
                        foreach (var gc in grid.Children)
                        {
                            if (gc is Image img) { qrResmi = img; break; }
                        }
                    }
                }

                if (qrResmi == null) return;
                if (string.IsNullOrEmpty(icerik) || icerik == "Henüz QR için veri seçilmedi...")
                {
                    qrResmi.Source = null;
                    return;
                }

                QRCoder.QRCodeGenerator qrGenerator = new QRCoder.QRCodeGenerator();
                QRCoder.QRCodeData qrCodeData = qrGenerator.CreateQrCode(icerik, QRCoder.QRCodeGenerator.ECCLevel.Q);
                QRCoder.PngByteQRCode qrCode = new QRCoder.PngByteQRCode(qrCodeData);
                byte[] qrBytes = qrCode.GetGraphic(20);

                using (var ms = new System.IO.MemoryStream(qrBytes))
                {
                    ms.Position = 0;
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    qrResmi.Source = bmp;
                }
            }
            catch { }
        }

        private void QrOnizlemeGuncelle()
        {
            if (txtQrOnizleme == null || txtQrAyirici == null || lstEtiketAlanlari.Items.Count == 0) return;

            string ayirici = txtQrAyirici.Text;
            var seciliQrListesi = lstEtiketAlanlari.Items.Cast<Models.ExcelSutunModel>()
                                    .Where(x => x.QrDaMi)
                                    .OrderBy(x => { int.TryParse(x.QrSira, out int sira); return sira == 0 ? 999 : sira; })
                                    .Select(x =>
                                    {
                                        string deger = string.IsNullOrEmpty(x.OrnekVeri) ? "VeriYok" : x.OrnekVeri;
                                        // ÇÖZÜM: QR EKRANINDA 8 HANEYE TAMAMLA
                                        if (x.AlanAdi.ToLower().Replace("_", " ").Contains("kurum kodu") && deger != "VeriYok")
                                            deger = deger.PadLeft(8, '0');
                                        return deger;
                                    }).ToList();

            if (seciliQrListesi.Count > 0)
            {
                string sonuc = string.Join(ayirici, seciliQrListesi);
                txtQrOnizleme.Text = sonuc;
                GercekQrUret(sonuc);
            }
            else
            {
                txtQrOnizleme.Text = "Henüz QR için veri seçilmedi...";
                GercekQrUret(null);
            }
        }

        private void txtQrAyirici_TextChanged(object sender, TextChangedEventArgs e) { QrOnizlemeGuncelle(); }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double zoomKatsayisi = e.Delta > 0 ? 1.1 : 0.9;
                if (canvasScale.ScaleX * zoomKatsayisi > 0.5 && canvasScale.ScaleX * zoomKatsayisi < 3.0)
                {
                    canvasScale.ScaleX *= zoomKatsayisi;
                    canvasScale.ScaleY *= zoomKatsayisi;
                }
                e.Handled = true;
            }
        }

        private void ExcelSec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sutunlar = _excelManager.ExceldenSutunlariGetir();
                if (sutunlar != null && sutunlar.Count > 0)
                {
                    lstEtiketAlanlari.ItemsSource = sutunlar;
                    MessageBox.Show($"{_excelManager.ToplamVeriSayisi} adet satır veri başarıyla yüklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TuvaliTemizle_Click(object sender, RoutedEventArgs e)
        {
            var silinecekler = new List<UIElement>();
            bool cerceveVarMi = false;

            foreach (UIElement child in EtiketCanvas.Children)
            {
                if (child is Line) continue;
                if (child is Border brd && brd.Tag?.ToString() == "SABLON_CERCEVE")
                {
                    cerceveVarMi = true;
                    continue;
                }
                silinecekler.Add(child);
            }
            foreach (var child in silinecekler) EtiketCanvas.Children.Remove(child);
            _seciliElemanlar.Clear();

            var sutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;
            if (sutunlar != null)
            {
                foreach (var sutun in sutunlar)
                {
                    sutun.EtiketteMi = false;
                    sutun.QrDaMi = false;
                    sutun.QrSira = "";
                }
            }

            txtQrOnizleme.Text = "Henüz QR için veri seçilmedi...";
            chkAdetSirasi.IsChecked = false;
            chkSayfaNo.IsChecked = false;

            if (!cerceveVarMi)
            {
                Border kirmiziCerceve = new Border { Width = 380, Height = 360, BorderBrush = Brushes.Red, BorderThickness = new Thickness(4), Background = Brushes.Transparent, Tag = "SABLON_CERCEVE" };
                Canvas.SetLeft(kirmiziCerceve, 10); Canvas.SetTop(kirmiziCerceve, 10);
                EtiketCanvas.Children.Add(kirmiziCerceve);
            }

            AltPaneliGuncelle();
        }

        private void chkSayfaNo_Checked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            if (_excelManager.ToplamVeriSayisi == 0)
            {
                MessageBox.Show("Lütfen önce Excel verisi yükleyiniz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                chkSayfaNo.IsChecked = false;
                return;
            }
        }
        private void chkSayfaNo_Unchecked(object sender, RoutedEventArgs e) { }

        private void chkAdetSirasi_Checked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            if (_excelManager.ToplamVeriSayisi == 0)
            {
                MessageBox.Show("Lütfen önce Excel verisi yükleyiniz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                chkAdetSirasi.IsChecked = false;
                return;
            }

            int hane = _excelManager.ToplamVeriSayisi.ToString().Length;
            if (hane < 3) hane = 3;
            string totalStr = _excelManager.ToplamVeriSayisi.ToString($"D{hane}");
            string ilkStr = 1.ToString($"D{hane}");

            string metin = $"Etiket No: {ilkStr} / {totalStr}";
            TuvaleYaziEkle(metin, 180, 20, 12, FontWeights.Bold, "Sağ", Brushes.Gray, "ADET_SIRASI");
        }
        private void chkAdetSirasi_Unchecked(object sender, RoutedEventArgs e) { ObjeSil("ADET_SIRASI"); }

        private void ObjeSil(string tagAdi)
        {
            UIElement silinecek = null;
            foreach (UIElement eleman in EtiketCanvas.Children)
            {
                if (eleman is FrameworkElement fe && fe.Tag?.ToString() == tagAdi)
                {
                    silinecek = eleman; break;
                }
            }
            if (silinecek != null)
            {
                EtiketCanvas.Children.Remove(silinecek);
                if (_seciliElemanlar.Contains(silinecek)) _seciliElemanlar.Remove(silinecek);
                AltPaneliGuncelle();
            }
        }

        private void QrOlsun_Checked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            var checkBox = sender as CheckBox;
            var secilenSutun = checkBox.DataContext as KutuBarkodWPF.Models.ExcelSutunModel;

            if (secilenSutun != null && string.IsNullOrEmpty(secilenSutun.QrSira))
            {
                int maxSira = 0;
                var sutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;
                if (sutunlar != null)
                {
                    foreach (var sutun in sutunlar)
                    {
                        if (sutun != secilenSutun && int.TryParse(sutun.QrSira, out int s) && s > maxSira) maxSira = s;
                    }
                }
                secilenSutun.QrSira = (maxSira + 1).ToString();
            }

            bool qrVarMi = false;
            foreach (UIElement child in EtiketCanvas.Children)
            {
                if (child is Grid g && g.Tag?.ToString() == "QR_KOD") { qrVarMi = true; break; }
            }

            if (!qrVarMi) QrCodeEkle(150, 110, 90, 90);
            QrOnizlemeGuncelle();
        }

        private void QrOlsun_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            var checkBox = sender as CheckBox;
            var secilenSutun = checkBox.DataContext as KutuBarkodWPF.Models.ExcelSutunModel;

            if (secilenSutun != null) secilenSutun.QrSira = "";

            bool hicQrKaldimi = false;
            var sutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;
            if (sutunlar != null)
            {
                foreach (var sutun in sutunlar)
                {
                    if (sutun.QrDaMi) { hicQrKaldimi = true; break; }
                }
            }

            if (!hicQrKaldimi) ObjeSil("QR_KOD");
            QrOnizlemeGuncelle();
        }

        private void QrSira_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || string.IsNullOrWhiteSpace(textBox.Text)) { QrOnizlemeGuncelle(); return; }

            var sutunModel = textBox.DataContext as KutuBarkodWPF.Models.ExcelSutunModel;
            var sutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;
            int toplamSutunSayisi = sutunlar != null ? sutunlar.Count : 0;

            if (int.TryParse(textBox.Text, out int girilenDeger))
            {
                if (girilenDeger > toplamSutunSayisi)
                {
                    MessageBox.Show($"En fazla veri sayınız kadar ({toplamSutunSayisi}) sıra numarası girebilirsiniz!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox.Text = "";
                    return;
                }

                if (sutunlar != null)
                {
                    foreach (var sutun in sutunlar)
                    {
                        if (sutun != sutunModel && sutun.QrSira == textBox.Text)
                        {
                            MessageBox.Show($"Bu sıra numarası ({textBox.Text}) zaten başka bir veri için kullanılıyor!", "Çakışma", MessageBoxButton.OK, MessageBoxImage.Warning);
                            textBox.Text = "";
                            return;
                        }
                    }
                }
            }
            else { textBox.Text = ""; return; }
            QrOnizlemeGuncelle();
        }

        private void EtiketteOlsun_Checked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            var checkBox = sender as CheckBox;
            var secilenSutun = checkBox.DataContext as KutuBarkodWPF.Models.ExcelSutunModel;

            if (secilenSutun != null)
            {
                string gercekDeger = string.IsNullOrEmpty(secilenSutun.OrnekVeri) ? "Veri Yok" : secilenSutun.OrnekVeri;

                // ÇÖZÜM: TUVALE EKLERKEN 8 HANEYE TAMAMLA
                if (secilenSutun.AlanAdi.ToLower().Replace("_", " ").Contains("kurum kodu") && gercekDeger != "Veri Yok")
                    gercekDeger = gercekDeger.PadLeft(8, '0');

                string metin = secilenSutun.AlanAdi + " : " + gercekDeger;
                TuvaleYaziEkle(metin, 50, 50, 14, FontWeights.Bold, "Sol", Brushes.Black, secilenSutun.AlanAdi);
            }
        }

        private void EtiketteOlsun_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_sablonYukleniyor) return;
            var checkBox = sender as CheckBox;
            var secilenSutun = checkBox.DataContext as KutuBarkodWPF.Models.ExcelSutunModel;
            if (secilenSutun != null) ObjeSil(secilenSutun.AlanAdi);
        }

        private void SeciliSil_Click(object sender, RoutedEventArgs e)
        {
            var sutunlar = lstEtiketAlanlari.ItemsSource as List<Models.ExcelSutunModel>;

            foreach (var eleman in _seciliElemanlar.ToList())
            {
                if (eleman is FrameworkElement fe && fe.Tag?.ToString() == "SABLON_CERCEVE") continue;
                if (eleman is Line) continue;

                string tag = (eleman as FrameworkElement)?.Tag?.ToString();

                if (tag == "ADET_SIRASI") chkAdetSirasi.IsChecked = false;
                else if (tag == "SAYFA_NO") chkSayfaNo.IsChecked = false;
                else if (tag == "QR_KOD")
                {
                    if (sutunlar != null)
                    {
                        foreach (var sutun in sutunlar)
                        {
                            sutun.QrDaMi = false;
                            sutun.QrSira = "";
                        }
                    }
                    txtQrOnizleme.Text = "Henüz QR için veri seçilmedi...";
                }
                else
                {
                    if (sutunlar != null)
                    {
                        var sutun = sutunlar.FirstOrDefault(x => x.AlanAdi == tag);
                        if (sutun != null) sutun.EtiketteMi = false;
                    }
                }

                EtiketCanvas.Children.Remove(eleman);
                _seciliElemanlar.Remove(eleman);
            }
            _seciliElemanlar.Clear();
            AltPaneliGuncelle();
        }

        private void QrCodeEkle(double x, double y, double w, double h)
        {
            Grid wrapper = new Grid { Width = w, Height = h, Background = Brushes.White, Tag = "QR_KOD" };
            Image imgQr = new Image { Name = "imgQrCodeBox", Stretch = Stretch.Uniform, Margin = new Thickness(5) };
            wrapper.Children.Add(imgQr);

            Thumb brThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNWSE, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            brThumb.DragDelta += (s, ev) => { double newW = Math.Max(20, wrapper.ActualWidth + ev.HorizontalChange); double newH = Math.Max(20, wrapper.ActualHeight + ev.VerticalChange); wrapper.Width = newW; wrapper.Height = newH; AltPaneliGuncelle(); }; wrapper.Children.Add(brThumb);

            Thumb blThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNESW, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            blThumb.DragDelta += (s, ev) => { double newW = Math.Max(20, wrapper.ActualWidth - ev.HorizontalChange); double newH = Math.Max(20, wrapper.ActualHeight + ev.VerticalChange); wrapper.Width = newW; wrapper.Height = newH; Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + ev.HorizontalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(blThumb);

            Thumb trThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNESW, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            trThumb.DragDelta += (s, ev) => { double newW = Math.Max(20, wrapper.ActualWidth + ev.HorizontalChange); double newH = Math.Max(20, wrapper.ActualHeight - ev.VerticalChange); wrapper.Width = newW; wrapper.Height = newH; Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + ev.VerticalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(trThumb);

            Thumb tlThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNWSE, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            tlThumb.DragDelta += (s, ev) => { double newW = Math.Max(20, wrapper.ActualWidth - ev.HorizontalChange); double newH = Math.Max(20, wrapper.ActualHeight - ev.VerticalChange); wrapper.Width = newW; wrapper.Height = newH; Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + ev.HorizontalChange); Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + ev.VerticalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(tlThumb);

            Canvas.SetLeft(wrapper, x); Canvas.SetTop(wrapper, y);
            wrapper.Cursor = Cursors.Hand; wrapper.MouseLeftButtonDown += Element_MouseLeftButtonDown; wrapper.MouseMove += Element_MouseMove; wrapper.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            EtiketCanvas.Children.Add(wrapper);
            QrOnizlemeGuncelle();
        }

        private void TuvaleYaziEkle(string icerik, double x, double y, int punto, FontWeight kalinlik, string hiza, Brush renk = null, string etiketId = "")
        {
            Grid wrapper = new Grid { Tag = etiketId };
            Border sarmalayici = new Border { Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(2), Padding = new Thickness(2) };
            TextBlock yeniYazi = new TextBlock { Text = icerik, FontSize = punto, FontWeight = kalinlik, Foreground = renk ?? Brushes.Black, TextAlignment = hiza == "Orta" ? TextAlignment.Center : (hiza == "Sağ" ? TextAlignment.Right : TextAlignment.Left), TextWrapping = TextWrapping.Wrap, MaxWidth = 350 };

            sarmalayici.Child = yeniYazi; wrapper.Children.Add(sarmalayici);

            Thumb brThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNWSE, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            brThumb.DragDelta += (s, e) => { double newW = Math.Max(20, wrapper.ActualWidth + e.HorizontalChange); wrapper.Width = newW; yeniYazi.Width = newW; yeniYazi.MaxWidth = newW; AltPaneliGuncelle(); }; wrapper.Children.Add(brThumb);

            Thumb blThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNESW, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            blThumb.DragDelta += (s, e) => { double newW = Math.Max(20, wrapper.ActualWidth - e.HorizontalChange); wrapper.Width = newW; yeniYazi.Width = newW; yeniYazi.MaxWidth = newW; Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + e.HorizontalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(blThumb);

            Thumb trThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNESW, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            trThumb.DragDelta += (s, e) => { double newW = Math.Max(20, wrapper.ActualWidth + e.HorizontalChange); wrapper.Width = newW; yeniYazi.Width = newW; yeniYazi.MaxWidth = newW; Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + e.VerticalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(trThumb);

            Thumb tlThumb = new Thumb { Width = 8, Height = 8, Cursor = Cursors.SizeNWSE, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Hidden, Style = (Style)FindResource("ResizeThumbStyle"), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)), BorderThickness = new Thickness(1.5) };
            tlThumb.DragDelta += (s, e) => { double newW = Math.Max(20, wrapper.ActualWidth - e.HorizontalChange); wrapper.Width = newW; yeniYazi.Width = newW; yeniYazi.MaxWidth = newW; Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + e.HorizontalChange); Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + e.VerticalChange); AltPaneliGuncelle(); }; wrapper.Children.Add(tlThumb);

            Canvas.SetLeft(wrapper, x); Canvas.SetTop(wrapper, y);
            wrapper.Cursor = Cursors.Hand; wrapper.MouseLeftButtonDown += Element_MouseLeftButtonDown; wrapper.MouseMove += Element_MouseMove; wrapper.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            EtiketCanvas.Children.Add(wrapper);
        }

        private void SecimleriTemizle()
        {
            foreach (var eleman in _seciliElemanlar)
            {
                if (eleman is Grid wrapper && wrapper.Tag?.ToString() != "SABLON_CERCEVE")
                {
                    if (wrapper.Tag?.ToString() == "QR_KOD") wrapper.Opacity = 1.0;
                    else
                    {
                        if (wrapper.Children[0] is Border brd)
                        {
                            brd.Background = Brushes.Transparent;
                            brd.BorderBrush = Brushes.Transparent;
                        }
                        foreach (var child in wrapper.Children)
                        {
                            if (child is Thumb t) t.Visibility = Visibility.Hidden;
                        }
                    }
                }
            }
            _seciliElemanlar.Clear();
            AltPaneliGuncelle();
        }

        private void ElemanSec(UIElement eleman)
        {
            if (!_seciliElemanlar.Contains(eleman))
            {
                _seciliElemanlar.Add(eleman);

                if (eleman is Grid wrapper && wrapper.Tag?.ToString() != "SABLON_CERCEVE")
                {
                    if (wrapper.Tag?.ToString() == "QR_KOD") wrapper.Opacity = 0.6;
                    else
                    {
                        if (wrapper.Children[0] is Border brd)
                        {
                            brd.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                            brd.Background = new SolidColorBrush(Color.FromArgb(38, 59, 130, 246));
                        }
                        foreach (var child in wrapper.Children)
                        {
                            if (child is Thumb t) t.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            AltPaneliGuncelle();
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { SecimleriTemizle(); }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tiklananEleman = sender as UIElement;
            if (Keyboard.Modifiers != ModifierKeys.Control && !_seciliElemanlar.Contains(tiklananEleman))
                SecimleriTemizle();

            ElemanSec(tiklananEleman);
            _isDragging = true;
            _ilkFareNoktasi = e.GetPosition(EtiketCanvas);

            _orijinalPozisyonlar.Clear();
            foreach (var eleman in _seciliElemanlar)
                _orijinalPozisyonlar[eleman] = new Point(Canvas.GetLeft(eleman), Canvas.GetTop(eleman));

            tiklananEleman.CaptureMouse();
            e.Handled = true;
        }

        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _seciliElemanlar.Count > 0)
            {
                Point currentMouse = e.GetPosition(EtiketCanvas);
                double deltaX = currentMouse.X - _ilkFareNoktasi.X;
                double deltaY = currentMouse.Y - _ilkFareNoktasi.Y;

                bool hizalandiX = false;
                bool hizalandiY = false;
                double hizalamaFarki = 6;

                foreach (var eleman in _seciliElemanlar)
                {
                    double yeniX = _orijinalPozisyonlar[eleman].X + deltaX;
                    double yeniY = _orijinalPozisyonlar[eleman].Y + deltaY;

                    if (_seciliElemanlar.Count == 1)
                    {
                        foreach (UIElement digerEleman in EtiketCanvas.Children)
                        {
                            if (digerEleman == eleman || digerEleman is Line || (digerEleman as FrameworkElement)?.Tag?.ToString() == "SABLON_CERCEVE") continue;

                            double digerX = Canvas.GetLeft(digerEleman);
                            double digerY = Canvas.GetTop(digerEleman);

                            if (Math.Abs(yeniX - digerX) < hizalamaFarki)
                            {
                                yeniX = digerX;
                                _snapYLine.X1 = yeniX; _snapYLine.X2 = yeniX;
                                _snapYLine.Y1 = 0; _snapYLine.Y2 = 380;
                                _snapYLine.Visibility = Visibility.Visible;
                                hizalandiX = true;
                            }

                            if (Math.Abs(yeniY - digerY) < hizalamaFarki)
                            {
                                yeniY = digerY;
                                _snapXLine.Y1 = yeniY; _snapXLine.Y2 = yeniY;
                                _snapXLine.X1 = 0; _snapXLine.X2 = 400;
                                _snapXLine.Visibility = Visibility.Visible;
                                hizalandiY = true;
                            }
                        }
                    }

                    Canvas.SetLeft(eleman, yeniX);
                    Canvas.SetTop(eleman, yeniY);
                }

                if (!hizalandiX) _snapYLine.Visibility = Visibility.Hidden;
                if (!hizalandiY) _snapXLine.Visibility = Visibility.Hidden;

                AltPaneliGuncelle();
            }
        }

        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                (sender as UIElement)?.ReleaseMouseCapture();
                _snapXLine.Visibility = Visibility.Hidden;
                _snapYLine.Visibility = Visibility.Hidden;
            }
        }

        private void AltPaneliGuncelle()
        {
            if (_seciliElemanlar.Count == 1)
            {
                _uiGuncelleniyor = true;

                var eleman = _seciliElemanlar[0] as FrameworkElement;
                txtX.Text = Math.Round(Canvas.GetLeft(eleman)).ToString();
                txtY.Text = Math.Round(Canvas.GetTop(eleman)).ToString();
                txtGenislik.Text = double.IsNaN(eleman.Width) ? "Auto" : Math.Round(eleman.Width).ToString();
                txtYukseklik.Text = double.IsNaN(eleman.Height) ? "Auto" : Math.Round(eleman.Height).ToString();

                if (eleman is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border brd && brd.Child is TextBlock txt)
                {
                    txtPunto.Text = txt.FontSize.ToString();
                    chkBold.IsChecked = txt.FontWeight == FontWeights.Bold;
                    chkMetniOrtala.IsChecked = txt.TextAlignment == TextAlignment.Center;
                    txtIcerik.Text = txt.Text;
                    txtIcerik.IsEnabled = true;
                }
                else
                {
                    txtIcerik.Text = "";
                    chkMetniOrtala.IsChecked = false;
                    txtIcerik.IsEnabled = false;
                }

                _uiGuncelleniyor = false;
            }
            else
            {
                _uiGuncelleniyor = true;
                txtX.Text = ""; txtY.Text = ""; txtPunto.Text = ""; txtGenislik.Text = ""; txtYukseklik.Text = ""; chkBold.IsChecked = false; chkMetniOrtala.IsChecked = false;
                txtIcerik.Text = ""; txtIcerik.IsEnabled = false;
                _uiGuncelleniyor = false;
            }
        }

        private void txtIcerik_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_uiGuncelleniyor || _seciliElemanlar.Count != 1) return;
            if (_seciliElemanlar[0] is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border brd && brd.Child is TextBlock txt)
            {
                txt.Text = txtIcerik.Text;
            }
        }

        private void Prop_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_uiGuncelleniyor || _seciliElemanlar.Count != 1) return;
            var eleman = _seciliElemanlar[0] as FrameworkElement;
            if (eleman == null) return;

            if (double.TryParse(txtX.Text, out double xVal)) Canvas.SetLeft(eleman, xVal);
            if (double.TryParse(txtY.Text, out double yVal)) Canvas.SetTop(eleman, yVal);

            if (double.TryParse(txtGenislik.Text, out double wVal))
            {
                eleman.Width = wVal;
                if (eleman is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border brd && brd.Child is TextBlock txt)
                {
                    txt.MaxWidth = wVal;
                    txt.Width = wVal;
                }
            }
            else eleman.Width = double.NaN;

            if (double.TryParse(txtYukseklik.Text, out double hVal)) eleman.Height = hVal;
            else eleman.Height = double.NaN;

            if (eleman is Grid wrp && wrp.Children.Count > 0 && wrp.Children[0] is Border b && b.Child is TextBlock t)
            {
                if (double.TryParse(txtPunto.Text, out double pt)) t.FontSize = pt;
            }
        }

        private void chkBold_Changed(object sender, RoutedEventArgs e)
        {
            if (_uiGuncelleniyor || _seciliElemanlar.Count != 1) return;
            if (_seciliElemanlar[0] is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border brd && brd.Child is TextBlock txt)
            {
                txt.FontWeight = chkBold.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private void chkMetniOrtala_Changed(object sender, RoutedEventArgs e)
        {
            if (_uiGuncelleniyor || _seciliElemanlar.Count != 1) return;
            if (_seciliElemanlar[0] is Grid wrapper && wrapper.Children.Count > 0 && wrapper.Children[0] is Border brd && brd.Child is TextBlock txt)
            {
                txt.TextAlignment = chkMetniOrtala.IsChecked == true ? TextAlignment.Center : TextAlignment.Left;
            }
        }

        private void VarsayilanSablonuYukle()
        {
            TuvaliTemizle_Click(null, null);
        }

        private void PdfOlustur_Click(object sender, RoutedEventArgs e)
        {
            if (_excelManager == null || _excelManager.ToplamVeriSayisi == 0)
            {
                MessageBox.Show("Lütfen önce Excel verisi yükleyiniz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Title = "PDF Olarak Kaydet";
            saveDialog.Filter = "PDF Dosyaları (*.pdf)|*.pdf";
            saveDialog.FileName = "MEB_Etiket_Ciktisi_" + DateTime.Now.ToString("ddMMyyyy") + ".pdf";

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    List<SablonElemani> sablon = SablonuCikart();
                    var tumVeriler = _excelManager.TumVerileriGetir();

                    var qrSutunlari = lstEtiketAlanlari.Items.Cast<Models.ExcelSutunModel>()
                                        .Where(x => x.QrDaMi)
                                        .OrderBy(x => { int.TryParse(x.QrSira, out int sira); return sira == 0 ? 999 : sira; })
                                        .Select(x => x.AlanAdi).ToList();

                    PdfManager pdfMotoru = new PdfManager();
                    pdfMotoru.PdfOlustur(saveDialog.FileName, tumVeriler, sablon, qrSutunlari, txtQrAyirici.Text, chkSayfaNo.IsChecked == true);

                    MessageBox.Show("PDF Başarıyla Oluşturuldu ve Kaydedildi!\n\nDosya: " + saveDialog.FileName, "İşlem Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PDF oluşturulurken bir hata meydana geldi.\nDosya açık kalmış olabilir.\nHata: " + ex.Message, "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ÇÖZÜM: Şablon kaydederken ve oluştururken OrnekVeri'nin Kurum Kodu 8 Hane zekası
        private List<SablonElemani> SablonuCikart()
        {
            var sablon = new List<SablonElemani>();

            foreach (UIElement eleman in EtiketCanvas.Children)
            {
                if (eleman is Line) continue;

                if (eleman is Border brd && brd.Tag?.ToString() == "SABLON_CERCEVE")
                {
                    SolidColorBrush firca = brd.BorderBrush as SolidColorBrush ?? Brushes.Red;
                    sablon.Add(new SablonElemani
                    {
                        Tur = "KUTU",
                        X = Canvas.GetLeft(brd),
                        Y = Canvas.GetTop(brd),
                        Genislik = brd.Width,
                        Yukseklik = brd.Height,
                        R = firca.Color.R,
                        G = firca.Color.G,
                        B = firca.Color.B
                    });
                }
                else if (eleman is Grid grid)
                {
                    string tag = grid.Tag?.ToString();
                    double x = Canvas.GetLeft(grid);
                    double y = Canvas.GetTop(grid);
                    double width = double.IsNaN(grid.Width) ? grid.ActualWidth : grid.Width;

                    if (tag == "QR_KOD")
                    {
                        sablon.Add(new SablonElemani { Tur = "QR", Tag = tag, X = x, Y = y, Genislik = grid.Width, Yukseklik = grid.Height });
                    }
                    else if (grid.Children.Count > 0 && grid.Children[0] is Border b && b.Child is TextBlock txt)
                    {
                        string ornekVeri = "";
                        if (tag != "QR_KOD" && tag != "SABLON_CERCEVE" && tag != "ADET_SIRASI" && tag != "SABLON_KUTUNO" && tag != "SERBEST_METIN")
                        {
                            var sutun = lstEtiketAlanlari.Items.Cast<Models.ExcelSutunModel>().FirstOrDefault(s => s.AlanAdi == tag);
                            if (sutun != null)
                            {
                                ornekVeri = string.IsNullOrEmpty(sutun.OrnekVeri) ? "Veri Yok" : sutun.OrnekVeri;
                                // ÇÖZÜM: KURUM KODU 8 HANE (ALT TİREYİ BOŞLUĞA ÇEVİREREK)
                                if (tag.ToLower().Replace("_", " ").Contains("kurum kodu") && ornekVeri != "Veri Yok")
                                    ornekVeri = ornekVeri.PadLeft(8, '0');
                            }
                        }

                        SolidColorBrush firca = txt.Foreground as SolidColorBrush ?? Brushes.Black;
                        sablon.Add(new SablonElemani
                        {
                            Tur = "YAZI",
                            Tag = tag,
                            Icerik = txt.Text,
                            OrnekVeri = ornekVeri,
                            X = x,
                            Y = y,
                            Genislik = width,
                            Yukseklik = double.IsNaN(grid.Height) ? 0 : grid.Height,
                            Punto = txt.FontSize,
                            KalinMi = txt.FontWeight == FontWeights.Bold,
                            Hizalama = txt.TextAlignment == TextAlignment.Center ? "Orta" : (txt.TextAlignment == TextAlignment.Right ? "Sağ" : "Sol"),
                            R = firca.Color.R,
                            G = firca.Color.G,
                            B = firca.Color.B
                        });
                    }
                }
            }
            return sablon;
        }
    }
}