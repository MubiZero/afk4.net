using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Pos;

namespace AFK4.OrganizationAdmin.App.Pos;

public sealed class PosCartLineViewModel : INotifyPropertyChanged
{
    private int quantity;

    public PosCartLineViewModel(PosProductDto product, int quantity)
    {
        Product = product;
        this.quantity = quantity;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PosProductDto Product { get; }

    public Guid ProductId => Product.ProductId;

    public string ProductName => Product.Name;

    public MoneyDto UnitPrice => Product.Price;

    public int Quantity
    {
        get => quantity;
        private set
        {
            if (SetField(ref quantity, value))
            {
                OnPropertyChanged(nameof(LineTotalMinorUnits));
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(LineTotalText));
            }
        }
    }

    public long LineTotalMinorUnits => Product.Price.MinorUnits * Quantity;

    public MoneyDto LineTotal => new(Product.Price.CurrencyCode, LineTotalMinorUnits);

    public string LineTotalText => $"{(LineTotalMinorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture)} {Product.Price.CurrencyCode}";

    public void Increment()
    {
        Quantity++;
    }

    public PosSaleLineDto ToSaleLine()
    {
        return new PosSaleLineDto(
            Product.ProductId,
            Product.Name,
            Quantity,
            Product.Price,
            LineTotal);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
