using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections.Generic;

namespace MauiGithubActionsSample;

public enum SlotKind
{
    Military,
    Economy
}

public sealed class YCell : INotifyPropertyChanged
{
    double _value;
    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) < 1e-12) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SlotModel : INotifyPropertyChanged
{
    int _maxValue = 100;
    bool _useSign = true;
    bool _useIncrease = true;

    public SlotKind Kind { get; }
    public int Index { get; private set; } // 0-based

    public string Title => Kind == SlotKind.Military ? $"軍事{Index + 1}" : $"経済{Index + 1}";

    public int MaxValue
    {
        get => _maxValue;
        set
        {
            if (_maxValue == value) return;
            _maxValue = value;
            OnPropertyChanged();
        }
    }

    /// <summary>ONなら 1/3でマイナス化。OFFなら常にプラス。</summary>
    public bool UseSign
    {
        get => _useSign;
        set
        {
            if (_useSign == value) return;
            _useSign = value;
            OnPropertyChanged();
        }
    }

    /// <summary>ONなら X + (|X| * Y%) を適用。</summary>
    public bool UseIncrease
    {
        get => _useIncrease;
        set
        {
            if (_useIncrease == value) return;
            _useIncrease = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<YCell> YCells { get; } = new();

    public SlotModel(SlotKind kind, int index, int days)
    {
        Kind = kind;
        Index = index;
        EnsureDays(days);
    }

    public void SetIndex(int index)
    {
        if (Index == index) return;
        Index = index;
        OnPropertyChanged(nameof(Title));
    }

    public void EnsureDays(int days)
    {
        if (days < 1) days = 1;
        while (YCells.Count < days) YCells.Add(new YCell { Value = 0.0 });
        while (YCells.Count > days) YCells.RemoveAt(YCells.Count - 1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DayResult
{
    public int Day { get; set; } // 1-based
    public List<int> MilitaryValues { get; set; } = new();
    public List<int> EconomyValues { get; set; } = new();

    public int MilitarySum => MilitaryValues.Sum();
    public int EconomySum => EconomyValues.Sum();

    public string MilitaryText => MilitaryValues.Count == 0 ? "—" : string.Join("  ", MilitaryValues.Select(v => v >= 0 ? $"+{v}" : v.ToString()));
    public string EconomyText => EconomyValues.Count == 0 ? "—" : string.Join("  ", EconomyValues.Select(v => v >= 0 ? $"+{v}" : v.ToString()));
}
