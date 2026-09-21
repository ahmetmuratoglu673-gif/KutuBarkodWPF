using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KutuBarkodWPF.Models
{
    public class ExcelSutunModel : INotifyPropertyChanged
    {
        private string _alanAdi;
        private string _ornekVeri;
        private bool _etiketteMi;
        private bool _qrDaMi;
        private string _qrSira;

        public string AlanAdi
        {
            get { return _alanAdi; }
            set { _alanAdi = value; OnPropertyChanged(); }
        }

        public string OrnekVeri
        {
            get { return _ornekVeri; }
            set { _ornekVeri = value; OnPropertyChanged(); }
        }

        public bool EtiketteMi
        {
            get { return _etiketteMi; }
            set { _etiketteMi = value; OnPropertyChanged(); }
        }

        public bool QrDaMi
        {
            get { return _qrDaMi; }
            set
            {
                _qrDaMi = value;
                OnPropertyChanged();
                OnPropertyChanged("QrSiraVisibility"); // Görünürlüğü tetikler
            }
        }

        public string QrSiraVisibility => _qrDaMi ? "Visible" : "Collapsed";

        public string QrSira
        {
            get { return _qrSira; }
            set { _qrSira = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}