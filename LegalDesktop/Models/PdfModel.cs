using System.ComponentModel;

public class PdfModel : INotifyPropertyChanged
{

    public int Id { get; set; }


    private bool _isSelected;
    public string Name { get; set; }
    public string LastModified { get; set; }

    public string? PrivateMessage { get; set; }
    public string Path { get; set; }
    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public string PathBackGround {  get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
